using System.Collections.Generic;

namespace Gaffer.Domain.Drama
{
    /// <summary>
    /// A pure drama event definition — one data record per event (TDD §8): what gates it
    /// (<see cref="Trigger"/>), how likely it is (<see cref="BaseWeight"/> shaped by trait biases),
    /// how rare it must stay (<see cref="CooldownWeeks"/>, <see cref="OncePerRun"/>), and the
    /// decision it forces (<see cref="Choices"/>, at least two — GDD §4.7 rule 2). Copy fields hold
    /// localization keys, never literal text (NON-NEGOTIABLE #8). Authored in an Infrastructure
    /// asset and mapped here; the engine only ever reads this type.
    /// </summary>
    public sealed class DramaEvent
    {
        public DramaEvent(
            DramaEventId id,
            DramaCategory category,
            string titleKey,
            string bodyKey,
            bool requiresSubject,
            DramaTrigger trigger,
            double baseWeight,
            int cooldownWeeks,
            IReadOnlyList<DramaChoice> choices,
            IReadOnlyList<DramaTraitBias> subjectTraitBiases = null,
            IReadOnlyList<DramaTraitBias> squadTraitBiases = null,
            bool oncePerRun = false)
        {
            Id = id;
            Category = category;
            TitleKey = titleKey;
            BodyKey = bodyKey;
            RequiresSubject = requiresSubject;
            Trigger = trigger;
            BaseWeight = baseWeight;
            CooldownWeeks = cooldownWeeks;
            Choices = choices;
            SubjectTraitBiases = subjectTraitBiases ?? System.Array.Empty<DramaTraitBias>();
            SquadTraitBiases = squadTraitBiases ?? System.Array.Empty<DramaTraitBias>();
            OncePerRun = oncePerRun;
        }

        public DramaEventId Id { get; }

        public DramaCategory Category { get; }

        public string TitleKey { get; }

        public string BodyKey { get; }

        /// <summary>Whether this event happens to a specific player (subject) or to the club itself.</summary>
        public bool RequiresSubject { get; }

        public DramaTrigger Trigger { get; }

        public double BaseWeight { get; }

        /// <summary>Weeks before this event may fire again — scarcity is what keeps drama valuable (GDD §4.7 rule 3).</summary>
        public int CooldownWeeks { get; }

        /// <summary>A set-piece anchor fires once per run, ever.</summary>
        public bool OncePerRun { get; }

        /// <summary>How the subject's own traits scale this event's weight for him.</summary>
        public IReadOnlyList<DramaTraitBias> SubjectTraitBiases { get; }

        /// <summary>How traits anywhere in the squad scale this event's weight (a leader calms the room).</summary>
        public IReadOnlyList<DramaTraitBias> SquadTraitBiases { get; }

        public IReadOnlyList<DramaChoice> Choices { get; }
    }
}
