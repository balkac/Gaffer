using Gaffer.Common;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class SplitMix64RandomNumberGeneratorTests
    {
        [Test]
        public void NextUInt64_Seed0_MatchesReferenceStream()
        {
            // Golden values from the canonical SplitMix64 reference (seed 0), so a refactor that
            // silently changes the algorithm fails loudly.
            var rng = new SplitMix64RandomNumberGenerator(0);

            Assert.That(rng.NextUInt64(), Is.EqualTo(0xE220A8397B1DCDAFUL));
            Assert.That(rng.NextUInt64(), Is.EqualTo(0x6E789E6AA1B965F4UL));
            Assert.That(rng.NextUInt64(), Is.EqualTo(0x06C45D188009454FUL));
        }

        [Test]
        public void NextUInt64_Seed42_MatchesReferenceStream()
        {
            var rng = new SplitMix64RandomNumberGenerator(42);

            Assert.That(rng.NextUInt64(), Is.EqualTo(0xBDD732262FEB6E95UL));
            Assert.That(rng.NextUInt64(), Is.EqualTo(0x28EFE333B266F103UL));
            Assert.That(rng.NextUInt64(), Is.EqualTo(0x47526757130F9F52UL));
        }

        [Test]
        public void NextUInt64_SameSeed_IsDeterministic()
        {
            var first = new SplitMix64RandomNumberGenerator(12345);
            var second = new SplitMix64RandomNumberGenerator(12345);

            for (int i = 0; i < 1000; i++)
            {
                Assert.That(first.NextUInt64(), Is.EqualTo(second.NextUInt64()));
            }
        }

        [Test]
        public void NextUInt64_DifferentSeeds_Diverge()
        {
            var first = new SplitMix64RandomNumberGenerator(1);
            var second = new SplitMix64RandomNumberGenerator(2);

            Assert.That(first.NextUInt64(), Is.Not.EqualTo(second.NextUInt64()));
        }

        [Test]
        public void NextInt_WithUpperBound_StaysInRange()
        {
            var rng = new SplitMix64RandomNumberGenerator(7);

            for (int i = 0; i < 10000; i++)
            {
                int value = rng.NextInt(6);
                Assert.That(value, Is.InRange(0, 5));
            }
        }

        [Test]
        public void NextInt_WithBounds_StaysInRange()
        {
            var rng = new SplitMix64RandomNumberGenerator(7);

            for (int i = 0; i < 10000; i++)
            {
                int value = rng.NextInt(-3, 4);
                Assert.That(value, Is.InRange(-3, 3));
            }
        }

        [Test]
        public void NextInt_MinNotBelowMax_Throws()
        {
            var rng = new SplitMix64RandomNumberGenerator(0);

            Assert.That(() => rng.NextInt(5, 5), Throws.ArgumentException);
        }

        [Test]
        public void NextDouble_OverManyDraws_StaysInUnitInterval()
        {
            var rng = new SplitMix64RandomNumberGenerator(99);

            for (int i = 0; i < 10000; i++)
            {
                double value = rng.NextDouble();
                Assert.That(value, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(value, Is.LessThan(1.0));
            }
        }
    }
}
