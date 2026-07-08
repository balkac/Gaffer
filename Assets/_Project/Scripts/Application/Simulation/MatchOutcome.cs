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
        public MatchOutcome(int homeGoals, int awayGoals, int homeShots, int awayShots, IReadOnlyList<MatchEvent> events)
        {
            HomeGoals = homeGoals;
            AwayGoals = awayGoals;
            HomeShots = homeShots;
            AwayShots = awayShots;
            Events = events;
        }

        public int HomeGoals { get; }

        public int AwayGoals { get; }

        /// <summary>Chances created by each side — a match-texture stat: the counter takes few, converts many.</summary>
        public int HomeShots { get; }

        public int AwayShots { get; }

        public IReadOnlyList<MatchEvent> Events { get; }
    }
}
