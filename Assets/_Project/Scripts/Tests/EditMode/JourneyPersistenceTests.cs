using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Narrative;
using Gaffer.Application.Run;
using Gaffer.Application.Serialization;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Save v7: the run's memory and its banked development survive a reload.
    ///
    /// <para>Both halves matter for a different reason. A journey cannot be re-derived from anything —
    /// a moment is a reading of a match the document does not keep — so losing it loses the story
    /// outright. The development period CAN be re-derived, badly: settling it early at capture (the fix
    /// before this one) neither lost nor duplicated anything, but paid development in a finer grain than
    /// playing on would have, worth +0.29% of a season to a manager who saved every week. Carrying it is
    /// what makes WHEN you save affect nothing at all.</para>
    /// </summary>
    public sealed class JourneyPersistenceTests
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

        private static RunBalance BalanceOf()
        {
            return new RunBalance(drama: new DramaSettings(maxEventsPerSeason: 0));
        }

        private static RunSession StartRun()
        {
            Result<RunSession> started = RunSessionFactory.Start(SetupOf(), BalanceOf());
            Assert.That(started.IsSuccess, Is.True, started.Error);
            return started.Value;
        }

        private static RunSession SaveAndReload(RunSession session)
        {
            SeasonSaveData document = session.Capture();
            Result<RunSession> resumed = RunSessionFactory.Resume(SetupOf(), BalanceOf(), document, document.MatchSeed);
            Assert.That(resumed.IsSuccess, Is.True, resumed.Error);
            return resumed.Value;
        }

        // ----- The memory ---------------------------------------------------------------------------------

        [Test]
        public void Capture_ThenResume_KeepsEveryJourneyMomentForMoment()
        {
            RunSession session = StartRun();
            for (int week = 0; week < 6; week++)
            {
                session.AdvanceWeek();
            }

            var before = new Dictionary<int, PlayerJourney>();
            foreach (PlayerJourney journey in session.Journeys.Journeys)
            {
                before[journey.Player.Value] = journey;
            }

            Assert.That(before.Count, Is.GreaterThan(0), "Nothing was followed, so this test proves nothing.");

            RunSession reloaded = SaveAndReload(session);

            Assert.That(reloaded.Journeys.Count, Is.EqualTo(before.Count));
            foreach (KeyValuePair<int, PlayerJourney> entry in before)
            {
                PlayerJourney after = reloaded.JourneyOf(new PlayerId(entry.Key));
                Assert.That(after, Is.Not.Null, $"player {entry.Key} is no longer followed");
                Assert.That(after.Name, Is.EqualTo(entry.Value.Name), $"player {entry.Key} name");
                Assert.That(after.Appearances, Is.EqualTo(entry.Value.Appearances), $"player {entry.Key} appearances");
                Assert.That(after.Goals, Is.EqualTo(entry.Value.Goals), $"player {entry.Key} goals");
                Assert.That(after.Moments.Count, Is.EqualTo(entry.Value.Moments.Count), $"player {entry.Key} moment count");

                for (int i = 0; i < after.Moments.Count; i++)
                {
                    CareerMoment mine = after.Moments[i];
                    CareerMoment theirs = entry.Value.Moments[i];
                    Assert.That(mine.Kind, Is.EqualTo(theirs.Kind), $"player {entry.Key} moment {i} kind");
                    Assert.That(mine.Season, Is.EqualTo(theirs.Season), $"player {entry.Key} moment {i} season");
                    Assert.That(mine.Round, Is.EqualTo(theirs.Round), $"player {entry.Key} moment {i} round");
                    Assert.That(mine.Minute, Is.EqualTo(theirs.Minute), $"player {entry.Key} moment {i} minute");
                    Assert.That(mine.Count, Is.EqualTo(theirs.Count), $"player {entry.Key} moment {i} count");
                }
            }
        }

        [Test]
        public void Resume_AfterAReload_DoesNotHandOutASecondDebut()
        {
            // The counters are the point of storing them: they are what "was this his first" is answered
            // from, and a reload that forgot them would give the whole squad a second debut.
            RunSession session = StartRun();
            session.AdvanceWeek();

            RunSession reloaded = SaveAndReload(session);
            Result<WeekOutcome> next = reloaded.AdvanceWeek();
            Assert.That(next.IsSuccess, Is.True, next.Error);

            for (int i = 0; i < next.Value.Moments.Count; i++)
            {
                Assert.That(next.Value.Moments[i].Kind, Is.Not.EqualTo(CareerMomentKind.Debut),
                    "A reloaded run handed out a debut to someone who had already played.");
            }
        }

        [Test]
        public void Capture_ASoldPlayer_IsStillInTheDocumentAfterHeHasLeft()
        {
            // Journeys are scoped to players the manager HAS HAD, not has. If persistence quietly narrowed
            // that to the current squad, the Hall of Legends would lose everyone worth putting in it.
            RunSession session = StartRun();
            session.AdvanceWeek();
            Player sold = session.Lineup().Starters[0];

            while (!session.IsWindowOpen && !session.IsSeasonComplete)
            {
                session.AdvanceWeek();
            }

            Result<TransferOutcome> sale = session.SellPlayer(sold);
            Assert.That(sale.IsSuccess, Is.True, sale.Error);

            RunSession reloaded = SaveAndReload(session);
            PlayerJourney journey = reloaded.JourneyOf(sold.Id);

            Assert.That(journey, Is.Not.Null, "The player you sold was dropped from the save.");
            Assert.That(journey.Name, Is.EqualTo(sold.Name));
        }

        // ----- The banked development ---------------------------------------------------------------------

        [Test]
        public void Capture_MidPeriod_LeavesTheSquadExactlyWherePlayingOnWouldHave()
        {
            // The +0.29% is gone: saving three weeks into a four-week period and reloading now continues
            // the period rather than cashing it out early, so the result is identical either way.
            RunSession straight = StartRun();
            for (int week = 0; week < 8; week++)
            {
                straight.AdvanceWeek();
            }

            RunSession reloaded = StartRun();
            for (int week = 0; week < 3; week++)
            {
                reloaded.AdvanceWeek();
            }

            reloaded = SaveAndReload(reloaded);
            for (int week = 0; week < 5; week++)
            {
                reloaded.AdvanceWeek();
            }

            AssertSameSquadRatings(straight, reloaded);
        }

        [Test]
        public void Capture_EveryWeek_CostsAndGainsNothingAcrossAWholeSeason()
        {
            // The measurement that started this: a manager who saved after every match used to lose 0.407
            // squad OVR a season, then — after the early-settle fix — to gain 0.178. Now: neither.
            RunSession straight = StartRun();
            while (!straight.IsSeasonComplete)
            {
                straight.AdvanceWeek();
            }

            RunSession scummed = StartRun();
            while (!scummed.IsSeasonComplete)
            {
                scummed.AdvanceWeek();
                scummed = SaveAndReload(scummed);
            }

            AssertSameSquadRatings(straight, scummed);
        }

        private static void AssertSameSquadRatings(RunSession expected, RunSession actual)
        {
            IReadOnlyList<Player> mine = expected.Squad.Players;
            for (int i = 0; i < mine.Count; i++)
            {
                Player theirs = Find(actual.Squad.Players, mine[i].Id);
                Assert.That(theirs, Is.Not.Null, $"player {mine[i].Id.Value} is missing after the reload");
                Assert.That(PlayerRatings.ForRole(theirs), Is.EqualTo(PlayerRatings.ForRole(mine[i])).Within(1e-9),
                    $"player {mine[i].Id.Value} ({mine[i].Name}) developed differently across a save");
            }
        }

        private static Player Find(IReadOnlyList<Player> players, PlayerId id)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Id == id)
                {
                    return players[i];
                }
            }

            return null;
        }
    }
}
