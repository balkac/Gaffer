using Gaffer.Application.Season;
using Gaffer.Presentation.Season;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The lines through the league table. Held here because the failure is silent: a line under the
    /// wrong row draws a club in the wrong zone on a table that otherwise reads perfectly.
    /// </summary>
    public sealed class TableZonesTests
    {
        private static readonly BoardTarget Target = new BoardTarget(promotionPosition: 2, survivalPosition: 6);
        private const int Clubs = 8;

        [Test]
        public void Of_AgreesWithTheBoardsOwnJudgement()
        {
            // The whole point of asking the evaluator: the table cannot disagree with the verdict.
            Assert.That(TableZones.Of(1, Target), Is.EqualTo(SeasonVerdict.Promoted));
            Assert.That(TableZones.Of(2, Target), Is.EqualTo(SeasonVerdict.Promoted));
            Assert.That(TableZones.Of(3, Target), Is.EqualTo(SeasonVerdict.Retained));
            Assert.That(TableZones.Of(6, Target), Is.EqualTo(SeasonVerdict.Retained));
            Assert.That(TableZones.Of(7, Target), Is.EqualTo(SeasonVerdict.Sacked));
        }

        [Test]
        public void LineBelow_FallsUnderTheLastPositionOfEachZone_AndNowhereElse()
        {
            for (int position = 1; position <= Clubs; position++)
            {
                bool expected = position == Target.PromotionPosition || position == Target.SurvivalPosition;
                Assert.That(TableZones.LineBelow(position, Target, Clubs), Is.EqualTo(expected), "position " + position);
            }
        }

        [Test]
        public void LineBelow_IsNeverDrawnUnderTheLastRow()
        {
            // A survival line that coincides with the bottom of the table separates the table from
            // nothing: everybody stays up, and there is no zone below to mark.
            var everybodySafe = new BoardTarget(promotionPosition: 2, survivalPosition: Clubs);
            Assert.That(TableZones.LineBelow(Clubs, everybodySafe, Clubs), Is.False);
        }

        [Test]
        public void LineBelow_DrawsOneLineWhenTheTwoNumbersCoincide()
        {
            // Promotion and survival at the same position: one boundary, one line. Two lines under one
            // row would be the same fact drawn twice.
            var coinciding = new BoardTarget(promotionPosition: 3, survivalPosition: 3);
            int lines = 0;
            for (int position = 1; position <= Clubs; position++)
            {
                if (TableZones.LineBelow(position, coinciding, Clubs))
                {
                    lines++;
                }
            }

            Assert.That(lines, Is.EqualTo(1));
        }
    }
}
