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
                Finishing = level, Pace = level, Technique = level, Positioning = level, Dribbling = level,
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
        public void Sign_WithEnoughBudget_AddsThePlayerAndDeductsTheFee()
        {
            Squad squad = SquadOf(20);
            Player target = Forward(1, 75, 24);
            long fee = TransferService.SignFee(target);

            Result<TransferResult> result = TransferService.Sign(fee + 500_000, squad, target);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Budget, Is.EqualTo(500_000));
            Assert.That(result.Value.Squad.Count, Is.EqualTo(21));
            Assert.That(result.Value.Squad.Contains(target.Id), Is.True);
            Assert.That(squad.Count, Is.EqualTo(20), "The original squad must be unchanged.");
        }

        [Test]
        public void Sign_WithoutEnoughBudget_Fails()
        {
            Squad squad = SquadOf(20);
            Player target = Forward(1, 80, 24);
            long fee = TransferService.SignFee(target);

            Result<TransferResult> result = TransferService.Sign(fee - 1, squad, target);

            Assert.That(result.IsFailure, Is.True);
        }

        [Test]
        public void Sign_PlayerAlreadyInSquad_Fails()
        {
            Player target = Forward(1, 70, 24);
            var squad = new Squad(new List<Player> { target });

            Result<TransferResult> result = TransferService.Sign(100_000_000, squad, target);

            Assert.That(result.IsFailure, Is.True);
        }

        [Test]
        public void Sell_RemovesThePlayerAndAddsTheFee()
        {
            Squad squad = SquadOf(20);
            Player onRoster = squad.Players[3];

            Result<TransferResult> result = TransferService.Sell(2_000_000, squad, onRoster);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Squad.Count, Is.EqualTo(19));
            Assert.That(result.Value.Squad.Contains(onRoster.Id), Is.False);
            Assert.That(result.Value.Budget, Is.EqualTo(2_000_000 + TransferService.SellFee(onRoster)));
        }

        [Test]
        public void Sell_DownToTheMinimumSquad_Fails()
        {
            Squad squad = SquadOf(11);
            Player onRoster = squad.Players[0];

            Result<TransferResult> result = TransferService.Sell(0, squad, onRoster);

            Assert.That(result.IsFailure, Is.True);
        }

        [Test]
        public void SignThenSell_LosesMoneyOnTheSpread()
        {
            // Buying at a premium and selling at a discount means churning the same player bleeds cash.
            Player target = Forward(1, 78, 25);
            long signFee = TransferService.SignFee(target);
            long sellFee = TransferService.SellFee(target);

            Assert.That(sellFee, Is.LessThan(signFee));
        }
    }
}
