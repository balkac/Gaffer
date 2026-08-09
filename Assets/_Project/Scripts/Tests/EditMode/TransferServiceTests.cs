using System.Collections.Generic;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class TransferServiceTests
    {
        private static Player Forward(int id, byte level, int age)
        {
            var attributes = new Attributes
            {
                Finishing = level,
                Pace = level,
                Technique = level,
                Positioning = level,
                Dribbling = level,
            };
            return new Player(new PlayerId(id), "P" + id, "England", Position.Forward, age, attributes, 70);
        }

        private static Squad SquadOf(int size)
        {
            var players = new List<Player>(size);
            for (int i = 0; i < size; i++)
            {
                players.Add(Forward(1000 + i, 55, 25));
            }

            return new Squad(players);
        }

        [Test]
        public void Sign_WithEnoughCashAndWageRoom_AddsPlayerAndUpdatesFinances()
        {
            Squad squad = SquadOf(20);
            Player target = Forward(1, 75, 24);
            long fee = TransferService.Fee(target);
            long wage = PlayerWage.Weekly(target);
            var finances = new Finances(fee + 500_000, 200_000, 100_000);

            Result<TransferResult> result = TransferService.Sign(finances, squad, target);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Finances.Cash, Is.EqualTo(500_000));
            Assert.That(result.Value.Finances.WeeklyWageBill, Is.EqualTo(100_000 + wage));
            Assert.That(result.Value.Squad.Count, Is.EqualTo(21));
            Assert.That(result.Value.Squad.Contains(target.Id), Is.True);
            Assert.That(squad.Count, Is.EqualTo(20), "The original squad must be unchanged.");
        }

        [Test]
        public void Sign_WithoutEnoughCash_Fails()
        {
            Squad squad = SquadOf(20);
            Player target = Forward(1, 80, 24);
            long fee = TransferService.Fee(target);
            var finances = new Finances(fee - 1, 1_000_000, 0);

            Result<TransferResult> result = TransferService.Sign(finances, squad, target);

            Assert.That(result.IsFailure, Is.True);
        }

        [Test]
        public void Sign_WithoutWageRoom_Fails()
        {
            Squad squad = SquadOf(20);
            Player target = Forward(1, 80, 24);
            long wage = PlayerWage.Weekly(target);
            // Plenty of cash, but the wage budget is already full to within less than his wage.
            var finances = new Finances(500_000_000, 100_000, 100_000 - (wage - 1));

            Result<TransferResult> result = TransferService.Sign(finances, squad, target);

            Assert.That(result.IsFailure, Is.True);
        }

        [Test]
        public void Sign_PlayerAlreadyInSquad_Fails()
        {
            Player target = Forward(1, 70, 24);
            var squad = new Squad(new List<Player> { target });

            Result<TransferResult> result = TransferService.Sign(new Finances(500_000_000, 500_000, 0), squad, target);

            Assert.That(result.IsFailure, Is.True);
        }

        [Test]
        public void Sell_RemovesPlayerAddsCashAndFreesWages()
        {
            Squad squad = SquadOf(20);
            Player onRoster = squad.Players[3];
            long fee = TransferService.Fee(onRoster);
            long wage = PlayerWage.Weekly(onRoster);
            var finances = new Finances(2_000_000, 200_000, 120_000);

            Result<TransferResult> result = TransferService.Sell(finances, squad, onRoster);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Squad.Count, Is.EqualTo(19));
            Assert.That(result.Value.Squad.Contains(onRoster.Id), Is.False);
            Assert.That(result.Value.Finances.Cash, Is.EqualTo(2_000_000 + fee));
            Assert.That(result.Value.Finances.WeeklyWageBill, Is.EqualTo(120_000 - wage));
        }

        [Test]
        public void Sell_DownToTheMinimumSquad_Fails()
        {
            Squad squad = SquadOf(11);
            Player onRoster = squad.Players[0];

            Result<TransferResult> result = TransferService.Sell(new Finances(0, 200_000, 100_000), squad, onRoster);

            Assert.That(result.IsFailure, Is.True);
        }

        [Test]
        public void Sell_PlayerNotInTheSquad_Fails()
        {
            // Sell's other failure branch. Selling someone you do not own would have minted his fee out of
            // nothing — Squad.Remove of an absent id is a no-op, so the squad would come back unchanged
            // with the cash added.
            Squad squad = SquadOf(20);
            Player stranger = Forward(9999, 70, 24);

            Result<TransferResult> result = TransferService.Sell(new Finances(0, 200_000, 100_000), squad, stranger);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("not in the squad"));
        }

        // --- Boundaries. Each test above sits one unit on the failing side of a comparison; these sit
        // --- exactly on it, which is the value that decides whether it is `>` or `>=` (CONVENTIONS §5).

        [Test]
        public void Sign_FeeExactlyEqualToCash_Succeeds()
        {
            Squad squad = SquadOf(20);
            Player target = Forward(1, 80, 24);
            long fee = TransferService.Fee(target);
            var finances = new Finances(fee, 1_000_000, 0);

            Result<TransferResult> result = TransferService.Sign(finances, squad, target);

            Assert.That(result.IsSuccess, Is.True, "Spending the last penny of the budget is affordable.");
            Assert.That(result.Value.Finances.Cash, Is.Zero);
        }

        [Test]
        public void Sign_WageExactlyEqualToHeadroom_Succeeds()
        {
            Squad squad = SquadOf(20);
            Player target = Forward(1, 80, 24);
            long wage = PlayerWage.Weekly(target);
            var finances = new Finances(500_000_000, 100_000, 100_000 - wage);

            Result<TransferResult> result = TransferService.Sign(finances, squad, target);

            Assert.That(result.IsSuccess, Is.True, "A wage that exactly fills the remaining headroom still fits.");
            Assert.That(result.Value.Finances.WageHeadroom, Is.Zero);
        }

        [Test]
        public void Sell_OnePlayerAboveTheMinimumSquad_Succeeds()
        {
            // The other side of Sell_DownToTheMinimumSquad_Fails: 12 may sell down to 11, 11 may not.
            Squad squad = SquadOf(MinSquadSize + 1);
            Player onRoster = squad.Players[0];

            Result<TransferResult> result = TransferService.Sell(new Finances(0, 200_000, 100_000), squad, onRoster);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Squad.Count, Is.EqualTo(MinSquadSize));
        }

        // Mirrors TransferService's own private constant; if that moves, the two boundary tests above
        // move with it and keep testing the boundary rather than the number 11.
        private const int MinSquadSize = 11;

        [Test]
        public void Fee_IsTheMarketValue_NoSpread()
        {
            // Value is value both ways — round-tripping an undeveloped player is break-even, no money
            // printer. Asserting Fee == PlayerValuation.Value would be asserting the method body against
            // itself; what is checked instead is the property that matters at the table: buy him and sell
            // him back, and the club is exactly where it started.
            Player player = Forward(1, 78, 25);
            Squad squad = SquadOf(20);
            var before = new Finances(500_000_000, 5_000_000, 1_000_000);

            Result<TransferResult> bought = TransferService.Sign(before, squad, player);
            Assert.That(bought.IsSuccess, Is.True);

            Result<TransferResult> soldBack = TransferService.Sell(bought.Value.Finances, bought.Value.Squad, player);
            Assert.That(soldBack.IsSuccess, Is.True);

            Assert.That(soldBack.Value.Finances.Cash, Is.EqualTo(before.Cash), "No spread: a round trip is break-even.");
            Assert.That(soldBack.Value.Finances.WeeklyWageBill, Is.EqualTo(before.WeeklyWageBill));
            Assert.That(soldBack.Value.Squad.Count, Is.EqualTo(squad.Count));
            Assert.That(bought.Value.Fee, Is.GreaterThan(0), "A free transfer would make the round trip trivially balanced.");
            Assert.That(soldBack.Value.Fee, Is.EqualTo(bought.Value.Fee));
        }

        [Test]
        public void PayWeeklyWages_DeductsTheWageBillFromCash_LeavesBudgetAndBill()
        {
            var finances = new Finances(1_000_000, 200_000, 150_000);

            Finances after = finances.PayWeeklyWages();

            Assert.That(after.Cash, Is.EqualTo(850_000));
            Assert.That(after.WeeklyWageBudget, Is.EqualTo(200_000), "The budget is a cap, not spent.");
            Assert.That(after.WeeklyWageBill, Is.EqualTo(150_000), "The bill is unchanged by paying it.");
        }

        [Test]
        public void PayWeeklyWages_OverManyWeeks_DrainsCashByBillEachWeek()
        {
            var finances = new Finances(1_000_000, 300_000, 100_000);

            for (int week = 0; week < 8; week++)
            {
                finances = finances.PayWeeklyWages();
            }

            // Eight weeks at 100k drains 800k — the wage budget is a real weekly cost now, not a passive cap.
            Assert.That(finances.Cash, Is.EqualTo(200_000));
        }

        [Test]
        public void PayWeeklyWages_WhenBillExceedsCash_GoesNegative()
        {
            var finances = new Finances(50_000, 300_000, 100_000);

            Finances after = finances.PayWeeklyWages();

            Assert.That(after.Cash, Is.EqualTo(-50_000), "An unaffordable wage bill overspends into the red.");
        }

        // --- The budget exchange: cash and wage ceiling, one rate both ways -------------------------------
        //
        // The manager with plenty of cash and no wage room had no move to make; this is the move. Most of
        // the tests below price against the calibrated rate rather than the literal 38, so re-tuning the
        // rate moves them with it — the one exception is the test that exists to pin the number itself.

        private static long Weeks => EconomySettings.Default.WageBudgetExchangeWeeks;

        [Test]
        public void ShiftWageBudget_GivingUpCeiling_PaysCashAtTheRate()
        {
            var finances = new Finances(1_000_000, 200_000, 150_000);

            Result<Finances> result = finances.ShiftWageBudget(-10_000);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value.WeeklyWageBudget, Is.EqualTo(190_000));
            Assert.That(result.Value.Cash, Is.EqualTo(1_000_000 + (10_000 * Weeks)));
            Assert.That(result.Value.WeeklyWageBill, Is.EqualTo(150_000), "Nobody was sold, so the bill is untouched.");
        }

        [Test]
        public void ShiftWageBudget_BuyingCeiling_CostsCashAtTheSameRate()
        {
            var finances = new Finances(1_000_000, 200_000, 150_000);

            Result<Finances> result = finances.ShiftWageBudget(10_000);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value.WeeklyWageBudget, Is.EqualTo(210_000));
            Assert.That(result.Value.Cash, Is.EqualTo(1_000_000 - (10_000 * Weeks)));
            Assert.That(result.Value.WageHeadroom, Is.EqualTo(60_000), "The new room is free room — the bill did not move.");
        }

        [Test]
        public void ShiftWageBudget_AtTheDefaultRate_PricesASeasonOfWeeks()
        {
            // The owner's number, pinned in one place: 38, a season's worth of match weeks. Freeing
            // 1,000/wk of ceiling is worth 38,000 of transfer cash, and 38,000 buys it back.
            Assert.That(EconomySettings.Default.WageBudgetExchangeWeeks, Is.EqualTo(38));

            Result<Finances> freed = new Finances(0, 100_000, 0).ShiftWageBudget(-1_000);
            Assert.That(freed.IsSuccess, Is.True, freed.Error);
            Assert.That(freed.Value.Cash, Is.EqualTo(38_000));

            Result<Finances> bought = new Finances(38_000, 100_000, 0).ShiftWageBudget(1_000);
            Assert.That(bought.IsSuccess, Is.True, bought.Error);
            Assert.That(bought.Value.Cash, Is.Zero);
        }

        [Test]
        public void ShiftWageBudget_RoundTrip_LeavesTheBooksExactlyAsTheyWere()
        {
            // One rate in both directions, so there is nothing to farm by cycling: out and back is the
            // identity, the same property Fee_IsTheMarketValue_NoSpread pins for buying and selling.
            var before = new Finances(2_000_000, 300_000, 220_000);

            Result<Finances> freed = before.ShiftWageBudget(-40_000);
            Assert.That(freed.IsSuccess, Is.True, freed.Error);
            Assert.That(freed.Value.Cash, Is.GreaterThan(before.Cash), "A trip that moved nothing would pass trivially.");

            Result<Finances> back = freed.Value.ShiftWageBudget(40_000);
            Assert.That(back.IsSuccess, Is.True, back.Error);

            Assert.That(back.Value.Cash, Is.EqualTo(before.Cash));
            Assert.That(back.Value.WeeklyWageBudget, Is.EqualTo(before.WeeklyWageBudget));
            Assert.That(back.Value.WeeklyWageBill, Is.EqualTo(before.WeeklyWageBill));
        }

        [Test]
        public void ShiftWageBudget_ManySmallShifts_MoveExactlyWhatOneBigOneWould()
        {
            // The rounding decision, argued at the table. The exchange is denominated in €/wk and the cash
            // leg is a multiplication, never a division, so there is no truncation anywhere to farm: a
            // thousand shifts of 1/wk land on the same books as one shift of 1,000/wk, and taking the
            // thousand small steps back returns the club to the penny. A cash-denominated exchange would
            // have failed this — €37 at a time would round away to nothing.
            var start = new Finances(10_000_000, 500_000, 100_000);

            Finances piecemeal = start;
            for (int i = 0; i < 1_000; i++)
            {
                Result<Finances> step = piecemeal.ShiftWageBudget(-1);
                Assert.That(step.IsSuccess, Is.True, step.Error);
                piecemeal = step.Value;
            }

            Result<Finances> lump = start.ShiftWageBudget(-1_000);
            Assert.That(lump.IsSuccess, Is.True, lump.Error);
            Assert.That(piecemeal.Cash, Is.EqualTo(lump.Value.Cash), "A thousand small sales must raise exactly what one big one raises.");
            Assert.That(piecemeal.WeeklyWageBudget, Is.EqualTo(lump.Value.WeeklyWageBudget));

            for (int i = 0; i < 1_000; i++)
            {
                Result<Finances> step = piecemeal.ShiftWageBudget(1);
                Assert.That(step.IsSuccess, Is.True, step.Error);
                piecemeal = step.Value;
            }

            Assert.That(piecemeal.Cash, Is.EqualTo(start.Cash), "Two thousand small conversions neither created nor destroyed money.");
            Assert.That(piecemeal.WeeklyWageBudget, Is.EqualTo(start.WeeklyWageBudget));
            Assert.That(piecemeal.WeeklyWageBill, Is.EqualTo(start.WeeklyWageBill));
        }

        [Test]
        public void ShiftWageBudget_BelowTheCommittedWageBill_FailsAndNamesTheLimit()
        {
            // 200k of ceiling with 150k already signed: 50k/wk is uncommitted and may be sold, one euro
            // more may not. Those contracts exist — a ceiling under the bill is not an overspend the board
            // can read, it is a book that does not add up.
            var finances = new Finances(1_000_000, 200_000, 150_000);

            Result<Finances> result = finances.ShiftWageBudget(-50_001);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("150000"), "the wage bill that blocks it");
            Assert.That(result.Error, Does.Contain("50000"), "the most that could have moved");
            Assert.That(result.Error, Does.Contain("1/wk"), "and by how much the ask was over");
        }

        [Test]
        public void ShiftWageBudget_ExactlyDownToTheWageBill_Succeeds()
        {
            // The boundary the test above sits one euro past: the ceiling may land exactly on the bill,
            // because every signed wage is still covered.
            var finances = new Finances(0, 200_000, 150_000);

            Result<Finances> result = finances.ShiftWageBudget(-50_000);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value.WageHeadroom, Is.Zero);
            Assert.That(result.Value.WeeklyWageBudget, Is.EqualTo(result.Value.WeeklyWageBill));
        }

        [Test]
        public void ShiftWageBudget_WhenTheBillAlreadyExceedsTheCeiling_FreesNothing()
        {
            // Headroom can be negative without anyone overspending: a season rollover re-derives the bill
            // from a developed squad against the same ceiling. Nothing is uncommitted, so nothing may be
            // sold — the guard must not read a negative headroom as room.
            var finances = new Finances(1_000_000, 100_000, 120_000);

            Result<Finances> result = finances.ShiftWageBudget(-1);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(finances.WageHeadroom, Is.EqualTo(-20_000), "the state this test is about");
        }

        [Test]
        public void ShiftWageBudget_MoreCashThanTheClubHas_FailsAndNamesTheShortfall()
        {
            long price = 1_000 * Weeks;
            var finances = new Finances(price - 1, 200_000, 0);

            Result<Finances> result = finances.ShiftWageBudget(1_000);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("Not enough transfer cash"));
            Assert.That(result.Error, Does.Contain(price.ToString()), "what it would have cost");
            Assert.That(result.Error, Does.Contain((price - 1).ToString()), "against what is in the bank");
        }

        [Test]
        public void ShiftWageBudget_CostExactlyEqualToCash_Succeeds()
        {
            var finances = new Finances(1_000 * Weeks, 200_000, 0);

            Result<Finances> result = finances.ShiftWageBudget(1_000);

            Assert.That(result.IsSuccess, Is.True, "Spending the last penny on wage room is affordable.");
            Assert.That(result.Value.Cash, Is.Zero);
        }

        [Test]
        public void ShiftWageBudget_WithNegativeCash_RefusesToBuyAnyMoreCeiling()
        {
            // Wages can overspend the cash into the red (PayWeeklyWages does not stop at zero). Buying
            // ceiling from an empty bank would deepen a debt the board is already looking at.
            Result<Finances> result = new Finances(-1, 200_000, 100_000).ShiftWageBudget(1);

            Assert.That(result.IsFailure, Is.True);
        }

        [Test]
        public void ShiftWageBudget_Zero_ChangesNothing()
        {
            var finances = new Finances(1_000_000, 200_000, 150_000);

            Result<Finances> result = finances.ShiftWageBudget(0);

            Assert.That(result.IsSuccess, Is.True, "An identity is not an error.");
            Assert.That(result.Value.Cash, Is.EqualTo(1_000_000));
            Assert.That(result.Value.WeeklyWageBudget, Is.EqualTo(200_000));
            Assert.That(result.Value.WeeklyWageBill, Is.EqualTo(150_000));
        }

        [Test]
        public void ShiftWageBudget_AnAmountTooLargeToPrice_FailsInsteadOfOverflowing()
        {
            // Above long.MaxValue/weeks the price wraps negative, which would read as "costs less than
            // nothing" and slip straight past the cash check — ceiling minted out of an arithmetic
            // accident. Refused in both directions, including the one value that has no positive twin.
            var rich = new Finances(long.MaxValue, long.MaxValue, 0);
            long tooBig = (long.MaxValue / Weeks) + 1;

            Assert.That(rich.ShiftWageBudget(tooBig).IsFailure, Is.True, "buying");
            Assert.That(rich.ShiftWageBudget(-tooBig).IsFailure, Is.True, "selling");
            Assert.That(rich.ShiftWageBudget(long.MinValue).IsFailure, Is.True, "long.MinValue");
        }

        [Test]
        public void ShiftWageBudget_AtAConfiguredRate_UsesTheAssetsWeeksNotTheDefault()
        {
            // The rate is balance data (NON-NEGOTIABLE #3), so a config asset must really move it.
            var economy = new EconomySettings(wageBudgetExchangeWeeks: 10);

            Result<Finances> result = new Finances(0, 100_000, 0).ShiftWageBudget(-1_000, economy);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value.Cash, Is.EqualTo(10_000));
        }
    }
}
