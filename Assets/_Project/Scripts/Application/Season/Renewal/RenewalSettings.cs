namespace Gaffer.Application.Season
{
    /// <summary>
    /// The balance constants <see cref="SquadRenewal"/> (and the gem cadence in <see cref="SeasonTransition"/>)
    /// read — retirement ages, how a high rating stays a player's retirement, the academy-gem cadence and its
    /// hidden band, and the youth intake age range. Kept in one injected value so tuning changes the numbers,
    /// not the code (NON-NEGOTIABLE #3); <c>RenewalBalanceSO</c> authors it in Unity, and <see cref="Default"/>
    /// is the calibrated baseline, plus the youth ability/potential band drawn around the squad's own
    /// level (tier persistence).
    /// <para>
    /// Immutable once built: get-only properties set by one constructor whose parameters are all optional,
    /// so a caller names only what it overrides. The defaults therefore live in the constructor signature
    /// and are baked into every calling assembly at compile time — after changing one, rebuild everything
    /// before trusting it (see <c>SettingsDefaultContractTests</c> for the convention and the full cost).
    /// </para>
    /// </summary>
    public sealed class RenewalSettings
    {
        /// <summary>
        /// Every parameter is optional and defaulted to the calibrated value, so a caller writes only what it
        /// changes: <c>new RenewalSettings(youthIntakePerSeason: 5)</c>.
        /// </summary>
        public RenewalSettings(
            int keeperTwilightAge = 36,
            int keeperHardAge = 43,
            int outfielderTwilightAge = 33,
            int outfielderHardAge = 40,
            double retirementRatingEase = 0.4,
            int gemCadenceSeasons = 5,
            byte gemMinAbility = 28,
            byte gemMaxAbility = 46,
            byte gemMinPotential = 86,
            byte gemMaxPotential = 96,
            int youthMinAge = 16,
            int youthMaxAge = 18,
            int youthIntakePerSeason = 1,
            int youthMinAbilityOffset = -25,
            byte youthMinAbilityFloor = 25,
            byte youthMinAbilityCeiling = 60,
            int youthMaxAbilityOffset = -8,
            byte youthMaxAbilityFloor = 35,
            byte youthMaxAbilityCeiling = 72,
            int youthMinPotentialOffset = -3,
            byte youthMinPotentialFloor = 45,
            byte youthMinPotentialCeiling = 85,
            int youthMaxPotentialOffset = 18,
            byte youthMaxPotentialFloor = 60,
            byte youthMaxPotentialCeiling = 95,
            int emptySquadAverageRating = 50)
        {
            KeeperTwilightAge = keeperTwilightAge;
            KeeperHardAge = keeperHardAge;
            OutfielderTwilightAge = outfielderTwilightAge;
            OutfielderHardAge = outfielderHardAge;
            RetirementRatingEase = retirementRatingEase;
            GemCadenceSeasons = gemCadenceSeasons;
            GemMinAbility = gemMinAbility;
            GemMaxAbility = gemMaxAbility;
            GemMinPotential = gemMinPotential;
            GemMaxPotential = gemMaxPotential;
            YouthMinAge = youthMinAge;
            YouthMaxAge = youthMaxAge;
            YouthIntakePerSeason = youthIntakePerSeason;
            YouthMinAbilityOffset = youthMinAbilityOffset;
            YouthMinAbilityFloor = youthMinAbilityFloor;
            YouthMinAbilityCeiling = youthMinAbilityCeiling;
            YouthMaxAbilityOffset = youthMaxAbilityOffset;
            YouthMaxAbilityFloor = youthMaxAbilityFloor;
            YouthMaxAbilityCeiling = youthMaxAbilityCeiling;
            YouthMinPotentialOffset = youthMinPotentialOffset;
            YouthMinPotentialFloor = youthMinPotentialFloor;
            YouthMinPotentialCeiling = youthMinPotentialCeiling;
            YouthMaxPotentialOffset = youthMaxPotentialOffset;
            YouthMaxPotentialFloor = youthMaxPotentialFloor;
            YouthMaxPotentialCeiling = youthMaxPotentialCeiling;
            EmptySquadAverageRating = emptySquadAverageRating;
        }

        // Retirement thresholds by role group: no one plays past Hard, and in the twilight years the odds
        // climb with age. Keepers play latest of all.
        public int KeeperTwilightAge { get; }

        public int KeeperHardAge { get; }

        public int OutfielderTwilightAge { get; }

        public int OutfielderHardAge { get; }

        /// <summary>How much a high rating eases retirement in the twilight years (0 = age only, higher = stars linger).</summary>
        public double RetirementRatingEase { get; }

        /// <summary>How often, in seasons, a club's academy yields a gem — rare and guaranteed, never a per-player chance.</summary>
        public int GemCadenceSeasons { get; }

        // The academy gem's hidden band: low visible ability (hides among ordinary prospects), rare high ceiling.
        public byte GemMinAbility { get; }

        public byte GemMaxAbility { get; }

        public byte GemMinPotential { get; }

        public byte GemMaxPotential { get; }

        // The age range youth arrive at.
        public int YouthMinAge { get; }

        public int YouthMaxAge { get; }

        /// <summary>
        /// How many academy youths join each season beyond replacing retirees — so a club's academy feeds the
        /// squad every year, not only when a veteran leaves. 0 disables it.
        /// <para>
        /// Nothing caps the result: this is exactly the rate a squad grows at, for ever. There was a
        /// <c>MaxSquadSize</c> here and it silenced the intake permanently at 25 (retirement replaces
        /// one-for-one, so no slot ever reopened); it was removed on 2026-08-09 in favour of growth, with
        /// expiring contracts — not yet built — as the intended drain. See <see cref="SquadRenewal"/>.
        /// </para>
        /// </summary>
        public int YouthIntakePerSeason { get; }

        // The ordinary youth band: each edge is the squad's average rating plus an offset, then held
        // inside its own absolute floor/ceiling. The offsets are what make tier persist — a strong
        // club's academy is stronger — and the floor/ceiling pair is what keeps a bottom-of-the-pyramid
        // or a superclub squad from producing unplayable or world-class teenagers by arithmetic alone.

        /// <summary>Offset from the squad average for the youth band's lowest visible ability.</summary>
        public int YouthMinAbilityOffset { get; }

        public byte YouthMinAbilityFloor { get; }

        public byte YouthMinAbilityCeiling { get; }

        /// <summary>Offset from the squad average for the youth band's highest visible ability.</summary>
        public int YouthMaxAbilityOffset { get; }

        public byte YouthMaxAbilityFloor { get; }

        public byte YouthMaxAbilityCeiling { get; }

        /// <summary>Offset from the squad average for the youth band's lowest hidden potential.</summary>
        public int YouthMinPotentialOffset { get; }

        public byte YouthMinPotentialFloor { get; }

        public byte YouthMinPotentialCeiling { get; }

        /// <summary>Offset from the squad average for the youth band's highest hidden potential — the
        /// only positive one, so the best prospect can climb past today's first team.</summary>
        public int YouthMaxPotentialOffset { get; }

        public byte YouthMaxPotentialFloor { get; }

        public byte YouthMaxPotentialCeiling { get; }

        /// <summary>The average to assume when there is no one to average — an empty squad still has
        /// to draw its intake from somewhere, and a 0 average would floor every band.</summary>
        public int EmptySquadAverageRating { get; }

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new RenewalSettings()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static RenewalSettings Default { get; } = new RenewalSettings();
    }
}
