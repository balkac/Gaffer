using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;

namespace Gaffer.Application.Season
{
    /// <summary>
    /// The outcome of one played fixture — the score plus the minute-by-minute events (goals) the
    /// presentation replays. Events are side-attributed for now; named scorers arrive with players
    /// (Faz 3) and become character beats in the narrative layer (Faz 5).
    /// </summary>
    public readonly struct MatchResult
    {
        public MatchResult(ClubId home, ClubId away, int homeGoals, int awayGoals, IReadOnlyList<MatchEvent> events)
        {
            Home = home;
            Away = away;
            HomeGoals = homeGoals;
            AwayGoals = awayGoals;
            Events = events;
        }

        public ClubId Home { get; }

        public ClubId Away { get; }

        public int HomeGoals { get; }

        public int AwayGoals { get; }

        public IReadOnlyList<MatchEvent> Events { get; }
    }
}
