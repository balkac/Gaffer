using System.Collections.Generic;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// A league fixture is not always just another game. Until 2026-08-13 it was: a whole season played on
    /// one <see cref="MatchContext"/>, so nothing was ever an occasion — which left the context-sensitive
    /// traits with no big day to fire on and the narrative layer's "what was this match" input inert. The
    /// Gate B probe made it visible: not one big-match goal in twenty seasons.
    /// </summary>
    public sealed class MatchOccasionTests
    {
        private const int Clubs = 20;
        private const int Rounds = 38;

        private static MatchContext Ordinary()
        {
            return new MatchContext(MatchImportance.Normal, 10_000, isTitleDecider: false, isRivalry: false);
        }

        // A table in club-id order, so place N holds club N-1 — the positions a test wants to talk about.
        private static IReadOnlyList<LeagueTableRow> StandingsInIdOrder()
        {
            var table = new LeagueTable(ClubIds());
            return table.Ordered();
        }

        private static List<ClubId> ClubIds()
        {
            var ids = new List<ClubId>(Clubs);
            for (int i = 0; i < Clubs; i++)
            {
                ids.Add(new ClubId(i));
            }

            return ids;
        }

        private static MatchContextBuilder Builder(RivalryTable rivalries = null)
        {
            return new MatchContextBuilder(rivalries ?? RivalryTable.None, MatchContextSettings.Default);
        }

        // ----- Nothing happens by default ------------------------------------------------------------------

        [Test]
        public void Build_AnOrdinaryMidSeasonFixture_IsLeftExactlyAsItWas()
        {
            MatchContext built = Builder().Build(Ordinary(), new ClubId(5), new ClubId(9), StandingsInIdOrder(), round: 10, roundCount: Rounds);

            Assert.That(built.Importance, Is.EqualTo(MatchImportance.Normal));
            Assert.That(built.IsRivalry, Is.False);
            Assert.That(built.IsTitleDecider, Is.False);
        }

        // ----- The three ways a fixture can matter ---------------------------------------------------------

        [Test]
        public void Build_TwoClubsDrawnAgainstEachOther_IsADerbyInAnyWeekOfTheSeason()
        {
            // The one kind of significance that does not depend on form — and must not, or the fixture
            // would wander from year to year and "his first derby goal" would mean nothing.
            var rivalries = RivalryTable.Draw(Clubs, new SplitMix64RandomNumberGenerator(42UL));
            var one = new ClubId(0);
            ClubId rival = rivalries.RivalOf(one);
            Assert.That(rival.Value, Is.GreaterThanOrEqualTo(0), "The draw left club 0 without a rival.");

            MatchContext built = Builder(rivalries).Build(Ordinary(), one, rival, StandingsInIdOrder(), round: 3, roundCount: Rounds);

            Assert.That(built.Importance, Is.EqualTo(MatchImportance.Derby));
            Assert.That(built.IsRivalry, Is.True);
        }

        [Test]
        public void Build_TwoTitleContendersInTheRunIn_IsADecider()
        {
            MatchContext built = Builder().Build(Ordinary(), new ClubId(0), new ClubId(1), StandingsInIdOrder(), round: Rounds - 2, roundCount: Rounds);

            Assert.That(built.Importance, Is.EqualTo(MatchImportance.Final));
            Assert.That(built.IsTitleDecider, Is.True);
        }

        [Test]
        public void Build_TheSameTwoContendersInSeptember_IsNotADeciderYet()
        {
            // Two clubs top of the table in September are not deciding anything; they are playing in
            // September. The run-in is what turns a position into a stake.
            MatchContext built = Builder().Build(Ordinary(), new ClubId(0), new ClubId(1), StandingsInIdOrder(), round: 2, roundCount: Rounds);

            Assert.That(built.IsTitleDecider, Is.False);
            Assert.That(built.Importance, Is.EqualTo(MatchImportance.Normal));
        }

        [Test]
        public void Build_TwoClubsInTheDropZoneInTheRunIn_IsASixPointer()
        {
            MatchContext built = Builder().Build(Ordinary(), new ClubId(Clubs - 1), new ClubId(Clubs - 2), StandingsInIdOrder(), round: Rounds - 1, roundCount: Rounds);

            Assert.That(built.Importance, Is.EqualTo(MatchImportance.RelegationSixPointer));
        }

        [Test]
        public void Build_AContenderAgainstAClubWithNothingAtStake_IsNotAnOccasion()
        {
            // Both sides have to want the same thing. One club chasing a title against a mid-table side
            // playing out the season is not a decider.
            MatchContext built = Builder().Build(Ordinary(), new ClubId(0), new ClubId(10), StandingsInIdOrder(), round: Rounds - 1, roundCount: Rounds);

            Assert.That(built.IsTitleDecider, Is.False);
            Assert.That(built.Importance, Is.EqualTo(MatchImportance.Normal));
        }

        // ----- Composition -------------------------------------------------------------------------------

        [Test]
        public void Build_ADerbyBetweenTwoContendersInMay_IsBothButCarriesTheBiggerImportance()
        {
            // The flags compose; the importance cannot. A match is one occasion, so the strongest wins the
            // label while the rivalry stays true underneath it.
            var rivalries = RivalryTable.Restore(BuildPairing(0, 1));
            MatchContext built = Builder(rivalries).Build(Ordinary(), new ClubId(0), new ClubId(1), StandingsInIdOrder(), round: Rounds - 1, roundCount: Rounds);

            Assert.That(built.IsRivalry, Is.True);
            Assert.That(built.IsTitleDecider, Is.True);
            Assert.That(built.Importance, Is.EqualTo(MatchImportance.Final));
        }

        [Test]
        public void Build_AContextThatArrivedRaised_IsNeverLowered()
        {
            var cupFinal = new MatchContext(MatchImportance.Final, 60_000, isTitleDecider: true, isRivalry: false);
            var rivalries = RivalryTable.Restore(BuildPairing(0, 1));

            MatchContext built = Builder(rivalries).Build(cupFinal, new ClubId(0), new ClubId(1), StandingsInIdOrder(), round: 3, roundCount: Rounds);

            Assert.That(built.Importance, Is.EqualTo(MatchImportance.Final));
            Assert.That(built.IsTitleDecider, Is.True);
            Assert.That(built.IsRivalry, Is.True);
        }

        // ----- The rivalry draw --------------------------------------------------------------------------

        [Test]
        public void Draw_PairsClubsMutuallyAndNeverWithThemselves()
        {
            var rivalries = RivalryTable.Draw(Clubs, new SplitMix64RandomNumberGenerator(7UL));

            for (int i = 0; i < Clubs; i++)
            {
                var club = new ClubId(i);
                ClubId rival = rivalries.RivalOf(club);
                Assert.That(rival, Is.Not.EqualTo(club), $"Club {i} is its own rival.");
                Assert.That(rivalries.RivalOf(rival), Is.EqualTo(club), $"Club {i}'s rivalry is not mutual.");
                Assert.That(rivalries.AreRivals(club, rival), Is.True);
            }
        }

        [Test]
        public void Draw_TheSameSeed_DrawsTheSameMap()
        {
            var first = RivalryTable.Draw(Clubs, new SplitMix64RandomNumberGenerator(99UL));
            var second = RivalryTable.Draw(Clubs, new SplitMix64RandomNumberGenerator(99UL));

            for (int i = 0; i < Clubs; i++)
            {
                Assert.That(second.RivalOf(new ClubId(i)), Is.EqualTo(first.RivalOf(new ClubId(i))), $"club {i}");
            }
        }

        [Test]
        public void Draw_AnOddLeague_LeavesExactlyOneClubWithoutARival()
        {
            var rivalries = RivalryTable.Draw(7, new SplitMix64RandomNumberGenerator(3UL));

            int without = 0;
            for (int i = 0; i < 7; i++)
            {
                if (rivalries.RivalOf(new ClubId(i)).Value < 0)
                {
                    without++;
                }
            }

            Assert.That(without, Is.EqualTo(1), "Somebody always is, but only one.");
        }

        private static int[] BuildPairing(int one, int other)
        {
            var rivalOf = new int[Clubs];
            for (int i = 0; i < Clubs; i++)
            {
                rivalOf[i] = -1;
            }

            rivalOf[one] = other;
            rivalOf[other] = one;
            return rivalOf;
        }
    }
}
