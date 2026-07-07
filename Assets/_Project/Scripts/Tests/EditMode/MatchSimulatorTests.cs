using Gaffer.Domain.Clubs;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class MatchSimulatorTests
    {
        private static MatchSimulator CreateSimulator()
        {
            MatchSimulationSettings settings = MatchSimulationSettings.Default;
            return new MatchSimulator(new PoissonChanceGenerator(settings), new QualityChanceResolver());
        }

        private static MatchCommand CreateBalancedCommand()
        {
            var strength = new TeamStrength(60, 60, 60);
            var context = new MatchContext(MatchImportance.Normal, 10000, isTitleDecider: false, isRivalry: false);
            return new MatchCommand(strength, strength, context);
        }

        [Test]
        public void Simulate_SameSeedAndCommand_IsDeterministic()
        {
            MatchSimulator simulator = CreateSimulator();
            MatchCommand command = CreateBalancedCommand();

            MatchOutcome first = simulator.Simulate(command, new SplitMix64RandomNumberGenerator(2024));
            MatchOutcome second = simulator.Simulate(command, new SplitMix64RandomNumberGenerator(2024));

            Assert.That(first.HomeGoals, Is.EqualTo(second.HomeGoals));
            Assert.That(first.AwayGoals, Is.EqualTo(second.AwayGoals));
            Assert.That(first.Events.Count, Is.EqualTo(second.Events.Count));
            for (int i = 0; i < first.Events.Count; i++)
            {
                Assert.That(first.Events[i].Minute, Is.EqualTo(second.Events[i].Minute));
                Assert.That(first.Events[i].Side, Is.EqualTo(second.Events[i].Side));
            }
        }

        [Test]
        public void Simulate_GoalEvents_MatchTheScore()
        {
            MatchSimulator simulator = CreateSimulator();
            MatchCommand command = CreateBalancedCommand();

            MatchOutcome outcome = simulator.Simulate(command, new SplitMix64RandomNumberGenerator(7));

            int goalEvents = 0;
            foreach (MatchEvent matchEvent in outcome.Events)
            {
                Assert.That(matchEvent.Kind, Is.EqualTo(MatchEventKind.Goal));
                goalEvents++;
            }

            Assert.That(goalEvents, Is.EqualTo(outcome.HomeGoals + outcome.AwayGoals));
        }

        [Test]
        public void Simulate_Events_AreOrderedByMinute()
        {
            MatchSimulator simulator = CreateSimulator();
            MatchCommand command = CreateBalancedCommand();

            MatchOutcome outcome = simulator.Simulate(command, new SplitMix64RandomNumberGenerator(555));

            for (int i = 1; i < outcome.Events.Count; i++)
            {
                Assert.That(outcome.Events[i].Minute, Is.GreaterThanOrEqualTo(outcome.Events[i - 1].Minute));
            }
        }

        [Test]
        public void Simulate_StrongHomeVersusWeakAway_HomeWinsMajority()
        {
            MatchSimulator simulator = CreateSimulator();
            var strong = new TeamStrength(85, 85, 85);
            var weak = new TeamStrength(45, 45, 45);
            var context = new MatchContext(MatchImportance.Normal, 10000, isTitleDecider: false, isRivalry: false);
            var command = new MatchCommand(strong, weak, context);

            int homeWins = 0;
            int awayWins = 0;
            var rng = new SplitMix64RandomNumberGenerator(1);
            for (int i = 0; i < 1000; i++)
            {
                MatchOutcome outcome = simulator.Simulate(command, rng);
                if (outcome.HomeGoals > outcome.AwayGoals)
                {
                    homeWins++;
                }
                else if (outcome.AwayGoals > outcome.HomeGoals)
                {
                    awayWins++;
                }
            }

            Assert.That(homeWins, Is.GreaterThan(awayWins * 3),
                $"A strong home side should dominate, but home won {homeWins} vs away {awayWins}.");
        }

        [Test]
        public void Simulate_BalancedTeams_AverageGoalsArePlausible()
        {
            MatchSimulator simulator = CreateSimulator();
            MatchCommand command = CreateBalancedCommand();

            int totalGoals = 0;
            const int matches = 2000;
            var rng = new SplitMix64RandomNumberGenerator(99);
            for (int i = 0; i < matches; i++)
            {
                MatchOutcome outcome = simulator.Simulate(command, rng);
                totalGoals += outcome.HomeGoals + outcome.AwayGoals;
            }

            double averageGoals = (double)totalGoals / matches;
            // Loose band for the untuned skeleton — Faz 1 tightens this toward ~2.5–3 with the harness.
            Assert.That(averageGoals, Is.InRange(1.0, 5.0),
                $"Average goals per match was {averageGoals:F2}, outside the plausible skeleton band.");
        }
    }
}
