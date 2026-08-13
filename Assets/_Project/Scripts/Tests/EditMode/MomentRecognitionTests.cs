using System.Collections.Generic;
using Gaffer.Application.Narrative;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Faz 5's first claim: the narrative layer RECOGNISES rather than generates. A goal is a fact —
    /// minute, side, scorer. "His first goal, in a derby, on his debut" is a moment, and the difference
    /// is three inputs the sim already has: what the match was, who the player is, and what had already
    /// happened to him. These pin that reading, and that recognition never touches the game.
    /// </summary>
    public sealed class MomentRecognitionTests
    {
        private static readonly ClubId Us = new ClubId(0);
        private static readonly ClubId Them = new ClubId(1);

        private static Player Striker(int id = 1)
        {
            return new Player(new PlayerId(id), "Ali Yilmaz", "Turkey", PlayerRole.Striker, 19, new Attributes { Finishing = 60 }, 88);
        }

        private static MatchResult Match(params MatchEvent[] events)
        {
            int home = 0;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Side == TeamSide.Home && events[i].Kind == MatchEventKind.Goal)
                {
                    home++;
                }
            }

            return new MatchResult(Us, Them, home, 0, home, 0, events);
        }

        private static MatchEvent Goal(int minute, PlayerId scorer)
        {
            return new MatchEvent(minute, TeamSide.Home, MatchEventKind.Goal, scorer);
        }

        private static MatchContext Ordinary()
        {
            return new MatchContext(MatchImportance.Normal, 10_000, isTitleDecider: false, isRivalry: false);
        }

        private static MatchContext Derby()
        {
            return new MatchContext(MatchImportance.Derby, 10_000, isTitleDecider: false, isRivalry: true);
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

        private static CareerMoment Only(IReadOnlyList<CareerMoment> moments, CareerMomentKind kind)
        {
            CareerMoment found = default;
            int count = 0;
            for (int i = 0; i < moments.Count; i++)
            {
                if (moments[i].Kind == kind)
                {
                    found = moments[i];
                    count++;
                }
            }

            Assert.That(count, Is.EqualTo(1), $"Expected exactly one {kind}, found {count}.");
            return found;
        }

        // ----- The three inputs ---------------------------------------------------------------------------

        [Test]
        public void Recognise_APlayersFirstAppearance_IsADebutAndHisSecondIsNot()
        {
            // History is the input. Nothing about the match itself distinguishes these two.
            var log = new JourneyLog();
            var recogniser = new MomentRecogniser();
            var eleven = new List<Player> { Striker() };

            IReadOnlyList<CareerMoment> first = recogniser.Recognise(log, Us, eleven, Match(), Ordinary(), 1, 1);
            Assert.That(Has(first, CareerMomentKind.Debut), Is.True, "A first appearance was not read as a debut.");

            IReadOnlyList<CareerMoment> second = recogniser.Recognise(log, Us, eleven, Match(), Ordinary(), 1, 2);
            Assert.That(Has(second, CareerMomentKind.Debut), Is.False, "A second appearance was read as another debut.");
        }

        [Test]
        public void Recognise_AGoalInAnOrdinaryMatch_IsNoOccasionButTheSameGoalInADerbyIs()
        {
            // The occasion is the input: the same fact, told apart only by the fixture it happened in.
            Player player = Striker();
            var eleven = new List<Player> { player };

            IReadOnlyList<CareerMoment> ordinary = new MomentRecogniser()
                .Recognise(new JourneyLog(), Us, eleven, Match(Goal(23, player.Id)), Ordinary(), 1, 1);
            Assert.That(Has(ordinary, CareerMomentKind.DerbyGoal), Is.False);

            IReadOnlyList<CareerMoment> derby = new MomentRecogniser()
                .Recognise(new JourneyLog(), Us, eleven, Match(Goal(23, player.Id)), Derby(), 1, 1);
            Assert.That(Has(derby, CareerMomentKind.DerbyGoal), Is.True);
        }

        [Test]
        public void Recognise_EachKindOfOccasion_IsItsOwnMomentAndNotAGenericBigMatch()
        {
            // Why the split exists. Collapsed to one "big match" kind, a career printed the SAME sentence
            // four times — the derby is a fixed fixture on a deterministic schedule, so it recurs every
            // season — and four identical lines are a fixture list wearing prose. Told apart, four derby
            // goals read as a habit, which is a story (PROGRESS 2026-08-13).
            Player player = Striker();
            var eleven = new List<Player> { player };

            AssertGoalIn(new MatchContext(MatchImportance.Derby, 10_000, false, true), CareerMomentKind.DerbyGoal, eleven, player);
            AssertGoalIn(new MatchContext(MatchImportance.Final, 10_000, true, false), CareerMomentKind.TitleDeciderGoal, eleven, player);
            AssertGoalIn(new MatchContext(MatchImportance.RelegationSixPointer, 10_000, false, false), CareerMomentKind.RelegationGoal, eleven, player);
        }

        [Test]
        public void Recognise_ADerbyThatIsAlsoATitleDecider_TellsTheBiggerStoryOnce()
        {
            // The occasions compose on the fixture but a goal is told once, so the strongest reading wins.
            // Two lines for one goal would read as two goals.
            Player player = Striker();
            var both = new MatchContext(MatchImportance.Final, 10_000, isTitleDecider: true, isRivalry: true);

            IReadOnlyList<CareerMoment> moments = new MomentRecogniser()
                .Recognise(new JourneyLog(), Us, new List<Player> { player }, Match(Goal(70, player.Id)), both, 1, 30);

            Assert.That(Has(moments, CareerMomentKind.TitleDeciderGoal), Is.True);
            Assert.That(Has(moments, CareerMomentKind.DerbyGoal), Is.False);
            Assert.That(Has(moments, CareerMomentKind.BigMatchGoal), Is.False);
        }

        private static void AssertGoalIn(MatchContext context, CareerMomentKind expected, List<Player> eleven, Player player)
        {
            IReadOnlyList<CareerMoment> moments = new MomentRecogniser()
                .Recognise(new JourneyLog(), Us, eleven, Match(Goal(23, player.Id)), context, 1, 1);

            Assert.That(Has(moments, expected), Is.True, expected.ToString());
            Assert.That(Has(moments, CareerMomentKind.BigMatchGoal), Is.False,
                $"{expected} also produced the generic fallback.");
        }

        [Test]
        public void Recognise_TheGoalThatIsHisFirstAndInADerbyAndOnHisDebut_IsEveryOneOfThoseMoments()
        {
            // The GDD's worked example. Each reading is true, so each is kept: a log that recorded only
            // the "best" one would lose the very detail that makes the line worth reading.
            Player player = Striker();
            IReadOnlyList<CareerMoment> moments = new MomentRecogniser()
                .Recognise(new JourneyLog(), Us, new List<Player> { player }, Match(Goal(68, player.Id)), Derby(), 1, 11);

            Assert.That(Has(moments, CareerMomentKind.Debut), Is.True);
            Assert.That(Has(moments, CareerMomentKind.FirstGoal), Is.True);
            Assert.That(Has(moments, CareerMomentKind.DerbyGoal), Is.True);
            Assert.That(Only(moments, CareerMomentKind.FirstGoal).Minute, Is.EqualTo(68));
        }

        // ----- Counting -----------------------------------------------------------------------------------

        [Test]
        public void Recognise_ThreeGoalsInOneMatch_IsAHattrickEvenInAnOrdinaryFixture()
        {
            // A hat-trick is rare enough to carry a match on its own — measured at 0.7% of all moments —
            // so unlike a brace it needs no occasion to be worth remembering.
            Player player = Striker();
            IReadOnlyList<CareerMoment> moments = new MomentRecogniser().Recognise(
                new JourneyLog(), Us, new List<Player> { player },
                Match(Goal(10, player.Id), Goal(40, player.Id), Goal(80, player.Id)), Ordinary(), 1, 1);

            Assert.That(Only(moments, CareerMomentKind.Hattrick).Count, Is.EqualTo(3));
        }

        [Test]
        public void Recognise_TwoGoalsInAnOrdinaryMatch_IsDeliberatelyNoMomentAtAll()
        {
            // Pinning an ABSENCE that was chosen (CONVENTIONS §5). Reading Gate B's output showed a brace
            // was 20.2% of every moment in the game — a good striker scores twice several times a season,
            // so it is a Tuesday, and the strongest career read "scored twice" fourteen times. Nothing in a
            // list feels rare. If a BraceRule ever comes back it must arrive with a condition that makes it
            // rare, and this test is what will notice if it comes back without one.
            Player player = Striker();
            var log = new JourneyLog();
            log.Restore(PlayerJourney.Restore(player.Id, player.Name, appearances: 60, goals: 30, moments: null));

            IReadOnlyList<CareerMoment> moments = new MomentRecogniser().Recognise(
                log, Us, new List<Player> { player },
                Match(Goal(22, player.Id), Goal(64, player.Id)), Ordinary(), 3, 20);

            Assert.That(moments, Is.Empty,
                "An everyday brace is being recorded as a moment again.");
        }

        [Test]
        public void Recognise_TheSameTwoGoalsInADerby_IsStillToldByTheOccasion()
        {
            // The other half: dropping the brace does not lose the match, it stops naming the ordinary one.
            // A fixture worth naming still gets its line, and it carries how many he scored.
            Player player = Striker();
            var log = new JourneyLog();
            log.Restore(PlayerJourney.Restore(player.Id, player.Name, appearances: 60, goals: 30, moments: null));

            IReadOnlyList<CareerMoment> moments = new MomentRecogniser().Recognise(
                log, Us, new List<Player> { player },
                Match(Goal(22, player.Id), Goal(64, player.Id)), Derby(), 3, 20);

            Assert.That(Only(moments, CareerMomentKind.DerbyGoal).Count, Is.EqualTo(2));
        }

        [Test]
        public void Recognise_AMilestoneJumpedRatherThanLandedOn_IsStillRecognised()
        {
            // A brace can take a striker from 9 goals to 11. A milestone that only fired on equality would
            // let him pass his tenth without it ever being a day.
            Player player = Striker();
            var log = new JourneyLog();
            log.Restore(PlayerJourney.Restore(player.Id, player.Name, appearances: 30, goals: 9, moments: null));

            IReadOnlyList<CareerMoment> moments = new MomentRecogniser().Recognise(
                log, Us, new List<Player> { player },
                Match(Goal(12, player.Id), Goal(77, player.Id)), Ordinary(), 2, 5);

            Assert.That(Only(moments, CareerMomentKind.GoalMilestone).Count, Is.EqualTo(10));
        }

        [Test]
        public void Recognise_AnEverydayGoalByAnEstablishedPlayer_IsNoMomentAtAll()
        {
            // The load-bearing negative. If every goal were a moment the log would be a list, and nothing
            // in a list feels rare — which is the whole bet Gate B is asked to settle.
            Player player = Striker();
            var log = new JourneyLog();
            log.Restore(PlayerJourney.Restore(player.Id, player.Name, appearances: 60, goals: 30, moments: null));

            IReadOnlyList<CareerMoment> moments = new MomentRecogniser().Recognise(
                log, Us, new List<Player> { player }, Match(Goal(55, player.Id)), Ordinary(), 3, 20);

            Assert.That(moments, Is.Empty, "An ordinary goal by an established player was recorded as a moment.");
        }

        // ----- The log is input as well as output ---------------------------------------------------------

        [Test]
        public void Recognise_WritesEveryMomentIntoTheJourneyItBelongsTo()
        {
            // The returned list is this week's echo; the journey is the memory the NEXT recognition reads.
            // An earlier draft only did the first, and produced a narrative that announced a debut and
            // then had no record it had ever happened.
            Player player = Striker();
            var log = new JourneyLog();
            IReadOnlyList<CareerMoment> returned = new MomentRecogniser()
                .Recognise(log, Us, new List<Player> { player }, Match(Goal(30, player.Id)), Derby(), 1, 1);

            PlayerJourney journey = log.Find(player.Id);
            Assert.That(journey, Is.Not.Null, "The log is not following a player who just played.");
            Assert.That(journey.Moments.Count, Is.EqualTo(returned.Count));
            Assert.That(journey.Appearances, Is.EqualTo(1));
            Assert.That(journey.Goals, Is.EqualTo(1));
        }

        [Test]
        public void Recognise_AMatchWithNoNamedScorers_ProducesNoGoalMoments()
        {
            // A strength-only match (the harness, a restored fixture) carries no scorer, so nothing
            // happened to anybody. Appearances still count; goals cannot be attributed to a nobody.
            Player player = Striker();
            var match = new MatchResult(Us, Them, 2, 0, 5, 3, new[]
            {
                new MatchEvent(20, TeamSide.Home, MatchEventKind.Goal),
                new MatchEvent(70, TeamSide.Home, MatchEventKind.Goal),
            });

            var log = new JourneyLog();
            IReadOnlyList<CareerMoment> moments = new MomentRecogniser()
                .Recognise(log, Us, new List<Player> { player }, match, Ordinary(), 1, 1);

            Assert.That(Has(moments, CareerMomentKind.FirstGoal), Is.False);
            Assert.That(log.Find(player.Id).Goals, Is.EqualTo(0));
            Assert.That(Has(moments, CareerMomentKind.Debut), Is.True, "He still played, so it was still his debut.");
        }

        [Test]
        public void Recognise_AGoalForTheOtherSide_IsNotCreditedToOurPlayer()
        {
            Player player = Striker();
            var match = new MatchResult(Us, Them, 0, 1, 3, 6, new[]
            {
                new MatchEvent(50, TeamSide.Away, MatchEventKind.Goal, new PlayerId(99)),
            });

            var log = new JourneyLog();
            new MomentRecogniser().Recognise(log, Us, new List<Player> { player }, match, Ordinary(), 1, 1);

            Assert.That(log.Find(player.Id).Goals, Is.EqualTo(0));
        }

        [Test]
        public void Recognise_TheSameMatchReadTwice_ReadsTheSameMoments()
        {
            // No randomness anywhere: a memory that changed when you looked at it twice would not be one.
            Player player = Striker();
            var first = new JourneyLog();
            var second = new JourneyLog();
            MatchResult match = Match(Goal(68, player.Id));

            IReadOnlyList<CareerMoment> a = new MomentRecogniser().Recognise(first, Us, new List<Player> { player }, match, Derby(), 1, 11);
            var copied = new List<CareerMoment>(a);
            IReadOnlyList<CareerMoment> b = new MomentRecogniser().Recognise(second, Us, new List<Player> { player }, match, Derby(), 1, 11);

            Assert.That(b.Count, Is.EqualTo(copied.Count));
            for (int i = 0; i < copied.Count; i++)
            {
                Assert.That(b[i].Kind, Is.EqualTo(copied[i].Kind), $"index {i}");
                Assert.That(b[i].Minute, Is.EqualTo(copied[i].Minute), $"index {i}");
                Assert.That(b[i].Count, Is.EqualTo(copied[i].Count), $"index {i}");
            }
        }

        [Test]
        public void JourneyLog_FollowsOnlyThePlayersItHasBeenAskedAbout()
        {
            // The 50,000-player constraint: a journey each would be memory and save weight spent on
            // careers nobody will ever read.
            var log = new JourneyLog();
            Player played = Striker(1);
            new MomentRecogniser().Recognise(log, Us, new List<Player> { played }, Match(), Ordinary(), 1, 1);

            Assert.That(log.IsFollowing(played.Id), Is.True);
            Assert.That(log.IsFollowing(new PlayerId(4127)), Is.False);
            Assert.That(log.Count, Is.EqualTo(1));
        }
    }
}
