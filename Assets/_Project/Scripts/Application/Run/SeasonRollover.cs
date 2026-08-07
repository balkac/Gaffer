using System.Collections.Generic;
using Gaffer.Application.Transfers;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Run
{
    /// <summary>
    /// What rolling the run on a year changed: the season it is now, who left the managed squad and who
    /// came through, the money and the market it starts on, and the eleven re-picked from the developed
    /// roster. The summer diff used to be computed twice, once in each window, by hashing player ids
    /// before and after — it is one derivation, so it has one owner (ARCHITECTURE §8a).
    /// </summary>
    public sealed class SeasonRollover
    {
        /// <summary>The season now being played (1 for the first).</summary>
        public int SeasonNumber { get; init; }

        /// <summary>Players who were in the squad last season and are not in it now — they retired.</summary>
        public IReadOnlyList<Player> Retired { get; init; }

        /// <summary>Players who were not in the squad last season and are now — the youth intake.</summary>
        public IReadOnlyList<Player> Arrived { get; init; }

        /// <summary>Cash carries over; the wage bill is re-derived from the developed squad.</summary>
        public Finances Finances { get; init; }

        /// <summary>The new season's free-agent market — a fresh set of prospects.</summary>
        public IReadOnlyList<Player> Market { get; init; }

        /// <summary>The eleven auto-picked from the developed squad.</summary>
        public LineupOutcome Lineup { get; init; }
    }
}
