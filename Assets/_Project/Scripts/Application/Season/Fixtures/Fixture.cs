using Gaffer.Domain.Clubs;

namespace Gaffer.Application.Season
{
    /// <summary>One scheduled match in a season: the round (match week) it is played and the two sides.</summary>
    public readonly struct Fixture
    {
        public Fixture(int round, ClubId home, ClubId away)
        {
            Round = round;
            Home = home;
            Away = away;
        }

        public int Round { get; }

        public ClubId Home { get; }

        public ClubId Away { get; }
    }
}
