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
            return GeneratedSquad(idBase: 0, seed: 7);
        }

        private static Squad GeneratedSquad(int idBase, ulong seed)
        {
            return new SquadGenerator(new PlayerGenerator())
                .Generate(idBase, new GenerationContext(), new SplitMix64RandomNumberGenerator(seed));
        }

        private static int[] IdsOf(IReadOnlyList<Player> players)
        {
            var ids = new int[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                ids[i] = players[i].Id.Value;
            }

            return ids;
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
        public void SelectBest_OneInstanceReusedAcrossSquads_LeavesNoResidue()
        {
            // SelectBest takes no IRandom and is a pure function of (squad, formation), so two separate
            // selectors agreeing proves nothing. The real risk is the scratch state one instance carries
            // between calls — the not-yet-picked pool and the returned eleven are both reused buffers
            // (PERFORMANCE §4, reuse scratch collections). So: ONE instance, squad A → squad B → squad A,
            // and run 3 must reproduce run 1 exactly. The ids are copied out of each result immediately
            // because SelectBest's contract is that its list is only valid until the next call — which
            // the intervening B pick is precisely what invalidates.
            var selector = new LineupSelector();
            Squad a = GeneratedSquad(idBase: 0, seed: 7);
            Squad b = GeneratedSquad(idBase: 500, seed: 31);

            int[] first = IdsOf(selector.SelectBest(a, Formation.F352));
            int[] between = IdsOf(selector.SelectBest(b, Formation.F433));
            int[] third = IdsOf(selector.SelectBest(a, Formation.F352));

            Assert.That(first.Length, Is.EqualTo(11));
            Assert.That(between.Length, Is.EqualTo(11));
            Assert.That(third, Is.EqualTo(first), "The A pick changed after an intervening B pick — the reused buffers leaked state.");
        }

        [Test]
        public void SelectBest_SameSquadTwiceOnOneInstance_IsIdentical()
        {
            var selector = new LineupSelector();
            Squad squad = GeneratedSquad();

            int[] first = IdsOf(selector.SelectBest(squad, Formation.F352));
            int[] second = IdsOf(selector.SelectBest(squad, Formation.F352));

            Assert.That(second, Is.EqualTo(first));
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
