using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// One key moment in a match — the raw material the narrative layer turns into a character beat
    /// (TDD §4.1 step 6). Records the minute, the side, the kind, and the <see cref="Scorer"/> when a
    /// squad fed the sim. <see cref="Scorer"/> is null when the command carried no squad (a strength-only
    /// harness or restored match), so the event stays side-attributed but unnamed.
    /// </summary>
    public readonly struct MatchEvent
    {
        public MatchEvent(int minute, TeamSide side, MatchEventKind kind)
            : this(minute, side, kind, null)
        {
        }

        public MatchEvent(int minute, TeamSide side, MatchEventKind kind, PlayerId? scorer)
        {
            Minute = minute;
            Side = side;
            Kind = kind;
            Scorer = scorer;
        }

        public int Minute { get; }

        public TeamSide Side { get; }

        public MatchEventKind Kind { get; }

        /// <summary>The player credited with the goal, or null when no squad fed the sim.</summary>
        public PlayerId? Scorer { get; }
    }
}
