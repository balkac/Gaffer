namespace Gaffer.Domain.Drama
{
    /// <summary>
    /// How a choice's consequence is expressed — a closed set of state levers the resolver knows how
    /// to apply (the levers are code-backed behavior; which event uses which, and how hard, is data).
    /// Every kind changes real state (GDD §4.7 rule 1): morale reaches next week's effective strength,
    /// cash reaches the finances, a forced sale reaches the squad.
    /// </summary>
    /// <remarks>
    /// The numeric values are a PERSISTENCE CONTRACT. This enum is authored into drama `.asset` files
    /// (<c>DramaEventSO.EffectDef.kind</c>) and Unity serializes an enum field as its ORDINAL with no way
    /// to ask for the name — so reordering these members silently re-points every authored effect in every
    /// shipped asset, with no error and no possible after-the-fact migration (UNITY.md §7). Never reorder,
    /// renumber, or reuse a value; only append. <c>PersistedEnumValueTests</c> pins every member so a
    /// reorder fails loudly, by name.
    /// </remarks>
    public enum DramaEffectKind
    {
        /// <summary>Morale points on the subject for a limited number of weeks (magnitude signed).</summary>
        SubjectMorale = 0,

        /// <summary>Morale points on every player in the squad for a limited number of weeks.</summary>
        TeamMorale = 1,

        /// <summary>A flat cash amount into (positive) or out of (negative) the club (magnitude in currency).</summary>
        Cash = 2,

        /// <summary>A fraction of the club's current cash (magnitude signed, e.g. -0.2 = a fifth cut).</summary>
        CashFraction = 3,

        /// <summary>One week of the subject's wage, fined into the club's cash.</summary>
        SubjectWageFine = 4,

        /// <summary>The subject is sold at his market fee — the caller executes the transfer it owns.</summary>
        SellSubject = 5,

        /// <summary>The trait in <see cref="DramaEffect.Trait"/> passes to the subject's heir apparent —
        /// the retiring captain anoints a successor (the resolver picks him deterministically).</summary>
        GrantTraitToSuccessor = 6,
    }

    /// <summary>One concrete consequence of a drama choice. Immutable data.</summary>
    public readonly struct DramaEffect
    {
        public DramaEffect(DramaEffectKind kind, double magnitude = 0.0, int durationWeeks = 0)
            : this(kind, default(Gaffer.Domain.Traits.TraitId), magnitude, durationWeeks)
        {
        }

        public DramaEffect(DramaEffectKind kind, Gaffer.Domain.Traits.TraitId trait, double magnitude = 0.0, int durationWeeks = 0)
        {
            Kind = kind;
            Trait = trait;
            Magnitude = magnitude;
            DurationWeeks = durationWeeks;
        }

        public DramaEffectKind Kind { get; }

        /// <summary>The trait a trait-granting kind bestows; unset for the other kinds.</summary>
        public Gaffer.Domain.Traits.TraitId Trait { get; }

        public double Magnitude { get; }

        public int DurationWeeks { get; }
    }
}
