using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class LineupSelectorTests
    {
        private static Squad GeneratedSquad()
        {
            return new SquadGenerator(new PlayerGenerator())
                .Generate(0, new GenerationContext(), new SplitMix64RandomNumberGenerator(7));
        }

        private static int CountAt(IReadOnlyList<Player> players, Position position)
        {
            int count = 0;
            foreach (Player player in players)
            {
                if (player.Position == position)
                {
                    count++;
                }
            }

            return count;
        }

        [Test]
        public void SelectBest_FillsElevenInTheFormationShape()
        {
            IReadOnlyList<Player> eleven = new LineupSelector().SelectBest(GeneratedSquad(), Formation.F433);

            Assert.That(eleven.Count, Is.EqualTo(11));
            Assert.That(CountAt(eleven, Position.Goalkeeper), Is.EqualTo(1));
            Assert.That(CountAt(eleven, Position.Defender), Is.EqualTo(4));
            Assert.That(CountAt(eleven, Position.Midfielder), Is.EqualTo(3));
            Assert.That(CountAt(eleven, Position.Forward), Is.EqualTo(3));
        }

        [Test]
        public void SelectBest_FillsEachSlotWithItsExactRole()
        {
            Squad squad = GeneratedSquad();
            IReadOnlyList<Player> eleven = new LineupSelector().SelectBest(squad, Formation.F433);

            // The generated squad has at least one of every role a 4-3-3 asks for, so each slot is an exact
            // match: the winger slots field wingers, the striker slot a striker — roles matter in selection.
            Dictionary<PlayerRole, int> wanted = RoleCounts(Formation.F433.Slots);
            var got = new List<PlayerRole>(eleven.Count);
            foreach (Player player in eleven)
            {
                got.Add(player.Role);
            }

            Dictionary<PlayerRole, int> filled = RoleCounts(got);
            foreach (KeyValuePair<PlayerRole, int> pair in wanted)
            {
                filled.TryGetValue(pair.Key, out int actual);
                Assert.That(actual, Is.EqualTo(pair.Value), $"Formation wanted {pair.Value}× {pair.Key}, eleven has {actual}.");
            }
        }

        private static Dictionary<PlayerRole, int> RoleCounts(IReadOnlyList<PlayerRole> roles)
        {
            var counts = new Dictionary<PlayerRole, int>();
            foreach (PlayerRole role in roles)
            {
                counts.TryGetValue(role, out int n);
                counts[role] = n + 1;
            }

            return counts;
        }

        [Test]
        public void SelectBest_IsDeterministic()
        {
            Squad squad = GeneratedSquad();
            IReadOnlyList<Player> first = new LineupSelector().SelectBest(squad, Formation.F352);
            IReadOnlyList<Player> second = new LineupSelector().SelectBest(squad, Formation.F352);

            Assert.That(first.Count, Is.EqualTo(second.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(first[i].Id.Value, Is.EqualTo(second[i].Id.Value));
            }
        }

        [Test]
        public void SelectBest_StrongerEleven_OutratesTheWholeSquadOnItsAxis()
        {
            Squad squad = GeneratedSquad();
            var builder = new EffectiveStrengthBuilder();

            TeamStrength wholeSquad = builder.Build(squad);
            TeamStrength bestEleven = builder.Build(new LineupSelector().SelectBest(squad, Formation.F442), Tactics.Balanced);

            // Fielding the best eleven should be at least as strong up front as averaging the entire squad.
            Assert.That(bestEleven.Attack, Is.GreaterThanOrEqualTo(wholeSquad.Attack));
        }
    }
}
