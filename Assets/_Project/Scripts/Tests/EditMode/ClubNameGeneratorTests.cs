using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Common;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class ClubNameGeneratorTests
    {
        private static IRandom Rng(ulong seed)
        {
            return new SplitMix64RandomNumberGenerator(seed);
        }

        [Test]
        public void GenerateClubName_SameSeed_IsDeterministic()
        {
            var gen = new ClubNameGenerator();

            Assert.That(gen.GenerateClubName(Rng(42)), Is.EqualTo(gen.GenerateClubName(Rng(42))));
        }

        [Test]
        public void GenerateClubName_IsNonEmptyAndTrimmed()
        {
            var gen = new ClubNameGenerator();
            IRandom rng = Rng(1);

            for (int i = 0; i < 50; i++)
            {
                string name = gen.GenerateClubName(rng);
                Assert.That(name, Is.Not.Null.And.Not.Empty);
                Assert.That(name, Is.EqualTo(name.Trim()), "no leading or trailing whitespace");
            }
        }

        [Test]
        public void GenerateDistinct_ReturnsExactlyCountUniqueNames()
        {
            var gen = new ClubNameGenerator();

            IReadOnlyList<string> names = gen.GenerateDistinct(24, Rng(7));

            Assert.That(names.Count, Is.EqualTo(24));
            Assert.That(new HashSet<string>(names).Count, Is.EqualTo(24), "every name is distinct");
        }

        [Test]
        public void GenerateDistinct_SameSeed_IsDeterministic()
        {
            var gen = new ClubNameGenerator();

            IReadOnlyList<string> a = gen.GenerateDistinct(20, Rng(99));
            IReadOnlyList<string> b = gen.GenerateDistinct(20, Rng(99));

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void GenerateLeagueName_IsDeterministicAndNonEmpty()
        {
            var gen = new ClubNameGenerator();

            string name = gen.GenerateLeagueName(Rng(3));
            Assert.That(name, Is.Not.Null.And.Not.Empty);
            Assert.That(name, Is.EqualTo(gen.GenerateLeagueName(Rng(3))));
        }
    }
}
