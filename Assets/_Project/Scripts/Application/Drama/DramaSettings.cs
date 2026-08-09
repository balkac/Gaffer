namespace Gaffer.Application.Drama
{
    /// <summary>
    /// The frequency envelope the drama engine enforces — scarcity is a design rule, not a tuning
    /// accident (GDD §4.7 rule 3): a weekly firing chance that answers to how the run is going, a
    /// minimum gap, and a season budget as a BACKSTOP, on top of each event's own cooldown. Injectable
    /// and defaulted like every balance object (NON-NEGOTIABLE #3); the authoring surface maps onto it.
    /// Immutable once built — get-only properties set by one all-optional constructor. The defaults live
    /// in the constructor signature and are baked into every calling assembly at compile time, so a
    /// changed default needs a full recompile before it is live everywhere (see
    /// <c>SettingsDefaultContractTests</c>).
    /// <para>
    /// CALIBRATION NOTE (2026-08-07). The envelope used to be <c>0.10</c> per weight capped at
    /// <c>0.35</c>, which fired in nearly every week it was allowed to: measured over 2000 seasons the
    /// engine produced 3.94 events a season and sat ON the four-event cap in 94% of them. Scarcity was
    /// coming entirely from the ceiling, which is the "feed held back by a cap" the design forbids — the
    /// cap is meant to be the thing that never normally binds. The chance is now roughly a quarter of
    /// that in a calm week and climbs with <see cref="CrisisChanceMultiplier"/> as the run turns bad, so
    /// a quiet mid-table year yields one or two stories and a relegation fight yields four. The spread,
    /// not the mean, is the point.
    /// </para>
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
            double weeklyChancePerWeight = 0.024,
            double maxWeeklyChance = 0.20,
            double crisisChanceMultiplier = 3.0,
            int crisisLossStreak = 3,
            int crisisTablePosition = 18)
        {
            MaxEventsPerSeason = maxEventsPerSeason;
            MinWeeksBetweenEvents = minWeeksBetweenEvents;
            WeeklyChancePerWeight = weeklyChancePerWeight;
            MaxWeeklyChance = maxWeeklyChance;
            CrisisChanceMultiplier = crisisChanceMultiplier;
            CrisisLossStreak = crisisLossStreak;
            CrisisTablePosition = crisisTablePosition;
        }

        /// <summary>
        /// Backstop on events per season — past it the engine stays silent until next year. It is NOT the
        /// scarcity mechanism (see the calibration note): under the calibrated envelope a season reaches
        /// it only when the run has genuinely fallen apart.
        /// </summary>
        public int MaxEventsPerSeason { get; }

        /// <summary>Weeks that must pass after any event before another may fire.</summary>
        public int MinWeeksBetweenEvents { get; }

        /// <summary>
        /// Weekly firing probability per unit of candidate weight in a CALM week — the sum of the week's
        /// candidate weights scales the chance anything fires, so a trait that halves an event's weight
        /// halves how often it happens (bias must be frequency-real, not just pick-order —
        /// NON-NEGOTIABLE #7). One event contributes one candidate however many players it fits, so this
        /// number is per event-in-play, not per eligible player.
        /// </summary>
        public double WeeklyChancePerWeight { get; }

        /// <summary>Ceiling on the weekly firing probability however heavy the week's candidates get.</summary>
        public double MaxWeeklyChance { get; }

        /// <summary>
        /// What the weekly chance is multiplied by at full crisis, relative to a calm week — the dial that
        /// makes drama answer to the run instead of arriving on a metronome. 1.0 switches the response off.
        /// </summary>
        public double CrisisChanceMultiplier { get; }

        /// <summary>
        /// Consecutive defeats that count as full crisis; the pressure ramps linearly up to it (0 = the
        /// form of the team never raises the chance).
        /// </summary>
        public int CrisisLossStreak { get; }

        /// <summary>
        /// 1-based league position at or below which the table itself weighs as much as one defeat
        /// (0 = the table never raises the chance). Default 18 — the drop zone of a 20-club league.
        /// </summary>
        public int CrisisTablePosition { get; }

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new DramaSettings()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static DramaSettings Default { get; } = new DramaSettings();
    }
}
