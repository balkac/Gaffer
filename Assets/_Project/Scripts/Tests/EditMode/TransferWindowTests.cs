using Gaffer.Application.Transfers;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class TransferWindowTests
    {
        // A 20-club double round-robin is 38 rounds; the winter break is at the halfway point (19).
        private const int RoundCount = 38;

        [Test]
        public void At_BeforeKickoff_IsSummer()
        {
            Assert.That(TransferWindow.At(0, RoundCount), Is.EqualTo(TransferWindowPhase.Summer));
        }

        [Test]
        public void At_Halfway_IsWinter()
        {
            Assert.That(TransferWindow.At(RoundCount / 2, RoundCount), Is.EqualTo(TransferWindowPhase.Winter));
        }

        [Test]
        public void At_MidFirstHalf_IsClosed()
        {
            Assert.That(TransferWindow.At(5, RoundCount), Is.EqualTo(TransferWindowPhase.Closed));
        }

        [Test]
        public void At_LateSeason_IsClosed()
        {
            Assert.That(TransferWindow.At(30, RoundCount), Is.EqualTo(TransferWindowPhase.Closed));
        }

        [Test]
        public void IsOpen_OnlyInSummerAndWinter()
        {
            Assert.That(TransferWindow.IsOpen(0, RoundCount), Is.True);
            Assert.That(TransferWindow.IsOpen(RoundCount / 2, RoundCount), Is.True);
            Assert.That(TransferWindow.IsOpen(1, RoundCount), Is.False);
            Assert.That(TransferWindow.IsOpen(RoundCount - 1, RoundCount), Is.False);
        }
    }
}
