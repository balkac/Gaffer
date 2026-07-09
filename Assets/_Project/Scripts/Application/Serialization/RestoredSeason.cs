using Gaffer.Application.Season;
using Gaffer.Domain.Leagues;

namespace Gaffer.Application.Serialization
{
    /// <summary>A season rebuilt from a save: the league it was rebuilt from, the resumed season, and the
    /// season number of the run, so play continues into the right year across a multi-season save.</summary>
    public sealed class RestoredSeason
    {
        public RestoredSeason(League league, LeagueSeason season, int seasonNumber)
        {
            League = league;
            Season = season;
            SeasonNumber = seasonNumber;
        }

        public League League { get; }

        public LeagueSeason Season { get; }

        public int SeasonNumber { get; }
    }
}
