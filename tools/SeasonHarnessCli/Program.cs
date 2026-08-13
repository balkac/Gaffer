using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Gaffer.Application.Narrative;
using Gaffer.Application.Run;
using Gaffer.Application.Season;
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
            string probe = null;

            // The market probe's two knobs. They are the numbers the editor windows hardcode, so the
            // ratio between them can be swept from here without editing a window and reopening Unity.
            long cash = RunSetup.Default.StartingCash;
            long wageBudget = RunSetup.Default.WeeklyWageBudget;

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
                    case "--probe":
                        probe = value;
                        break;
                    case "--cash":
                        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out cash) || cash < 0)
                        {
                            return Fail("--cash must be a non-negative integer.");
                        }

                        break;
                    case "--wage-budget":
                        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out wageBudget) || wageBudget < 0)
                        {
                            return Fail("--wage-budget must be a non-negative integer.");
                        }

                        break;
                    default:
                        return Fail($"Unrecognised option '{name}'.");
                }
            }

            // The probes answer questions the season distribution cannot: they measure the GENERATOR, the
            // season rollover and the market instead of the match model. They print and stop — none of
            // them carries a Gate A verdict, so none of them decides the exit code.
            if (!string.IsNullOrEmpty(probe))
            {
                return RunProbe(probe, config, cash, wageBudget);
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

        private static int RunProbe(string probe, HarnessConfig config, long cash, long wageBudget)
        {
            switch (probe)
            {
                case "ranks":
                    PrintGeneratorRanks(new GeneratorRankProbe().Measure(config.SeasonCount, config.TeamCount, config.Seed));
                    return 0;
                case "academy":
                    PrintAcademy(new AcademyProbe().Measure(config.TeamCount, config.SeasonCount, config.Seed, RenewalSettings.Default));
                    return 0;
                case "market":
                    return PrintMarket(new MarketProbe().Measure(
                        new RunSetup(startingCash: cash, weeklyWageBudget: wageBudget)));
                case "story":
                    PrintStories(new StoryProbe().Measure(config.TeamCount, config.SeasonCount, config.Seed, marketSize: 200, guaranteedGems: 3));
                    return 0;
                default:
                    return Fail($"Unknown probe '{probe}'. Try ranks, academy, market or story.");
            }
        }

        // Gate B, read as prose. Deliberately plain: the question is whether the SHAPE of a career reads
        // as though someone wrote it, and dressing the output up would answer a different question.
        private static void PrintStories(StoryReport report)
        {
            Console.WriteLine();
            Console.WriteLine($"Story probe — {report.Club}, {report.Seasons} seasons");
            Console.WriteLine($"  {report.Careers} careers followed, {report.MomentCount} moments, {report.MomentsPerCareer:F1} per career");
            Console.WriteLine();

            if (report.Careers == 0)
            {
                Console.WriteLine("  Nothing was followed. The narrative layer is not wired in.");
                return;
            }

            int show = report.Arcs.Count < 5 ? report.Arcs.Count : 5;
            for (int i = 0; i < show; i++)
            {
                PrintArc(report.Arcs[i], i + 1, report);
            }

            Console.WriteLine("Read the top arc and ask the only question that matters:");
            Console.WriteLine("does it read like a career, or like a list of things that happened?");
        }

        // Told season by season, because that is how a career is told — the owner's reading of the first
        // Gate B output was that a flat stream of moments is a LIST, and he was right. A year he simply
        // played has a row of its own here; without one it would read as a gap rather than a quiet season.
        private static void PrintArc(StoryArc arc, int rank, StoryReport report)
        {
            string name = string.IsNullOrEmpty(arc.Name) ? "(left the club)" : arc.Name;
            CareerChronicle chronicle = report.ChronicleOf(arc.Player);
            CareerSummary summary = chronicle.Summary;

            Console.WriteLine($"  {rank}. {name.ToUpperInvariant()}");
            Console.WriteLine($"      {summary.SeasonsAtTheClub} seasons · {summary.Appearances} games · {summary.Goals} goals{Fees(summary)}");

            for (int i = 0; i < chronicle.Seasons.Count; i++)
            {
                ChronicleSeason season = chronicle.Seasons[i];
                Console.WriteLine($"      S{season.Season,-3} {season.Played.Appearances,3} games {season.Played.Goals,3} goals{Standout(season)}");
            }

            Console.WriteLine();
        }

        // What is remembered of a season, on the season's own line — never a bullet list underneath it.
        private static string Standout(ChronicleSeason season)
        {
            if (season.Moments.Count == 0)
            {
                return string.Empty;
            }

            var built = new System.Text.StringBuilder("   ");
            for (int i = 0; i < season.Moments.Count; i++)
            {
                if (i > 0)
                {
                    built.Append("; ");
                }

                built.Append(Describe(season.Moments[i]));
            }

            return built.ToString();
        }

        private static string Fees(CareerSummary summary)
        {
            if (summary.CameThroughTheAcademy && summary.DepartureFee > 0)
            {
                return $"  ·  academy, sold for {summary.DepartureFee:N0}";
            }

            if (summary.ArrivalFee > 0 && summary.DepartureFee > 0)
            {
                return $"  ·  {summary.ArrivalFee:N0} in, {summary.DepartureFee:N0} out ({summary.Profit:+#,0;-#,0}) ";
            }

            return summary.CameThroughTheAcademy ? "  ·  academy" : string.Empty;
        }

        // Dev-tool English (NON-NEGOTIABLE #8 exemption: this assembly cannot enter a build). The shipped
        // wording comes from the string table in Faz 5.4 — this is the SHAPE under it, nothing more.
        private static string Describe(CareerMoment moment)
        {
            switch (moment.Kind)
            {
                case CareerMomentKind.Debut:
                    return "made his debut";
                case CareerMomentKind.FirstGoal:
                    return $"scored his first goal ({moment.Minute}')";
                case CareerMomentKind.BigMatchGoal:
                    return $"scored in a match that mattered ({moment.Minute}')";
                case CareerMomentKind.DerbyGoal:
                    return $"scored in the derby ({moment.Minute}')";
                case CareerMomentKind.TitleDeciderGoal:
                    return $"scored with the title on the line ({moment.Minute}')";
                case CareerMomentKind.RelegationGoal:
                    return $"scored in a relegation six-pointer ({moment.Minute}')";
                case CareerMomentKind.Brace:
                    return "scored twice";
                case CareerMomentKind.Hattrick:
                    return "scored a hat-trick";
                case CareerMomentKind.AppearanceMilestone:
                    return $"made his {moment.Count}th appearance";
                case CareerMomentKind.GoalMilestone:
                    return $"reached {moment.Count} goals";
                case CareerMomentKind.Signing:
                    return $"signed for {moment.Count:N0}";
                case CareerMomentKind.Sale:
                    return $"sold for {moment.Count:N0}";
                case CareerMomentKind.AcademyArrival:
                    return "came through the academy";
                case CareerMomentKind.BreakoutSeason:
                    return "kicked on";
                case CareerMomentKind.Retirement:
                    return "retired";
                default:
                    return moment.Kind.ToString();
            }
        }

        // Finding 1's other half: the harness's own league builder is not the shipped one, so the same
        // question — is rank 1 really the strongest club — has to be asked of LeagueGenerator separately.
        private static void PrintGeneratorRanks(GeneratorRankReport report)
        {
            Console.WriteLine($"LeagueGenerator rank probe — {report.Leagues} leagues of {report.ClubCount} clubs");
            Console.WriteLine();
            Console.WriteLine("Rank    mean strength     min       max     times strongest in its league");
            foreach (GeneratorRankRow row in report.Rows)
            {
                Console.WriteLine(
                    $"  #{row.Rank + 1,-3} {Fixed(row.MeanStrength),12} {Fixed(row.MinStrength),9} {Fixed(row.MaxStrength),9}" +
                    $"     {row.TimesStrongest,6}  {Percent(Share(row.TimesStrongest, report.Leagues)),7}");
            }

            Console.WriteLine();
            Console.WriteLine($"Adjacent ranks out of strength order   {report.MeanInversions:0.00} per league (of {report.ClubCount - 1} pairs)");
            Console.WriteLine($"Rank 1 really was the strongest club   {Percent(Share(report.RankOneStrongest, report.Leagues))}");
        }

        private static void PrintAcademy(AcademyReport report)
        {
            Console.WriteLine($"Academy intake probe — no squad cap, {report.YouthIntakePerSeason} youth/season promised");
            Console.WriteLine();
            Console.WriteLine("Season  clubs   intake granted   intake cancelled   retirements   mean squad size before");
            long granted = 0;
            long total = 0;
            foreach (AcademySeasonRow row in report.Seasons)
            {
                granted += row.IntakeGranted;
                total += row.Clubs;
                Console.WriteLine(
                    $"  {row.SeasonNumber,-6} {row.Clubs,5}   {row.IntakeGranted,14}   {row.IntakeCancelled,16}   {row.Retirements,11}" +
                    $"   {row.MeanSquadSizeBefore,22:0.00}");
            }

            Console.WriteLine();
            Console.WriteLine($"Club-seasons with an academy arrival   {granted} of {total}  {Percent(Share(granted, total))}");
            Console.WriteLine(report.FirstSilentSeason == 0
                ? "Every season brought at least one club an intake."
                : $"From season {report.FirstSilentSeason} on, NO club in the league received one.");
            Console.WriteLine(
                "Squad size is uncapped by design — the intake is pure growth and expiring contracts (not built");
            Console.WriteLine(
                "yet) are the intended drain, so the mean-size column climbing for ever is expected, not a bug.");
        }

        private static int PrintMarket(Result<MarketReport> measured)
        {
            if (measured.IsFailure)
            {
                return Fail(measured.Error);
            }

            MarketReport report = measured.Value;
            Console.WriteLine($"Market probe — {report.ManagedClub}, {report.SquadSize} players");
            Console.WriteLine();
            Console.WriteLine($"Transfer cash        {report.StartingCash}");
            Console.WriteLine($"Wage ceiling         {report.WageBudget}/wk");
            Console.WriteLine($"Inherited wage bill  {report.OpeningWageBill}/wk  ({Percent(Share(report.OpeningWageBill, report.WageBudget))} of the ceiling)");
            Console.WriteLine($"Wage room free       {report.WageBudget - report.OpeningWageBill}/wk");
            Console.WriteLine();

            Console.WriteLine($"Market of {report.MarketSize} — median fee {report.MedianFee}, median wage {report.MedianWage}/wk");
            Console.WriteLine($"  affordable on cash alone       {report.CashAffordable,3}  {Percent(Share(report.CashAffordable, report.MarketSize)),7}");
            Console.WriteLine($"  affordable on wage room alone  {report.WageAffordable,3}  {Percent(Share(report.WageAffordable, report.MarketSize)),7}");
            Console.WriteLine($"  affordable on both             {report.BothAffordable,3}  {Percent(Share(report.BothAffordable, report.MarketSize)),7}");
            Console.WriteLine();

            Console.WriteLine("Wages are paid out of the same transfer cash, and nothing pays in yet:");
            Console.WriteLine($"  a season of the inherited bill costs   {report.InheritedSeasonWageDrain}  ({Percent(Share(report.InheritedSeasonWageDrain, report.StartingCash))} of the cash)");
            Console.WriteLine($"  cash genuinely free for fees           {report.DiscretionaryCash}");
            Console.WriteLine($"  prospects that reaches                 {report.DiscretionaryAffordable,3}  {Percent(Share(report.DiscretionaryAffordable, report.MarketSize)),7}");
            Console.WriteLine();

            Console.WriteLine("Greedy walk — cheapest affordable prospect, over and over");
            foreach (MarketSigning signing in report.Signings)
            {
                Console.WriteLine(
                    $"  {signing.Order,2}. {signing.Name,-22} age {signing.Age,2}   fee {signing.Fee,9}   wage {signing.WeeklyWage,6}/wk" +
                    $"   leaves {signing.CashAfter,9} cash, {signing.WageRoomAfter,6}/wk room");
            }

            Console.WriteLine($"  stopped after {report.Signings.Count} signings — blocked by {report.BlockedBy}");
            Console.WriteLine();
            Console.WriteLine($"A season of that wage bill drains {report.SeasonWageDrain} from transfer cash");
            Console.WriteLine(report.WeeksToInsolvency == 0
                ? "  the cash left survives the season"
                : $"  the cash left runs out in week {report.WeeksToInsolvency}");
            return 0;
        }

        private static double Share(long count, long total)
        {
            return total == 0 ? 0.0 : 100.0 * count / total;
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
            // Strength is printed BESIDE the titles, for every rank rather than only the winners. The two
            // columns together are what separate "the sim rewards the wrong club" from "the club labelled
            // #2 is simply the strongest one" — the title column alone reads as a sim bias either way.
            Console.WriteLine("Titles by pre-season rank   (strength = the mean of the three axes the sim was handed)");
            foreach (ChampionShare share in report.ChampionShares)
            {
                Console.WriteLine(
                    $"  #{share.Rank + 1,-3} {share.Name,-22} {share.Titles,6}  {Percent(share.Percentage),7}" +
                    $"   strength {Fixed(share.MeanStrength),7}");
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
            Console.WriteLine("  --probe <name>  run a diagnostic instead of the season distribution:");
            Console.WriteLine("                    ranks    LeagueGenerator rank vs actual squad strength");
            Console.WriteLine("                             (--seasons is the league sample size)");
            Console.WriteLine("                    academy  how often the guaranteed youth intake arrives");
            Console.WriteLine("                    market   what the two budgets can actually reach");
            Console.WriteLine("                    story    Gate B: the strongest careers a run produced");
            Console.WriteLine("                             (--seasons is how many to play, try 20)");
            Console.WriteLine("  --cash <n>          market probe: transfer cash to start on");
            Console.WriteLine("  --wage-budget <n>   market probe: weekly wage ceiling to start on");
            Console.WriteLine("Exit code 1 if any gate fails, 2 on a bad argument.");
        }
    }
}
