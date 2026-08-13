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

        public bool HasPlayed => Appearances > 0;

        public void RecordAppearance()
        {
            Appearances++;
        }

        public void RecordGoals(int goals)
        {
            Goals += goals;
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
        public static PlayerJourney Restore(PlayerId player, string name, int appearances, int goals, IReadOnlyList<CareerMoment> moments)
        {
            var journey = new PlayerJourney(player, name) { Appearances = appearances, Goals = goals };
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
