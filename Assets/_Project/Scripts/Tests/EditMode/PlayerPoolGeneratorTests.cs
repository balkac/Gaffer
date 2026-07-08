using Gaffer.Application.Generation;
using Gaffer.Common;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class PlayerPoolGeneratorTests
    {
        private const int PoolSize = 200;
        private const int GuaranteedGems = 5;

        private static GenerationContext OrdinaryContext()
        {
            return new GenerationContext();
        }

        private static GenerationContext GemContext()
        {
            // Low visible ability, high hidden ceiling, young — a gem waiting to be discovered.
            return new GenerationContext
            {
                MinAge = 16,
                MaxAge = 19,
                MinAbility = 35,
                MaxAbility = 52,
                MinPotential = 84,
                MaxPotential = 95,
            };
        }

        private static bool IsDiscoverableGem(Player player)
        {
            double visible = (player.Attributes.Pace + player.Attributes.Finishing + player.Attributes.Passing +
                              player.Attributes.Tackling + player.Attributes.Positioning + player.Attributes.Stamina) / 6.0;
            return visible <= 55.0 && player.HiddenPotential >= 82 && player.Age <= 20;
        }

        [Test]
        public void GeneratePool_AlwaysContainsAtLeastTheGuaranteedGems()
        {
            var generator = new PlayerPoolGenerator(new PlayerGenerator());

            var pool = generator.GeneratePool(PoolSize, GuaranteedGems, OrdinaryContext(), GemContext(), new SplitMix64RandomNumberGenerator(2024));

            int gems = 0;
            foreach (Player player in pool)
            {
                if (IsDiscoverableGem(player))
                {
                    gems++;
                }
            }

            Assert.That(gems, Is.GreaterThanOrEqualTo(GuaranteedGems),
                $"Only {gems} discoverable gems in the pool; the guarantee is {GuaranteedGems}.");
        }

        [Test]
        public void GeneratePool_GemsAreRare_NotMostOfThePool()
        {
            var generator = new PlayerPoolGenerator(new PlayerGenerator());

            var pool = generator.GeneratePool(PoolSize, GuaranteedGems, OrdinaryContext(), GemContext(), new SplitMix64RandomNumberGenerator(2024));

            int gems = 0;
            foreach (Player player in pool)
            {
                if (IsDiscoverableGem(player))
                {
                    gems++;
                }
            }

            // Discovery must stay special: gems are a small fraction of the pool.
            Assert.That(gems, Is.LessThan(PoolSize / 4));
        }

        [Test]
        public void GeneratePool_SameSeed_IsDeterministic()
        {
            var generator = new PlayerPoolGenerator(new PlayerGenerator());
            GenerationContext ordinary = OrdinaryContext();
            GenerationContext gem = GemContext();

            var first = generator.GeneratePool(PoolSize, GuaranteedGems, ordinary, gem, new SplitMix64RandomNumberGenerator(7));
            var second = generator.GeneratePool(PoolSize, GuaranteedGems, ordinary, gem, new SplitMix64RandomNumberGenerator(7));

            Assert.That(first.Count, Is.EqualTo(second.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(first[i].Name, Is.EqualTo(second[i].Name));
                Assert.That(first[i].Attributes, Is.EqualTo(second[i].Attributes));
                Assert.That(first[i].HiddenPotential, Is.EqualTo(second[i].HiddenPotential));
            }
        }
    }
}
