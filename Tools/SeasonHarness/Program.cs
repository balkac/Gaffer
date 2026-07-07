using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Gaffer.Application.Simulation;
using Gaffer.Common;

namespace Gaffer.Tools.SeasonHarness
{
    /// <summary>
    /// Entry point: simulate many seasons on the pure Application core, headless, and emit both a
    /// console summary and a broadcast-styled HTML report — the Gate A believability check.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            HarnessConfig config = ParseArgs(args);

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

            HarnessReport report = statistics.BuildReport(config, teams);

            new ConsoleReportWriter().Write(report);

            string htmlPath = Path.GetFullPath(config.OutputHtmlPath);
            string directory = Path.GetDirectoryName(htmlPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(htmlPath, new HtmlReportWriter().Render(report));
            Console.WriteLine();
            Console.WriteLine("HTML report -> " + htmlPath);
            return 0;
        }

        private static HarnessConfig ParseArgs(string[] args)
        {
            var config = new HarnessConfig();
            for (int i = 0; i + 1 < args.Length; i += 2)
            {
                string value = args[i + 1];
                switch (args[i])
                {
                    case "--seasons":
                        config.SeasonCount = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "--teams":
                        config.TeamCount = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "--seed":
                        config.Seed = ulong.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case "--out":
                        config.OutputHtmlPath = value;
                        break;
                }
            }

            return config;
        }
    }
}
