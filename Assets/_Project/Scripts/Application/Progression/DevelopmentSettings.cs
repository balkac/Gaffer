namespace Gaffer.Application.Progression
{
    /// <summary>
    /// The balance constants <see cref="PlayerDevelopment"/> reads, kept in one injected value so tuning
    /// changes the numbers, not the code (NON-NEGOTIABLE #3). The pure core consumes this; the Infrastructure
    /// <c>DevelopmentBalanceSO</c> is the Unity authoring surface that maps to it, and <see cref="Default"/>
    /// keeps the core working headless with the calibrated values. A mutable settings object (like
    /// <c>GenerationContext</c>): defaults inline, tweak by object initializer.
    /// </summary>
    public sealed class DevelopmentSettings
    {
        // Growth as a fraction of the remaining ability gap, by age band — steep for teenagers, a trickle by
        // the late twenties, zero after (ages read against the thresholds below in PlayerDevelopment).
        public double GrowthRateTo20 { get; set; } = 0.14;

        public double GrowthRateTo22 { get; set; } = 0.10;

        public double GrowthRateTo24 { get; set; } = 0.07;

        public double GrowthRateTo26 { get; set; } = 0.045;

        public double GrowthRateTo29 { get; set; } = 0.02;

        // Per-season multiplier on growth and decline so a career is not a smooth curve (some seasons kick on,
        // some stall). Centred on 1.0.
        public double MinSeasonVariance { get; set; } = 0.6;

        public double MaxSeasonVariance { get; set; } = 1.4;

        // The age each role tends to peak at before decline — keepers latest, pacey forwards first. A stable
        // per-player offset then shifts this a couple of years either way.
        public int KeeperPeakAge { get; set; } = 34;

        public int CentralPeakAge { get; set; } = 32;

        public int WidePeakAge { get; set; } = 31;

        public int ForwardPeakAge { get; set; } = 30;

        /// <summary>No one declines before this age, whatever his role or offset — physical decline is real by then.</summary>
        public int MinDeclineAge { get; set; } = 30;

        // Ability lost per season past the peak, accelerating with years (capped) into the mid-thirties.
        public double DeclinePerYear { get; set; } = 0.9;

        public int MaxDeclineYears { get; set; } = 6;

        /// <summary>How much of a season's decline hits the role's own attributes vs the athletic erosion on top.</summary>
        public double GeneralDeclineFactor { get; set; } = 0.6;

        /// <summary>Floor for the role's rating attributes as age erodes them — an old pro loses a step, not his craft.</summary>
        public byte AttributeFloor { get; set; } = 25;

        /// <summary>Floor for the raw physical attributes under athletic decline.</summary>
        public byte PhysicalFloor { get; set; } = 15;

        /// <summary>The calibrated defaults — what the core uses when no config asset overrides them.</summary>
        public static DevelopmentSettings Default => new DevelopmentSettings();
    }
}
