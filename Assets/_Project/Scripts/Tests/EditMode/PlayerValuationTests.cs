using Gaffer.Application.Transfers;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class PlayerValuationTests
    {
        private static Player Forward(byte level, int age, byte potential = 70)
        {
            var attributes = new Attributes
            {
                Finishing = level, Pace = level, Technique = level, Positioning = level, Dribbling = level,
            };
            return new Player(new PlayerId(1), "Test Forward", "England", Position.Forward, age, attributes, potential);
        }

        [Test]
        public void Value_HigherAbility_IsWorthMore()
        {
            long weak = PlayerValuation.Value(Forward(50, 25));
            long strong = PlayerValuation.Value(Forward(85, 25));

            Assert.That(strong, Is.GreaterThan(weak));
        }

        [Test]
        public void Value_OlderAtSameAbility_IsWorthLess()
        {
            long prime = PlayerValuation.Value(Forward(75, 25));
            long veteran = PlayerValuation.Value(Forward(75, 34));

            Assert.That(veteran, Is.LessThan(prime));
        }

        [Test]
        public void Value_DoesNotPriceInHiddenPotential()
        {
            // Two identical players but different hidden ceilings value the same — the market cannot see it.
            long lowCeiling = PlayerValuation.Value(Forward(55, 18, potential: 60));
            long highCeiling = PlayerValuation.Value(Forward(55, 18, potential: 95));

            Assert.That(highCeiling, Is.EqualTo(lowCeiling));
        }

        [Test]
        public void Value_IsNonNegativeAndRounded()
        {
            long value = PlayerValuation.Value(Forward(40, 30));

            Assert.That(value, Is.GreaterThanOrEqualTo(0));
            Assert.That(value % 50_000, Is.EqualTo(0));
        }

        [Test]
        public void Value_PrimeStar_OutvaluesAJourneyman()
        {
            long star = PlayerValuation.Value(Forward(88, 26));
            long journeyman = PlayerValuation.Value(Forward(58, 29));

            Assert.That(star, Is.GreaterThan(journeyman * 3));
        }
    }
}
