using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Tools.SeasonHarness;

namespace Gaffer.Tools.SeasonHarness.Cli
{
    /// <summary>
    /// The console face of the believability harness (Gate A): run N seasons headlessly and print the
    /// distribution — goals per match, the goal histogram, the home/draw/away split, how often the
    /// favourite wins, and the harness's own gate verdicts.
    /// <para>
    /// It wires the harness in exactly the order <c>SeasonHarnessWindow.RunHarness</c> does — one RNG
    /// stream created from the seed and threaded through every season — so the console and the editor
    /// window report the SAME numbers for the same seed. Anything else would make the instrument a
    /// second opinion instead of the measurement.
    /// </para>
    /// <para>
    /// Raw English throughout: never-shipped developer tooling, the NON-NEGOTIABLE #8 exemption.
    /// </para>
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            // The instrument's output must not depend on the machine that ran it: the report's own gate
            // strings are formatted by HarnessStatistics with the ambient culture, so a decimal comma
            // locale would print "2,63" where CI expects "2.63". Pinned once, here.
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var config = new HarnessConfig();
            string htmlPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--help" || arg == "-h")
                {
                    PrintUsage();
                    return 0;
                }

                if (!TryReadOption(args, ref i, out string name, out string value))
                {
                    Console.Error.WriteLine($"Unrecognised argument '{arg}'.");
                    PrintUsage();
                    return 2;
                }

                switch (name)
                {
                    case "--seasons":
                        if (!TryParseInt(value, out int seasons, min: 1))
                        {
                            return Fail("--seasons must be a positive integer.");
                        }

                        config.SeasonCount = seasons;
                        break;
                    case "--teams":
                        if (!TryParseInt(value, out int teams, min: 2))
                        {
                            return Fail("--teams must be an integer of at least 2.");
                        }

                        config.TeamCount = teams;
                        break;
                    case "--seed":
                        if (!ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed))
                        {
                            return Fail("--seed must be a non-negative integer.");
                        }

                        config.Seed = seed;
                        break;
                    case "--html":
                        htmlPath = value;
                        break;
                    default:
                        return Fail($"Unrecognised option '{name}'.");
                }
            }

            HarnessReport report = Run(config);
            Print(report);

            if (!string.IsNullOrEmpty(htmlPath))
            {
                WriteHtml(report, htmlPath);
            }

            // Exit code is the gate: a failing distribution must fail a CI step, not just print red text.
            foreach (GateCheck check in report.GateChecks)
            {
                if (check.Status == GateStatus.Fail)
                {
                    return 1;
                }
            }

            return 0;
        }

        // The exact wiring SeasonHarnessWindow uses — same order, one RNG stream for the whole run.
        private static HarnessReport Run(HarnessConfig config)
        {
            var rng = new SplitMix64RandomNumberGenerator(config.Seed);
            IReadOnlyList<TeamProfile> teams = new LeagueFactory().CreateTeams(config, rng);
            var simulator = new MatchSimulator(
                new PoissonChanceGenerator(MatchSimulationSettings.Default),
                new QualityChanceResolver());
            var runner = new SeasonRunner(simulator);
            var statistics = new HarnessStatistics(config.TeamCount);

            for (int season = 0; season < config.SeasonCount; season++)
            {
                runner.RunSeason(teams, rng, statistics);
            }

            return statistics.BuildReport(config, teams);
        }

        private static void Print(HarnessReport report)
        {
            HarnessConfig config = report.Config;
            Console.WriteLine($"Season harness — {config.SeasonCount} seasons, {config.TeamCount} teams, seed {config.Seed}");
            Console.WriteLine($"{report.TotalMatches} matches simulated");
            Console.WriteLine();

            Console.WriteLine($"Goals per match      {Fixed(report.AverageGoalsPerMatch)}");
            Console.WriteLine($"Home / draw / away   {Percent(report.HomeWinPercentage)} / {Percent(report.DrawPercentage)} / {Percent(report.AwayWinPercentage)}");
            Console.WriteLine($"Favourite W/D/L      {Percent(report.FavouriteWinPercentage)} / {Percent(report.FavouriteDrawPercentage)} / {Percent(report.FavouriteUpsetPercentage)}");
            Console.WriteLine();

            Console.WriteLine("Goals per match, distribution");
            foreach (HistogramBin bin in report.GoalBins)
            {
                Console.WriteLine($"  {bin.Label,-5} {Percent(bin.Percentage),7}  {Bar(bin.Percentage)}");
            }

            Console.WriteLine();
            Console.WriteLine("Titles by pre-season rank");
            foreach (ChampionShare share in report.ChampionShares)
            {
                if (share.Titles == 0)
                {
                    continue;
                }

                Console.WriteLine($"  #{share.Rank + 1,-3} {share.Name,-18} {share.Titles,6}  {Percent(share.Percentage),7}");
            }

            Console.WriteLine();
            Console.WriteLine("Gates");
            foreach (GateCheck check in report.GateChecks)
            {
                Console.WriteLine($"  [{check.Status.ToString().ToUpperInvariant(),-4}] {check.Label,-26} {check.Value,-10} {check.Detail}");
            }
        }

        private static void WriteHtml(HarnessReport report, string path)
        {
            string full = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(full, new HtmlReportWriter().Render(report));
            Console.WriteLine();
            Console.WriteLine($"HTML report written to {full}");
        }

        private static string Bar(double percentage)
        {
            var width = (int)Math.Round(percentage / 2.0);
            return new string('#', width < 0 ? 0 : width);
        }

        private static string Fixed(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static string Percent(double value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture) + " %";
        }

        // Accepts both "--seasons 1000" and "--seasons=1000".
        private static bool TryReadOption(string[] args, ref int index, out string name, out string value)
        {
            string arg = args[index];
            name = null;
            value = null;

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                return false;
            }

            int split = arg.IndexOf('=');
            if (split >= 0)
            {
                name = arg.Substring(0, split);
                value = arg.Substring(split + 1);
                return true;
            }

            if (index + 1 >= args.Length)
            {
                return false;
            }

            name = arg;
            value = args[++index];
            return true;
        }

        private static bool TryParseInt(string text, out int value, int min)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= min;
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine(message);
            return 2;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: dotnet run --project tools/SeasonHarnessCli [options]");
            Console.WriteLine("  --seasons <n>   seasons to simulate (default 1000)");
            Console.WriteLine("  --teams <n>     teams in the league (default 20)");
            Console.WriteLine("  --seed <n>      RNG seed (default 20260707)");
            Console.WriteLine("  --html <path>   also write the HTML report to this path");
            Console.WriteLine("Exit code 1 if any gate fails, 2 on a bad argument.");
        }
    }
}
