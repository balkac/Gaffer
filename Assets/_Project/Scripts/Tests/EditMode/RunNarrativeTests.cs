using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Narrative;
using Gaffer.Application.Run;
using Gaffer.Common;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The narrative layer wired into a run that is actually being played. <see cref="MomentRecognitionTests"/>
    /// pins the reading; these pin that the reading HAPPENS, on the weeks and at the events where a story
    /// is made — and that it stays scoped to the manager's own players rather than the whole world.
    /// </summary>
    public sealed class RunNarrativeTests
    {
        private const ulong Seed = 20260813UL;

        private static RunSetup SetupOf()
        {
            return new RunSetup(
                teamCount: 8,
                seed: Seed,
                managedClubIndex: 3,
                promotionPosition: 2,
                survivalPosition: 6,
                startingCash: 20_000_000L,
                weeklyWageBudget: 900_000L,
                marketSize: 30,
                guaranteedGems: 3);
        }

        private static RunSession StartRun()
        {
            Result<RunSession> started = RunSessionFactory.Start(
                SetupOf(), new RunBalance(drama: new DramaSettings(maxEventsPerSeason: 0)));
            Assert.That(started.IsSuccess, Is.True, started.Error);
            return started.Value;
        }

        private static bool Has(IReadOnlyList<CareerMoment> moments, CareerMomentKind kind)
        {
            for (int i = 0; i < moments.Count; i++)
            {
                if (moments[i].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void AdvanceWeek_TheFirstWeek_GivesTheWholeElevenTheirDebut()
        {
            RunSession session = StartRun();
            int eleven = session.Lineup().Starters.Count;

            Result<WeekOutcome> week = session.AdvanceWeek();
            Assert.That(week.IsSuccess, Is.True, week.Error);

            int debuts = 0;
            for (int i = 0; i < week.Value.Moments.Count; i++)
            {
                if (week.Value.Moments[i].Kind == CareerMomentKind.Debut)
                {
                    debuts++;
                }
            }

            Assert.That(debuts, Is.EqualTo(eleven),
                "Everyone who played the opening match was playing his first game for the club.");
        }

        [Test]
        public void AdvanceWeek_TheSecondWeek_HandsOutNoDebutsToTheSameEleven()
        {
            RunSession session = StartRun();
            session.AdvanceWeek();

            Result<WeekOutcome> second = session.AdvanceWeek();
            Assert.That(second.IsSuccess, Is.True, second.Error);
            Assert.That(Has(second.Value.Moments, CareerMomentKind.Debut), Is.False,
                "An unchanged eleven collected a second set of debuts — the log is not being read back.");
        }

        [Test]
        public void AdvanceWeek_OverASeason_MostWeeksProduceNoMomentsAtAll()
        {
            // The bet Gate B settles. If every week produced moments the log would be a fixture list, and
            // nothing in a fixture list feels rare.
            RunSession session = StartRun();
            session.AdvanceWeek();

            int quiet = 0;
            int played = 0;
            while (!session.IsSeasonComplete)
            {
                Result<WeekOutcome> week = session.AdvanceWeek();
                Assert.That(week.IsSuccess, Is.True, week.Error);
                played++;
                if (week.Value.Moments.Count == 0)
                {
                    quiet++;
                }
            }

            Assert.That(quiet, Is.GreaterThan(0),
                $"Every one of {played} weeks after the opener produced a moment; nothing can feel rare.");
        }

        [Test]
        public void Journeys_AreKeptForTheManagersPlayersAndNotForTheWorld()
        {
            // The 50,000-player constraint, asserted against a real run: the market is thirty players and
            // the rest of the league has seven more squads, and none of them is being followed.
            RunSession session = StartRun();
            session.AdvanceWeek();

            Assert.That(session.Journeys.Count, Is.EqualTo(session.Lineup().Starters.Count));
            for (int i = 0; i < session.Market.Count; i++)
            {
                Assert.That(session.Journeys.IsFollowing(session.Market[i].Id), Is.False,
                    "A player nobody has signed is being followed.");
            }
        }

        [Test]
        public void SellPlayer_OpensTheSaleMomentAndKeepsFollowingHimAfterHeLeaves()
        {
            // The owner's decision (2026-08-13): a sold player stays followed. It is what makes "the boy
            // you let go" a story three seasons later rather than a deletion.
            RunSession session = StartRun();
            session.AdvanceWeek();

            Player sold = session.Lineup().Starters[0];
            Assert.That(session.JourneyOf(sold.Id), Is.Not.Null);

            while (!session.IsWindowOpen && !session.IsSeasonComplete)
            {
                session.AdvanceWeek();
            }

            Assert.That(session.IsWindowOpen, Is.True, "No transfer window reopened this season.");
            Result<TransferOutcome> sale = session.SellPlayer(sold);
            Assert.That(sale.IsSuccess, Is.True, sale.Error);

            PlayerJourney journey = session.JourneyOf(sold.Id);
            Assert.That(journey, Is.Not.Null, "The log stopped following a player the moment he was sold.");

            bool recorded = false;
            long fee = 0L;
            for (int i = 0; i < journey.Moments.Count; i++)
            {
                if (journey.Moments[i].Kind == CareerMomentKind.Sale)
                {
                    recorded = true;
                    fee = journey.Moments[i].Count;
                }
            }

            Assert.That(recorded, Is.True, "The sale left no mark on his journey.");
            Assert.That(fee, Is.EqualTo(sale.Value.Fee), "The sale moment does not carry what he went for.");
        }

        [Test]
        public void StartNextSeason_ClosesTheJourneyOfAPlayerWhoRetired()
        {
            RunSession session = StartRun();
            session.AdvanceToEndOfSeason();

            var playedFor = new List<PlayerId>();
            foreach (PlayerJourney journey in session.Journeys.Journeys)
            {
                playedFor.Add(journey.Player);
            }

            Result<SeasonRollover> rollover = session.StartNextSeason();
            Assert.That(rollover.IsSuccess, Is.True, rollover.Error);

            for (int i = 0; i < rollover.Value.Retired.Count; i++)
            {
                PlayerId gone = rollover.Value.Retired[i].Id;
                if (!playedFor.Contains(gone))
                {
                    // He never got a game, so there is no story to close — asserted in the negative below.
                    Assert.That(session.JourneyOf(gone), Is.Null,
                        "A journey was opened for a player only to record that he retired.");
                    continue;
                }

                PlayerJourney journey = session.JourneyOf(gone);
                Assert.That(journey, Is.Not.Null);
                Assert.That(Has(journey.Moments, CareerMomentKind.Retirement), Is.True,
                    $"Player {gone.Value} retired without his journey being closed.");
            }
        }

        [Test]
        public void AdvanceWeek_TheSameSeedTwice_RecognisesTheSameMoments()
        {
            RunSession first = StartRun();
            RunSession second = StartRun();

            for (int week = 0; week < 6; week++)
            {
                Result<WeekOutcome> a = first.AdvanceWeek();
                Result<WeekOutcome> b = second.AdvanceWeek();
                Assert.That(b.Value.Moments.Count, Is.EqualTo(a.Value.Moments.Count), $"week {week}");
                for (int i = 0; i < a.Value.Moments.Count; i++)
                {
                    Assert.That(b.Value.Moments[i].Kind, Is.EqualTo(a.Value.Moments[i].Kind), $"week {week} moment {i}");
                    Assert.That(b.Value.Moments[i].Player, Is.EqualTo(a.Value.Moments[i].Player), $"week {week} moment {i}");
                }
            }
        }
    }
}
