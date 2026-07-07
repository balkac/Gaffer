using System.Collections.Generic;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// The immutable result of simulating a match: the score and the minute-ordered key-moment events
    /// the presentation replays (ARCHITECTURE §8). A read model built for its consumer — it carries
    /// what the view and narrative need, nothing more.
    /// </summary>
    public sealed class MatchOutcome
    {
        public MatchOutcome(int homeGoals, int awayGoals, IReadOnlyList<MatchEvent> events)
        {
            HomeGoals = homeGoals;
            AwayGoals = awayGoals;
            Events = events;
        }

        public int HomeGoals { get; }

        public int AwayGoals { get; }

        public IReadOnlyList<MatchEvent> Events { get; }
    }
}
