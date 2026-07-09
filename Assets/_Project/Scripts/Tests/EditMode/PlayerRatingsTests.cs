using Gaffer.Application.Simulation;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class PlayerRatingsTests
    {
        // Every attribute at the same value, so any role rating (weights sum to 1) collapses to that value —
        // the baseline the specialist tests deviate from on a few attributes at a time.
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

        [Test]
        public void ForRole_UniformAttributes_CollapsesToThatValue()
        {
            Attributes a = Uniform(60);

            // Weights sum to 1 for every role, so a flat sheet reads 60 whichever role scores it.
            Assert.That(PlayerRatings.ForRole(PlayerRole.Striker, a), Is.EqualTo(60.0).Within(1e-9));
            Assert.That(PlayerRatings.ForRole(PlayerRole.CentreBack, a), Is.EqualTo(60.0).Within(1e-9));
            Assert.That(PlayerRatings.ForRole(PlayerRole.RightBack, a), Is.EqualTo(60.0).Within(1e-9));
            Assert.That(PlayerRatings.ForRole(PlayerRole.Goalkeeper, a), Is.EqualTo(60.0).Within(1e-9));
        }

        [Test]
        public void ForRole_PaceAndCrossingSpecialist_RatesHigherAtFullBackThanCentreBack()
        {
            // A modern full-back: quick, whips a cross in, but no aerial stopper — weak heading/strength.
            Attributes a = Uniform(45);
            a.Pace = 90;
            a.Crossing = 90;
            a.Stamina = 85;

            double asFullBack = PlayerRatings.ForRole(PlayerRole.RightBack, a);
            double asCentreBack = PlayerRatings.ForRole(PlayerRole.CentreBack, a);

            Assert.That(asFullBack, Is.GreaterThan(asCentreBack));
        }

        [Test]
        public void ForRole_AerialStopper_RatesHigherAtCentreBackThanFullBack()
        {
            // A commanding centre-back: dominant in the air and the tackle, but slow with no crossing.
            Attributes a = Uniform(45);
            a.Heading = 90;
            a.Marking = 88;
            a.Tackling = 88;
            a.Strength = 85;

            double asCentreBack = PlayerRatings.ForRole(PlayerRole.CentreBack, a);
            double asFullBack = PlayerRatings.ForRole(PlayerRole.RightBack, a);

            Assert.That(asCentreBack, Is.GreaterThan(asFullBack));
        }

        [Test]
        public void ForRole_DribblingRunner_RatesHigherAtWingThanStriker()
        {
            // A flying winger: pace and dribbling, but not a clinical finisher — the striker role leans on
            // finishing he lacks, so he is worth more out wide than through the middle.
            Attributes a = Uniform(45);
            a.Pace = 90;
            a.Dribbling = 90;
            a.Finishing = 40;

            double asWing = PlayerRatings.ForRole(PlayerRole.RightWing, a);
            double asStriker = PlayerRatings.ForRole(PlayerRole.Striker, a);

            Assert.That(asWing, Is.GreaterThan(asStriker));
        }

        [Test]
        public void ForRole_PlayerOverload_MatchesRoleAndAttributes()
        {
            Attributes a = Uniform(50);
            a.Finishing = 88;
            var player = new Player(new PlayerId(1), "Test", "England", PlayerRole.Striker, 24, a, 80);

            Assert.That(PlayerRatings.ForRole(player), Is.EqualTo(PlayerRatings.ForRole(PlayerRole.Striker, a)).Within(1e-9));
        }
    }
}
