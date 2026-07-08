using System;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Transfers
{
    /// <summary>The new budget and squad after a transfer, and the fee that moved.</summary>
    public sealed class TransferResult
    {
        public TransferResult(long budget, Squad squad, long fee)
        {
            Budget = budget;
            Squad = squad;
            Fee = fee;
        }

        public long Budget { get; }

        public Squad Squad { get; }

        public long Fee { get; }
    }

    /// <summary>
    /// Signs and sells players against a budget — the low-friction transfer model (GDD §4.4): no agents,
    /// just a fee and a yes/no. The economy stays tense on purpose. You pay a premium over market value to
    /// prise a player away, and you receive a little under value when you sell, so churning the squad
    /// bleeds money — it is not a printer. Expected failures (can't afford, not on the roster, squad too
    /// thin) return a <see cref="Result{T}"/> to handle, not an exception.
    /// </summary>
    public static class TransferService
    {
        private const double BuyPremium = 1.15;
        private const double SellDiscount = 0.92;
        private const int MinSquadSize = 11;

        public static long SignFee(Player player)
        {
            return Round(PlayerValuation.Value(player) * BuyPremium);
        }

        public static long SellFee(Player player)
        {
            return Round(PlayerValuation.Value(player) * SellDiscount);
        }

        public static Result<TransferResult> Sign(long budget, Squad squad, Player player)
        {
            if (squad.Contains(player.Id))
            {
                return Result<TransferResult>.Failure("That player is already in the squad.");
            }

            long fee = SignFee(player);
            if (fee > budget)
            {
                return Result<TransferResult>.Failure($"Not enough budget: the fee is {fee} but only {budget} is available.");
            }

            return Result<TransferResult>.Success(new TransferResult(budget - fee, squad.Add(player), fee));
        }

        public static Result<TransferResult> Sell(long budget, Squad squad, Player player)
        {
            if (!squad.Contains(player.Id))
            {
                return Result<TransferResult>.Failure("That player is not in the squad.");
            }

            if (squad.Count <= MinSquadSize)
            {
                return Result<TransferResult>.Failure($"The squad is already down to {MinSquadSize}; you can't sell more.");
            }

            long fee = SellFee(player);
            return Result<TransferResult>.Success(new TransferResult(budget + fee, squad.Remove(player.Id), fee));
        }

        private static long Round(double value)
        {
            return (long)Math.Round(value);
        }
    }
}
