using Gaffer.Common;
using Gaffer.Domain.Clubs;

namespace Gaffer.Application.Season
{
    /// <summary>
    /// Who hates whom. Each club is paired with exactly one rival for the whole run, so a derby is a
    /// fixture two clubs meet twice a year with something extra on it — not a label the table hands out
    /// and takes back.
    ///
    /// <para><b>Why a pairing and not a rule.</b> A rivalry is the one kind of significance that has
    /// nothing to do with form: the two clubs closest in the table are having a good season, and the two
    /// who cannot stand each other are having a rivalry. Deriving it from standings would make derbies
    /// wander from year to year, and a career moment that reads "his first derby goal" needs the derby to
    /// have been the same fixture the season before.</para>
    ///
    /// <para>Drawn once from the run's seed, so it reproduces exactly and no two runs share a map. With
    /// an odd number of clubs one is left without a rival, which is honest — somebody always is.</para>
    /// </summary>
    public sealed class RivalryTable
    {
        private const int NoRival = -1;

        private readonly int[] _rivalOf;

        private RivalryTable(int[] rivalOf)
        {
            _rivalOf = rivalOf;
        }

        /// <summary>An empty map — nobody has a rival. What a league with fewer than two clubs gets.</summary>
        public static RivalryTable None { get; } = new RivalryTable(new int[0]);

        /// <summary>
        /// Pairs the clubs at random and keeps the pairing for the run. The shuffle is a
        /// Fisher-Yates over the club indices, drawn from <paramref name="rng"/>, so the map is a pure
        /// function of the seed (NON-NEGOTIABLE #2).
        /// </summary>
        public static RivalryTable Draw(int clubCount, IRandom rng)
        {
            if (clubCount < 2 || rng == null)
            {
                return None;
            }

            var order = new int[clubCount];
            for (int i = 0; i < clubCount; i++)
            {
                order[i] = i;
            }

            for (int i = clubCount - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                int swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }

            var rivalOf = new int[clubCount];
            for (int i = 0; i < clubCount; i++)
            {
                rivalOf[i] = NoRival;
            }

            for (int i = 0; i + 1 < clubCount; i += 2)
            {
                rivalOf[order[i]] = order[i + 1];
                rivalOf[order[i + 1]] = order[i];
            }

            return new RivalryTable(rivalOf);
        }

        /// <summary>Rebuilds a saved map. The pairing outlives a season, so it is run state, not season state.</summary>
        public static RivalryTable Restore(int[] rivalOf)
        {
            return rivalOf == null || rivalOf.Length == 0 ? None : new RivalryTable(rivalOf);
        }

        /// <summary>The pairing as stored, for a save. Index is the club, value is its rival or -1.</summary>
        public int[] Capture()
        {
            return _rivalOf;
        }

        public bool AreRivals(ClubId one, ClubId other)
        {
            int index = one.Value;
            return index >= 0 && index < _rivalOf.Length && _rivalOf[index] == other.Value;
        }

        /// <summary>His rival, or a club id of -1 when he has none.</summary>
        public ClubId RivalOf(ClubId club)
        {
            int index = club.Value;
            return new ClubId(index >= 0 && index < _rivalOf.Length ? _rivalOf[index] : NoRival);
        }
    }
}
