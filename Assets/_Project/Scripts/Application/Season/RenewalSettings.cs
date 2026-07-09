namespace Gaffer.Application.Season
{
    /// <summary>
    /// The balance constants <see cref="SquadRenewal"/> (and the gem cadence in <see cref="SeasonTransition"/>)
    /// read — retirement ages, how a high rating stays a player's retirement, the academy-gem cadence and its
    /// hidden band, and the youth intake age range. Kept in one injected value so tuning changes the numbers,
    /// not the code (NON-NEGOTIABLE #3); <c>RenewalBalanceSO</c> authors it in Unity, and <see cref="Default"/>
    /// is the calibrated baseline. The youth ability/potential band relative to the squad's level stays
    /// structural (tier persistence) for now. A mutable settings object like <c>DevelopmentSettings</c>.
    /// </summary>
    public sealed class RenewalSettings
    {
        // Retirement thresholds by role group: no one plays past Hard, and in the twilight years the odds
        // climb with age. Keepers play latest of all.
        public int KeeperTwilightAge { get; set; } = 36;

        public int KeeperHardAge { get; set; } = 43;

        public int OutfielderTwilightAge { get; set; } = 33;

        public int OutfielderHardAge { get; set; } = 40;

        /// <summary>How much a high rating eases retirement in the twilight years (0 = age only, higher = stars linger).</summary>
        public double RetirementRatingEase { get; set; } = 0.4;

        /// <summary>How often, in seasons, a club's academy yields a gem — rare and guaranteed, never a per-player chance.</summary>
        public int GemCadenceSeasons { get; set; } = 5;

        // The academy gem's hidden band: low visible ability (hides among ordinary prospects), rare high ceiling.
        public byte GemMinAbility { get; set; } = 28;

        public byte GemMaxAbility { get; set; } = 46;

        public byte GemMinPotential { get; set; } = 86;

        public byte GemMaxPotential { get; set; } = 96;

        // The age range youth arrive at.
        public int YouthMinAge { get; set; } = 16;

        public int YouthMaxAge { get; set; } = 18;

        public static RenewalSettings Default => new RenewalSettings();
    }
}
