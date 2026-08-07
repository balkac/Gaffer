using Gaffer.Application.Simulation;

namespace Gaffer.Application.Run
{
    /// <summary>
    /// The knobs a run is started with — the world's size and seed, which club is managed, what the
    /// board demands, the money it starts on, and the market it shops in. One value object instead of a
    /// dozen fields copied into each editor window, so a run is set up in one place and read back the
    /// same way everywhere (ARCHITECTURE §6). Immutable once built: get-only properties set by one
    /// all-optional constructor keep the cached <see cref="Default"/> from being edited by whoever reads
    /// it. The scalar defaults live in the constructor signature and are baked into every calling assembly
    /// at compile time, so a changed default needs a full recompile before it is live everywhere (see
    /// <c>SettingsDefaultContractTests</c>).
    /// </summary>
    public sealed class RunSetup
    {
        /// <summary>
        /// Every parameter is optional and defaulted to the calibrated value, so a caller writes only what
        /// it changes: <c>new RunSetup(teamCount: 8)</c>. The three struct-valued knobs take
        /// <c>null</c> for "leave it alone" — their defaults are constructor calls, which C# cannot put in
        /// a signature; the pleasant side effect is that those three are the only ones never baked into a
        /// caller.
        /// </summary>
        public RunSetup(
            int teamCount = 20,
            ulong seed = 20260709UL,
            int managedClubIndex = 15,
            int promotionPosition = 3,
            int survivalPosition = 17,
            long startingCash = 6_000_000L,
            long weeklyWageBudget = 160_000L,
            int marketSize = 40,
            int guaranteedGems = 3,
            Formation? formation = null,
            Tactics? tactics = null,
            MatchContext? matchContext = null)
        {
            TeamCount = teamCount;
            Seed = seed;
            ManagedClubIndex = managedClubIndex;
            PromotionPosition = promotionPosition;
            SurvivalPosition = survivalPosition;
            StartingCash = startingCash;
            WeeklyWageBudget = weeklyWageBudget;
            MarketSize = marketSize;
            GuaranteedGems = guaranteedGems;
            Formation = formation ?? Simulation.Formation.F442;
            Tactics = tactics ?? Simulation.Tactics.Balanced;
            MatchContext = matchContext ??
                new MatchContext(MatchImportance.Normal, 12000, isTitleDecider: false, isRivalry: false);
        }

        /// <summary>How many clubs contest the league. Clamped to [4, 64] when the run is built.</summary>
        public int TeamCount { get; }

        /// <summary>The run's master seed — world generation, matches, drama and the market all derive
        /// their own streams from it, so the same seed reproduces the same run (NON-NEGOTIABLE #2).</summary>
        public ulong Seed { get; }

        /// <summary>Which club the manager takes, by table rank at generation (0 = strongest).</summary>
        public int ManagedClubIndex { get; }

        /// <summary>Finish at or above this position to go up.</summary>
        public int PromotionPosition { get; }

        /// <summary>Finish at or above this position to keep the job.</summary>
        public int SurvivalPosition { get; }

        /// <summary>Transfer cash the run starts with.</summary>
        public long StartingCash { get; }

        /// <summary>The weekly wage budget the wage bill must stay under.</summary>
        public long WeeklyWageBudget { get; }

        /// <summary>How many free agents the market shows each season.</summary>
        public int MarketSize { get; }

        /// <summary>How many of them are guaranteed discoverable gems (TDD §5).</summary>
        public int GuaranteedGems { get; }

        /// <summary>The shape the managed club lines up in until the manager changes it.</summary>
        public Formation Formation { get; }

        /// <summary>The tactical setup the managed club starts on.</summary>
        public Tactics Tactics { get; }

        /// <summary>The stakes every league fixture is played under (traits read it).</summary>
        public MatchContext MatchContext { get; }

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
            return new RunSetup(
                teamCount: TeamCount,
                seed: seed,
                managedClubIndex: ManagedClubIndex,
                promotionPosition: PromotionPosition,
                survivalPosition: SurvivalPosition,
                startingCash: StartingCash,
                weeklyWageBudget: WeeklyWageBudget,
                marketSize: MarketSize,
                guaranteedGems: GuaranteedGems,
                formation: Formation,
                tactics: Tactics,
                matchContext: MatchContext);
        }
    }
}
