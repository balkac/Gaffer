using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class AttributesTests
    {
        [Test]
        public void Initializer_EachGroup_RoundTrips()
        {
            var attributes = new Attributes
            {
                Finishing = 71,
                Tackling = 42,
                Passing = 63,
                Pace = 80,
                Positioning = 66,
                Stamina = 74,
                Reflexes = 55,
                GkPositioning = 58,
            };

            Assert.That(attributes.Finishing, Is.EqualTo(71));
            Assert.That(attributes.Tackling, Is.EqualTo(42));
            Assert.That(attributes.Passing, Is.EqualTo(63));
            Assert.That(attributes.Pace, Is.EqualTo(80));
            Assert.That(attributes.Positioning, Is.EqualTo(66));
            Assert.That(attributes.Stamina, Is.EqualTo(74));
            Assert.That(attributes.Reflexes, Is.EqualTo(55));
            Assert.That(attributes.GkPositioning, Is.EqualTo(58));
        }

        [Test]
        public void UnsetFields_DefaultToZero()
        {
            var attributes = new Attributes { Finishing = 90 };

            // A striker's goalkeeping stats sit at zero unless set — the outfield/keeper split.
            Assert.That(attributes.Reflexes, Is.EqualTo(0));
            Assert.That(attributes.Handling, Is.EqualTo(0));
            Assert.That(attributes.OneOnOnes, Is.EqualTo(0));
        }

        [Test]
        public void Equality_SameValues_AreEqual()
        {
            Attributes left = Sample();
            Attributes right = Sample();

            Assert.That(left, Is.EqualTo(right));
            Assert.That(left == right, Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }

        [Test]
        public void Equality_DifferentStat_AreNotEqual()
        {
            Attributes baseline = Sample();
            Attributes faster = Sample();
            faster.Pace = 99;

            Assert.That(baseline, Is.Not.EqualTo(faster));
            Assert.That(baseline != faster, Is.True);
        }

        [Test]
        public void Equality_DifferentGoalkeepingStat_AreNotEqual()
        {
            Attributes baseline = Sample();
            Attributes sharper = Sample();
            sharper.Reflexes = 88;

            Assert.That(baseline, Is.Not.EqualTo(sharper));
        }

        private static Attributes Sample()
        {
            return new Attributes
            {
                Finishing = 70,
                Technique = 65,
                Passing = 72,
                Tackling = 40,
                Marking = 44,
                Positioning = 68,
                Pace = 77,
                Stamina = 80,
                Strength = 61,
                Reflexes = 30,
            };
        }
    }
}
