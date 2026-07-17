using Gaffer.Domain.Traits;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for one trait (NON-NEGOTIABLE #3: a new trait is a new asset, not
    /// code). Every field is one of the pure <see cref="Trait"/>'s mechanical levers; <see cref="ToTrait"/>
    /// maps to the domain type the sim actually consumes (TDD §7 — definition authored here, read by
    /// simulation and drama alike). Name field holds a localization key, never display text.
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Content/Trait", fileName = "Trait")]
    public sealed class TraitSO : ScriptableObject
    {
        [Tooltip("Stable id slug, e.g. 'derby-beast' — saves reference traits by this, so never rename casually.")]
        [SerializeField] private string slug = "new-trait";

        [Tooltip("Localization key for the display name (never literal text).")]
        [SerializeField] private string nameKey = "trait.new_trait.name";

        [Tooltip("Relative weight the generator assigns this trait with (rarity).")]
        [SerializeField] private double assignmentWeight = 1.0;

        [Header("Match-context modifier — which stakes wake this trait, and how hard")]
        [SerializeField] private bool onDerby;
        [SerializeField] private bool onRivalry;
        [SerializeField] private bool onFinal;
        [SerializeField] private bool onTitleDecider;
        [SerializeField] private bool onRelegationSixPointer;
        [Tooltip("Crowd size that counts as big (0 = not crowd-keyed).")]
        [SerializeField] private int bigCrowdThreshold;
        [Tooltip("Rating multiplier when any flagged stake is present (1 = no match-day side).")]
        [SerializeField] private double matchMultiplier = 1.0;

        [Header("Dressing room")]
        [Tooltip("Rating multiplier on every TEAMMATE while he is in the eleven (1 = none).")]
        [SerializeField] private double teammateAura = 1.0;

        [Header("Development curve")]
        [Tooltip("Scale on seasonal growth toward potential (1 = neutral; a dodger sits below).")]
        [SerializeField] private double growthMultiplier = 1.0;
        [Tooltip("Years added to the decline onset age (negative = fades early).")]
        [SerializeField] private int declineOnsetShift;
        [Tooltip("Scale on post-peak erosion (1 = neutral; a glass man wears faster).")]
        [SerializeField] private double declineRateMultiplier = 1.0;

        /// <summary>Fills the serialized fields from a pure definition — used by the editor tooling to
        /// materialise the built-in catalog as tunable assets (no hand-authored YAML, decision #28).</summary>
        public void Author(Trait trait)
        {
            slug = trait.Id.Value;
            nameKey = trait.NameKey;
            assignmentWeight = trait.AssignmentWeight;

            MatchStakes stakes = trait.Match.Stakes;
            onDerby = (stakes & MatchStakes.Derby) != 0;
            onRivalry = (stakes & MatchStakes.Rivalry) != 0;
            onFinal = (stakes & MatchStakes.Final) != 0;
            onTitleDecider = (stakes & MatchStakes.TitleDecider) != 0;
            onRelegationSixPointer = (stakes & MatchStakes.RelegationSixPointer) != 0;
            bigCrowdThreshold = trait.Match.BigCrowdThreshold;
            matchMultiplier = stakes == MatchStakes.None ? 1.0 : trait.Match.Multiplier;

            teammateAura = trait.TeammateAura;
            growthMultiplier = trait.GrowthMultiplier;
            declineOnsetShift = trait.DeclineOnsetShift;
            declineRateMultiplier = trait.DeclineRateMultiplier;
        }

        public Trait ToTrait()
        {
            MatchStakes stakes = MatchStakes.None;
            if (onDerby)
            {
                stakes |= MatchStakes.Derby;
            }

            if (onRivalry)
            {
                stakes |= MatchStakes.Rivalry;
            }

            if (onFinal)
            {
                stakes |= MatchStakes.Final;
            }

            if (onTitleDecider)
            {
                stakes |= MatchStakes.TitleDecider;
            }

            if (onRelegationSixPointer)
            {
                stakes |= MatchStakes.RelegationSixPointer;
            }

            if (bigCrowdThreshold > 0)
            {
                stakes |= MatchStakes.BigCrowd;
            }

            MatchTraitModifier match = stakes == MatchStakes.None
                ? MatchTraitModifier.None
                : new MatchTraitModifier(stakes, matchMultiplier, bigCrowdThreshold);

            return new Trait(
                new TraitId(slug), nameKey, assignmentWeight, match,
                teammateAura, growthMultiplier, declineOnsetShift, declineRateMultiplier);
        }
    }
}
