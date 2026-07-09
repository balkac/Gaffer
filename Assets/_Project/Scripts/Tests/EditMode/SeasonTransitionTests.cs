using System.Collections.Generic;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class SeasonTransitionTests
    {
        private static Attributes Uniform(byte stat)
        {
            return new Attributes
            {
                Finishing = stat, Technique = stat, FirstTouch = stat, Dribbling = stat, Passing = stat,
                Crossing = stat, Heading = stat, LongShots = stat, Marking = stat, Tackling = stat,
                Penalties = stat, FreeKicks = stat, Corners = stat, LongThrows = stat,
                Pace = stat, Acceleration = stat, Stamina = stat, Strength = stat, Agility = stat,
                Jumping = stat, Balance = stat, Positioning = stat,
                Reflexes = stat, Handling = stat, AerialReach = stat, CommandOfArea = stat,
                OneOnOnes = stat, Kicking = stat, GkPositioning = stat,
            };
        }

        private static Player P(int id, PlayerRole role, int age, byte ability, byte potential)
        {
            return new Player(new PlayerId(id), "P" + id, "England", role, age, Uniform(ability), potential);
        }

        // A believable little roster spread across the lines, so the strength builder has each axis manned.
        private static Squad YoungSquad(byte ability, byte potential)
        {
            return new Squad(new List<Player>
            {
                P(0, PlayerRole.Goalkeeper, 18, ability, potential),
                P(1, PlayerRole.RightBack, 18, ability, potential),
                P(2, PlayerRole.CentreBack, 18, ability, potential),
                P(3, PlayerRole.LeftBack, 18, ability, potential),
                P(4, PlayerRole.CentralMidfield, 18, ability, potential),
                P(5, PlayerRole.CentralMidfield, 18, ability, potential),
                P(6, PlayerRole.AttackingMidfield, 18, ability, potential),
                P(7, PlayerRole.RightWing, 18, ability, potential),
                P(8, PlayerRole.LeftWing, 18, ability, potential),
                P(9, PlayerRole.Striker, 18, ability, potential),
                P(10, PlayerRole.Striker, 18, ability, potential),
            });
        }

        private static League LeagueWith(params Club[] clubs)
        {
            return new League("Test League", new List<Club>(clubs));
        }

        private static Club ClubWithSquad(int id, Squad squad)
        {
            return new Club(new ClubId(id), "Club " + id, squad, new EffectiveStrengthBuilder().Build(squad));
        }

        [Test]
        public void ToNextSeason_AgesEveryPlayerByOneYear()
        {
            League league = LeagueWith(ClubWithSquad(0, YoungSquad(50, 85)));

            League next = new SeasonTransition().ToNextSeason(league, 1234UL, 2);

            IReadOnlyList<Player> before = league.Clubs[0].Squad.Players;
            IReadOnlyList<Player> after = next.Clubs[0].Squad.Players;
            Assert.That(after.Count, Is.EqualTo(before.Count));
            for (int i = 0; i < after.Count; i++)
            {
                Assert.That(after[i].Age, Is.EqualTo(before[i].Age + 1));
                Assert.That(after[i].Id, Is.EqualTo(before[i].Id));
            }
        }

        [Test]
        public void ToNextSeason_YoungSquadBelowPotential_LiftsStrength()
        {
            League league = LeagueWith(ClubWithSquad(0, YoungSquad(50, 85)));

            League next = new SeasonTransition().ToNextSeason(league, 1234UL, 2);

            TeamStrength before = league.Clubs[0].Strength;
            TeamStrength after = next.Clubs[0].Strength;
            Assert.That(after.Attack, Is.GreaterThan(before.Attack));
            Assert.That(after.Midfield, Is.GreaterThan(before.Midfield));
            Assert.That(after.Defence, Is.GreaterThan(before.Defence));
        }

        [Test]
        public void ToNextSeason_RecomputesStrengthFromTheDevelopedSquad()
        {
            League league = LeagueWith(ClubWithSquad(0, YoungSquad(50, 85)));

            League next = new SeasonTransition().ToNextSeason(league, 1234UL, 2);

            Club club = next.Clubs[0];
            TeamStrength expected = new EffectiveStrengthBuilder().Build(club.Squad);
            Assert.That(club.Strength.Attack, Is.EqualTo(expected.Attack).Within(1e-9));
            Assert.That(club.Strength.Midfield, Is.EqualTo(expected.Midfield).Within(1e-9));
            Assert.That(club.Strength.Defence, Is.EqualTo(expected.Defence).Within(1e-9));
        }

        [Test]
        public void ToNextSeason_IsDeterministic()
        {
            League league = LeagueWith(ClubWithSquad(0, YoungSquad(50, 85)));
            var transition = new SeasonTransition();

            League a = transition.ToNextSeason(league, 1234UL, 2);
            League b = transition.ToNextSeason(league, 1234UL, 2);

            IReadOnlyList<Player> pa = a.Clubs[0].Squad.Players;
            IReadOnlyList<Player> pb = b.Clubs[0].Squad.Players;
            for (int i = 0; i < pa.Count; i++)
            {
                Assert.That(pa[i].Attributes, Is.EqualTo(pb[i].Attributes));
                Assert.That(pa[i].Age, Is.EqualTo(pb[i].Age));
            }
        }

        [Test]
        public void ToNextSeason_DifferentSeasonNumber_DevelopsDifferently()
        {
            League league = LeagueWith(ClubWithSquad(0, YoungSquad(50, 85)));
            var transition = new SeasonTransition();

            League s2 = transition.ToNextSeason(league, 1234UL, 2);
            League s3 = transition.ToNextSeason(league, 1234UL, 3);

            // Same start, different season index → the per-player rng differs, so at least one player's
            // developed attributes come out different (any single player can coincide after integer rounding).
            IReadOnlyList<Player> a = s2.Clubs[0].Squad.Players;
            IReadOnlyList<Player> b = s3.Clubs[0].Squad.Players;
            bool anyDifferent = false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Attributes != b[i].Attributes)
                {
                    anyDifferent = true;
                    break;
                }
            }

            Assert.That(anyDifferent, Is.True);
        }

        [Test]
        public void ToNextSeason_SquadlessClub_PassesThroughUnchanged()
        {
            var strengthOnly = new Club(new ClubId(5), "Strength Only", new TeamStrength(60, 60, 60));
            League league = LeagueWith(strengthOnly);

            League next = new SeasonTransition().ToNextSeason(league, 1234UL, 2);

            Club club = next.Clubs[0];
            Assert.That(club.Squad, Is.Null);
            Assert.That(club.Name, Is.EqualTo("Strength Only"));
            Assert.That(club.Strength.Attack, Is.EqualTo(60.0).Within(1e-9));
        }
    }
}
