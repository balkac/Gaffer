namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// The balance behind goal attribution (<see cref="WeightedScorerSelector"/>, NON-NEGOTIABLE #3):
    /// how the two scoring pathways weigh a player's attributes, and how much each position takes part
    /// in each pathway. Tuning these reshapes who scores — more corner-header defenders, rarer keeper
    /// goals — without touching the selection algorithm. Injectable and defaulted like every balance
    /// object; the authoring surface (`SimulationBalanceSO`) maps onto it. Treat an instance as
    /// immutable once built — <see cref="Default"/> is shared.
    /// </summary>
    public sealed class ScorerWeights
    {
        /// <summary>Floor keeping every outfielder a live threat; keepers are exempt.</summary>
        public double MinOutfielderWeight { get; set; } = 0.5;

        /// <summary>Open play: weight of finishing.</summary>
        public double OpenPlayFinishing { get; set; } = 0.6;

        /// <summary>Open play: weight of positioning.</summary>
        public double OpenPlayPositioning { get; set; } = 0.2;

        /// <summary>Open play: weight of pace.</summary>
        public double OpenPlayPace { get; set; } = 0.2;

        /// <summary>Aerial: weight of heading.</summary>
        public double AerialHeading { get; set; } = 0.6;

        /// <summary>Aerial: weight of jumping.</summary>
        public double AerialJumping { get; set; } = 0.25;

        /// <summary>Aerial: weight of strength.</summary>
        public double AerialStrength { get; set; } = 0.15;

        /// <summary>How much of the open-play pathway each position gets.</summary>
        public double OpenPlayForward { get; set; } = 1.0;

        public double OpenPlayMidfielder { get; set; } = 0.55;

        public double OpenPlayDefender { get; set; } = 0.10;

        public double OpenPlayGoalkeeper { get; set; } = 0.003;

        /// <summary>How much of the aerial (set-piece) pathway each position gets.</summary>
        public double AerialForward { get; set; } = 0.45;

        public double AerialMidfielder { get; set; } = 0.25;

        public double AerialDefender { get; set; } = 0.30;

        public double AerialGoalkeeper { get; set; } = 0.004;

        /// <summary>The calibrated defaults — cached; what the core uses when no config asset overrides them.</summary>
        public static ScorerWeights Default { get; } = new ScorerWeights();
    }
}
