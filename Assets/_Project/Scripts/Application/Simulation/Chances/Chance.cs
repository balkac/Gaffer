namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// A created goal-scoring chance: which side made it, the minute, and an xG-like quality in
    /// [0, 1) that the resolver turns into a goal or a miss (TDD §4.1 step 3).
    /// </summary>
    public readonly struct Chance
    {
        public Chance(TeamSide side, int minute, double quality)
        {
            Side = side;
            Minute = minute;
            Quality = quality;
        }

        public TeamSide Side { get; }

        public int Minute { get; }

        public double Quality { get; }
    }
}
