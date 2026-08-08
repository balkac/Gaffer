using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Transfers
{
    /// <summary>
    /// A club's money at a moment: transfer cash for fees, and a weekly wage budget the wage bill must stay
    /// under (the CM 01/02 two-budget model). Immutable — a transfer returns a new one.
    ///
    /// <para>The two are not sealed off from each other: <see cref="ShiftWageBudget(long, EconomySettings)"/>
    /// trades one for the other at the board's rate, so cash that cannot be spent for want of wage room is
    /// not dead money. What it may never do is put the ceiling under the wage bill already signed.</para>
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

        /// <summary>Moves money between the two budgets at the board's calibrated rate.</summary>
        public Result<Finances> ShiftWageBudget(long weeklyDelta)
        {
            return ShiftWageBudget(weeklyDelta, EconomySettings.Default);
        }

        /// <summary>
        /// Moves money between the two budgets, against specific economy balance (from a config asset).
        /// A positive <paramref name="weeklyDelta"/> raises the weekly wage ceiling by that much and pays
        /// for it out of transfer cash; a negative one gives up that much ceiling and takes the cash. The
        /// wage bill itself never moves — nobody is bought or sold — and neither does anything else in the
        /// run, so this is available at any time, in or out of a transfer window. It answers the owner's
        /// complaint directly: cash you cannot spend because the wage ceiling is full is no longer dead
        /// money.
        ///
        /// <para><b>The rate.</b> <see cref="EconomySettings.WageBudgetExchangeWeeks"/> (38, a season of
        /// match weeks) both ways, so <c>Shift(+x)</c> followed by <c>Shift(-x)</c> is the identity and
        /// there is no arbitrage in cycling.</para>
        ///
        /// <para><b>Rounding: there is none, by construction, and that is the point.</b> The amount is
        /// always named in €/week — the unit the ceiling is in — and the cash leg is that amount
        /// <em>multiplied</em> by the rate, never divided by it. Integer multiplication is exact, so a
        /// shift moves exactly <c>weekly × weeks</c> and the opposite shift moves exactly the same number
        /// back; the round trip is the identity rather than something that happens to come out even, and
        /// splitting one shift into a hundred small ones moves the same total (the sum of the parts is the
        /// multiplication distributed). Had this taken a cash amount instead, the ceiling would be
        /// <c>cash / weeks</c> and integer division would truncate: converting €37 at a time would destroy
        /// the lot while one lump kept it, and the mirrored rule would round the other way into a money
        /// printer. So the exchange refuses to be denominated in cash at all — the editor's field is €/wk
        /// in both directions.</para>
        ///
        /// <para><b>What it refuses.</b> The ceiling may never fall below the wage bill already committed:
        /// those contracts are signed, and a ceiling under the bill would put
        /// <see cref="WageHeadroom"/> in the red through a path that is not "you overspent". You cannot
        /// convert cash you do not have. And an amount so large that pricing it would overflow a
        /// <c>long</c> is refused rather than wrapped — a wrapped price reads as negative and would slip
        /// past the cash check, minting ceiling out of nothing. Each refusal names the limit and the gap,
        /// like <see cref="TransferService.Sign"/>'s. Zero is a no-op, not an error.</para>
        /// </summary>
        public Result<Finances> ShiftWageBudget(long weeklyDelta, EconomySettings economy)
        {
            if (weeklyDelta == 0)
            {
                return Result<Finances>.Success(this);
            }

            long weeks = ExchangeWeeks(economy);
            long largest = long.MaxValue / weeks;
            if (weeklyDelta > largest || weeklyDelta < -largest)
            {
                return Result<Finances>.Failure(
                    $"That is more than the books can price: at {weeks} weeks to the euro-per-week, at most {largest}/wk can move at once.");
            }

            long weekly = weeklyDelta < 0 ? -weeklyDelta : weeklyDelta;
            long cash = weekly * weeks;

            if (weeklyDelta < 0)
            {
                if (weekly > WageHeadroom)
                {
                    return Result<Finances>.Failure(
                        $"The wage ceiling can't go below the {WeeklyWageBill}/wk already committed: at most {WageHeadroom}/wk can be given up, which is {weekly - WageHeadroom}/wk less than you asked for.");
                }

                if (Cash > long.MaxValue - cash)
                {
                    return Result<Finances>.Failure(
                        $"That is more than the books can hold: {cash} on top of {Cash} in transfer cash does not fit.");
                }

                return Result<Finances>.Success(new Finances(Cash + cash, WeeklyWageBudget - weekly, WeeklyWageBill));
            }

            if (cash > Cash)
            {
                return Result<Finances>.Failure(
                    $"Not enough transfer cash: {weekly}/wk more wage ceiling costs {cash} but only {Cash} is available.");
            }

            if (WeeklyWageBudget > long.MaxValue - weekly)
            {
                return Result<Finances>.Failure(
                    $"That is more than the books can hold: {weekly}/wk on top of a {WeeklyWageBudget}/wk ceiling does not fit.");
            }

            return Result<Finances>.Success(new Finances(Cash - cash, WeeklyWageBudget + weekly, WeeklyWageBill));
        }

        // The rate is config-editable (NON-NEGOTIABLE #3) and a rate under one week would be a money pump
        // in both directions at once: giving up ceiling would pay nothing, and cash would buy ceiling for
        // free. One week is the true floor and the calibrated 38 passes through untouched — the same guard,
        // for the same reason, that PlayerWage puts on its rounding step. Both directions read it here, so
        // a clamped rate is still one rate and the round trip stays neutral.
        private static long ExchangeWeeks(EconomySettings economy)
        {
            return economy.WageBudgetExchangeWeeks > 0 ? economy.WageBudgetExchangeWeeks : 1;
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
            return Fee(player, EconomySettings.Default);
        }

        /// <summary>The fee against specific economy balance (from a config asset).</summary>
        public static long Fee(Player player, EconomySettings economy)
        {
            return PlayerValuation.Value(player, economy);
        }

        public static Result<TransferResult> Sign(Finances finances, Squad squad, Player player)
        {
            return Sign(finances, squad, player, EconomySettings.Default);
        }

        /// <summary>Signs against specific economy balance (from a config asset).</summary>
        public static Result<TransferResult> Sign(Finances finances, Squad squad, Player player, EconomySettings economy)
        {
            if (squad.Contains(player.Id))
            {
                return Result<TransferResult>.Failure("That player is already in the squad.");
            }

            long fee = Fee(player, economy);
            if (fee > finances.Cash)
            {
                return Result<TransferResult>.Failure($"Not enough transfer cash: the fee is {fee} but only {finances.Cash} is available.");
            }

            long wage = PlayerWage.Weekly(player, economy);
            if (wage > finances.WageHeadroom)
            {
                return Result<TransferResult>.Failure($"No room in the wage budget: his wage is {wage}/wk but only {finances.WageHeadroom}/wk is free.");
            }

            var after = new Finances(finances.Cash - fee, finances.WeeklyWageBudget, finances.WeeklyWageBill + wage);
            return Result<TransferResult>.Success(new TransferResult(after, squad.Add(player), fee));
        }

        public static Result<TransferResult> Sell(Finances finances, Squad squad, Player player)
        {
            return Sell(finances, squad, player, EconomySettings.Default);
        }

        /// <summary>Sells against specific economy balance (from a config asset).</summary>
        public static Result<TransferResult> Sell(Finances finances, Squad squad, Player player, EconomySettings economy)
        {
            if (!squad.Contains(player.Id))
            {
                return Result<TransferResult>.Failure("That player is not in the squad.");
            }

            if (squad.Count <= MinSquadSize)
            {
                return Result<TransferResult>.Failure($"The squad is already down to {MinSquadSize}; you can't sell more.");
            }

            long fee = Fee(player, economy);
            long wage = PlayerWage.Weekly(player, economy);
            var after = new Finances(finances.Cash + fee, finances.WeeklyWageBudget, finances.WeeklyWageBill - wage);
            return Result<TransferResult>.Success(new TransferResult(after, squad.Remove(player.Id), fee));
        }
    }
}
