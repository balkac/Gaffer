using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class SquadRenewalTests
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

        private static Player P(int id, PlayerRole role, int age, byte ability = 60)
        {
            return new Player(new PlayerId(id), "P" + id, "England", role, age, Uniform(ability), 75);
        }

        private static Squad SquadOf(params Player[] players)
        {
            return new Squad(new List<Player>(players));
        }

        private static SquadRenewal Renewal()
        {
            return new SquadRenewal(new PlayerGenerator());
        }

        [Test]
        public void Renew_YoungSquad_NobodyRetiresOrJoins()
        {
            Squad squad = SquadOf(P(0, PlayerRole.CentreBack, 22), P(1, PlayerRole.Striker, 24), P(2, PlayerRole.Goalkeeper, 26));
            int nextId = 1000;

            Squad renewed = Renewal().Renew(squad, 99UL, 2, ref nextId);

            Assert.That(renewed.Players.Count, Is.EqualTo(3));
            Assert.That(nextId, Is.EqualTo(1000), "no retirements, so no new ids handed out");
            var ids = new List<int>();
            foreach (Player p in renewed.Players)
            {
                ids.Add(p.Id.Value);
            }

            Assert.That(ids, Is.EquivalentTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void Renew_OutfielderAtHardAge_RetiresAndSameRoleYouthJoins()
        {
            Squad squad = SquadOf(P(0, PlayerRole.CentreBack, 40), P(1, PlayerRole.Striker, 23), P(2, PlayerRole.Goalkeeper, 25));
            int nextId = 1000;

            Squad renewed = Renewal().Renew(squad, 99UL, 2, ref nextId);

            Assert.That(renewed.Players.Count, Is.EqualTo(3), "one in for one out keeps the size fixed");
            Assert.That(HasId(renewed, 0), Is.False, "the 40-year-old retired");

            Player youth = FindFresh(renewed);
            Assert.That(youth, Is.Not.Null);
            Assert.That(youth.Role, Is.EqualTo(PlayerRole.CentreBack), "the intake fills the vacated role");
            Assert.That(youth.Age, Is.LessThanOrEqualTo(18));
        }

        [Test]
        public void Renew_NewYouthGetIdsPastEveryExistingOne()
        {
            Squad squad = SquadOf(P(0, PlayerRole.Striker, 41), P(7, PlayerRole.Striker, 41), P(3, PlayerRole.CentreBack, 22));
            int nextId = 500;

            Squad renewed = Renewal().Renew(squad, 99UL, 2, ref nextId);

            foreach (Player p in renewed.Players)
            {
                if (p.Id.Value >= 500)
                {
                    Assert.That(p.Age, Is.LessThanOrEqualTo(18), "only the new intake carries the fresh ids");
                }
            }

            Assert.That(nextId, Is.EqualTo(502), "two retirees → two fresh ids consumed");
        }

        [Test]
        public void Renew_KeeperOutlastsOutfielder_ByThreshold()
        {
            // A keeper below his own twilight (36) is guaranteed to stay; an outfielder at the outfield hard
            // age (40) is guaranteed to retire — so keepers play to ages that end an outfielder's career.
            Squad keeperSquad = SquadOf(P(0, PlayerRole.Goalkeeper, 35), P(1, PlayerRole.Striker, 22));
            Squad outfieldSquad = SquadOf(P(0, PlayerRole.CentreBack, 40), P(1, PlayerRole.Striker, 22));
            int a = 1000;
            int b = 2000;

            Squad afterKeeper = Renewal().Renew(keeperSquad, 99UL, 2, ref a);
            Squad afterOutfield = Renewal().Renew(outfieldSquad, 99UL, 2, ref b);

            Assert.That(HasId(afterKeeper, 0), Is.True, "a 35-year-old keeper is below twilight and stays");
            Assert.That(HasId(afterOutfield, 0), Is.False, "a 40-year-old outfielder is at the hard age and retires");
        }

        [Test]
        public void Renew_IsDeterministic()
        {
            Squad squad = SquadOf(P(0, PlayerRole.CentreBack, 41), P(1, PlayerRole.Striker, 23), P(2, PlayerRole.LeftWing, 38));
            int idA = 1000;
            int idB = 1000;

            Squad a = Renewal().Renew(squad, 99UL, 2, ref idA);
            Squad b = Renewal().Renew(squad, 99UL, 2, ref idB);

            Assert.That(idA, Is.EqualTo(idB));
            Assert.That(a.Players.Count, Is.EqualTo(b.Players.Count));
            for (int i = 0; i < a.Players.Count; i++)
            {
                Assert.That(a.Players[i].Id.Value, Is.EqualTo(b.Players[i].Id.Value));
                Assert.That(a.Players[i].Age, Is.EqualTo(b.Players[i].Age));
                Assert.That(a.Players[i].Attributes, Is.EqualTo(b.Players[i].Attributes));
            }
        }

        [Test]
        public void Renew_WithGemSeed_IntakeIncludesAHiddenGem()
        {
            // One forced retiree → one intake slot; with the gem seed on, that youth is a hidden gem:
            // low visible ability (no better than an ordinary prospect) but a rare, high ceiling.
            Squad squad = SquadOf(P(0, PlayerRole.Striker, 40, 60), P(1, PlayerRole.CentreBack, 24, 60));
            int nextId = 1000;

            Squad renewed = Renewal().Renew(squad, 99UL, 2, ref nextId, seedGem: true);

            Player youth = FindFresh(renewed);
            Assert.That(youth, Is.Not.Null);
            Assert.That(youth.HiddenPotential, Is.GreaterThanOrEqualTo(86), "the gem carries a rare high ceiling");
            Assert.That(PlayerRatings.ForRole(youth), Is.LessThan(55.0), "but hides behind a low current ability");
        }

        [Test]
        public void Renew_WithoutGemSeed_IntakeIsOrdinary()
        {
            Squad squad = SquadOf(P(0, PlayerRole.Striker, 40, 60), P(1, PlayerRole.CentreBack, 24, 60));
            int nextId = 1000;

            Squad renewed = Renewal().Renew(squad, 99UL, 2, ref nextId, seedGem: false);

            Player youth = FindFresh(renewed);
            Assert.That(youth, Is.Not.Null);
            Assert.That(youth.HiddenPotential, Is.LessThan(86), "an ordinary prospect tops out below gem territory");
        }

        [Test]
        public void Renew_GemSeedButNoVacancy_AddsNoOne()
        {
            // No retirements → no intake slot, so the gem seed has nowhere to land and the squad is unchanged.
            Squad squad = SquadOf(P(0, PlayerRole.Striker, 24, 60), P(1, PlayerRole.CentreBack, 25, 60));
            int nextId = 1000;

            Squad renewed = Renewal().Renew(squad, 99UL, 2, ref nextId, seedGem: true);

            Assert.That(renewed.Players.Count, Is.EqualTo(2));
            Assert.That(nextId, Is.EqualTo(1000));
        }

        [Test]
        public void Renew_LowerRetirementHardAge_RetiresAPlayerTheDefaultWouldKeep()
        {
            // At 33 a centre-back sits exactly on the default twilight, so by default he is certain to stay;
            // drop the hard age below 33 and he must retire. Only the balance differs.
            Squad squad = SquadOf(P(0, PlayerRole.CentreBack, 33, 60), P(1, PlayerRole.Striker, 24, 60));

            int defId = 1000;
            Squad byDefault = new SquadRenewal(new PlayerGenerator()).Renew(squad, 99UL, 2, ref defId);
            Assert.That(HasId(byDefault, 0), Is.True, "default keeps a 33-year-old on the twilight threshold");

            var settings = RenewalSettings.Default;
            settings.OutfielderTwilightAge = 30;
            settings.OutfielderHardAge = 32;
            int cutId = 2000;
            Squad tuned = new SquadRenewal(new PlayerGenerator(), settings).Renew(squad, 99UL, 2, ref cutId);
            Assert.That(HasId(tuned, 0), Is.False, "a lower hard age forces the 33-year-old out");
        }

        [Test]
        public void Renew_HigherGemPotentialSettings_RaiseTheGemCeiling()
        {
            var settings = RenewalSettings.Default;
            settings.GemMinPotential = 95;
            settings.GemMaxPotential = 96;
            Squad squad = SquadOf(P(0, PlayerRole.Striker, 40, 60), P(1, PlayerRole.CentreBack, 24, 60));
            int nextId = 1000;

            Squad renewed = new SquadRenewal(new PlayerGenerator(), settings).Renew(squad, 99UL, 2, ref nextId, seedGem: true);

            Player youth = FindFresh(renewed);
            Assert.That(youth, Is.Not.Null);
            Assert.That(youth.HiddenPotential, Is.GreaterThanOrEqualTo(95));
        }

        private static bool HasId(Squad squad, int id)
        {
            foreach (Player p in squad.Players)
            {
                if (p.Id.Value == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static Player FindFresh(Squad squad)
        {
            foreach (Player p in squad.Players)
            {
                if (p.Id.Value >= 1000)
                {
                    return p;
                }
            }

            return null;
        }
    }
}
