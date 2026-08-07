using Gaffer.Application.Simulation;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class MatchContextTests
    {
        [Test]
        public void Constructor_TwoAdjacentBooleans_LandInTheirOwnProperties()
        {
            // MatchContext is a data carrier, so the only thing its constructor can get wrong is which
            // argument goes where — and `isTitleDecider` and `isRivalry` are adjacent bools of the same
            // type, the one pairing the compiler cannot catch if they are swapped in the body. Context
            // decides which traits fire on the occasion (see TraitTests), so a swap would silently fire
            // derby traits in title deciders. They are therefore given DIFFERENT values here; the old
            // pair of tests passed true/true and false/false, under which a swap is invisible.
            var decider = new MatchContext(MatchImportance.Derby, 45000, isTitleDecider: true, isRivalry: false);

            Assert.That(decider.Importance, Is.EqualTo(MatchImportance.Derby));
            Assert.That(decider.CrowdSize, Is.EqualTo(45000));
            Assert.That(decider.IsTitleDecider, Is.True);
            Assert.That(decider.IsRivalry, Is.False);

            var derby = new MatchContext(MatchImportance.Normal, 8000, isTitleDecider: false, isRivalry: true);

            Assert.That(derby.Importance, Is.EqualTo(MatchImportance.Normal));
            Assert.That(derby.CrowdSize, Is.EqualTo(8000));
            Assert.That(derby.IsTitleDecider, Is.False);
            Assert.That(derby.IsRivalry, Is.True);
        }

        // DELETED: Constructor_RoutineFixture_DefaultsAreCarried. It was named for defaults on a type
        // that has none — every member is a required constructor argument — and its three assertions
        // were a strict subset of the test above it.
    }
}
