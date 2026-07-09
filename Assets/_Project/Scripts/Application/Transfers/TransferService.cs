using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Transfers
{
    /// <summary>
    /// A club's money at a moment: transfer cash for fees, and a weekly wage budget the wage bill must stay
    /// under (the CM 01/02 two-budget model). Immutable — a transfer returns a new one.
    /// </summary>
    public readonly struct Finances
    {
        public Finances(long cash, long weeklyWageBudget, long weeklyWageBill)
        {
            Cash = cash;
            WeeklyWageBudget = weeklyWageBudget;
            WeeklyWageBill = weeklyWageBill;
        }

        public long Cash { get; }

        public long WeeklyWageBudget { get; }

        public long WeeklyWageBill { get; }

        public long WageHeadroom => WeeklyWageBudget - WeeklyWageBill;

        /// <summary>
        /// Advances one match-week: the wage bill is paid out of transfer cash. This is what makes the economy
        /// tense the CM 01/02 way — the wage budget is no longer a passive cap, it drains real money every week,
        /// so a bloated wage bill bleeds the cash you need for signings. Cash may go negative (an overspend the
        /// board will notice). Only the wages move; the budget and bill are unchanged. Income (gate receipts,
        /// prize money) is a later slice (decision #18).
        /// </summary>
        public Finances PayWeeklyWages()
        {
            return new Finances(Cash - WeeklyWageBill, WeeklyWageBudget, WeeklyWageBill);
        }
    }

    /// <summary>The finances and squad after a transfer, plus the fee that moved.</summary>
    public sealed class TransferResult
    {
        public TransferResult(Finances finances, Squad squad, long fee)
        {
            Finances = finances;
            Squad = squad;
            Fee = fee;
        }

        public Finances Finances { get; }

        public Squad Squad { get; }

        public long Fee { get; }
    }

    /// <summary>
    /// Signs and sells players — the low-friction transfer model (GDD §4.4): a fee and a yes/no, no agents.
    /// A transfer is priced at the player's market value with no premium or discount — value is value. The
    /// economy stays tense not through a fee tax but the CM 01/02 way: a signing must fit both the transfer
    /// cash and the weekly wage budget, and wages bite every week, so you cannot hoard talent. Because value
    /// tracks current ability (not the hidden ceiling), an undeveloped player round-trips at break-even —
    /// there is no money printer; the flip's profit comes from growing him first. Expected failures return a
    /// <see cref="Result{T}"/>.
    /// </summary>
    public static class TransferService
    {
        private const int MinSquadSize = 11;

        public static long Fee(Player player)
        {
            return PlayerValuation.Value(player);
        }

        public static Result<TransferResult> Sign(Finances finances, Squad squad, Player player)
        {
            if (squad.Contains(player.Id))
            {
                return Result<TransferResult>.Failure("That player is already in the squad.");
            }

            long fee = Fee(player);
            if (fee > finances.Cash)
            {
                return Result<TransferResult>.Failure($"Not enough transfer cash: the fee is {fee} but only {finances.Cash} is available.");
            }

            long wage = PlayerWage.Weekly(player);
            if (wage > finances.WageHeadroom)
            {
                return Result<TransferResult>.Failure($"No room in the wage budget: his wage is {wage}/wk but only {finances.WageHeadroom}/wk is free.");
            }

            var after = new Finances(finances.Cash - fee, finances.WeeklyWageBudget, finances.WeeklyWageBill + wage);
            return Result<TransferResult>.Success(new TransferResult(after, squad.Add(player), fee));
        }

        public static Result<TransferResult> Sell(Finances finances, Squad squad, Player player)
        {
            if (!squad.Contains(player.Id))
            {
                return Result<TransferResult>.Failure("That player is not in the squad.");
            }

            if (squad.Count <= MinSquadSize)
            {
                return Result<TransferResult>.Failure($"The squad is already down to {MinSquadSize}; you can't sell more.");
            }

            long fee = Fee(player);
            long wage = PlayerWage.Weekly(player);
            var after = new Finances(finances.Cash + fee, finances.WeeklyWageBudget, finances.WeeklyWageBill - wage);
            return Result<TransferResult>.Success(new TransferResult(after, squad.Remove(player.Id), fee));
        }
    }
}
