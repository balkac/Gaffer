using Gaffer.Domain.Clubs;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using NUnit.Framework;

namespace Gaffer.Tests.Unity
{
    /// <summary>
    /// Runs the pure simulation inside the Unity runtime (Mono / IL2CPP) through the Unity Test
    /// Runner, proving it stays deterministic there too — not only under dotnet. Mirrors the headless
    /// checks so a runtime-specific divergence (e.g. floating point) would surface here.
    /// </summary>
    public sealed class MatchSimulatorUnityTests
    {
        [Test]
        public void Simulate_SameSeed_IsDeterministicOnUnityRuntime()
        {
            var simulator = new MatchSimulator(
                new PoissonChanceGenerator(MatchSimulationSettings.Default),
                new QualityChanceResolver());
            var command = new MatchCommand(
                new TeamStrength(70.0, 65.0, 60.0),
                new TeamStrength(60.0, 62.0, 64.0),
                new MatchContext(MatchImportance.Normal, 12000, isTitleDecider: false, isRivalry: false));

            MatchOutcome first = simulator.Simulate(command, new SplitMix64RandomNumberGenerator(4242));
            MatchOutcome second = simulator.Simulate(command, new SplitMix64RandomNumberGenerator(4242));

            Assert.That(first.HomeGoals, Is.EqualTo(second.HomeGoals));
            Assert.That(first.AwayGoals, Is.EqualTo(second.AwayGoals));
            Assert.That(first.Events.Count, Is.EqualTo(second.Events.Count));
        }

        [Test]
        public void SplitMix64_Seed0_MatchesReferenceOnUnityRuntime()
        {
            var rng = new SplitMix64RandomNumberGenerator(0);

            Assert.That(rng.NextUInt64(), Is.EqualTo(0xE220A8397B1DCDAFUL));
        }
    }
}
