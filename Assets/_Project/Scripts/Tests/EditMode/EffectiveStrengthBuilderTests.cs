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
            // Every attribute set to the same value, so each weighted role rating collapses to that value
            // whichever axis (or role branch) reads it — the tests can reason about the axes directly.
            var attributes = new Attributes
            {
                Finishing = stat, Technique = stat, FirstTouch = stat, Dribbling = stat, Passing = stat,
                Crossing = stat, Heading = stat, LongShots = stat, Marking = stat, Tackling = stat,
                Penalties = stat, FreeKicks = stat, Corners = stat, LongThrows = stat,
                Pace = stat, Acceleration = stat, Stamina = stat, Strength = stat, Agility = stat,
                Jumping = stat, Balance = stat, Positioning = stat,
                Reflexes = stat, Handling = stat, AerialReach = stat, CommandOfArea = stat,
                OneOnOnes = stat, Kicking = stat, GkPositioning = stat,
            };
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

        private static Squad StandardSquad(byte stat)
        {
            return SquadOf(
                PlayerAt(Position.Goalkeeper, stat), PlayerAt(Position.Defender, stat),
                PlayerAt(Position.Defender, stat), PlayerAt(Position.Midfielder, stat),
                PlayerAt(Position.Midfielder, stat), PlayerAt(Position.Forward, stat),
                PlayerAt(Position.Forward, stat));
        }

        [Test]
        public void Build_BalancedTactics_MatchTheDefaultBuild()
        {
            var builder = new EffectiveStrengthBuilder();
            Squad squad = StandardSquad(60);

            TeamStrength plain = builder.Build(squad);
            TeamStrength balanced = builder.Build(squad, Tactics.Balanced);

            Assert.That(balanced.Attack, Is.EqualTo(plain.Attack).Within(1e-9));
            Assert.That(balanced.Midfield, Is.EqualTo(plain.Midfield).Within(1e-9));
            Assert.That(balanced.Defence, Is.EqualTo(plain.Defence).Within(1e-9));
        }

        [Test]
        public void Build_AttackingMentality_LiftsAttackAndThinsDefence()
        {
            var builder = new EffectiveStrengthBuilder();
            Squad squad = StandardSquad(60);

            TeamStrength balanced = builder.Build(squad, Tactics.Balanced);
            TeamStrength attacking = builder.Build(squad, new Tactics(Mentality.VeryAttacking, Tempo.Standard, Pressing.Standard));

            Assert.That(attacking.Attack, Is.GreaterThan(balanced.Attack));
            Assert.That(attacking.Defence, Is.LessThan(balanced.Defence));
        }

        [Test]
        public void Build_DefensiveMentality_LiftsDefenceAndThinsAttack()
        {
            var builder = new EffectiveStrengthBuilder();
            Squad squad = StandardSquad(60);

            TeamStrength balanced = builder.Build(squad, Tactics.Balanced);
            TeamStrength defensive = builder.Build(squad, new Tactics(Mentality.VeryDefensive, Tempo.Standard, Pressing.Standard));

            Assert.That(defensive.Defence, Is.GreaterThan(balanced.Defence));
            Assert.That(defensive.Attack, Is.LessThan(balanced.Attack));
        }

        [Test]
        public void Build_HighPress_LiftsMidfield()
        {
            var builder = new EffectiveStrengthBuilder();
            Squad squad = StandardSquad(60);

            TeamStrength balanced = builder.Build(squad, Tactics.Balanced);
            TeamStrength press = builder.Build(squad, new Tactics(Mentality.Balanced, Tempo.Standard, Pressing.Press));

            Assert.That(press.Midfield, Is.GreaterThan(balanced.Midfield));
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
