using Gaffer.Application.Generation;
using Gaffer.Common;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class PlayerGeneratorTests
    {
        private static GenerationContext Context()
        {
            return new GenerationContext();
        }

        [Test]
        public void Generate_SameSeedAndContext_IsDeterministic()
        {
            var generator = new PlayerGenerator();
            GenerationContext context = Context();

            Player first = generator.Generate(new PlayerId(1), context, new SplitMix64RandomNumberGenerator(99));
            Player second = generator.Generate(new PlayerId(1), context, new SplitMix64RandomNumberGenerator(99));

            Assert.That(first.Name, Is.EqualTo(second.Name));
            Assert.That(first.Nationality, Is.EqualTo(second.Nationality));
            Assert.That(first.Position, Is.EqualTo(second.Position));
            Assert.That(first.Age, Is.EqualTo(second.Age));
            Assert.That(first.Attributes, Is.EqualTo(second.Attributes));
            Assert.That(first.HiddenPotential, Is.EqualTo(second.HiddenPotential));
        }

        [Test]
        public void Generate_OverManyPlayers_StaysWithinContextBands()
        {
            var generator = new PlayerGenerator();
            GenerationContext context = Context();
            var rng = new SplitMix64RandomNumberGenerator(7);

            for (int i = 0; i < 2000; i++)
            {
                Player player = generator.Generate(new PlayerId(i), context, rng);

                Assert.That(player.Age, Is.InRange(context.MinAge, context.MaxAge));
                Assert.That(player.HiddenPotential, Is.InRange(context.MinPotential, context.MaxPotential));
                Assert.That(player.Attributes.Pace, Is.InRange(context.MinAbility, context.MaxAbility));
                Assert.That(player.Attributes.Finishing, Is.InRange(context.MinAbility, context.MaxAbility));
                Assert.That(player.Attributes.Stamina, Is.InRange(context.MinAbility, context.MaxAbility));
            }
        }

        [Test]
        public void Generate_Name_HasAFirstAndLastPart()
        {
            var generator = new PlayerGenerator();

            Player player = generator.Generate(new PlayerId(1), Context(), new SplitMix64RandomNumberGenerator(3));

            string[] parts = player.Name.Split(' ');
            Assert.That(parts.Length, Is.EqualTo(2));
            Assert.That(parts[0], Is.Not.Empty);
            Assert.That(parts[1], Is.Not.Empty);
        }

        [Test]
        public void Generate_DifferentSeeds_ProduceVariety()
        {
            var generator = new PlayerGenerator();
            GenerationContext context = Context();

            Player a = generator.Generate(new PlayerId(1), context, new SplitMix64RandomNumberGenerator(1));
            Player b = generator.Generate(new PlayerId(1), context, new SplitMix64RandomNumberGenerator(2));

            Assert.That(a.Name != b.Name || a.Attributes != b.Attributes, Is.True);
        }
    }
}
