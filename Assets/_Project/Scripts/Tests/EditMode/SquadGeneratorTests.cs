using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class SquadGeneratorTests
    {
        private static SquadGenerator Generator()
        {
            return new SquadGenerator(new PlayerGenerator());
        }

        private static int CountAt(Squad squad, Position position)
        {
            int count = 0;
            foreach (Player player in squad.Players)
            {
                if (player.Position == position)
                {
                    count++;
                }
            }

            return count;
        }

        [Test]
        public void Generate_ProducesTheStandardLineUp()
        {
            Squad squad = Generator().Generate(0, new GenerationContext(), new SplitMix64RandomNumberGenerator(1));

            Assert.That(squad.Count, Is.EqualTo(SquadGenerator.SquadSize));
            Assert.That(CountAt(squad, Position.Goalkeeper), Is.EqualTo(SquadGenerator.Goalkeepers));
            Assert.That(CountAt(squad, Position.Defender), Is.EqualTo(SquadGenerator.Defenders));
            Assert.That(CountAt(squad, Position.Midfielder), Is.EqualTo(SquadGenerator.Midfielders));
            Assert.That(CountAt(squad, Position.Forward), Is.EqualTo(SquadGenerator.Forwards));
        }

        [Test]
        public void Generate_HandsOutUniqueIdsFromTheOffset()
        {
            const int offset = 100;
            Squad squad = Generator().Generate(offset, new GenerationContext(), new SplitMix64RandomNumberGenerator(1));

            var ids = new HashSet<int>();
            foreach (Player player in squad.Players)
            {
                Assert.That(player.Id.Value, Is.InRange(offset, offset + SquadGenerator.SquadSize - 1));
                Assert.That(ids.Add(player.Id.Value), Is.True, "Player ids within a squad must be unique.");
            }

            Assert.That(ids.Count, Is.EqualTo(SquadGenerator.SquadSize));
        }

        [Test]
        public void Generate_SameSeed_IsDeterministic()
        {
            Squad first = Generator().Generate(0, new GenerationContext(), new SplitMix64RandomNumberGenerator(42));
            Squad second = Generator().Generate(0, new GenerationContext(), new SplitMix64RandomNumberGenerator(42));

            Assert.That(first.Count, Is.EqualTo(second.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(first.Players[i].Name, Is.EqualTo(second.Players[i].Name));
                Assert.That(first.Players[i].Position, Is.EqualTo(second.Players[i].Position));
                Assert.That(first.Players[i].Attributes, Is.EqualTo(second.Players[i].Attributes));
            }
        }
    }
}
