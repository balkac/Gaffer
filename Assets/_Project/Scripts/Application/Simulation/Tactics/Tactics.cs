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
    /// How a side turns possession into chances — patient possession (more chances, lower quality),
    /// balanced, or the counter (fewer chances but clinical, higher-quality breaks). Unlike mentality,
    /// this shapes the <see cref="ChanceProfile"/>, not the effective strength — it is how the counter
    /// plays differently in the sim, not just on paper.
    /// </summary>
    public enum Approach
    {
        Possession,
        Balanced,
        Counter,
    }

    /// <summary>
    /// A side's tactical setup: mentality, tempo, pressing, and approach (TDD §6.1). Mentality and
    /// pressing shift the effective strength <see cref="EffectiveStrengthBuilder"/> derives — attacking
    /// lifts attack and thins the defence, a high press wins the midfield but exposes the line. Tempo and
    /// approach instead shape the <see cref="ChanceProfile"/>: tempo drives how many chances a side makes,
    /// approach the volume-versus-quality trade (the counter makes fewer but sharper chances). This split
    /// keeps each axis mechanically real without double-counting. <see cref="Balanced"/> is the neutral
    /// setup that changes nothing. Each strength axis exposes a scale centred on zero; how far each step
    /// bends the sim comes from the injected <see cref="TacticsSettings"/> (data-driven, NON-NEGOTIABLE #3).
    /// </summary>
    public readonly struct Tactics
    {
        public Tactics(Mentality mentality, Tempo tempo, Pressing pressing)
            : this(mentality, tempo, pressing, Approach.Balanced)
        {
        }

        public Tactics(Mentality mentality, Tempo tempo, Pressing pressing, Approach approach)
        {
            Mentality = mentality;
            Tempo = tempo;
            Pressing = pressing;
            Approach = approach;
        }

        public Mentality Mentality { get; }

        public Tempo Tempo { get; }

        public Pressing Pressing { get; }

        public Approach Approach { get; }

        public static Tactics Balanced => new Tactics(Mentality.Balanced, Tempo.Standard, Pressing.Standard, Approach.Balanced);

        /// <summary>-2 (very defensive) … +2 (very attacking).</summary>
        public int MentalityScale => (int)Mentality - (int)Mentality.Balanced;

        /// <summary>-1 (patient) … +1 (intense).</summary>
        public int TempoScale => (int)Tempo - (int)Tempo.Standard;

        /// <summary>-1 (contain) … +1 (press).</summary>
        public int PressingScale => (int)Pressing - (int)Pressing.Standard;
    }
}
