using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The component that decides whether a created chance becomes a goal. It is two tokens of code and
    /// the whole scoreline rests on it, so what is pinned here is the contract rather than the
    /// expression: the comparison direction, both ends of the quality range, the boundary, the draw
    /// budget the match rng stream depends on, and the fact that the goal rate really is the quality.
    /// </summary>
    public sealed class QualityChanceResolverTests
    {
        // Feeds a scripted sequence of doubles and counts how many draws were taken.
        private sealed class ScriptedRandom : IRandom
        {
            private readonly IReadOnlyList<double> _doubles;
            private int _index;

            public ScriptedRandom(params double[] doubles)
            {
                _doubles = doubles;
            }

            public int DoublesDrawn { get; private set; }

            public ulong NextUInt64()
            {
                Assert.Fail("The resolver must not draw raw 64-bit values.");
                return 0UL;
            }

            public int NextInt(int maxExclusive)
            {
                Assert.Fail("The resolver must not draw integers.");
                return 0;
            }

            public int NextInt(int minInclusive, int maxExclusive)
            {
                Assert.Fail("The resolver must not draw integers.");
                return 0;
            }

            public double NextDouble()
            {
                DoublesDrawn++;
                return _doubles[_index++ % _doubles.Count];
            }
        }

        private static Chance WithQuality(double quality)
        {
            return new Chance(TeamSide.Home, 42, quality);
        }

        [Test]
        public void ResolvesToGoal_DrawBelowQuality_IsAGoal()
        {
            var rng = new ScriptedRandom(0.29);

            Assert.That(new QualityChanceResolver().ResolvesToGoal(WithQuality(0.30), rng), Is.True);
        }

        [Test]
        public void ResolvesToGoal_DrawAboveQuality_IsAMiss()
        {
            var rng = new ScriptedRandom(0.31);

            Assert.That(new QualityChanceResolver().ResolvesToGoal(WithQuality(0.30), rng), Is.False);
        }

        [Test]
        public void ResolvesToGoal_DrawExactlyAtQuality_IsAMiss()
        {
            // The boundary, and the direction of the inequality with it (CONVENTIONS §5 wants both
            // ends). Flipping `<` to `<=` here would leave every other test in this file passing.
            var rng = new ScriptedRandom(0.30);

            Assert.That(new QualityChanceResolver().ResolvesToGoal(WithQuality(0.30), rng), Is.False);
        }

        [Test]
        public void ResolvesToGoal_ZeroQuality_NeverScoresEvenOnTheLowestPossibleDraw()
        {
            // NextDouble is documented as [0, 1), so 0.0 is a draw that really occurs. A worthless
            // chance must still miss on it.
            var rng = new ScriptedRandom(0.0);

            Assert.That(new QualityChanceResolver().ResolvesToGoal(WithQuality(0.0), rng), Is.False);
        }

        [Test]
        public void ResolvesToGoal_CertainChance_ScoresOnEveryDrawTheRangeAllows()
        {
            // The other end: quality 1.0 beats every value NextDouble can return, including the one just
            // under the open upper bound.
            var rng = new ScriptedRandom(0.0, 0.5, 0.999999999999);
            var resolver = new QualityChanceResolver();

            for (int i = 0; i < 3; i++)
            {
                Assert.That(resolver.ResolvesToGoal(WithQuality(1.0), rng), Is.True);
            }
        }

        [Test]
        public void ResolvesToGoal_OneCall_DrawsExactlyOneDouble()
        {
            // The match rng is one shared stream: every chance in the match, and every fixture seeded
            // from it, shifts if this method's draw budget changes. A second draw added here would
            // silently re-roll the whole league (NON-NEGOTIABLE #2).
            var rng = new ScriptedRandom(0.5);

            new QualityChanceResolver().ResolvesToGoal(WithQuality(0.6), rng);

            Assert.That(rng.DoublesDrawn, Is.EqualTo(1));
        }

        [Test]
        public void ResolvesToGoal_OverManyUniformDraws_ScoresAtAboutTheChancesQuality()
        {
            // The property the whole match model rests on: quality is a probability, not just a
            // threshold with the right sign. A resolver that answered `draw < quality / 2`, or that
            // compared against a fixed 0.5, passes every single-draw test above and fails this one.
            const int trials = 200_000;
            var rng = new SplitMix64RandomNumberGenerator(20260807UL);
            var resolver = new QualityChanceResolver();
            Chance chance = WithQuality(0.35);

            int goals = 0;
            for (int i = 0; i < trials; i++)
            {
                if (resolver.ResolvesToGoal(chance, rng))
                {
                    goals++;
                }
            }

            double rate = goals / (double)trials;
            Assert.That(rate, Is.EqualTo(0.35).Within(0.01),
                $"A 0.35-quality chance scored at {rate:F4} over {trials} draws.");
        }

        [Test]
        public void ResolvesToGoal_SameSeedAndChance_ProducesTheSameSequenceOfVerdicts()
        {
            var chances = new List<Chance>();
            for (int i = 0; i < 50; i++)
            {
                chances.Add(WithQuality(i / 50.0));
            }

            var resolver = new QualityChanceResolver();
            var first = new List<bool>();
            var firstRng = new SplitMix64RandomNumberGenerator(99UL);
            foreach (Chance chance in chances)
            {
                first.Add(resolver.ResolvesToGoal(chance, firstRng));
            }

            var secondRng = new SplitMix64RandomNumberGenerator(99UL);
            var second = new List<bool>();
            foreach (Chance chance in chances)
            {
                second.Add(new QualityChanceResolver().ResolvesToGoal(chance, secondRng));
            }

            Assert.That(second, Is.EqualTo(first).AsCollection);
            Assert.That(first, Has.Some.True, "The fixture must contain goals, or this compares two empty verdicts.");
            Assert.That(first, Has.Some.False, "The fixture must contain misses too.");
        }
    }
}
