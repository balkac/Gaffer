using System;
using System.Collections.Generic;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Traits;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for one drama event (NON-NEGOTIABLE #3: a new event is a new
    /// asset). Mirrors the pure <see cref="DramaEvent"/> record — trigger, weight, cooldown, trait
    /// biases, and the choices with their effects — and <see cref="ToEvent"/> maps to it. All copy
    /// fields hold localization keys, never display text.
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Content/Drama Event", fileName = "DramaEvent")]
    public sealed class DramaEventSO : ScriptableObject
    {
        [Serializable]
        public sealed class BiasDef
        {
            [Tooltip("Trait id slug, e.g. 'loyal'.")]
            public string traitSlug;

            [Tooltip("Weight multiplier when the trait is present (below 1 = calms, above 1 = feeds).")]
            public double weightMultiplier = 1.0;
        }

        [Serializable]
        public sealed class EffectDef
        {
            public DramaEffectKind kind;

            [Tooltip("Signed size: morale points, flat cash, or a cash fraction — per the kind.")]
            public double magnitude;

            [Tooltip("Weeks a morale effect stays live (ignored by the cash and squad kinds).")]
            public int durationWeeks;

            [Tooltip("Trait id slug for the trait-granting kind (ignored by the others).")]
            public string traitSlug;
        }

        [Serializable]
        public sealed class ChoiceDef
        {
            [Tooltip("Localization key for the choice label.")]
            public string labelKey;

            public List<EffectDef> effects = new List<EffectDef>();
        }

        [Tooltip("Stable id slug, e.g. 'transfer-request'.")]
        [SerializeField] private string slug = "new-event";

        [SerializeField] private DramaCategory category = DramaCategory.Personal;

        [Tooltip("Localization key for the title.")]
        [SerializeField] private string titleKey = "drama.new_event.title";

        [Tooltip("Localization key for the body copy.")]
        [SerializeField] private string bodyKey = "drama.new_event.body";

        [Header("Trigger — who and when (0/false/empty = not conditioned on it)")]
        [SerializeField] private bool requiresSubject;
        [SerializeField] private int minSubjectAge;
        [SerializeField] private int maxSubjectAge;
        [SerializeField] private double minSubjectRating;
        [SerializeField] private double minSubjectPotentialGap;
        [SerializeField] private bool subjectBenched;
        [SerializeField] private string requiredSubjectTraitSlug;
        [SerializeField] private int minLossStreak;
        [SerializeField] private int minTablePosition;
        [SerializeField] private bool requiresOpenWindow;

        [Header("Frequency — scarcity keeps drama valuable")]
        [SerializeField] private double baseWeight = 1.0;
        [SerializeField] private int cooldownWeeks = 12;
        [SerializeField] private bool oncePerRun;

        [Header("Trait biases")]
        [Tooltip("How the SUBJECT's own traits scale this event's weight for him.")]
        [SerializeField] private List<BiasDef> subjectTraitBiases = new List<BiasDef>();

        [Tooltip("How traits anywhere in the squad scale this event's weight.")]
        [SerializeField] private List<BiasDef> squadTraitBiases = new List<BiasDef>();

        [Header("The decision (at least two choices)")]
        [SerializeField] private List<ChoiceDef> choices = new List<ChoiceDef>();

        public DramaEvent ToEvent()
        {
            var trigger = new DramaTrigger
            {
                MinSubjectAge = minSubjectAge,
                MaxSubjectAge = maxSubjectAge,
                MinSubjectRating = minSubjectRating,
                MinSubjectPotentialGap = minSubjectPotentialGap,
                SubjectBenched = subjectBenched,
                MinLossStreak = minLossStreak,
                MinTablePosition = minTablePosition,
                RequiresOpenWindow = requiresOpenWindow,
                RequiredSubjectTrait = string.IsNullOrEmpty(requiredSubjectTraitSlug)
                    ? default
                    : new TraitId(requiredSubjectTraitSlug),
            };

            return new DramaEvent(
                new DramaEventId(slug), category, titleKey, bodyKey, requiresSubject, trigger,
                baseWeight, cooldownWeeks, MapChoices(), MapBiases(subjectTraitBiases), MapBiases(squadTraitBiases), oncePerRun);
        }

        private IReadOnlyList<DramaChoice> MapChoices()
        {
            var mapped = new List<DramaChoice>(choices.Count);
            foreach (ChoiceDef choice in choices)
            {
                var effects = new List<DramaEffect>(choice.effects.Count);
                foreach (EffectDef effect in choice.effects)
                {
                    effects.Add(new DramaEffect(
                        effect.kind,
                        string.IsNullOrEmpty(effect.traitSlug) ? default : new TraitId(effect.traitSlug),
                        effect.magnitude,
                        effect.durationWeeks));
                }

                mapped.Add(new DramaChoice(choice.labelKey, effects));
            }

            return mapped;
        }

        private static IReadOnlyList<DramaTraitBias> MapBiases(List<BiasDef> biases)
        {
            if (biases == null || biases.Count == 0)
            {
                return null;
            }

            var mapped = new List<DramaTraitBias>(biases.Count);
            foreach (BiasDef bias in biases)
            {
                if (!string.IsNullOrEmpty(bias.traitSlug))
                {
                    mapped.Add(new DramaTraitBias(new TraitId(bias.traitSlug), bias.weightMultiplier));
                }
            }

            return mapped;
        }
    }
}
