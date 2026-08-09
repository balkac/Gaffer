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
            var profile = ChanceProfile.FromTactics(Tactics.Balanced);

            Assert.That(profile.Volume, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(profile.Quality, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void FromTactics_Counter_MakesFewerButSharperChances()
        {
            var profile = ChanceProfile.FromTactics(
                new Tactics(Mentality.Balanced, Tempo.Standard, Pressing.Standard, Approach.Counter));

            Assert.That(profile.Volume, Is.LessThan(1.0));
            Assert.That(profile.Quality, Is.GreaterThan(1.0));
        }

        [Test]
        public void FromTactics_Possession_MakesMoreButTamerChances()
        {
            var profile = ChanceProfile.FromTactics(
                new Tactics(Mentality.Balanced, Tempo.Standard, Pressing.Standard, Approach.Possession));

            Assert.That(profile.Volume, Is.GreaterThan(1.0));
            Assert.That(profile.Quality, Is.LessThan(1.0));
        }

        [Test]
        public void FromTactics_Tempo_TradesVolumeForQuality()
        {
            // Tempo used to drive volume alone, which made Intense 15% more chances for nothing and
            // Patient a pure loss. It now makes the same trade approach does — more chances, hurried ones
            // — only finer, so the two axes stay distinguishable. NoFreeLunchTests is the general guard.
            var intense = ChanceProfile.FromTactics(new Tactics(Mentality.Balanced, Tempo.Intense, Pressing.Standard));
            var patient = ChanceProfile.FromTactics(new Tactics(Mentality.Balanced, Tempo.Patient, Pressing.Standard));

            Assert.That(intense.Volume, Is.GreaterThan(1.0));
            Assert.That(intense.Quality, Is.LessThan(1.0), "an intense tempo hurries the chances it makes");
            Assert.That(patient.Volume, Is.LessThan(1.0));
            Assert.That(patient.Quality, Is.GreaterThan(1.0), "a patient side works fewer but better ones");
        }

        [Test]
        public void FromTactics_Tempo_IsAFinerDialThanApproach()
        {
            // The judgement call behind the calibration, pinned: if tempo mirrored approach, Intense would
            // be Possession and Patient the Counter, and two of the four axes would stop being different
            // decisions. Approach is the coarse shape choice; tempo trims it.
            var intense = ChanceProfile.FromTactics(new Tactics(Mentality.Balanced, Tempo.Intense, Pressing.Standard));
            var possession = ChanceProfile.FromTactics(
                new Tactics(Mentality.Balanced, Tempo.Standard, Pressing.Standard, Approach.Possession));

            Assert.That(intense.Volume - 1.0, Is.LessThan((possession.Volume - 1.0) * 0.75),
                "tempo's volume swing must stay clearly under approach's, or the two axes duplicate each other");
            Assert.That(1.0 - intense.Quality, Is.LessThan((1.0 - possession.Quality) * 0.75),
                "and its quality swing with it");
        }

        [Test]
        public void Generator_CounterSide_MakesFewerChancesOfHigherQuality()
        {
            var generator = new PoissonChanceGenerator(MatchSimulationSettings.Default);
            var strength = new TeamStrength(60, 60, 60);
            var counter = ChanceProfile.FromTactics(
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
