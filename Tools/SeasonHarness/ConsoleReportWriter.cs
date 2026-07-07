using System;
using System.Globalization;

namespace Gaffer.Tools.SeasonHarness
{
    /// <summary>Prints a concise text summary of the report — the quick CI / terminal view.</summary>
    public sealed class ConsoleReportWriter
    {
        public void Write(HarnessReport report)
        {
            HarnessConfig config = report.Config;
            Console.WriteLine();
            Console.WriteLine("GAFFER | SEASON HARNESS");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine(
                config.SeasonCount + " seasons | " + config.TeamCount + " teams | seed " +
                config.Seed + " | " + report.TotalMatches + " matches");
            Console.WriteLine();

            Console.WriteLine("GATE A");
            foreach (GateCheck check in report.GateChecks)
            {
                Console.WriteLine(
                    "  " + Tag(check.Status) + "  " + Pad(check.Label, 20) + " " +
                    Pad(check.Value, 16) + " " + check.Detail);
            }
            Console.WriteLine();

            Console.WriteLine("RESULTS");
            Console.WriteLine(
                "  home " + Pct(report.HomeWinPercentage) + "  draw " + Pct(report.DrawPercentage) +
                "  away " + Pct(report.AwayWinPercentage));
            Console.WriteLine(
                "  favourite: win " + Pct(report.FavouriteWinPercentage) + "  draw " +
                Pct(report.FavouriteDrawPercentage) + "  upset " + Pct(report.FavouriteUpsetPercentage));
            Console.WriteLine();

            Console.WriteLine("GOALS PER MATCH  (avg " + report.AverageGoalsPerMatch.ToString("F2", CultureInfo.InvariantCulture) + ")");
            foreach (HistogramBin bin in report.GoalBins)
            {
                Console.WriteLine("  " + Pad(bin.Label, 3) + " " + Bar(bin.Percentage) + " " + Pct(bin.Percentage));
            }
        }

        private static string Tag(GateStatus status)
        {
            switch (status)
            {
                case GateStatus.Pass:
                    return "[PASS]";
                case GateStatus.Warn:
                    return "[WARN]";
                default:
                    return "[FAIL]";
            }
        }

        private static string Bar(double percentage)
        {
            int cells = (int)Math.Round(percentage / 2.0);
            if (cells > 40)
            {
                cells = 40;
            }

            return new string('#', cells).PadRight(40);
        }

        private static string Pct(double value)
        {
            return value.ToString("F1", CultureInfo.InvariantCulture) + "%";
        }

        private static string Pad(string value, int width)
        {
            return value.Length >= width ? value : value.PadRight(width);
        }
    }
}
