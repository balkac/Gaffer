using Gaffer.Application.Simulation;

namespace Gaffer.Application.Run
{
    /// <summary>
    /// The knobs a run is started with — the world's size and seed, which club is managed, what the
    /// board demands, the money it starts on, and the market it shops in. One value object instead of a
    /// dozen fields copied into each editor window, so a run is set up in one place and read back the
    /// same way everywhere (ARCHITECTURE §6). Immutable once built: <c>init</c> keeps the cached
    /// <see cref="Default"/> from being edited by whoever reads it.
    /// </summary>
    public sealed class RunSetup
    {
        /// <summary>How many clubs contest the league. Clamped to [4, 64] when the run is built.</summary>
        public int TeamCount { get; init; } = 20;

        /// <summary>The run's master seed — world generation, matches, drama and the market all derive
        /// their own streams from it, so the same seed reproduces the same run (NON-NEGOTIABLE #2).</summary>
        public ulong Seed { get; init; } = 20260709UL;

        /// <summary>Which club the manager takes, by table rank at generation (0 = strongest).</summary>
        public int ManagedClubIndex { get; init; } = 15;

        /// <summary>Finish at or above this position to go up.</summary>
        public int PromotionPosition { get; init; } = 3;

        /// <summary>Finish at or above this position to keep the job.</summary>
        public int SurvivalPosition { get; init; } = 17;

        /// <summary>Transfer cash the run starts with.</summary>
        public long StartingCash { get; init; } = 6_000_000L;

        /// <summary>The weekly wage budget the wage bill must stay under.</summary>
        public long WeeklyWageBudget { get; init; } = 160_000L;

        /// <summary>How many free agents the market shows each season.</summary>
        public int MarketSize { get; init; } = 40;

        /// <summary>How many of them are guaranteed discoverable gems (TDD §5).</summary>
        public int GuaranteedGems { get; init; } = 3;

        /// <summary>The shape the managed club lines up in until the manager changes it.</summary>
        public Formation Formation { get; init; } = Formation.F442;

        /// <summary>The tactical setup the managed club starts on.</summary>
        public Tactics Tactics { get; init; } = Tactics.Balanced;

        /// <summary>The stakes every league fixture is played under (traits read it).</summary>
        public MatchContext MatchContext { get; init; } =
            new MatchContext(MatchImportance.Normal, 12000, isTitleDecider: false, isRivalry: false);

        /// <summary>The default run setup — one cached, shared, immutable instance (PERFORMANCE §8).</summary>
        public static RunSetup Default { get; } = new RunSetup();

        /// <summary>
        /// This setup on a different seed. Resuming a save uses it: the run must continue on the seed the
        /// save was written with, or the remaining fixtures diverge from the run being resumed, however
        /// the caller's own seed field has been edited since. Copies every member — a new one added above
        /// belongs here too.
        /// </summary>
        public RunSetup WithSeed(ulong seed)
        {
            return new RunSetup
            {
                TeamCount = TeamCount,
                Seed = seed,
                ManagedClubIndex = ManagedClubIndex,
                PromotionPosition = PromotionPosition,
                SurvivalPosition = SurvivalPosition,
                StartingCash = StartingCash,
                WeeklyWageBudget = WeeklyWageBudget,
                MarketSize = MarketSize,
                GuaranteedGems = GuaranteedGems,
                Formation = Formation,
                Tactics = Tactics,
                MatchContext = MatchContext,
            };
        }
    }
}
