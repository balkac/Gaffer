namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// The balance behind goal attribution (<see cref="WeightedScorerSelector"/>, NON-NEGOTIABLE #3):
    /// how the two scoring pathways weigh a player's attributes, and how much each position takes part
    /// in each pathway. Tuning these reshapes who scores — more corner-header defenders, rarer keeper
    /// goals — without touching the selection algorithm. Injectable and defaulted like every balance
    /// object; the authoring surface (`SimulationBalanceSO`) maps onto it. Immutable once built —
    /// <see cref="Default"/> is one shared cached instance, and get-only properties set by one
    /// all-optional constructor are what make that safe rather than merely intended. The defaults live in
    /// the constructor signature and are baked into every calling assembly at compile time, so a changed
    /// default needs a full recompile before it is live everywhere (see <c>SettingsDefaultContractTests</c>).
    /// </summary>
    public sealed class ScorerWeights
    {
        /// <summary>
        /// Every parameter is optional and defaulted to the calibrated value, so a caller writes only what
        /// it changes: <c>new ScorerWeights(openPlayGoalkeeper: 0.0)</c>.
        /// </summary>
        public ScorerWeights(
            double minOutfielderWeight = 0.5,
            double openPlayFinishing = 0.6,
            double openPlayPositioning = 0.2,
            double openPlayPace = 0.2,
            double aerialHeading = 0.6,
            double aerialJumping = 0.25,
            double aerialStrength = 0.15,
            double openPlayForward = 1.0,
            double openPlayMidfielder = 0.55,
            double openPlayDefender = 0.10,
            double openPlayGoalkeeper = 0.003,
            double aerialForward = 0.45,
            double aerialMidfielder = 0.25,
            double aerialDefender = 0.30,
            double aerialGoalkeeper = 0.004)
        {
            MinOutfielderWeight = minOutfielderWeight;
            OpenPlayFinishing = openPlayFinishing;
            OpenPlayPositioning = openPlayPositioning;
            OpenPlayPace = openPlayPace;
            AerialHeading = aerialHeading;
            AerialJumping = aerialJumping;
            AerialStrength = aerialStrength;
            OpenPlayForward = openPlayForward;
            OpenPlayMidfielder = openPlayMidfielder;
            OpenPlayDefender = openPlayDefender;
            OpenPlayGoalkeeper = openPlayGoalkeeper;
            AerialForward = aerialForward;
            AerialMidfielder = aerialMidfielder;
            AerialDefender = aerialDefender;
            AerialGoalkeeper = aerialGoalkeeper;
        }

        /// <summary>Floor keeping every outfielder a live threat; keepers are exempt.</summary>
        public double MinOutfielderWeight { get; }

        /// <summary>Open play: weight of finishing.</summary>
        public double OpenPlayFinishing { get; }

        /// <summary>Open play: weight of positioning.</summary>
        public double OpenPlayPositioning { get; }

        /// <summary>Open play: weight of pace.</summary>
        public double OpenPlayPace { get; }

        /// <summary>Aerial: weight of heading.</summary>
        public double AerialHeading { get; }

        /// <summary>Aerial: weight of jumping.</summary>
        public double AerialJumping { get; }

        /// <summary>Aerial: weight of strength.</summary>
        public double AerialStrength { get; }

        /// <summary>How much of the open-play pathway each position gets.</summary>
        public double OpenPlayForward { get; }

        public double OpenPlayMidfielder { get; }

        public double OpenPlayDefender { get; }

        public double OpenPlayGoalkeeper { get; }

        /// <summary>How much of the aerial (set-piece) pathway each position gets.</summary>
        public double AerialForward { get; }

        public double AerialMidfielder { get; }

        public double AerialDefender { get; }

        public double AerialGoalkeeper { get; }

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new ScorerWeights()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static ScorerWeights Default { get; } = new ScorerWeights();
    }
}
