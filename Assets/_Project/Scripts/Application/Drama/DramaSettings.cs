namespace Gaffer.Application.Drama
{
    /// <summary>
    /// The frequency envelope the drama engine enforces — scarcity is a design rule, not a tuning
    /// accident (GDD §4.7 rule 3): a season budget, a minimum gap, and a weekly firing chance on top
    /// of each event's own cooldown. Injectable and defaulted like every balance object
    /// (NON-NEGOTIABLE #3); the authoring surface maps onto it.
    /// </summary>
    public sealed class DramaSettings
    {
        /// <summary>Hard cap on events per season — past it the engine stays silent until next year.</summary>
        public int MaxEventsPerSeason { get; set; } = 4;

        /// <summary>Weeks that must pass after any event before another may fire.</summary>
        public int MinWeeksBetweenEvents { get; set; } = 4;

        /// <summary>
        /// Weekly firing probability per unit of candidate weight — the sum of the week's candidate
        /// weights scales the chance anything fires, so a trait that halves an event's weight halves
        /// how often it happens (bias must be frequency-real, not just pick-order — NON-NEGOTIABLE #7).
        /// </summary>
        public double WeeklyChancePerWeight { get; set; } = 0.10;

        /// <summary>Ceiling on the weekly firing probability however heavy the week's candidates get.</summary>
        public double MaxWeeklyChance { get; set; } = 0.35;

        /// <summary>The calibrated defaults — what the core uses when no config asset overrides them.</summary>
        public static DramaSettings Default => new DramaSettings();
    }
}
