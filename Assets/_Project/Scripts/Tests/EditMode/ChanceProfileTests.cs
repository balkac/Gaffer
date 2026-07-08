using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class ChanceProfileTests
    {
        private static MatchContext Context()
        {
            return new MatchContext(MatchImportance.Normal, 10000, isTitleDecider: false, isRivalry: false);
        }

        [Test]
        public void FromTactics_Balanced_IsNeutral()
        {
            ChanceProfile profile = ChanceProfile.FromTactics(Tactics.Balanced);

            Assert.That(profile.Volume, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(profile.Quality, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void FromTactics_Counter_MakesFewerButSharperChances()
        {
            ChanceProfile profile = ChanceProfile.FromTactics(
                new Tactics(Mentality.Balanced, Tempo.Standard, Pressing.Standard, Approach.Counter));

            Assert.That(profile.Volume, Is.LessThan(1.0));
            Assert.That(profile.Quality, Is.GreaterThan(1.0));
        }

        [Test]
        public void FromTactics_Possession_MakesMoreButTamerChances()
        {
            ChanceProfile profile = ChanceProfile.FromTactics(
                new Tactics(Mentality.Balanced, Tempo.Standard, Pressing.Standard, Approach.Possession));

            Assert.That(profile.Volume, Is.GreaterThan(1.0));
            Assert.That(profile.Quality, Is.LessThan(1.0));
        }

        [Test]
        public void FromTactics_Tempo_DrivesVolumeOnly()
        {
            ChanceProfile intense = ChanceProfile.FromTactics(new Tactics(Mentality.Balanced, Tempo.Intense, Pressing.Standard));
            ChanceProfile patient = ChanceProfile.FromTactics(new Tactics(Mentality.Balanced, Tempo.Patient, Pressing.Standard));

            Assert.That(intense.Volume, Is.GreaterThan(1.0));
            Assert.That(patient.Volume, Is.LessThan(1.0));
            Assert.That(intense.Quality, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(patient.Quality, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void Generator_CounterSide_MakesFewerChancesOfHigherQuality()
        {
            var generator = new PoissonChanceGenerator(MatchSimulationSettings.Default);
            var strength = new TeamStrength(60, 60, 60);
            ChanceProfile counter = ChanceProfile.FromTactics(
                new Tactics(Mentality.Balanced, Tempo.Standard, Pressing.Standard, Approach.Counter));

            var counterCommand = new MatchCommand(strength, strength, null, null, counter, ChanceProfile.Neutral, Context());
            var balancedCommand = new MatchCommand(strength, strength, Context());

            HomeChances(generator, counterCommand, 4000, 1, out int counterCount, out double counterQuality);
            HomeChances(generator, balancedCommand, 4000, 1, out int balancedCount, out double balancedQuality);

            Assert.That(counterCount, Is.LessThan(balancedCount), "The counter should make fewer chances.");

            double counterAvg = counterQuality / counterCount;
            double balancedAvg = balancedQuality / balancedCount;
            Assert.That(counterAvg, Is.GreaterThan(balancedAvg), "The counter's chances should be sharper on average.");
        }

        private static void HomeChances(PoissonChanceGenerator generator, MatchCommand command, int matches, ulong seed, out int count, out double quality)
        {
            var rng = new SplitMix64RandomNumberGenerator(seed);
            count = 0;
            quality = 0.0;
            for (int i = 0; i < matches; i++)
            {
                IReadOnlyList<Chance> chances = generator.GenerateChances(command, rng);
                foreach (Chance chance in chances)
                {
                    if (chance.Side == TeamSide.Home)
                    {
                        count++;
                        quality += chance.Quality;
                    }
                }
            }
        }
    }
}
