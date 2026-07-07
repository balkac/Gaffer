using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class AttributesTests
    {
        [Test]
        public void Constructor_EachStat_RoundTrips()
        {
            var attributes = new Attributes(1, 2, 3, 4, 5, 6);

            Assert.That(attributes.Pace, Is.EqualTo(1));
            Assert.That(attributes.Finishing, Is.EqualTo(2));
            Assert.That(attributes.Passing, Is.EqualTo(3));
            Assert.That(attributes.Tackling, Is.EqualTo(4));
            Assert.That(attributes.Positioning, Is.EqualTo(5));
            Assert.That(attributes.Stamina, Is.EqualTo(6));
        }

        [Test]
        public void Equality_SameValues_AreEqual()
        {
            var left = new Attributes(70, 65, 72, 40, 68, 80);
            var right = new Attributes(70, 65, 72, 40, 68, 80);

            Assert.That(left, Is.EqualTo(right));
            Assert.That(left == right, Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }

        [Test]
        public void Equality_DifferentStat_AreNotEqual()
        {
            var baseline = new Attributes(70, 65, 72, 40, 68, 80);
            var faster = new Attributes(99, 65, 72, 40, 68, 80);

            Assert.That(baseline, Is.Not.EqualTo(faster));
            Assert.That(baseline != faster, Is.True);
        }
    }
}
