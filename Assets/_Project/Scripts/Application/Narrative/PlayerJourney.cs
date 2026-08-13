using System.Collections.Generic;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// One player's history at the club: the moments worth remembering, and the running totals that let
    /// the next one be recognised. "Is this his first goal" and "is this his fiftieth game" cannot be
    /// answered from a match — only from what came before — which is why the log is an INPUT to
    /// recognition and not merely its output.
    /// </summary>
    public sealed class PlayerJourney
    {
        private readonly List<CareerMoment> _moments = new List<CareerMoment>();

        // Season by season, in the order they were played. A list rather than a dictionary: it is short
        // (one entry a year), it is always read in order, and the season being appended to is almost
        // always the last one (PERFORMANCE §8).
        private readonly List<PlayerSeason> _seasons = new List<PlayerSeason>();

        public PlayerJourney(PlayerId player, string name)
        {
            Player = player;
            Name = name;
        }

        public PlayerId Player { get; }

        /// <summary>
        /// His name, snapshotted when the journey opened. Held here rather than looked up, because the
        /// whole point is following a player AFTER he leaves — and a squad lookup answers "who is at the
        /// club", which by then is nobody. The Gate B probe read its three strongest careers as
        /// "(left the club)" before this existed.
        /// </summary>
        public string Name { get; }

        /// <summary>Appearances so far. The counter, not a count of Debut moments — most games are not moments.</summary>
        public int Appearances { get; private set; }

        public int Goals { get; private set; }

        public IReadOnlyList<CareerMoment> Moments => _moments;

        /// <summary>
        /// His seasons, earliest first — the unit a career is actually told in. A year in which nothing
        /// worth a MOMENT happened still has a row here, with the games and the goals in it, which is the
        /// difference between "he had a quiet season" and a gap in the story.
        /// </summary>
        public IReadOnlyList<PlayerSeason> Seasons => _seasons;

        public bool HasPlayed => Appearances > 0;

        /// <summary>Credits a game in a given season, to the lifetime total and to that year's row.</summary>
        public void RecordAppearance(int season)
        {
            Appearances++;
            Add(season, appearances: 1, goals: 0);
        }

        public void RecordGoals(int season, int goals)
        {
            Goals += goals;
            Add(season, appearances: 0, goals: goals);
        }

        private void Add(int season, int appearances, int goals)
        {
            // Almost always the last row, because a career is played in order; the scan behind it is for
            // the restore path and for anything that reads a season out of turn.
            for (int i = _seasons.Count - 1; i >= 0; i--)
            {
                if (_seasons[i].Season == season)
                {
                    _seasons[i] = _seasons[i].Plus(appearances, goals);
                    return;
                }
            }

            _seasons.Add(new PlayerSeason(season, appearances, goals));
        }

        public void Add(CareerMoment moment)
        {
            _moments.Add(moment);
        }

        /// <summary>
        /// Rebuilds a journey from a save (5.2). The totals are restored rather than recomputed from the
        /// moments, because they were never derivable from them — a player's fiftieth appearance is a
        /// moment, his forty-ninth is not, and the counter is the only thing that knew.
        /// </summary>
        public static PlayerJourney Restore(PlayerId player, string name, int appearances, int goals, IReadOnlyList<CareerMoment> moments, IReadOnlyList<PlayerSeason> seasons = null)
        {
            var journey = new PlayerJourney(player, name) { Appearances = appearances, Goals = goals };
            if (seasons != null)
            {
                for (int i = 0; i < seasons.Count; i++)
                {
                    journey._seasons.Add(seasons[i]);
                }
            }

            if (moments != null)
            {
                for (int i = 0; i < moments.Count; i++)
                {
                    journey._moments.Add(moments[i]);
                }
            }

            return journey;
        }
    }
}
