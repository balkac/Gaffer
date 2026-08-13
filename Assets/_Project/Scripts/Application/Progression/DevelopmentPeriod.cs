using System.Collections.Generic;

namespace Gaffer.Application.Progression
{
    /// <summary>
    /// Development a run has earned but not yet paid out: the match weeks banked since the last tick, and
    /// how many of them each player started.
    ///
    /// <para><b>Why it is worth persisting.</b> It used to live only in memory, so a reload lost it — a
    /// manager who saved after every match gave up 0.407 squad OVR a season, about a tenth of a season's
    /// development, compounding every year. Settling the period early at capture fixed the loss but paid
    /// the development in a finer grain than playing on would have, which was worth +0.29% a season to the
    /// same manager. Carrying the period is what makes WHEN YOU SAVE affect nothing at all
    /// (PROGRESS 2026-08-13).</para>
    ///
    /// <para>The whole league is counted, not just the managed club: every club's development is weighted
    /// by its own minutes, and a save that remembered only the manager's would restart every rival on
    /// bench rate.</para>
    /// </summary>
    public sealed class DevelopmentPeriod
    {
        public DevelopmentPeriod(int roundsPlayed, IReadOnlyList<int> playerIds, IReadOnlyList<int> appearances)
        {
            RoundsPlayed = roundsPlayed;
            PlayerIds = playerIds;
            Appearances = appearances;
        }

        /// <summary>Match weeks played since the last development tick.</summary>
        public int RoundsPlayed { get; }

        /// <summary>Who played them, aligned with <see cref="Appearances"/>.</summary>
        public IReadOnlyList<int> PlayerIds { get; }

        /// <summary>How many of those weeks each started, aligned with <see cref="PlayerIds"/>.</summary>
        public IReadOnlyList<int> Appearances { get; }

        /// <summary>True when there is nothing banked — a run that has just ticked, or has never played.</summary>
        public bool IsEmpty => RoundsPlayed <= 0;
    }
}
