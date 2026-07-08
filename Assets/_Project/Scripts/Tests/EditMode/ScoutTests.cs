using Gaffer.Application.Transfers;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class ScoutTests
    {
        private static Player Prospect(byte potential)
        {
            var attributes = new Attributes
            {
                Finishing = 62, Pace = 71, Technique = 58, Positioning = 66, Dribbling = 54,
            };
            return new Player(new PlayerId(9), "Test Prospect", "England", Position.Forward, 18, attributes, potential);
        }

        [Test]
        public void Observe_FullAccuracy_IsExact()
        {
            Player player = Prospect(88);

            ScoutReport report = new Scout().Observe(player, 1.0);

            Assert.That(report.PotentialLow, Is.EqualTo(88));
            Assert.That(report.PotentialHigh, Is.EqualTo(88));
            foreach (AttributeEstimate estimate in report.KeyAttributes)
            {
                Assert.That(estimate.Low, Is.EqualTo(estimate.High), "A fully scouted attribute is a single value.");
            }
        }

        [Test]
        public void Observe_LowAccuracy_BandAlwaysContainsTheTruth()
        {
            Player player = Prospect(88);

            ScoutReport report = new Scout().Observe(player, 0.0);

            Assert.That(report.PotentialLow, Is.LessThanOrEqualTo(88));
            Assert.That(report.PotentialHigh, Is.GreaterThanOrEqualTo(88));

            // Finishing is the forward's first key attribute; its true value is 62.
            AttributeEstimate finishing = report.KeyAttributes[0];
            Assert.That(finishing.Label, Is.EqualTo("FIN"));
            Assert.That(finishing.Low, Is.LessThanOrEqualTo(62));
            Assert.That(finishing.High, Is.GreaterThanOrEqualTo(62));
        }

        [Test]
        public void Observe_HigherAccuracy_NarrowsTheBand()
        {
            Player player = Prospect(80);
            var scout = new Scout();

            ScoutReport vague = scout.Observe(player, 0.2);
            ScoutReport sharp = scout.Observe(player, 0.9);

            int vagueWidth = vague.PotentialHigh - vague.PotentialLow;
            int sharpWidth = sharp.PotentialHigh - sharp.PotentialLow;
            Assert.That(sharpWidth, Is.LessThan(vagueWidth));
        }

        [Test]
        public void Observe_SamePlayerAndAccuracy_IsDeterministic()
        {
            Player player = Prospect(75);
            var scout = new Scout();

            ScoutReport first = scout.Observe(player, 0.4);
            ScoutReport second = scout.Observe(player, 0.4);

            Assert.That(first.PotentialLow, Is.EqualTo(second.PotentialLow));
            Assert.That(first.PotentialHigh, Is.EqualTo(second.PotentialHigh));
            Assert.That(first.KeyAttributes.Count, Is.EqualTo(second.KeyAttributes.Count));
            for (int i = 0; i < first.KeyAttributes.Count; i++)
            {
                Assert.That(first.KeyAttributes[i].Low, Is.EqualTo(second.KeyAttributes[i].Low));
                Assert.That(first.KeyAttributes[i].High, Is.EqualTo(second.KeyAttributes[i].High));
            }
        }

        [Test]
        public void Observe_KeyAttributes_CoverTheRole()
        {
            Player player = Prospect(70);

            ScoutReport report = new Scout().Observe(player, 0.5);

            Assert.That(report.KeyAttributes.Count, Is.EqualTo(RoleKeyAttributes.For(Position.Forward).Count));
        }
    }
}
