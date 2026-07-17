namespace Gaffer.Domain.Drama
{
    /// <summary>
    /// How a choice's consequence is expressed — a closed set of state levers the resolver knows how
    /// to apply (the levers are code-backed behavior; which event uses which, and how hard, is data).
    /// Every kind changes real state (GDD §4.7 rule 1): morale reaches next week's effective strength,
    /// cash reaches the finances, a forced sale reaches the squad.
    /// </summary>
    public enum DramaEffectKind
    {
        /// <summary>Morale points on the subject for a limited number of weeks (magnitude signed).</summary>
        SubjectMorale,

        /// <summary>Morale points on every player in the squad for a limited number of weeks.</summary>
        TeamMorale,

        /// <summary>A flat cash amount into (positive) or out of (negative) the club (magnitude in currency).</summary>
        Cash,

        /// <summary>A fraction of the club's current cash (magnitude signed, e.g. -0.2 = a fifth cut).</summary>
        CashFraction,

        /// <summary>One week of the subject's wage, fined into the club's cash.</summary>
        SubjectWageFine,

        /// <summary>The subject is sold at his market fee — the caller executes the transfer it owns.</summary>
        SellSubject,
    }

    /// <summary>One concrete consequence of a drama choice. Immutable data.</summary>
    public readonly struct DramaEffect
    {
        public DramaEffect(DramaEffectKind kind, double magnitude = 0.0, int durationWeeks = 0)
        {
            Kind = kind;
            Magnitude = magnitude;
            DurationWeeks = durationWeeks;
        }

        public DramaEffectKind Kind { get; }

        public double Magnitude { get; }

        public int DurationWeeks { get; }
    }
}
