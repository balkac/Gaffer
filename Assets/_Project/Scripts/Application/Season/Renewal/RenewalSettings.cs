namespace Gaffer.Application.Season
{
    /// <summary>
    /// The balance constants <see cref="SquadRenewal"/> (and the gem cadence in <see cref="SeasonTransition"/>)
    /// read — retirement ages, how a high rating stays a player's retirement, the academy-gem cadence and its
    /// hidden band, and the youth intake age range. Kept in one injected value so tuning changes the numbers,
    /// not the code (NON-NEGOTIABLE #3); <c>RenewalBalanceSO</c> authors it in Unity, and <see cref="Default"/>
    /// is the calibrated baseline, plus the youth ability/potential band drawn around the squad's own
    /// level (tier persistence). Immutable once built — defaults inline, overrides by object
    /// initializer, and <c>init</c> makes that the only way in.
    /// </summary>
    public sealed class RenewalSettings
    {
        // Retirement thresholds by role group: no one plays past Hard, and in the twilight years the odds
        // climb with age. Keepers play latest of all.
        public int KeeperTwilightAge { get; init; } = 36;

        public int KeeperHardAge { get; init; } = 43;

        public int OutfielderTwilightAge { get; init; } = 33;

        public int OutfielderHardAge { get; init; } = 40;

        /// <summary>How much a high rating eases retirement in the twilight years (0 = age only, higher = stars linger).</summary>
        public double RetirementRatingEase { get; init; } = 0.4;

        /// <summary>How often, in seasons, a club's academy yields a gem — rare and guaranteed, never a per-player chance.</summary>
        public int GemCadenceSeasons { get; init; } = 5;

        // The academy gem's hidden band: low visible ability (hides among ordinary prospects), rare high ceiling.
        public byte GemMinAbility { get; init; } = 28;

        public byte GemMaxAbility { get; init; } = 46;

        public byte GemMinPotential { get; init; } = 86;

        public byte GemMaxPotential { get; init; } = 96;

        // The age range youth arrive at.
        public int YouthMinAge { get; init; } = 16;

        public int YouthMaxAge { get; init; } = 18;

        /// <summary>
        /// How many academy youths join each season beyond replacing retirees — so a club's academy feeds the
        /// squad every year, not only when a veteran leaves. Held to <see cref="MaxSquadSize"/>. 0 disables it.
        /// </summary>
        public int YouthIntakePerSeason { get; init; } = 1;

        /// <summary>The size the academy intake grows the squad toward and never pushes it past.</summary>
        public int MaxSquadSize { get; init; } = 25;

        // The ordinary youth band: each edge is the squad's average rating plus an offset, then held
        // inside its own absolute floor/ceiling. The offsets are what make tier persist — a strong
        // club's academy is stronger — and the floor/ceiling pair is what keeps a bottom-of-the-pyramid
        // or a superclub squad from producing unplayable or world-class teenagers by arithmetic alone.
        // These lived as literals in SquadRenewal.YouthContext next to the gem band that was already
        // config; they are balance, so they belong here (NON-NEGOTIABLE #3). Every value below is the
        // literal that shipped, so Default reproduces the previous youth exactly.

        /// <summary>Offset from the squad average for the youth band's lowest visible ability.</summary>
        public int YouthMinAbilityOffset { get; init; } = -25;

        public byte YouthMinAbilityFloor { get; init; } = 25;

        public byte YouthMinAbilityCeiling { get; init; } = 60;

        /// <summary>Offset from the squad average for the youth band's highest visible ability.</summary>
        public int YouthMaxAbilityOffset { get; init; } = -8;

        public byte YouthMaxAbilityFloor { get; init; } = 35;

        public byte YouthMaxAbilityCeiling { get; init; } = 72;

        /// <summary>Offset from the squad average for the youth band's lowest hidden potential.</summary>
        public int YouthMinPotentialOffset { get; init; } = -3;

        public byte YouthMinPotentialFloor { get; init; } = 45;

        public byte YouthMinPotentialCeiling { get; init; } = 85;

        /// <summary>Offset from the squad average for the youth band's highest hidden potential — the
        /// only positive one, so the best prospect can climb past today's first team.</summary>
        public int YouthMaxPotentialOffset { get; init; } = 18;

        public byte YouthMaxPotentialFloor { get; init; } = 60;

        public byte YouthMaxPotentialCeiling { get; init; } = 95;

        /// <summary>The average to assume when there is no one to average — an empty squad still has
        /// to draw its intake from somewhere, and a 0 average would floor every band.</summary>
        public int EmptySquadAverageRating { get; init; } = 50;

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new RenewalSettings()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static RenewalSettings Default { get; } = new RenewalSettings();
    }
}
