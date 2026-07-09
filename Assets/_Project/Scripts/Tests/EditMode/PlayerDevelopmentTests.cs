using Gaffer.Application.Progression;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class PlayerDevelopmentTests
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

        private static Player Player(PlayerRole role, int age, byte ability, byte potential)
        {
            return new Player(new PlayerId(1), "Test", "England", role, age, Uniform(ability), potential);
        }

        private static IRandom Rng(ulong seed)
        {
            return new SplitMix64RandomNumberGenerator(seed);
        }

        [Test]
        public void Develop_AgesThePlayerByOneSeason()
        {
            var dev = new PlayerDevelopment();
            Player before = Player(PlayerRole.CentralMidfield, 20, 55, 80);

            Player after = dev.Develop(before, Rng(1));

            Assert.That(after.Age, Is.EqualTo(21));
            Assert.That(after.Id, Is.EqualTo(before.Id));
            Assert.That(after.HiddenPotential, Is.EqualTo(before.HiddenPotential));
        }

        [Test]
        public void Develop_YoungGemBelowPotential_RaisesRoleRatingOneSeason()
        {
            var dev = new PlayerDevelopment();
            Player before = Player(PlayerRole.Striker, 18, 50, 85);

            Player after = dev.Develop(before, Rng(7));

            Assert.That(PlayerRatings.ForRole(after), Is.GreaterThan(PlayerRatings.ForRole(before)));
        }

        [Test]
        public void Develop_YoungGemOverManySeasons_ClimbsTowardPotentialWithoutExceedingIt()
        {
            var dev = new PlayerDevelopment();
            const byte potential = 85;
            Player player = Player(PlayerRole.Striker, 17, 50, potential);
            double start = PlayerRatings.ForRole(player);
            IRandom rng = Rng(42);

            for (int season = 0; season < 10; season++)
            {
                player = dev.Develop(player, rng);
            }

            double end = PlayerRatings.ForRole(player);
            Assert.That(end, Is.GreaterThan(start + 12.0), "a young gem should grow substantially over a decade");
            Assert.That(end, Is.LessThanOrEqualTo(potential + 1.0), "growth must not run past the potential ceiling");
        }

        [Test]
        public void Develop_PlayerAtCeiling_StaysFlat()
        {
            var dev = new PlayerDevelopment();
            // Already at his potential (ability == ceiling) and in his prime — nothing left to realise, no
            // decline yet, so the rating holds.
            Player before = Player(PlayerRole.CentralMidfield, 27, 78, 78);

            Player after = dev.Develop(before, Rng(3));

            Assert.That(PlayerRatings.ForRole(after), Is.EqualTo(PlayerRatings.ForRole(before)).Within(1e-9));
        }

        [Test]
        public void Develop_PlayerPast25BelowPotential_StillGrows()
        {
            var dev = new PlayerDevelopment();
            // A 26-year-old well short of his ceiling still develops (CM 01/02: 25+ is not a hard wall), just
            // far slower than a teenager. The gap here is large enough that growth lands regardless of seed.
            Player before = Player(PlayerRole.CentralMidfield, 26, 50, 88);

            Player after = dev.Develop(before, Rng(3));

            Assert.That(PlayerRatings.ForRole(after), Is.GreaterThan(PlayerRatings.ForRole(before)));
        }

        [Test]
        public void Develop_Veteran_LowersRoleRating()
        {
            var dev = new PlayerDevelopment();
            Player before = Player(PlayerRole.RightWing, 34, 75, 80);

            Player after = dev.Develop(before, Rng(9));

            Assert.That(PlayerRatings.ForRole(after), Is.LessThan(PlayerRatings.ForRole(before)));
        }

        [Test]
        public void Develop_KeeperAtThirty_DoesNotDeclineYet()
        {
            var dev = new PlayerDevelopment();
            // Not every player declines at 30 (CM 01/02): a keeper peaks late, so at 30 his rating holds —
            // no growth left, no decline yet.
            Player before = Player(PlayerRole.Goalkeeper, 30, 75, 78);

            Player after = dev.Develop(before, Rng(4));

            Assert.That(PlayerRatings.ForRole(after), Is.EqualTo(PlayerRatings.ForRole(before)).Within(1e-9));
        }

        [Test]
        public void Develop_VeteranPositionalPlayerOverSeasons_VisiblyDeclines()
        {
            var dev = new PlayerDevelopment();
            // A centre-back's rating uses none of the raw athletic attributes, so age must still erode his
            // general ability — otherwise a 38-year-old reads identical to his prime. Well past any peak (a
            // centre-back's onset lands by ~35 even with the latest offset), several seasons move the OVR
            // clearly, not by a rounding wobble.
            Player player = Player(PlayerRole.CentreBack, 36, 78, 80);
            double start = PlayerRatings.ForRole(player);
            IRandom rng = Rng(5);

            for (int season = 0; season < 3; season++)
            {
                player = dev.Develop(player, rng);
            }

            Assert.That(PlayerRatings.ForRole(player), Is.LessThan(start - 2.0));
        }

        [Test]
        public void Develop_Veteran_PaceBoundRoleDeclinesMoreThanPositionalRole()
        {
            var dev = new PlayerDevelopment();
            // Same attribute sheet and seed, so the physical decline is identical — only the role differs.
            Player winger = Player(PlayerRole.RightWing, 34, 75, 80);
            Player centreBack = Player(PlayerRole.CentreBack, 34, 75, 80);

            double wingDrop = PlayerRatings.ForRole(winger) - PlayerRatings.ForRole(dev.Develop(winger, Rng(11)));
            double backDrop = PlayerRatings.ForRole(centreBack) - PlayerRatings.ForRole(dev.Develop(centreBack, Rng(11)));

            Assert.That(wingDrop, Is.GreaterThan(backDrop));
        }

        [Test]
        public void Develop_SameSeed_IsDeterministic()
        {
            var dev = new PlayerDevelopment();
            Player before = Player(PlayerRole.LeftBack, 19, 48, 82);

            Player a = dev.Develop(before, Rng(123));
            Player b = dev.Develop(before, Rng(123));

            Assert.That(a.Attributes, Is.EqualTo(b.Attributes));
            Assert.That(a.Age, Is.EqualTo(b.Age));
        }
    }
}
