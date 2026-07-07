using System.Collections.Generic;
using Gaffer.Application.Season;
using Gaffer.Domain.Clubs;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class FixtureSchedulerTests
    {
        private static IReadOnlyList<ClubId> CreateClubs(int count)
        {
            var clubs = new List<ClubId>(count);
            for (int i = 0; i < count; i++)
            {
                clubs.Add(new ClubId(i));
            }

            return clubs;
        }

        [Test]
        public void CreateDoubleRoundRobin_ForEvenLeague_HasEveryOrderedPairOnce()
        {
            IReadOnlyList<ClubId> clubs = CreateClubs(20);

            IReadOnlyList<Fixture> fixtures = new FixtureScheduler().CreateDoubleRoundRobin(clubs);

            Assert.That(fixtures.Count, Is.EqualTo(20 * 19));
            var seen = new HashSet<(int, int)>();
            foreach (Fixture fixture in fixtures)
            {
                bool added = seen.Add((fixture.Home.Value, fixture.Away.Value));
                Assert.That(added, Is.True, $"Duplicate fixture {fixture.Home.Value} v {fixture.Away.Value}.");
                Assert.That(fixture.Home.Value, Is.Not.EqualTo(fixture.Away.Value));
            }
        }

        [Test]
        public void CreateDoubleRoundRobin_EachRound_HasEveryClubExactlyOnce()
        {
            IReadOnlyList<ClubId> clubs = CreateClubs(20);

            IReadOnlyList<Fixture> fixtures = new FixtureScheduler().CreateDoubleRoundRobin(clubs);

            var byRound = new Dictionary<int, HashSet<int>>();
            foreach (Fixture fixture in fixtures)
            {
                if (!byRound.TryGetValue(fixture.Round, out HashSet<int> clubsThisRound))
                {
                    clubsThisRound = new HashSet<int>();
                    byRound[fixture.Round] = clubsThisRound;
                }

                Assert.That(clubsThisRound.Add(fixture.Home.Value), Is.True);
                Assert.That(clubsThisRound.Add(fixture.Away.Value), Is.True);
            }

            Assert.That(byRound.Count, Is.EqualTo(2 * (20 - 1)));
            foreach (HashSet<int> clubsThisRound in byRound.Values)
            {
                Assert.That(clubsThisRound.Count, Is.EqualTo(20));
            }
        }

        [Test]
        public void CreateDoubleRoundRobin_ForOddLeague_GivesEachClubTwoMatchesPerOpponent()
        {
            IReadOnlyList<ClubId> clubs = CreateClubs(5);

            IReadOnlyList<Fixture> fixtures = new FixtureScheduler().CreateDoubleRoundRobin(clubs);

            Assert.That(fixtures.Count, Is.EqualTo(5 * 4));
            var appearances = new Dictionary<int, int>();
            foreach (Fixture fixture in fixtures)
            {
                appearances[fixture.Home.Value] = appearances.GetValueOrDefault(fixture.Home.Value) + 1;
                appearances[fixture.Away.Value] = appearances.GetValueOrDefault(fixture.Away.Value) + 1;
            }

            foreach (int played in appearances.Values)
            {
                Assert.That(played, Is.EqualTo(2 * (5 - 1)));
            }
        }
    }
}
