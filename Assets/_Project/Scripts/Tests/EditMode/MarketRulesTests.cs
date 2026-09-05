using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Run;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Players;
using Gaffer.Presentation;
using Gaffer.Presentation.Market;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The rules the market screen draws by, held apart from the screen. Each fails quietly on a device:
    /// a card that promises a signing the core refuses, a filter that loses a line, a fee written with the
    /// wrong decimal mark in the wrong language.
    /// </summary>
    public sealed class MarketRulesTests
    {
        // ----- Affordability ----------------------------------------------------------------------------

        [Test]
        public void Affordability_AgreesWithTheCore_OnEveryPlayerInTheMarket()
        {
            // The one property that matters: the card says yes exactly when TransferService would. Checked
            // against the real market and the real starting money, so the two rules cannot drift apart
            // without this failing.
            RunSession session = StartRun();
            IReadOnlyList<Player> market = session.GetMarket();
            EconomySettings economy = EconomySettings.Default;
            int affordable = 0;

            for (int i = 0; i < market.Count; i++)
            {
                Player player = market[i];
                var verdict = Affordability.For(
                    TransferService.Fee(player, economy),
                    PlayerWage.Weekly(player, economy),
                    session.Finances);
                Result<TransferResult> core = TransferService.Sign(session.Finances, session.Squad, player, economy);

                Assert.That(verdict.Affordable, Is.EqualTo(core.IsSuccess), player.Name + ": " + core.Error);
                if (verdict.Affordable)
                {
                    affordable++;
                }
            }

            Assert.That(affordable, Is.GreaterThan(0).And.LessThan(market.Count), "the sample must contain both answers to mean anything");
        }

        [Test]
        public void Affordability_NamesTheShortfall_InTheManagersUnits()
        {
            var finances = new Finances(cash: 1_000_000L, weeklyWageBudget: 100_000L, weeklyWageBill: 80_000L);

            var cash = Affordability.For(fee: 1_400_000L, weeklyWage: 10_000L, finances);
            Assert.That(cash.CashShort, Is.True);
            Assert.That(cash.WageShort, Is.False);
            Assert.That(cash.CashLeft, Is.EqualTo(-400_000L), "short by exactly the difference");

            var wage = Affordability.For(fee: 500_000L, weeklyWage: 25_000L, finances);
            Assert.That(wage.CashShort, Is.False);
            Assert.That(wage.WageShort, Is.True);
            Assert.That(wage.WageRoomLeft, Is.EqualTo(-5_000L));

            var fine = Affordability.For(fee: 500_000L, weeklyWage: 20_000L, finances);
            Assert.That(fine.Affordable, Is.True, "a wage that exactly fills the room is allowed, as the core allows it");
            Assert.That(fine.CashLeft, Is.EqualTo(500_000L));
            Assert.That(fine.WageRoomLeft, Is.EqualTo(0L));
        }

        // ----- The list ---------------------------------------------------------------------------------

        [Test]
        public void Select_KeepsOnlyTheLineAsked_AndTheSourceOrder()
        {
            RunSession session = StartRun();
            IReadOnlyList<Player> market = session.GetMarket();
            var shown = new List<Player>();

            MarketList.Select(market, Position.Defender, null, shown);

            Assert.That(shown, Is.Not.Empty);
            int expected = 0;
            for (int i = 0; i < market.Count; i++)
            {
                if (market[i].Position == Position.Defender)
                {
                    Assert.That(shown[expected].Id, Is.EqualTo(market[i].Id), "source order is kept");
                    expected++;
                }
            }

            Assert.That(shown.Count, Is.EqualTo(expected), "nobody off the line, nobody on it dropped");
        }

        [Test]
        public void Select_WithNoLine_AndAPredicate_KeepsExactlyWhatThePredicateAllows()
        {
            RunSession session = StartRun();
            IReadOnlyList<Player> market = session.GetMarket();
            var shown = new List<Player>();

            MarketList.Select(market, null, p => p.Age <= 21, shown);

            Assert.That(shown, Is.Not.Empty);
            for (int i = 0; i < shown.Count; i++)
            {
                Assert.That(shown[i].Age, Is.LessThanOrEqualTo(21));
            }

            int young = 0;
            for (int i = 0; i < market.Count; i++)
            {
                if (market[i].Age <= 21)
                {
                    young++;
                }
            }

            Assert.That(shown.Count, Is.EqualTo(young));
        }

        [Test]
        public void SortByRating_IsBestFirst_AndStableBetweenCalls()
        {
            RunSession session = StartRun();
            var players = new List<Player>(session.GetMarket());

            MarketList.SortByRating(players);
            for (int i = 1; i < players.Count; i++)
            {
                double above = PlayerRatings.ForRole(players[i - 1]);
                double below = PlayerRatings.ForRole(players[i]);
                Assert.That(above, Is.GreaterThanOrEqualTo(below), "best first");
                if (above == below)
                {
                    Assert.That(players[i - 1].Id.Value, Is.LessThan(players[i].Id.Value), "equal men keep a fixed order");
                }
            }

            var again = new List<Player>(players);
            again.Reverse();
            MarketList.SortByRating(again);
            for (int i = 0; i < players.Count; i++)
            {
                Assert.That(again[i].Id, Is.EqualTo(players[i].Id), "the same list from any starting order");
            }
        }

        // ----- Money ------------------------------------------------------------------------------------

        [Test]
        public void UiMoney_WritesThreeSteps_AndTheLocalesDecimalMark()
        {
            Assert.That(UiMoney.Format(1_250_000L, "en"), Is.EqualTo("€1.3M"));
            Assert.That(UiMoney.Format(1_250_000L, "tr"), Is.EqualTo("€1,3M"));
            Assert.That(UiMoney.Format(1_000_000L, "en"), Is.EqualTo("€1.0M"));
            Assert.That(UiMoney.Format(850_000L, "en"), Is.EqualTo("€850k"));
            Assert.That(UiMoney.Format(850_000L, "tr"), Is.EqualTo("€850k"));
            Assert.That(UiMoney.Format(900L, "en"), Is.EqualTo("€900"));
            Assert.That(UiMoney.Format(0L, "tr"), Is.EqualTo("€0"));
            Assert.That(UiMoney.Format(-400_000L, "en"), Is.EqualTo("-€400k"));
        }

        [Test]
        public void UiMoney_AnUnknownLocale_StillWritesTheNumber()
        {
            Assert.That(UiMoney.Format(1_250_000L, "xx-nowhere"), Is.EqualTo("€1.3M"));
            Assert.That(UiMoney.Format(1_250_000L, null), Is.EqualTo("€1.3M"));
        }

        // ----- Building material ------------------------------------------------------------------------

        private static RunSession StartRun()
        {
            Result<RunSession> started = RunSessionFactory.Start(
                new RunSetup(
                    teamCount: 8,
                    seed: 7UL,
                    managedClubIndex: 0,
                    promotionPosition: 2,
                    survivalPosition: 6,
                    startingCash: 3_000_000L,
                    weeklyWageBudget: 400_000L,
                    marketSize: 60,
                    guaranteedGems: 2),
                new RunBalance(drama: new DramaSettings(maxEventsPerSeason: 0)));

            Assert.That(started.IsSuccess, Is.True, started.Error);
            return started.Value;
        }
    }
}
