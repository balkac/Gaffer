using System.Collections.Generic;
using Gaffer.Application.Season;
using Gaffer.Domain.Clubs;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class SeasonEvaluatorTests
    {
        private const int ClubCount = 20;

        // Builds a table where club k finishes exactly at position k+1: every lower id beats every
        // higher id, so points strictly decrease with the id.
        private static LeagueTable BuildRankedTable()
        {
            var clubs = new List<ClubId>(ClubCount);
            for (int i = 0; i < ClubCount; i++)
            {
                clubs.Add(new ClubId(i));
            }

            var table = new LeagueTable(clubs);
            for (int high = 0; high < ClubCount; high++)
            {
                for (int low = high + 1; low < ClubCount; low++)
                {
                    table.RecordMatch(new ClubId(high), new ClubId(low), 1, 0);
                }
            }

            return table;
        }

        [Test]
        public void Evaluate_ChampionUnderPromotionTarget_IsPromoted()
        {
            SeasonVerdict verdict = new SeasonEvaluator().Evaluate(BuildRankedTable(), new ClubId(0), new BoardTarget(2, 17));

            Assert.That(verdict, Is.EqualTo(SeasonVerdict.Promoted));
        }

        [Test]
        public void Evaluate_MidTableWithinSurvival_IsRetained()
        {
            SeasonVerdict verdict = new SeasonEvaluator().Evaluate(BuildRankedTable(), new ClubId(9), new BoardTarget(2, 17));

            Assert.That(verdict, Is.EqualTo(SeasonVerdict.Retained));
        }

        [Test]
        public void Evaluate_ExactlyAtSurvivalPosition_IsRetained()
        {
            // Club 16 finishes 17th, survival threshold 17 -> retained.
            SeasonVerdict verdict = new SeasonEvaluator().Evaluate(BuildRankedTable(), new ClubId(16), new BoardTarget(2, 17));

            Assert.That(verdict, Is.EqualTo(SeasonVerdict.Retained));
        }

        [Test]
        public void Evaluate_BelowSurvivalPosition_IsSacked()
        {
            // Club 17 finishes 18th, survival threshold 17 -> sacked.
            SeasonVerdict verdict = new SeasonEvaluator().Evaluate(BuildRankedTable(), new ClubId(17), new BoardTarget(2, 17));

            Assert.That(verdict, Is.EqualTo(SeasonVerdict.Sacked));
        }
    }
}
