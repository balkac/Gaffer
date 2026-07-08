using System;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class PlayerRolesTests
    {
        [Test]
        public void Line_MapsEachRoleToItsBroadPosition()
        {
            Assert.That(PlayerRoles.Line(PlayerRole.Goalkeeper), Is.EqualTo(Position.Goalkeeper));
            Assert.That(PlayerRoles.Line(PlayerRole.RightBack), Is.EqualTo(Position.Defender));
            Assert.That(PlayerRoles.Line(PlayerRole.CentreBack), Is.EqualTo(Position.Defender));
            Assert.That(PlayerRoles.Line(PlayerRole.DefensiveMidfield), Is.EqualTo(Position.Midfielder));
            Assert.That(PlayerRoles.Line(PlayerRole.LeftMidfield), Is.EqualTo(Position.Midfielder));
            Assert.That(PlayerRoles.Line(PlayerRole.RightWing), Is.EqualTo(Position.Forward));
            Assert.That(PlayerRoles.Line(PlayerRole.Striker), Is.EqualTo(Position.Forward));
        }

        [Test]
        public void Representative_RoundTripsThroughLine()
        {
            foreach (Position position in (Position[])Enum.GetValues(typeof(Position)))
            {
                PlayerRole role = PlayerRoles.Representative(position);
                Assert.That(PlayerRoles.Line(role), Is.EqualTo(position));
            }
        }

        [Test]
        public void Player_FromRole_DerivesTheMatchingLine()
        {
            var attributes = new Attributes { Pace = 60 };
            var player = new Player(new PlayerId(1), "Test", "England", PlayerRole.LeftBack, 24, attributes, 70);

            Assert.That(player.Role, Is.EqualTo(PlayerRole.LeftBack));
            Assert.That(player.Position, Is.EqualTo(Position.Defender));
        }

        [Test]
        public void Abbrev_IsShortAndDistinctPerRole()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (PlayerRole role in (PlayerRole[])Enum.GetValues(typeof(PlayerRole)))
            {
                string abbrev = PlayerRoles.Abbrev(role);
                Assert.That(abbrev, Is.Not.Empty);
                Assert.That(seen.Add(abbrev), Is.True, $"Abbreviation '{abbrev}' is used by more than one role.");
            }
        }
    }
}
