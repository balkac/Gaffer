using Gaffer.Application.Drama;
using Gaffer.Application.Progression;
using Gaffer.Application.Rivals;
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
    /// <para>Immutable once built: get-only properties set by one all-optional constructor. This bundle
    /// carries no numbers of its own — every parameter defaults to <c>null</c> and resolves to the owning
    /// type's <c>Default</c> at run time, so unlike the settings types it holds, nothing here is baked
    /// into a calling assembly and no member can go stale between recompiles.</para>
    /// </summary>
    public sealed class RunBalance
    {
        /// <summary>
        /// Every parameter is optional; <c>null</c> means "the calibrated default for that type", so a
        /// caller names only the bundles it authors: <c>new RunBalance(drama: authored)</c>.
        /// </summary>
        public RunBalance(
            MatchSimulationSettings? simulation = null,
            TacticsSettings tacticsBalance = null,
            ScorerWeights scorer = null,
            DevelopmentSettings development = null,
            RenewalSettings renewal = null,
            DramaSettings drama = null,
            MoraleSettings morale = null,
            MatchContextSettings matchContexts = null,
            RivalSettings rivals = null,
            EconomySettings economy = null,
            ScoutingSettings scouting = null,
            Gaffer.Domain.Traits.TraitCatalog traits = null,
            Gaffer.Domain.Drama.DramaCatalog dramaEvents = null)
        {
            Simulation = simulation ?? MatchSimulationSettings.Default;
            TacticsBalance = tacticsBalance ?? TacticsSettings.Default;
            Scorer = scorer ?? ScorerWeights.Default;
            Development = development ?? DevelopmentSettings.Default;
            Renewal = renewal ?? RenewalSettings.Default;
            Drama = drama ?? DramaSettings.Default;
            Morale = morale ?? MoraleSettings.Default;
            MatchContexts = matchContexts ?? MatchContextSettings.Default;
            Rivals = rivals ?? RivalSettings.Default;
            Economy = economy ?? EconomySettings.Default;
            Scouting = scouting ?? ScoutingSettings.Default;
            Traits = traits ?? Gaffer.Domain.Traits.TraitCatalog.Default;
            DramaEvents = dramaEvents ?? Gaffer.Domain.Drama.DramaCatalog.Default;
        }

        public MatchSimulationSettings Simulation { get; }

        public TacticsSettings TacticsBalance { get; }

        public ScorerWeights Scorer { get; }

        public DevelopmentSettings Development { get; }

        public RenewalSettings Renewal { get; }

        public DramaSettings Drama { get; }

        public MoraleSettings Morale { get; }

        /// <summary>When a league fixture stops being just another game — the run-in, the places that
        /// count as a title race or a relegation fight (<see cref="MatchContextBuilder"/>).</summary>
        public MatchContextSettings MatchContexts { get; }

        /// <summary>How the clubs the manager does not run behave in the market and on the pitch.</summary>
        public RivalSettings Rivals { get; }

        public EconomySettings Economy { get; }

        public ScoutingSettings Scouting { get; }

        public Gaffer.Domain.Traits.TraitCatalog Traits { get; }

        public Gaffer.Domain.Drama.DramaCatalog DramaEvents { get; }

        /// <summary>The calibrated balance — one cached, shared, immutable instance (PERFORMANCE §8).</summary>
        public static RunBalance Default { get; } = new RunBalance();
    }
}
