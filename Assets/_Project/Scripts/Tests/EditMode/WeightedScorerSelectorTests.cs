using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class WeightedScorerSelectorTests
    {
        private static Player PlayerAt(int id, Position position, byte finishing)
        {
            var attributes = new Attributes { Finishing = finishing, Positioning = 60, Pace = 60 };
            return new Player(new PlayerId(id), "Player " + id, "England", position, 24, attributes, 70);
        }

        private static int CountPicks(Squad squad, int draws, ulong seed, int wantedId)
        {
            var selector = new WeightedScorerSelector();
            var rng = new SplitMix64RandomNumberGenerator(seed);
            int hits = 0;
            for (int i = 0; i < draws; i++)
            {
                if (selector.SelectScorer(squad, rng)?.Value == wantedId)
                {
                    hits++;
                }
            }

            return hits;
        }

        [Test]
        public void SelectScorer_NullOrEmptySquad_ReturnsNull()
        {
            var selector = new WeightedScorerSelector();
            var rng = new SplitMix64RandomNumberGenerator(1);

            Assert.That(selector.SelectScorer(null, rng), Is.Null);
            Assert.That(selector.SelectScorer(new Squad(new List<Player>()), rng), Is.Null);
        }

        [Test]
        public void SelectScorer_ReturnsAPlayerFromTheSquad()
        {
            var squad = new Squad(new List<Player>
            {
                PlayerAt(3, Position.Forward, 80),
                PlayerAt(7, Position.Midfielder, 55),
                PlayerAt(9, Position.Defender, 45),
            });
            var selector = new WeightedScorerSelector();
            var rng = new SplitMix64RandomNumberGenerator(42);

            for (int i = 0; i < 200; i++)
            {
                PlayerId? scorer = selector.SelectScorer(squad, rng);
                Assert.That(scorer, Is.Not.Null);
                Assert.That(new[] { 3, 7, 9 }, Contains.Item(scorer.Value.Value));
            }
        }

        [Test]
        public void SelectScorer_SameSeed_IsDeterministic()
        {
            var squad = new Squad(new List<Player>
            {
                PlayerAt(0, Position.Forward, 82),
                PlayerAt(1, Position.Midfielder, 58),
                PlayerAt(2, Position.Defender, 44),
            });

            var first = new WeightedScorerSelector().SelectScorer(squad, new SplitMix64RandomNumberGenerator(2026));
            var second = new WeightedScorerSelector().SelectScorer(squad, new SplitMix64RandomNumberGenerator(2026));

            Assert.That(first.Value, Is.EqualTo(second.Value));
        }

        [Test]
        public void SelectScorer_Forward_ScoresFarMoreThanDefender()
        {
            var squad = new Squad(new List<Player>
            {
                PlayerAt(1, Position.Forward, 80),
                PlayerAt(2, Position.Defender, 45),
            });

            int forwardPicks = CountPicks(squad, 2000, 99, wantedId: 1);
            int defenderPicks = CountPicks(squad, 2000, 99, wantedId: 2);

            Assert.That(forwardPicks, Is.GreaterThan(defenderPicks * 3),
                $"A striker should far outscore a defender, but got {forwardPicks} vs {defenderPicks}.");
        }

        [Test]
        public void SelectScorer_Goalkeeper_AlmostNeverScores()
        {
            var squad = new Squad(new List<Player>
            {
                PlayerAt(1, Position.Forward, 80),
                PlayerAt(2, Position.Goalkeeper, 20),
            });

            int keeperPicks = CountPicks(squad, 2000, 7, wantedId: 2);

            Assert.That(keeperPicks, Is.LessThan(100),
                $"A keeper should almost never score, but scored {keeperPicks} of 2000.");
        }
    }
}
