using Gaffer.Application.Transfers;

namespace Gaffer.Application.Run
{
    /// <summary>
    /// What moving money between the two budgets changed: how much weekly wage ceiling moved, the transfer
    /// cash that moved the other way, and the club's money afterwards.
    ///
    /// <para>Nobody was bought or sold, so — unlike <see cref="TransferOutcome"/> — there is no team sheet
    /// to carry: the finances are the whole record. They are on the outcome so the view replays what the
    /// command did rather than reaching back into the session for the new numbers (ARCHITECTURE §8,
    /// NON-NEGOTIABLE #4).</para>
    /// </summary>
    public sealed class BudgetShiftOutcome
    {
        /// <summary>
        /// Every value is required: this record is only ever built by <see cref="RunSession"/> once a
        /// shift has completed, where all of it is known.
        /// </summary>
        public BudgetShiftOutcome(long weeklyWageBudgetDelta, long cashDelta, Finances finances)
        {
            WeeklyWageBudgetDelta = weeklyWageBudgetDelta;
            CashDelta = cashDelta;
            Finances = finances;
        }

        /// <summary>How the weekly wage ceiling moved: positive bought room, negative gave room up.</summary>
        public long WeeklyWageBudgetDelta { get; }

        /// <summary>
        /// How the transfer cash moved — always the opposite sign, at
        /// <see cref="EconomySettings.WageBudgetExchangeWeeks"/> to one.
        /// </summary>
        public long CashDelta { get; }

        /// <summary>The club's money after the shift.</summary>
        public Finances Finances { get; }
    }
}
