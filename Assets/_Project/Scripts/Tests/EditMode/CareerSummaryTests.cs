using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Narrative;
using Gaffer.Application.Run;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Faz 5.5: a spell at the club, read back as one shape. The number that has to be right is
    /// <see cref="CareerSummary.Profit"/> — GDD §4.4's discover-grow-sell flip only pays off if the payoff
    /// is visible, and until now the manager was expected to have kept track of the two fees himself.
    /// </summary>
    public sealed class CareerSummaryTests
    {
        private const ulong Seed = 20260813UL;
        private static readonly ClubId Us = new ClubId(0);

        private static PlayerJourney JourneyWith(params CareerMoment[] moments)
        {
            return PlayerJourney.Restore(new PlayerId(7), "Ali Yilmaz", appearances: 47, goals: 19, moments: moments);
        }

        private static CareerMoment Moment(CareerMomentKind kind, int season, long count = 0L)
        {
            return new CareerMoment(kind, new PlayerId(7), Us, season, 1, CareerMoment.NoMinute, count);
        }

        [Test]
        public void Create_ABoughtAndSoldPlayer_ReportsWhatTheClubMadeOnHim()
        {
            var summary = CareerSummary.Create(JourneyWith(
                Moment(CareerMomentKind.Signing, 1, 500_000L),
                Moment(CareerMomentKind.FirstGoal, 1),
                Moment(CareerMomentKind.Sale, 4, 12_000_000L)));

            Assert.That(summary.ArrivalFee, Is.EqualTo(500_000L));
            Assert.That(summary.DepartureFee, Is.EqualTo(12_000_000L));
            Assert.That(summary.Profit, Is.EqualTo(11_500_000L));
            Assert.That(summary.HasLeft, Is.True);
            Assert.That(summary.SeasonsAtTheClub, Is.EqualTo(4));
        }

        [Test]
        public void Create_AnAcademyPlayerSold_CountsTheWholeFeeAsProfit()
        {
            // The flip at its purest: he cost nothing because the club made him.
            var summary = CareerSummary.Create(JourneyWith(
                Moment(CareerMomentKind.AcademyArrival, 2),
                Moment(CareerMomentKind.Sale, 5, 8_000_000L)));

            Assert.That(summary.CameThroughTheAcademy, Is.True);
            Assert.That(summary.ArrivalFee, Is.EqualTo(0L));
            Assert.That(summary.Profit, Is.EqualTo(8_000_000L));
        }

        [Test]
        public void Create_APlayerSoldForLessThanHeCost_ReportsTheLossHonestly()
        {
            // Negative is a real answer. A summary that floored at zero would quietly turn every bad
            // signing into a break-even one.
            var summary = CareerSummary.Create(JourneyWith(
                Moment(CareerMomentKind.Signing, 1, 4_000_000L),
                Moment(CareerMomentKind.Sale, 2, 900_000L)));

            Assert.That(summary.Profit, Is.EqualTo(-3_100_000L));
        }

        [Test]
        public void Create_APlayerStillAtTheClub_HasNotLeftAndIsWorthNothingYet()
        {
            var summary = CareerSummary.Create(JourneyWith(
                Moment(CareerMomentKind.Signing, 1, 500_000L),
                Moment(CareerMomentKind.AppearanceMilestone, 3, 50L)));

            Assert.That(summary.HasLeft, Is.False);
            Assert.That(summary.DepartureFee, Is.EqualTo(0L));
        }

        [Test]
        public void Create_APlayerWhoRetired_HasLeftWithoutAFee()
        {
            var summary = CareerSummary.Create(JourneyWith(
                Moment(CareerMomentKind.AcademyArrival, 1),
                Moment(CareerMomentKind.Retirement, 14)));

            Assert.That(summary.HasLeft, Is.True);
            Assert.That(summary.DepartureFee, Is.EqualTo(0L));
            Assert.That(summary.Profit, Is.EqualTo(0L));
        }

        [Test]
        public void Create_APlayerTheManagerNeverHad_IsAnEmptyAnswerRatherThanAThrow()
        {
            var summary = CareerSummary.Create(null);

            Assert.That(summary.Appearances, Is.EqualTo(0));
            Assert.That(summary.HasLeft, Is.False);
        }

        // ----- The recap ----------------------------------------------------------------------------------

        [Test]
        public void Recap_ASeason_HoldsOnlyThatSeasonsMomentsInWeekOrder()
        {
            var log = new JourneyLog();
            log.Restore(PlayerJourney.Restore(new PlayerId(1), "One", 10, 2, new[]
            {
                new CareerMoment(CareerMomentKind.Debut, new PlayerId(1), Us, 1, 5),
                new CareerMoment(CareerMomentKind.FirstGoal, new PlayerId(1), Us, 2, 30),
            }));
            log.Restore(PlayerJourney.Restore(new PlayerId(2), "Two", 8, 1, new[]
            {
                new CareerMoment(CareerMomentKind.Debut, new PlayerId(2), Us, 2, 3),
            }));

            var recap = SeasonRecap.Create(log, 2);

            Assert.That(recap.Moments.Count, Is.EqualTo(2));
            Assert.That(recap.Moments[0].Round, Is.EqualTo(3), "The recap is not in week order.");
            Assert.That(recap.Moments[1].Round, Is.EqualTo(30));
            Assert.That(recap.WasQuiet, Is.False);
        }

        [Test]
        public void Recap_ASeasonNothingHappenedIn_IsEmptyRatherThanNull()
        {
            var recap = SeasonRecap.Create(new JourneyLog(), 9);

            Assert.That(recap, Is.Not.Null);
            Assert.That(recap.WasQuiet, Is.True);
        }

        // ----- Through a real run -------------------------------------------------------------------------

        [Test]
        public void SellPlayer_TheSummaryOfTheManYouJustSold_CarriesBothFees()
        {
            RunSession session = StartRun();
            session.AdvanceWeek();

            Player target = Youngest(session.GetMarket());
            while (!session.IsWindowOpen && !session.IsSeasonComplete)
            {
                session.AdvanceWeek();
            }

            Result<TransferOutcome> signing = session.SignPlayer(target);
            Assert.That(signing.IsSuccess, Is.True, signing.Error);
            Result<TransferOutcome> sale = session.SellPlayer(session.Squad.Players[session.Squad.Players.Count - 1]);
            Assert.That(sale.IsSuccess, Is.True, sale.Error);

            CareerSummary summary = session.CareerOf(sale.Value.Player.Id);
            Assert.That(summary.HasLeft, Is.True);
            Assert.That(summary.DepartureFee, Is.EqualTo(sale.Value.Fee));
        }

        private static RunSession StartRun()
        {
            var setup = new RunSetup(
                teamCount: 8, seed: Seed, managedClubIndex: 3,
                promotionPosition: 2, survivalPosition: 6,
                startingCash: 20_000_000L, weeklyWageBudget: 900_000L,
                marketSize: 30, guaranteedGems: 3);

            Result<RunSession> started = RunSessionFactory.Start(
                setup, new RunBalance(drama: new DramaSettings(maxEventsPerSeason: 0)));
            Assert.That(started.IsSuccess, Is.True, started.Error);
            return started.Value;
        }

        private static Player Youngest(IReadOnlyList<Player> players)
        {
            Player youngest = players[0];
            for (int i = 1; i < players.Count; i++)
            {
                if (players[i].Age < youngest.Age)
                {
                    youngest = players[i];
                }
            }

            return youngest;
        }
    }
}
