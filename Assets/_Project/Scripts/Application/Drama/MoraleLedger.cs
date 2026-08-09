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
        private readonly MoraleSettings _settings;

        public MoraleLedger()
            : this(MoraleSettings.Default)
        {
        }

        /// <summary>Runs on specific morale balance (from a config asset). Null falls back to the
        /// calibrated defaults.</summary>
        public MoraleLedger(MoraleSettings settings)
        {
            _settings = settings ?? MoraleSettings.Default;
        }

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

            double maxAbs = _settings.MaxAbsPoints;
            if (total > maxAbs)
            {
                return maxAbs;
            }

            if (total < -maxAbs)
            {
                return -maxAbs;
            }

            return total;
        }

        public double RatingMultiplierOf(PlayerId id)
        {
            return 1.0 + (_settings.RatingPerPoint * PointsOf(id));
        }

        /// <summary>
        /// Every live entry, for a save (schema v6). Points AND the weeks each has LEFT, because that is
        /// what a resume has to put back: an entry restored at its original duration would let a wound
        /// outlive the run that caused it. Allocates a fresh list — this runs once per save, not per week,
        /// so the ledger's allocation-free tick (PERFORMANCE §8) is not what is being measured here.
        /// <para>Restoring is <see cref="Apply"/> once per entry: an entry's remaining weeks IS a duration
        /// from where the run stands, so there is no second code path to keep in step with this one.</para>
        /// </summary>
        public IReadOnlyList<MoraleEntry> CaptureEntries()
        {
            var captured = new List<MoraleEntry>(_entries.Count);
            foreach (KeyValuePair<PlayerId, List<Entry>> pair in _entries)
            {
                List<Entry> list = pair.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    captured.Add(new MoraleEntry(pair.Key, list[i].Points, list[i].WeeksLeft));
                }
            }

            return captured;
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
