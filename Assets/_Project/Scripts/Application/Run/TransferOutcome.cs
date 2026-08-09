using Gaffer.Application.Transfers;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Run
{
    /// <summary>
    /// What a completed signing or sale changed: the player who moved, what it cost or brought in, the
    /// money afterwards, and the team sheet as it stands now — the eleven is re-picked from the new
    /// roster in the same step, so the view never has to remember to do it (ARCHITECTURE §8a).
    /// </summary>
    public sealed class TransferOutcome
    {
        /// <summary>
        /// Every value is required: this record is only ever built by <see cref="RunSession"/> once a
        /// transfer has completed, where all of it is known, so there is no default worth having and an
        /// omitted field should not compile.
        /// </summary>
        public TransferOutcome(
            Player player,
            bool isSale,
            long fee,
            long weeklyWage,
            Finances finances,
            LineupOutcome lineup)
        {
            Player = player;
            IsSale = isSale;
            Fee = fee;
            WeeklyWage = weeklyWage;
            Finances = finances;
            Lineup = lineup;
        }

        /// <summary>The player who moved.</summary>
        public Player Player { get; }

        /// <summary>True for a sale, false for a signing.</summary>
        public bool IsSale { get; }

        /// <summary>The fee that moved (always positive; the direction is <see cref="IsSale"/>).</summary>
        public long Fee { get; }

        /// <summary>His weekly wage — leaving the bill on a sale, joining it on a signing.</summary>
        public long WeeklyWage { get; }

        /// <summary>The club's money after the transfer.</summary>
        public Finances Finances { get; }

        /// <summary>The team sheet re-picked from the new roster.</summary>
        public LineupOutcome Lineup { get; }
    }
}
