namespace Gaffer.Application.Simulation
{
    /// <summary>How far a side tips toward attack or defence — the primary tactical axis (TDD §6.1).</summary>
    public enum Mentality
    {
        VeryDefensive,
        Defensive,
        Balanced,
        Attacking,
        VeryAttacking,
    }

    /// <summary>How fast a side plays — patient build-up versus an intense, end-to-end pace.</summary>
    public enum Tempo
    {
        Patient,
        Standard,
        Intense,
    }

    /// <summary>How high a side presses — sitting back to contain versus hunting the ball high.</summary>
    public enum Pressing
    {
        Contain,
        Standard,
        Press,
    }

    /// <summary>
    /// A side's tactical setup: mentality, tempo, and pressing (TDD §6.1). It shifts the effective
    /// strength <see cref="EffectiveStrengthBuilder"/> derives — attacking lifts attack and thins the
    /// defence, an intense tempo pushes forward at a defensive cost, a high press wins the midfield but
    /// exposes the line. <see cref="Balanced"/> is the neutral setup that changes nothing. Each axis
    /// exposes a scale centred on zero so the builder can weight it; the weights tune into a BalanceSO.
    /// </summary>
    public readonly struct Tactics
    {
        public Tactics(Mentality mentality, Tempo tempo, Pressing pressing)
        {
            Mentality = mentality;
            Tempo = tempo;
            Pressing = pressing;
        }

        public Mentality Mentality { get; }

        public Tempo Tempo { get; }

        public Pressing Pressing { get; }

        public static Tactics Balanced => new Tactics(Mentality.Balanced, Tempo.Standard, Pressing.Standard);

        /// <summary>-2 (very defensive) … +2 (very attacking).</summary>
        public int MentalityScale => (int)Mentality - (int)Mentality.Balanced;

        /// <summary>-1 (patient) … +1 (intense).</summary>
        public int TempoScale => (int)Tempo - (int)Tempo.Standard;

        /// <summary>-1 (contain) … +1 (press).</summary>
        public int PressingScale => (int)Pressing - (int)Pressing.Standard;
    }
}
