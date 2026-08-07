namespace Gaffer.Application.Drama
{
    /// <summary>
    /// The frequency envelope the drama engine enforces — scarcity is a design rule, not a tuning
    /// accident (GDD §4.7 rule 3): a season budget, a minimum gap, and a weekly firing chance on top
    /// of each event's own cooldown. Injectable and defaulted like every balance object
    /// (NON-NEGOTIABLE #3); the authoring surface maps onto it. Immutable once built — get-only properties
    /// set by one all-optional constructor. The defaults live in the constructor signature and are baked
    /// into every calling assembly at compile time, so a changed default needs a full recompile before it
    /// is live everywhere (see <c>SettingsDefaultContractTests</c>).
    /// </summary>
    public sealed class DramaSettings
    {
        /// <summary>
        /// Every parameter is optional and defaulted to the calibrated value, so a caller writes only what
        /// it changes: <c>new DramaSettings(maxEventsPerSeason: 0)</c>.
        /// </summary>
        public DramaSettings(
            int maxEventsPerSeason = 4,
            int minWeeksBetweenEvents = 4,
            double weeklyChancePerWeight = 0.10,
            double maxWeeklyChance = 0.35)
        {
            MaxEventsPerSeason = maxEventsPerSeason;
            MinWeeksBetweenEvents = minWeeksBetweenEvents;
            WeeklyChancePerWeight = weeklyChancePerWeight;
            MaxWeeklyChance = maxWeeklyChance;
        }

        /// <summary>Hard cap on events per season — past it the engine stays silent until next year.</summary>
        public int MaxEventsPerSeason { get; }

        /// <summary>Weeks that must pass after any event before another may fire.</summary>
        public int MinWeeksBetweenEvents { get; }

        /// <summary>
        /// Weekly firing probability per unit of candidate weight — the sum of the week's candidate
        /// weights scales the chance anything fires, so a trait that halves an event's weight halves
        /// how often it happens (bias must be frequency-real, not just pick-order — NON-NEGOTIABLE #7).
        /// </summary>
        public double WeeklyChancePerWeight { get; }

        /// <summary>Ceiling on the weekly firing probability however heavy the week's candidates get.</summary>
        public double MaxWeeklyChance { get; }

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new DramaSettings()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static DramaSettings Default { get; } = new DramaSettings();
    }
}
