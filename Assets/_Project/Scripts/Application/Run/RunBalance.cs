using Gaffer.Application.Drama;
using Gaffer.Application.Progression;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;

namespace Gaffer.Application.Run
{
    /// <summary>
    /// Every tuning object and content catalog a run is played on, in one bundle. This is the seam the
    /// framework layers fill from their config assets (<c>SimulationBalanceSO</c>, <c>TraitCatalogSO</c>,
    /// …) and the only place the run's object graph reads balance from — so a caller cannot wire the
    /// league generator on one trait catalog and the season on another, which is exactly the divergence
    /// two hand-wired editor windows had grown (ARCHITECTURE §6).
    /// <para>Any member left null falls back to that type's calibrated default when the run is built
    /// (ARCHITECTURE §7 — the fallback lives at the wiring seam, not inside the collaborator).</para>
    /// </summary>
    public sealed class RunBalance
    {
        public MatchSimulationSettings Simulation { get; init; } = MatchSimulationSettings.Default;

        public TacticsSettings TacticsBalance { get; init; } = TacticsSettings.Default;

        public ScorerWeights Scorer { get; init; } = ScorerWeights.Default;

        public DevelopmentSettings Development { get; init; } = DevelopmentSettings.Default;

        public RenewalSettings Renewal { get; init; } = RenewalSettings.Default;

        public DramaSettings Drama { get; init; } = DramaSettings.Default;

        public MoraleSettings Morale { get; init; } = MoraleSettings.Default;

        public EconomySettings Economy { get; init; } = EconomySettings.Default;

        public ScoutingSettings Scouting { get; init; } = ScoutingSettings.Default;

        public Gaffer.Domain.Traits.TraitCatalog Traits { get; init; } = Gaffer.Domain.Traits.TraitCatalog.Default;

        public Gaffer.Domain.Drama.DramaCatalog DramaEvents { get; init; } = Gaffer.Domain.Drama.DramaCatalog.Default;

        /// <summary>The calibrated balance — one cached, shared, immutable instance (PERFORMANCE §8).</summary>
        public static RunBalance Default { get; } = new RunBalance();
    }
}
