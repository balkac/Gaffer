using System.Collections.Generic;
using System.Text;
using Gaffer.Common;

namespace Gaffer.Domain.Traits
{
    /// <summary>
    /// The set of trait definitions a run plays with, looked up by id. <see cref="Default"/> is the
    /// built-in calibrated catalog so the pure core and headless tests run without any assets;
    /// Infrastructure's trait assets map onto this type and override it (config-as-override, the
    /// BalanceSO pattern).
    /// <para>
    /// STRICTNESS POSTURE (ARCHITECTURE §11) — this type serves TWO situations and answers them
    /// oppositely, so both are written down here rather than left to whichever caller reads first:
    /// </para>
    /// <para>
    /// 1. LOOKUP (<see cref="Find"/>) is TOLERANT, and that is chosen for PLAYER SAVE DATA. A save
    /// outlives the build that wrote it, so a slug this build no longer defines must degrade — the
    /// player keeps his character, the trait rebinds if the definition returns — never crash a load.
    /// <c>SeasonSaveMapper</c> deliberately restores unknown slugs for exactly this reason.
    /// </para>
    /// <para>
    /// 2. LOADING AUTHORED CONTENT (<see cref="Validate"/>, and <c>DramaCatalog.ValidateAgainst</c>)
    /// is STRICT, and that is chosen for CONTENT THAT SHIPS INSIDE THE BUILD. Asset and reader are
    /// atomic here, so the schema version is a document fact and THERE IS NO ACCEPTANCE POLICY TO
    /// TUNE: a slug that resolves to nothing is an authoring typo, and a typo'd trait is a trait
    /// that silently becomes flavor text — precisely what NON-NEGOTIABLE #7 forbids and what no
    /// tolerant lookup can ever report. It must break CI instead.
    /// </para>
    /// <para>
    /// THIS IS THE SETTING THAT FLIPS the day content stops travelling with the binary. Served
    /// remotely, strict rejection is the mechanism that bricks live clients: unknown members would
    /// then have to be ignored, the version negotiated in the request, and content re-published
    /// rather than migrated. Nothing else will remind you — so change posture here, deliberately.
    /// </para>
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

        /// <summary>
        /// The trait for this id, or null when the catalog does not define it — the TOLERANT half of
        /// the posture above, kept that way for the save-restore path. Callers on the content-loading
        /// path must not lean on it; they go through <see cref="Validate"/> first.
        /// </summary>
        public Trait Find(TraitId id)
        {
            return _byId.TryGetValue(id, out Trait trait) ? trait : null;
        }

        /// <summary>Whether this catalog defines the id — the question <see cref="Find"/> answers with
        /// null, asked without pretending the absence is a usable trait.</summary>
        public bool Defines(TraitId id)
        {
            return !string.IsNullOrEmpty(id.Value) && _byId.ContainsKey(id);
        }

        /// <summary>
        /// The STRICT half of the posture: checks this catalog's own integrity as authored content that
        /// ships in the build — every trait carries a slug, and no two traits share one (the second
        /// would silently win the dictionary and erase the first's mechanics). Reports EVERY problem,
        /// naming each offending id, so one authoring pass fixes them all. An expected, recoverable
        /// failure of a load, so it is a <see cref="Result"/>, not a throw (CONVENTIONS §4).
        /// </summary>
        public Result Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();
            for (int i = 0; i < _traits.Count; i++)
            {
                // A null entry cannot reach here — the constructor's id indexing would have raised on it
                // already, which is the right answer for a broken invariant (CONVENTIONS §4).
                string id = _traits[i].Id.Value;
                if (string.IsNullOrEmpty(id))
                {
                    problems.Add($"Trait at index {i} has no id slug.");
                    continue;
                }

                if (!seen.Add(id))
                {
                    problems.Add($"Trait id '{id}' is defined more than once.");
                }
            }

            return Describe(problems, "Trait catalog");
        }

        /// <summary>Joins collected authoring problems into one failure message — one report per load
        /// rather than one per fix-compile-run cycle. Shared with <c>DramaCatalog</c>'s validation.</summary>
        internal static Result Describe(List<string> problems, string subject)
        {
            if (problems.Count == 0)
            {
                return Result.Success();
            }

            var message = new StringBuilder();
            message.Append(subject).Append(" is invalid: ");
            for (int i = 0; i < problems.Count; i++)
            {
                if (i > 0)
                {
                    message.Append(' ');
                }

                message.Append(problems[i]);
            }

            return Result.Failure(message.ToString());
        }

        /// <summary>
        /// The built-in catalog: four match-context traits (GDD §4.2's canonical pair plus the crowd
        /// and the dressing room), three development traits (the real per-player driver decisions
        /// #21/#23 deferred to this phase), and two drama-bias traits whose mechanics live in the
        /// event weights that reference them (loyal shrinks a transfer request, a press magnet feeds
        /// the scandal pages). Numbers are starting calibration; the asset layer tunes them.
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
            new Trait(
                new TraitId("loyal"), "trait.loyal.name", 1.2),
            new Trait(
                new TraitId("press-magnet"), "trait.press_magnet.name", 0.6),
        });
    }
}
