using System.Collections.Generic;

namespace Gaffer.Domain.Traits
{
    /// <summary>
    /// The set of trait definitions a run plays with, looked up by id. <see cref="Default"/> is the
    /// built-in calibrated catalog so the pure core and headless tests run without any assets;
    /// Infrastructure's trait assets map onto this type and override it (config-as-override, the
    /// BalanceSO pattern). An id the catalog does not know resolves to null and is ignored — a save
    /// from a differently-authored catalog degrades instead of crashing.
    /// </summary>
    public sealed class TraitCatalog
    {
        private readonly Dictionary<TraitId, Trait> _byId;
        private readonly List<Trait> _traits;

        public TraitCatalog(IReadOnlyList<Trait> traits)
        {
            _traits = new List<Trait>(traits);
            _byId = new Dictionary<TraitId, Trait>(traits.Count);
            foreach (Trait trait in traits)
            {
                _byId[trait.Id] = trait;
            }
        }

        public IReadOnlyList<Trait> Traits => _traits;

        /// <summary>The trait for this id, or null when the catalog does not define it.</summary>
        public Trait Find(TraitId id)
        {
            return _byId.TryGetValue(id, out Trait trait) ? trait : null;
        }

        /// <summary>
        /// The built-in catalog: four match-context traits (GDD §4.2's canonical pair plus the crowd
        /// and the dressing room) and three development traits (the real per-player driver decisions
        /// #21/#23 deferred to this phase). Numbers are starting calibration; the asset layer tunes them.
        /// </summary>
        public static TraitCatalog Default { get; } = new TraitCatalog(new[]
        {
            new Trait(
                new TraitId("derby-beast"), "trait.derby_beast.name", 1.0,
                new MatchTraitModifier(MatchStakes.Derby | MatchStakes.Rivalry, 1.12)),
            new Trait(
                new TraitId("big-game-bottler"), "trait.big_game_bottler.name", 1.0,
                new MatchTraitModifier(
                    MatchStakes.Derby | MatchStakes.Rivalry | MatchStakes.Final
                    | MatchStakes.TitleDecider | MatchStakes.RelegationSixPointer,
                    0.88)),
            new Trait(
                new TraitId("showman"), "trait.showman.name", 0.8,
                new MatchTraitModifier(MatchStakes.BigCrowd, 1.08, bigCrowdThreshold: 25000)),
            new Trait(
                new TraitId("dressing-room-leader"), "trait.dressing_room_leader.name", 0.5,
                teammateAura: 1.03),
            new Trait(
                new TraitId("training-dodger"), "trait.training_dodger.name", 1.0,
                growthMultiplier: 0.5),
            new Trait(
                new TraitId("model-professional"), "trait.model_professional.name", 1.0,
                growthMultiplier: 1.25,
                declineOnsetShift: 2),
            new Trait(
                new TraitId("glass-man"), "trait.glass_man.name", 0.8,
                declineOnsetShift: -3,
                declineRateMultiplier: 1.5),
        });
    }
}
