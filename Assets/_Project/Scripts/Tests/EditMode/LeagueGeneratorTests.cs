using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class LeagueGeneratorTests
    {
        private static LeagueGenerator Generator()
        {
            return new LeagueGenerator(new SquadGenerator(new PlayerGenerator()));
        }

        private static IRandom Rng(ulong seed)
        {
            return new SplitMix64RandomNumberGenerator(seed);
        }

        private static double Overall(TeamStrength s)
        {
            return s.Attack + s.Midfield + s.Defence;
        }

        [Test]
        public void Generate_ProducesRequestedClubsEachWithAFullSquad()
        {
            League league = Generator().Generate(20, Rng(1));

            Assert.That(league.Clubs.Count, Is.EqualTo(20));
            Assert.That(league.Name, Is.Not.Null.And.Not.Empty);
            foreach (Club club in league.Clubs)
            {
                Assert.That(club.Name, Is.Not.Null.And.Not.Empty);
                Assert.That(club.Squad, Is.Not.Null);
                Assert.That(club.Squad.Players.Count, Is.EqualTo(SquadGenerator.SquadSize));
            }
        }

        [Test]
        public void Generate_ClubNamesAreDistinct()
        {
            League league = Generator().Generate(20, Rng(5));

            var names = new HashSet<string>();
            foreach (Club club in league.Clubs)
            {
                names.Add(club.Name);
            }

            Assert.That(names.Count, Is.EqualTo(20));
        }

        [Test]
        public void Generate_PlayerIdsAreUniqueAcrossClubs()
        {
            League league = Generator().Generate(20, Rng(2));

            var ids = new HashSet<int>();
            foreach (Club club in league.Clubs)
            {
                foreach (Player player in club.Squad.Players)
                {
                    Assert.That(ids.Add(player.Id.Value), Is.True, "player id " + player.Id.Value + " collided");
                }
            }
        }

        [Test]
        public void Generate_TopClubIsStrongerThanBottomClub()
        {
            League league = Generator().Generate(20, Rng(9));

            Assert.That(Overall(league.Clubs[0].Strength), Is.GreaterThan(Overall(league.Clubs[19].Strength)));
        }

        [Test]
        public void Generate_SameSeed_IsDeterministic()
        {
            League a = Generator().Generate(10, Rng(123));
            League b = Generator().Generate(10, Rng(123));

            Assert.That(a.Name, Is.EqualTo(b.Name));
            for (int i = 0; i < a.Clubs.Count; i++)
            {
                Assert.That(a.Clubs[i].Name, Is.EqualTo(b.Clubs[i].Name));
                Assert.That(a.Clubs[i].Squad.Players[0].Attributes, Is.EqualTo(b.Clubs[i].Squad.Players[0].Attributes));
            }
        }
    }
}
