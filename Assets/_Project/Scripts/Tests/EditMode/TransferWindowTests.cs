using Gaffer.Application.Transfers;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class TransferWindowTests
    {
        [Test]
        public void NextOpening_BeforeTheBreak_IsTheBreak_AndAfterIt_IsNothingThisSeason()
        {
            // What a closed market tells the manager: when it reopens. It must agree with At(), which is
            // why both live in one class — a screen promising week 19 while At() opens at 18 would be a
            // promise the game breaks.
            Assert.That(TransferWindow.NextOpening(0, 38), Is.EqualTo(19));
            Assert.That(TransferWindow.NextOpening(5, 38), Is.EqualTo(19));
            Assert.That(TransferWindow.At(19, 38), Is.EqualTo(TransferWindowPhase.Winter), "the promised round is a real window");
            Assert.That(TransferWindow.NextOpening(19, 38), Is.Null, "during the winter window there is no NEXT one this season");
            Assert.That(TransferWindow.NextOpening(30, 38), Is.Null);
            Assert.That(TransferWindow.NextOpening(0, 0), Is.Null, "a season with no rounds has no window to wait for");
        }

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
