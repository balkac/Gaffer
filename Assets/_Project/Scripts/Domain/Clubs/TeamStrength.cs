namespace Gaffer.Domain.Clubs
{
    /// <summary>
    /// A team's phase-based effective strength for one match — attack, midfield control, and defence
    /// as separate axes, not a single scalar (TDD §6.1). For now the command carries it pre-built;
    /// deriving it from squad + tactics + form + traits is a later step (BuildEffectiveStrength).
    /// </summary>
    public readonly struct TeamStrength
    {
        public TeamStrength(double attack, double midfield, double defence)
        {
            Attack = attack;
            Midfield = midfield;
            Defence = defence;
        }

        public double Attack { get; }

        public double Midfield { get; }

        public double Defence { get; }
    }
}
