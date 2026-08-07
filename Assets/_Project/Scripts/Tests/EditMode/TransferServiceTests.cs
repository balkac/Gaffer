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
    }
}
