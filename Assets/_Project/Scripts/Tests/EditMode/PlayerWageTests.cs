using Gaffer.Application.Transfers;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class PlayerWageTests
    {
        private static Player Forward(byte level)
        {
            var attributes = new Attributes
            {
                Finishing = level, Pace = level, Technique = level, Positioning = level, Dribbling = level,
            };
            return new Player(new PlayerId(1), "Test Forward", "England", Position.Forward, 25, attributes, 70);
        }

        [Test]
        public void Weekly_HigherAbility_CommandsMore()
        {
            long squadPlayer = PlayerWage.Weekly(Forward(50));
            long star = PlayerWage.Weekly(Forward(88));

            Assert.That(star, Is.GreaterThan(squadPlayer));
        }

        [Test]
        public void Weekly_VeryLowAbility_StillHasAFloor()
        {
            long wage = PlayerWage.Weekly(Forward(5));

            Assert.That(wage, Is.GreaterThanOrEqualTo(500));
        }

        [Test]
        public void Weekly_IsRounded()
        {
            long wage = PlayerWage.Weekly(Forward(63));

            Assert.That(wage % 500, Is.EqualTo(0));
        }
    }
}
