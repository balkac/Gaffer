namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// The balance behind goal attribution (<see cref="WeightedScorerSelector"/>, NON-NEGOTIABLE #3):
    /// how the two scoring pathways weigh a player's attributes, and how much each position takes part
    /// in each pathway. Tuning these reshapes who scores — more corner-header defenders, rarer keeper
    /// goals — without touching the selection algorithm. Injectable and defaulted like every balance
    /// object; the authoring surface (`SimulationBalanceSO`) maps onto it. Immutable once built —
    /// <see cref="Default"/> is one shared cached instance, and <c>init</c> is what makes that safe
    /// rather than merely intended.
    /// </summary>
    public sealed class ScorerWeights
    {
        /// <summary>Floor keeping every outfielder a live threat; keepers are exempt.</summary>
        public double MinOutfielderWeight { get; init; } = 0.5;

        /// <summary>Open play: weight of finishing.</summary>
        public double OpenPlayFinishing { get; init; } = 0.6;

        /// <summary>Open play: weight of positioning.</summary>
        public double OpenPlayPositioning { get; init; } = 0.2;

        /// <summary>Open play: weight of pace.</summary>
        public double OpenPlayPace { get; init; } = 0.2;

        /// <summary>Aerial: weight of heading.</summary>
        public double AerialHeading { get; init; } = 0.6;

        /// <summary>Aerial: weight of jumping.</summary>
        public double AerialJumping { get; init; } = 0.25;

        /// <summary>Aerial: weight of strength.</summary>
        public double AerialStrength { get; init; } = 0.15;

        /// <summary>How much of the open-play pathway each position gets.</summary>
        public double OpenPlayForward { get; init; } = 1.0;

        public double OpenPlayMidfielder { get; init; } = 0.55;

        public double OpenPlayDefender { get; init; } = 0.10;

        public double OpenPlayGoalkeeper { get; init; } = 0.003;

        /// <summary>How much of the aerial (set-piece) pathway each position gets.</summary>
        public double AerialForward { get; init; } = 0.45;

        public double AerialMidfielder { get; init; } = 0.25;

        public double AerialDefender { get; init; } = 0.30;

        public double AerialGoalkeeper { get; init; } = 0.004;

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new ScorerWeights()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static ScorerWeights Default { get; } = new ScorerWeights();
    }
}
