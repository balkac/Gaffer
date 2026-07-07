using Gaffer.Domain.Clubs;

namespace Gaffer.Application.Season
{
    /// <summary>The score of one played fixture — a record the presentation can replay.</summary>
    public readonly struct MatchResult
    {
        public MatchResult(ClubId home, ClubId away, int homeGoals, int awayGoals)
        {
            Home = home;
            Away = away;
            HomeGoals = homeGoals;
            AwayGoals = awayGoals;
        }

        public ClubId Home { get; }

        public ClubId Away { get; }

        public int HomeGoals { get; }

        public int AwayGoals { get; }
    }
}
