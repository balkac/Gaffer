using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Narrative;
using Gaffer.Application.Run;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Common.Localization;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using Gaffer.Infrastructure.Localization;
using Gaffer.Presentation.Matchday;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// How an afternoon is read off a played week. Two rules carry the screen and both fail QUIETLY when
    /// they are wrong: a story attached to the wrong goal names the wrong player in a sentence that still
    /// reads perfectly, and a mis-ordered feed looks like a feed. Neither would draw an exception, so both
    /// are pinned here rather than left to be noticed on a screenshot.
    /// </summary>
    public sealed class MatchBeatsTests
    {
        private const int ClubCount = 8;
        private const ulong Seed = 99UL;
        private const int ManagedIndex = 0;

        // ----- The fold ---------------------------------------------------------------------------------

        [Test]
        public void Of_AGoalAndTheMomentItCreated_AreOneBeatAndNotTwo()
        {
            RunSession session = StartRun();
            PlayerId scorer = FirstPlayer(session);

            MatchResult match = Played(session, 2, 0, Goal(68, TeamSide.Home, scorer));

            List<MatchBeat> beats = MatchBeats.Of(
                Week(match, Moment(CareerMomentKind.FirstGoal, scorer, session.ManagedClub, minute: 68)),
                match,
                session,
                English());

            Assert.That(beats.Count, Is.EqualTo(1), "the moment should be folded into the goal, not listed beside it");
            Assert.That(beats[0].IsGoal, Is.True);
            Assert.That(beats[0].HasStory, Is.True, "the goal that made a career moment should carry it");
        }

        [Test]
        public void Of_AFoldedStory_DoesNotRepeatTheMinuteOrTheScorerTheRowAlreadyNames()
        {
            // Found on the device: the row said "41' · GOAL · Pauquet" and the line under it began "41' —
            // Pauquet ...". The journey log's line stands alone and has to; the report's does not.
            RunSession session = StartRun();
            PlayerId scorer = FirstPlayer(session);
            string name = session.Squad.Players[0].Name;

            MatchResult match = Played(session, 1, 0, Goal(41, TeamSide.Home, scorer));
            List<MatchBeat> beats = MatchBeats.Of(
                Week(match, Moment(CareerMomentKind.FirstGoal, scorer, session.ManagedClub, minute: 41)),
                match,
                session,
                English());

            Assert.That(beats[0].HasStory, Is.True);
            Assert.That(beats[0].Headline, Does.Contain(name), "the row names the scorer");
            Assert.That(beats[0].Story, Does.Not.Contain(name), "so the story must not name him again");
            Assert.That(beats[0].Story, Does.Not.Contain("41"), "nor read the minute again");
        }

        [Test]
        public void Of_AMomentBelongingToADifferentScorer_IsNotStolenByTheGoalBeforeIt()
        {
            // The rule that is easy to get wrong: matching on minute alone would hand the 68th-minute
            // scorer a sentence about the man who scored at 70. It would read perfectly and name the
            // wrong player, which is worse than saying nothing.
            RunSession session = StartRun();
            PlayerId first = PlayerAt(session, 0);
            PlayerId second = PlayerAt(session, 1);

            MatchResult match = Played(
                session,
                2,
                0,
                Goal(68, TeamSide.Home, first),
                Goal(70, TeamSide.Home, second));

            List<MatchBeat> beats = MatchBeats.Of(
                Week(match, Moment(CareerMomentKind.FirstGoal, second, session.ManagedClub, minute: 70)),
                match,
                session,
                English());

            Assert.That(beats.Count, Is.EqualTo(2));
            Assert.That(beats[0].Minute, Is.EqualTo(70));
            Assert.That(beats[0].HasStory, Is.True, "the 70th-minute scorer earned the moment");
            Assert.That(beats[1].Minute, Is.EqualTo(68));
            Assert.That(beats[1].HasStory, Is.False, "the earlier goal must not inherit somebody else's story");
        }

        [Test]
        public void Of_AMomentNoGoalExplains_StandsAsItsOwnBeat()
        {
            // A debut is not a goal and still happened to somebody. It keeps its place in the minute order
            // rather than being dropped because nothing went in.
            RunSession session = StartRun();
            PlayerId scorer = PlayerAt(session, 0);
            PlayerId debutant = PlayerAt(session, 1);
            MatchResult match = Played(session, 1, 0, Goal(68, TeamSide.Home, scorer));

            List<MatchBeat> beats = MatchBeats.Of(
                Week(match, Moment(CareerMomentKind.Debut, debutant, session.ManagedClub, minute: 68)),
                match,
                session,
                English());

            Assert.That(beats.Count, Is.EqualTo(2));
            Assert.That(beats[0].IsGoal, Is.True, "at the same minute the goal is read out first");
            Assert.That(beats[1].IsGoal, Is.False);
            Assert.That(beats[1].HasStory, Is.False, "a standalone moment carries its words in the headline");
        }

        // ----- The order --------------------------------------------------------------------------------

        [Test]
        public void Of_ReadsTheAfternoonLatestFirst()
        {
            RunSession session = StartRun();
            PlayerId scorer = FirstPlayer(session);
            MatchResult match = Played(
                session,
                2,
                1,
                Goal(12, TeamSide.Home, scorer),
                Goal(68, TeamSide.Away, null),
                Goal(45, TeamSide.Home, scorer));

            List<MatchBeat> beats = MatchBeats.Of(Week(match), match, session, English());

            Assert.That(beats.Count, Is.EqualTo(3));
            Assert.That(beats[0].Minute, Is.EqualTo(68));
            Assert.That(beats[1].Minute, Is.EqualTo(45));
            Assert.That(beats[2].Minute, Is.EqualTo(12));
        }

        [Test]
        public void Of_AGoalAgainst_IsStillOnTheFeedAndIsMarkedAsTheirs()
        {
            // A conceded goal is part of the afternoon. Leaving it off would make a 2-1 read as two goals
            // and no answer.
            RunSession session = StartRun();
            MatchResult match = Played(session, 1, 1, Goal(20, TeamSide.Home, FirstPlayer(session)), Goal(70, TeamSide.Away, null));

            List<MatchBeat> beats = MatchBeats.Of(Week(match), match, session, English());

            Assert.That(beats.Count, Is.EqualTo(2));
            Assert.That(beats[0].IsOurs, Is.False, "the away goal belongs to the opposition here");
            Assert.That(beats[1].IsOurs, Is.True, "the managed club is at home in this fixture");
        }

        [Test]
        public void Of_AGoallessWeekWithNothingRecognised_ReadsAsNoBeatsRatherThanAnEmptyRow()
        {
            RunSession session = StartRun();
            MatchResult match = Played(session, 0, 0);

            Assert.That(MatchBeats.Of(Week(match), match, session, English()), Is.Empty);
        }

        // ----- Building material ------------------------------------------------------------------------

        private static LocalizedStrings English()
        {
            return GameStrings.Default.For(Locales.Reference);
        }

        private static RunSession StartRun()
        {
            Result<RunSession> started = RunSessionFactory.Start(
                new RunSetup(
                    teamCount: ClubCount,
                    seed: Seed,
                    managedClubIndex: ManagedIndex,
                    promotionPosition: 2,
                    survivalPosition: 6,
                    startingCash: 6_000_000L,
                    weeklyWageBudget: 400_000L,
                    marketSize: 12,
                    guaranteedGems: 2),
                new RunBalance(drama: new DramaSettings(maxEventsPerSeason: 0)));

            Assert.That(started.IsSuccess, Is.True, started.Error);
            return started.Value;
        }

        private static PlayerId FirstPlayer(RunSession session)
        {
            return PlayerAt(session, 0);
        }

        private static PlayerId PlayerAt(RunSession session, int index)
        {
            return session.Squad.Players[index].Id;
        }

        /// <summary>The managed club at home against whoever is next along, so "ours" is a real answer and
        /// not an artefact of a null session.</summary>
        private static MatchResult Played(RunSession session, int homeGoals, int awayGoals, params MatchEvent[] events)
        {
            var away = new ClubId(session.ManagedClub.Value == 0 ? 1 : 0);
            return new MatchResult(
                session.ManagedClub,
                away,
                homeGoals,
                awayGoals,
                homeShots: 10,
                awayShots: 7,
                events: events);
        }

        private static MatchEvent Goal(int minute, TeamSide side, PlayerId? scorer)
        {
            return new MatchEvent(minute, side, MatchEventKind.Goal, scorer);
        }

        private static CareerMoment Moment(CareerMomentKind kind, PlayerId player, ClubId club, int minute)
        {
            return new CareerMoment(kind, player, club, season: 1, round: 3, minute: minute);
        }

        private static WeekOutcome Week(MatchResult match, params CareerMoment[] moments)
        {
            return new WeekOutcome(
                round: 3,
                playedRounds: 4,
                roundCount: 14,
                isSeasonComplete: false,
                matches: new[] { match },
                managedMatch: match,
                tablePosition: 5,
                lossStreak: 0,
                finances: new Finances(1_000_000L, 400_000L, 300_000L),
                wagesPaid: 300_000L,
                windowPhase: TransferWindowPhase.Closed,
                drama: null,
                verdict: null,
                finalPosition: 0,
                moments: moments);
        }
    }
}
