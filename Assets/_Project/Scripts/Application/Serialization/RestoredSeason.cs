using Gaffer.Application.Season;
using Gaffer.Domain.Leagues;

namespace Gaffer.Application.Serialization
{
    /// <summary>A season rebuilt from a save: the league it was rebuilt from and the resumed season.</summary>
    public sealed class RestoredSeason
    {
        public RestoredSeason(League league, LeagueSeason season)
        {
            League = league;
            Season = season;
        }

        public League League { get; }

        public LeagueSeason Season { get; }
    }
}
