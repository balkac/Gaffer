namespace Gaffer.Application.Progression
{
    /// <summary>
    /// The balance constants <see cref="PlayerDevelopment"/> reads, kept in one injected value so tuning
    /// changes the numbers, not the code (NON-NEGOTIABLE #3). The pure core consumes this; the Infrastructure
    /// <c>DevelopmentBalanceSO</c> is the Unity authoring surface that maps to it, and <see cref="Default"/>
    /// keeps the core working headless with the calibrated values. Immutable once built — get-only
    /// properties set by one constructor whose parameters are all optional, so a caller names only what it
    /// overrides. The defaults live in the constructor signature and are baked into every calling assembly
    /// at compile time, so a changed default needs a full recompile before it is live everywhere (see
    /// <c>SettingsDefaultContractTests</c>).
    /// </summary>
    public sealed class DevelopmentSettings
    {
        /// <summary>
        /// Every parameter is optional and defaulted to the calibrated value, so a caller writes only what
        /// it changes: <c>new DevelopmentSettings(declinePerYear: 2.0)</c>.
        /// </summary>
        public DevelopmentSettings(
            double growthRateTo20 = 0.14,
            double growthRateTo22 = 0.10,
            double growthRateTo24 = 0.07,
            double growthRateTo26 = 0.045,
            double growthRateTo29 = 0.02,
            double minSeasonVariance = 0.6,
            double maxSeasonVariance = 1.4,
            int keeperPeakAge = 34,
            int centralPeakAge = 32,
            int widePeakAge = 31,
            int forwardPeakAge = 30,
            int minDeclineAge = 30,
            double declinePerYear = 0.9,
            int maxDeclineYears = 6,
            double generalDeclineFactor = 0.6,
            byte attributeFloor = 25,
            byte physicalFloor = 15,
            int weeksPerTick = 4,
            int seasonWeeks = 38,
            double starterPlayingTime = 1.0,
            double benchPlayingTime = 0.45,
            double unattachedPlayingTime = 0.35)
        {
            GrowthRateTo20 = growthRateTo20;
            GrowthRateTo22 = growthRateTo22;
            GrowthRateTo24 = growthRateTo24;
            GrowthRateTo26 = growthRateTo26;
            GrowthRateTo29 = growthRateTo29;
            MinSeasonVariance = minSeasonVariance;
            MaxSeasonVariance = maxSeasonVariance;
            KeeperPeakAge = keeperPeakAge;
            CentralPeakAge = centralPeakAge;
            WidePeakAge = widePeakAge;
            ForwardPeakAge = forwardPeakAge;
            MinDeclineAge = minDeclineAge;
            DeclinePerYear = declinePerYear;
            MaxDeclineYears = maxDeclineYears;
            GeneralDeclineFactor = generalDeclineFactor;
            AttributeFloor = attributeFloor;
            PhysicalFloor = physicalFloor;
            WeeksPerTick = weeksPerTick < 1 ? 1 : weeksPerTick;
            SeasonWeeks = seasonWeeks < 1 ? 1 : seasonWeeks;
            StarterPlayingTime = starterPlayingTime;
            BenchPlayingTime = benchPlayingTime;
            UnattachedPlayingTime = unattachedPlayingTime;
        }

        // Growth as a fraction of the remaining ability gap, by age band — steep for teenagers, a trickle by
        // the late twenties, zero after (ages read against the thresholds below in PlayerDevelopment).
        public double GrowthRateTo20 { get; }

        public double GrowthRateTo22 { get; }

        public double GrowthRateTo24 { get; }

        public double GrowthRateTo26 { get; }

        public double GrowthRateTo29 { get; }

        // Per-season multiplier on growth and decline so a career is not a smooth curve (some seasons kick on,
        // some stall). Centred on 1.0.
        public double MinSeasonVariance { get; }

        public double MaxSeasonVariance { get; }

        // The age each role tends to peak at before decline — keepers latest, pacey forwards first. A stable
        // per-player offset then shifts this a couple of years either way.
        public int KeeperPeakAge { get; }

        public int CentralPeakAge { get; }

        public int WidePeakAge { get; }

        public int ForwardPeakAge { get; }

        /// <summary>No one declines before this age, whatever his role or offset — physical decline is real by then.</summary>
        public int MinDeclineAge { get; }

        // Ability lost per season past the peak, accelerating with years (capped) into the mid-thirties.
        public double DeclinePerYear { get; }

        public int MaxDeclineYears { get; }

        /// <summary>How much of a season's decline hits the role's own attributes vs the athletic erosion on top.</summary>
        public double GeneralDeclineFactor { get; }

        /// <summary>Floor for the role's rating attributes as age erodes them — an old pro loses a step, not his craft.</summary>
        public byte AttributeFloor { get; }

        /// <summary>Floor for the raw physical attributes under athletic decline.</summary>
        public byte PhysicalFloor { get; }

        /// <summary>
        /// How many match weeks pass between development ticks — the resolution at which a career moves
        /// inside a season. Development used to land in one jump at the summer rollover, which is not what
        /// a season feels like: a teenager should be visibly better in March than he was in August (the
        /// owner's ask, 2026-08-13).
        ///
        /// <para>Not one, deliberately. A week of a season's growth is roughly a fifth of an attribute
        /// point — under the resolution of a <c>byte</c>, so it would land as noise — and every tick
        /// rebuilds each club's roster and drops the memoised elevens
        /// (<c>LeagueSeason._autoElevenByClub</c>). Four weeks is about nine visible steps a season, at a
        /// ninth of the churn. Tune it here rather than in code.</para>
        /// </summary>
        public int WeeksPerTick { get; }

        /// <summary>
        /// The match weeks in a full season — the denominator that makes a tick a FRACTION of a season, so
        /// the ticks over a season add up to what the old once-a-year jump delivered instead of multiplying
        /// it. A league whose real round count differs is handled by the caller passing its own; this is the
        /// fallback for anything that develops without a season around it (the market).
        /// </summary>
        public int SeasonWeeks { get; }

        // What a spell of playing time is worth to a career. Football's oldest development rule and the
        // reason squad rotation is a decision rather than a chore: minutes are what a young player grows
        // on. A regular starter keeps the full historical rate, so the calibrated curve still describes
        // someone who plays; anyone below that grows more slowly than he used to.
        /// <summary>Growth multiplier for a player who started every match in the period.</summary>
        public double StarterPlayingTime { get; }

        /// <summary>Growth multiplier for a squad player who did not start — training, not matches.</summary>
        public double BenchPlayingTime { get; }

        /// <summary>
        /// Growth multiplier for a player nobody has signed. The market is not a freezer — an unattached
        /// teenager still develops — but he develops like someone without a first team.
        /// </summary>
        public double UnattachedPlayingTime { get; }

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new DevelopmentSettings()</c> would
        /// be a method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static DevelopmentSettings Default { get; } = new DevelopmentSettings();
    }
}
