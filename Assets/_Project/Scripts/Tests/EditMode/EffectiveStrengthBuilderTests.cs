using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class EffectiveStrengthBuilderTests
    {
        private static Player PlayerAt(Position position, byte stat)
        {
            var attributes = new Attributes(stat, stat, stat, stat, stat, stat);
            return new Player(new PlayerId(0), "Test Player", "England", position, 24, attributes, 70);
        }

        private static Squad SquadOf(params Player[] players)
        {
            return new Squad(new List<Player>(players));
        }

        [Test]
        public void Build_UniformSquad_ProducesUniformAxes()
        {
            var builder = new EffectiveStrengthBuilder();
            Squad squad = SquadOf(
                PlayerAt(Position.Goalkeeper, 60),
                PlayerAt(Position.Defender, 60),
                PlayerAt(Position.Midfielder, 60),
                PlayerAt(Position.Forward, 60));

            TeamStrength strength = builder.Build(squad);

            // Every attribute is 60, so every weighted role rating collapses to 60 on all three axes.
            Assert.That(strength.Attack, Is.EqualTo(60.0).Within(1e-9));
            Assert.That(strength.Midfield, Is.EqualTo(60.0).Within(1e-9));
            Assert.That(strength.Defence, Is.EqualTo(60.0).Within(1e-9));
        }

        [Test]
        public void Build_StrongForwardsWeakDefenders_LiftsAttackAboveDefence()
        {
            var builder = new EffectiveStrengthBuilder();
            Squad squad = SquadOf(
                PlayerAt(Position.Forward, 90),
                PlayerAt(Position.Forward, 90),
                PlayerAt(Position.Midfielder, 60),
                PlayerAt(Position.Defender, 40),
                PlayerAt(Position.Goalkeeper, 40));

            TeamStrength strength = builder.Build(squad);

            Assert.That(strength.Attack, Is.GreaterThan(strength.Defence));
        }

        [Test]
        public void Build_StrongDefendersWeakForwards_LiftsDefenceAboveAttack()
        {
            var builder = new EffectiveStrengthBuilder();
            Squad squad = SquadOf(
                PlayerAt(Position.Forward, 40),
                PlayerAt(Position.Midfielder, 60),
                PlayerAt(Position.Defender, 90),
                PlayerAt(Position.Defender, 90),
                PlayerAt(Position.Goalkeeper, 90));

            TeamStrength strength = builder.Build(squad);

            Assert.That(strength.Defence, Is.GreaterThan(strength.Attack));
        }

        [Test]
        public void Build_LineWithNoPlayers_FallsBackToSquadWideAverage()
        {
            var builder = new EffectiveStrengthBuilder();
            // No forwards at all — the attack axis must still come out plausible, not zero or a crash.
            Squad squad = SquadOf(
                PlayerAt(Position.Midfielder, 70),
                PlayerAt(Position.Defender, 70),
                PlayerAt(Position.Goalkeeper, 70));

            TeamStrength strength = builder.Build(squad);

            Assert.That(strength.Attack, Is.GreaterThan(0.0));
            Assert.That(strength.Attack, Is.EqualTo(70.0).Within(1e-9));
        }

        [Test]
        public void Build_EmptySquad_ReturnsZeroedStrength()
        {
            var builder = new EffectiveStrengthBuilder();

            TeamStrength strength = builder.Build(SquadOf());

            Assert.That(strength.Attack, Is.EqualTo(0.0));
            Assert.That(strength.Midfield, Is.EqualTo(0.0));
            Assert.That(strength.Defence, Is.EqualTo(0.0));
        }

        [Test]
        public void Build_StrongerForwardLine_ProducesHigherAttackAxis()
        {
            var builder = new EffectiveStrengthBuilder();
            Squad weak = SquadOf(PlayerAt(Position.Forward, 50), PlayerAt(Position.Midfielder, 60));
            Squad strong = SquadOf(PlayerAt(Position.Forward, 80), PlayerAt(Position.Midfielder, 60));

            TeamStrength weakStrength = builder.Build(weak);
            TeamStrength strongStrength = builder.Build(strong);

            Assert.That(strongStrength.Attack, Is.GreaterThan(weakStrength.Attack));
        }
    }
}
