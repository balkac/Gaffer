using System.Collections.Generic;

namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// A career told the way a career is told: **season by season**, each year carrying its own games and
    /// goals and whatever happened in it.
    ///
    /// <para><b>Why it replaced reading the moments directly.</b> The first thing Gate B was shown was a
    /// flat chronological stream of moments, and the owner's verdict was that it read as a LIST — <em>"a
    /// career tells its seasons and its achievements."</em> He was right, and the fault was structural
    /// rather than a matter of wording: a stream of moments has no place to put a year in which somebody
    /// simply played well, so those years vanished and the gaps read as missing rather than quiet. A
    /// season is the unit, and the moments belong inside it.</para>
    ///
    /// <para>A projection, stored nowhere, built from the journey on demand.</para>
    /// </summary>
    public sealed class CareerChronicle
    {
        private CareerChronicle(CareerSummary summary, IReadOnlyList<ChronicleSeason> seasons)
        {
            Summary = summary;
            Seasons = seasons;
        }

        /// <summary>The spell as a whole — how he arrived, what he did, how he left, what he was worth.</summary>
        public CareerSummary Summary { get; }

        /// <summary>Each season he was at the club, earliest first.</summary>
        public IReadOnlyList<ChronicleSeason> Seasons { get; }

        public static CareerChronicle Create(PlayerJourney journey)
        {
            var seasons = new List<ChronicleSeason>();
            if (journey == null)
            {
                return new CareerChronicle(CareerSummary.Create(null), seasons);
            }

            IReadOnlyList<PlayerSeason> played = journey.Seasons;
            for (int i = 0; i < played.Count; i++)
            {
                seasons.Add(new ChronicleSeason(played[i], MomentsIn(journey, played[i].Season)));
            }

            // A season a player did not play in can still hold a moment — he arrived, he was sold, he
            // retired — and those years have no row of games to hang on. They are added so a career never
            // silently loses the year it ended in.
            AddMomentOnlySeasons(journey, seasons);
            seasons.Sort(BySeason);

            return new CareerChronicle(CareerSummary.Create(journey), seasons);
        }

        private static readonly System.Comparison<ChronicleSeason> BySeason =
            (left, right) => left.Season.CompareTo(right.Season);

        private static void AddMomentOnlySeasons(PlayerJourney journey, List<ChronicleSeason> seasons)
        {
            IReadOnlyList<CareerMoment> moments = journey.Moments;
            for (int i = 0; i < moments.Count; i++)
            {
                int season = moments[i].Season;
                bool known = false;
                for (int s = 0; s < seasons.Count && !known; s++)
                {
                    known = seasons[s].Season == season;
                }

                if (!known)
                {
                    seasons.Add(new ChronicleSeason(new PlayerSeason(season, 0, 0), MomentsIn(journey, season)));
                }
            }
        }

        private static List<CareerMoment> MomentsIn(PlayerJourney journey, int season)
        {
            IReadOnlyList<CareerMoment> all = journey.Moments;
            var mine = new List<CareerMoment>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Season == season)
                {
                    mine.Add(all[i]);
                }
            }

            return mine;
        }
    }

    /// <summary>One year of a career: what he played, and what is remembered of it.</summary>
    public sealed class ChronicleSeason
    {
        public ChronicleSeason(PlayerSeason played, IReadOnlyList<CareerMoment> moments)
        {
            Played = played;
            Moments = moments;
        }

        public PlayerSeason Played { get; }

        public int Season => Played.Season;

        /// <summary>What is remembered of it. Empty is common and correct — most years are just played.</summary>
        public IReadOnlyList<CareerMoment> Moments { get; }
    }
}
