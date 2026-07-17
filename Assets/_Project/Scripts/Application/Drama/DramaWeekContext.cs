using System.Collections.Generic;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Drama
{
    /// <summary>
    /// The snapshot of run state the drama engine reads each week — command in, outcome out: the
    /// engine never reaches into the season, the caller hands it what this week looks like. Squad
    /// and starters come from the live roster; streak and position from the table; the window flag
    /// from the calendar.
    /// </summary>
    public readonly struct DramaWeekContext
    {
        public DramaWeekContext(
            IReadOnlyList<Player> squad,
            IReadOnlyList<Player> starters,
            int tablePosition,
            int lossStreak,
            bool isWindowOpen)
        {
            Squad = squad;
            Starters = starters;
            TablePosition = tablePosition;
            LossStreak = lossStreak;
            IsWindowOpen = isWindowOpen;
        }

        public IReadOnlyList<Player> Squad { get; }

        /// <summary>The current starting eleven, for bench-grievance conditions; may be null when unknown.</summary>
        public IReadOnlyList<Player> Starters { get; }

        /// <summary>1-based league position; 0 when unknown.</summary>
        public int TablePosition { get; }

        /// <summary>Consecutive losses coming into this week.</summary>
        public int LossStreak { get; }

        public bool IsWindowOpen { get; }
    }
}
