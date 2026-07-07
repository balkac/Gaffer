namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// One key moment in a match — the raw material the narrative layer turns into a character beat
    /// (TDD §4.1 step 6). The skeleton records the minute, the side, and the kind; player attribution
    /// arrives once squads feed the sim.
    /// </summary>
    public readonly struct MatchEvent
    {
        public MatchEvent(int minute, TeamSide side, MatchEventKind kind)
        {
            Minute = minute;
            Side = side;
            Kind = kind;
        }

        public int Minute { get; }

        public TeamSide Side { get; }

        public MatchEventKind Kind { get; }
    }
}
