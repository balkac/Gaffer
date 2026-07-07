namespace Gaffer.Editor.Harness
{
    /// <summary>Run parameters for the believability harness. Overridable from the command line.</summary>
    public sealed class HarnessConfig
    {
        public int SeasonCount { get; set; } = 1000;

        public int TeamCount { get; set; } = 20;

        public ulong Seed { get; set; } = 20260707UL;

        // Pre-season quality spread across the league, best (rank 0) to worst.
        public double TopQuality { get; set; } = 70.0;

        public double BottomQuality { get; set; } = 46.0;

        // Per-axis jitter so a team is not uniform across attack/midfield/defence.
        public double AxisJitter { get; set; } = 3.0;

        public string OutputHtmlPath { get; set; } = "out/season-report.html";
    }
}
