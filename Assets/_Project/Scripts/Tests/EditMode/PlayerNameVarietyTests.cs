using System;
using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Common;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The guard for name variety at the size the game is designed for.
    /// <para>
    /// It exists because the old generator drew a first name and a surname from two 28-entry lists, giving
    /// a hard ceiling of 784 distinct names — and nothing measured it. Playtesting is what found it
    /// ("isimler çok benzer oluşuyor"): at one 500-player league only 367 names were distinct and every
    /// surname repeated about eighteen times, and at 50,000 the pool produced 784 names, 1.6% of the
    /// players. A manager game's world is its players, so that ceiling was a design defect rather than a
    /// polish item.
    /// </para>
    /// <para>
    /// The floor below is deliberately well under what the generator measures today (95.1% at 50,000), so
    /// it fails on a real regression — a shrunk pool, a broken draw — rather than on ordinary variation.
    /// Collisions are expected and fine: drawing 50,000 names from any finite pool produces some by the
    /// birthday paradox, and a generator that produced none would be one that is not really random.
    /// </para>
    /// </summary>
    public sealed class PlayerNameVarietyTests
    {
        private const int WorldSize = 50_000;
        private const double DistinctFloor = 0.90;

        private static GenerationContext Context()
        {
            return new GenerationContext
            {
                MinAge = 16,
                MaxAge = 34,
                MinAbility = 40,
                MaxAbility = 80,
                MinPotential = 50,
                MaxPotential = 95,
            };
        }

        [Test]
        public void Generate_AcrossAFiftyThousandPlayerWorld_KeepsNamesMostlyDistinct()
        {
            var generator = new PlayerGenerator();
            GenerationContext context = Context();
            var rng = new SplitMix64RandomNumberGenerator(12345UL);
            var names = new HashSet<string>();

            for (int i = 0; i < WorldSize; i++)
            {
                names.Add(generator.Generate(new PlayerId(i), context, PlayerRole.CentralMidfield, rng).Name);
            }

            double distinct = names.Count / (double)WorldSize;
            Assert.That(distinct, Is.GreaterThan(DistinctFloor),
                $"Only {names.Count} distinct names across {WorldSize} players ({distinct:P1}). The world is " +
                "supposed to read as that many different people; check the name pools have not shrunk.");
        }

        [Test]
        public void Generate_SameSeed_ProducesTheSameNames()
        {
            var generator = new PlayerGenerator();
            GenerationContext context = Context();

            var first = new List<string>(200);
            var second = new List<string>(200);
            var a = new SplitMix64RandomNumberGenerator(777UL);
            var b = new SplitMix64RandomNumberGenerator(777UL);
            for (int i = 0; i < 200; i++)
            {
                first.Add(generator.Generate(new PlayerId(i), context, PlayerRole.Striker, a).Name);
                second.Add(generator.Generate(new PlayerId(i), context, PlayerRole.Striker, b).Name);
            }

            Assert.That(second, Is.EqualTo(first), "Names must be reproducible from the seed (NON-NEGOTIABLE #2).");
        }

        [Test]
        public void Generate_EveryPlayer_CarriesANationalityTheNameCouldHaveComeFrom()
        {
            // The old generator ignored Nationality entirely, so an "Italian" was called Harry Walker. The
            // name and the nationality are drawn from the same pool now, and this is what keeps them paired.
            var generator = new PlayerGenerator();
            GenerationContext context = Context();
            var rng = new SplitMix64RandomNumberGenerator(4242UL);
            var known = new HashSet<string>();
            for (int i = 0; i < PlayerNamePools.Count; i++)
            {
                known.Add(PlayerNamePools.At(i).Nationality);
            }

            for (int i = 0; i < 2_000; i++)
            {
                Player player = generator.Generate(new PlayerId(i), context, rng);
                Assert.That(known, Contains.Item(player.Nationality), player.Name);
                Assert.That(PlayerNamePools.For(player.Nationality), Is.Not.Null, player.Nationality);
            }
        }

        [Test]
        public void Pools_EveryOne_OffersEnoughNamesToBeWorthDrawingFrom()
        {
            // A per-pool floor, because the world-level test above can be carried by the big pools while a
            // small one quietly repeats itself for everyone born there.
            for (int i = 0; i < PlayerNamePools.Count; i++)
            {
                PlayerNamePool pool = PlayerNamePools.At(i);
                Assert.That(pool.DistinctNameCount, Is.GreaterThan(20_000L),
                    $"{pool.Nationality} can only spell {pool.DistinctNameCount} names.");
            }
        }
    }
}
