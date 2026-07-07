using Gaffer.Application.Simulation;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class MatchContextTests
    {
        [Test]
        public void Constructor_Fields_RoundTrip()
        {
            var context = new MatchContext(MatchImportance.Derby, 45000, isTitleDecider: true, isRivalry: true);

            Assert.That(context.Importance, Is.EqualTo(MatchImportance.Derby));
            Assert.That(context.CrowdSize, Is.EqualTo(45000));
            Assert.That(context.IsTitleDecider, Is.True);
            Assert.That(context.IsRivalry, Is.True);
        }

        [Test]
        public void Constructor_RoutineFixture_DefaultsAreCarried()
        {
            var context = new MatchContext(MatchImportance.Normal, 8000, isTitleDecider: false, isRivalry: false);

            Assert.That(context.Importance, Is.EqualTo(MatchImportance.Normal));
            Assert.That(context.IsTitleDecider, Is.False);
            Assert.That(context.IsRivalry, Is.False);
        }
    }
}
