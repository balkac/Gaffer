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
        public void SelectBest_PicksTheHighestRatedPerLine()
        {
            Squad squad = GeneratedSquad();
            IReadOnlyList<Player> eleven = new LineupSelector().SelectBest(squad, Formation.F442);
            var chosen = new HashSet<int>();
            foreach (Player player in eleven)
            {
                chosen.Add(player.Id.Value);
            }

            // No benched forward may out-rate a selected forward — the best of each line must start.
            double worstStartingForward = double.MaxValue;
            double bestBenchedForward = double.MinValue;
            foreach (Player player in squad.Players)
            {
                if (player.Position != Position.Forward)
                {
                    continue;
                }

                double rating = PlayerRatings.ForPosition(player);
                if (chosen.Contains(player.Id.Value))
                {
                    worstStartingForward = System.Math.Min(worstStartingForward, rating);
                }
                else
                {
                    bestBenchedForward = System.Math.Max(bestBenchedForward, rating);
                }
            }

            Assert.That(worstStartingForward, Is.GreaterThanOrEqualTo(bestBenchedForward));
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
