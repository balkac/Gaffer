using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Drama
{
    /// <summary>
    /// The drama engine's grip on the pitch: per-player morale as stacked, week-limited entries.
    /// A scandal or a refused transfer leaves points here, the strength step multiplies each player's
    /// rating by <see cref="RatingMultiplierOf"/>, and <see cref="TickWeek"/> lets wounds (and highs)
    /// fade on schedule — so a drama choice is measurably felt in the next weeks' results and then
    /// lets go. Bounded so stacked drama never breaks the sim's believability.
    /// </summary>
    public sealed class MoraleLedger : IPlayerConditionSource
    {
        private const double PerPoint = 0.012;
        private const double MaxAbsPoints = 8.0;

        private struct Entry
        {
            public double Points;
            public int WeeksLeft;
        }

        private readonly Dictionary<PlayerId, List<Entry>> _entries = new Dictionary<PlayerId, List<Entry>>();

        // Scratch for the players whose entries all expired this tick, reused across weeks (cleared
        // per call) so an uneventful tick allocates nothing (PERFORMANCE §8).
        private readonly List<PlayerId> _emptiedScratch = new List<PlayerId>();

        /// <summary>Adds signed morale points on the player for the coming weeks; stacks with what is live.</summary>
        public void Apply(PlayerId player, double points, int weeks)
        {
            if (weeks <= 0 || points == 0.0)
            {
                return;
            }

            if (!_entries.TryGetValue(player, out List<Entry> list))
            {
                list = new List<Entry>();
                _entries[player] = list;
            }

            list.Add(new Entry { Points = points, WeeksLeft = weeks });
        }

        /// <summary>Live morale points on the player (clamped sum of active entries).</summary>
        public double PointsOf(PlayerId player)
        {
            if (!_entries.TryGetValue(player, out List<Entry> list))
            {
                return 0.0;
            }

            double total = 0.0;
            foreach (Entry entry in list)
            {
                total += entry.Points;
            }

            if (total > MaxAbsPoints)
            {
                return MaxAbsPoints;
            }

            if (total < -MaxAbsPoints)
            {
                return -MaxAbsPoints;
            }

            return total;
        }

        public double RatingMultiplierOf(PlayerId id)
        {
            return 1.0 + (PerPoint * PointsOf(id));
        }

        /// <summary>Ages every entry a week and drops the expired — call once per played round.</summary>
        public void TickWeek()
        {
            List<PlayerId> emptied = _emptiedScratch;
            emptied.Clear();
            foreach (KeyValuePair<PlayerId, List<Entry>> pair in _entries)
            {
                List<Entry> list = pair.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Entry entry = list[i];
                    entry.WeeksLeft--;
                    if (entry.WeeksLeft <= 0)
                    {
                        list.RemoveAt(i);
                    }
                    else
                    {
                        list[i] = entry;
                    }
                }

                if (list.Count == 0)
                {
                    emptied.Add(pair.Key);
                }
            }

            for (int i = 0; i < emptied.Count; i++)
            {
                _entries.Remove(emptied[i]);
            }
        }
    }
}
