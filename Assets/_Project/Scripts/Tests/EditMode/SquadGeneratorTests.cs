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

            // The designed line-up, as literals. Read from SquadGenerator's own constants this compared
            // the generator against itself: editing Defenders to 3 would have changed what is generated
            // AND what is expected, and the test would still pass on a squad that cannot field a back
            // four. These numbers are the contract — 2 keepers, 6 defenders, 7 midfielders, 5 forwards,
            // 20 in all, enough for any formation the lineup selector fills.
            Assert.That(squad.Count, Is.EqualTo(20));
            Assert.That(CountAt(squad, Position.Goalkeeper), Is.EqualTo(2));
            Assert.That(CountAt(squad, Position.Defender), Is.EqualTo(6));
            Assert.That(CountAt(squad, Position.Midfielder), Is.EqualTo(7));
            Assert.That(CountAt(squad, Position.Forward), Is.EqualTo(5));
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
