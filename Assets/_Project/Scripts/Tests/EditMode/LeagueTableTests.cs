using System.Collections.Generic;
using Gaffer.Application.Season;
using Gaffer.Domain.Clubs;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The table's four-level comparator: points, then goal difference, then goals for, then the lower
    /// club id. Every level is exercised in isolation here, and the last one — the id tie-break the
    /// class doc sells as its determinism guarantee — needs a genuine tie to reach at all, which no
    /// other test in the suite produces (<c>SeasonEvaluatorTests</c> builds strictly-ordered tables on
    /// purpose).
    /// </summary>
    public sealed class LeagueTableTests
    {
        // Registration order is deliberately NOT id order in every fixture below. A sort that merely
        // preserved input order would satisfy an "ordered by id" assertion by accident; entering the
        // clubs backwards means only a comparator that really reads the id can pass.
        private static LeagueTable TableOf(params int[] clubIds)
        {
            var clubs = new List<ClubId>(clubIds.Length);
            foreach (int id in clubIds)
            {
                clubs.Add(new ClubId(id));
            }

            return new LeagueTable(clubs);
        }

        private static IReadOnlyList<int> OrderOf(LeagueTable table)
        {
            IReadOnlyList<LeagueTableRow> rows = table.Ordered();
            var ids = new List<int>(rows.Count);
            foreach (LeagueTableRow row in rows)
            {
                ids.Add(row.Club.Value);
            }

            return ids;
        }

        [Test]
        public void RecordMatch_AWin_GivesThreePointsToTheWinnerAndNoneToTheLoser()
        {
            LeagueTable table = TableOf(0, 1);

            table.RecordMatch(new ClubId(0), new ClubId(1), 2, 1);

            IReadOnlyList<LeagueTableRow> rows = table.Ordered();
            Assert.That(rows[0].Club.Value, Is.EqualTo(0));
            Assert.That(rows[0].Points, Is.EqualTo(3));
            Assert.That(rows[0].Won, Is.EqualTo(1));
            Assert.That(rows[0].GoalsFor, Is.EqualTo(2));
            Assert.That(rows[0].GoalsAgainst, Is.EqualTo(1));
            Assert.That(rows[1].Points, Is.Zero);
            Assert.That(rows[1].Lost, Is.EqualTo(1));
            Assert.That(rows[1].GoalsFor, Is.EqualTo(1));
            Assert.That(rows[1].GoalsAgainst, Is.EqualTo(2));
        }

        [Test]
        public void RecordMatch_ADraw_GivesOnePointToBothSides()
        {
            LeagueTable table = TableOf(0, 1);

            table.RecordMatch(new ClubId(0), new ClubId(1), 1, 1);

            foreach (LeagueTableRow row in table.Ordered())
            {
                Assert.That(row.Points, Is.EqualTo(1));
                Assert.That(row.Drawn, Is.EqualTo(1));
                Assert.That(row.Played, Is.EqualTo(1));
            }
        }

        [Test]
        public void Ordered_FewerPointsButFarBetterGoalDifference_StillRanksBelow()
        {
            // Points are the first key and nothing below it can overturn them. Club 1 grinds out two 1-0
            // wins (6 points, +2); club 0 wins once 6-0 (3 points, +6). The three-times-better goal
            // difference must not lift club 0 above club 1.
            LeagueTable table = TableOf(3, 2, 1, 0);
            table.RecordMatch(new ClubId(0), new ClubId(2), 6, 0);
            table.RecordMatch(new ClubId(1), new ClubId(2), 1, 0);
            table.RecordMatch(new ClubId(1), new ClubId(3), 1, 0);

            IReadOnlyList<LeagueTableRow> rows = table.Ordered();
            Assert.That(rows[0].Club.Value, Is.EqualTo(1));
            Assert.That(rows[0].Points, Is.EqualTo(6));
            Assert.That(rows[1].Club.Value, Is.EqualTo(0));
            Assert.That(rows[1].Points, Is.EqualTo(3));
            Assert.That(rows[1].GoalDifference, Is.GreaterThan(rows[0].GoalDifference),
                "The fixture is pointless unless the lower-ranked club really has the better goal difference.");
            Assert.That(OrderOf(table), Is.EqualTo(new[] { 1, 0, 3, 2 }).AsCollection);
        }

        [Test]
        public void Ordered_EqualPoints_RanksByGoalDifference()
        {
            LeagueTable table = TableOf(2, 1, 0);
            table.RecordMatch(new ClubId(0), new ClubId(2), 5, 0); // club 0: 3 pts, GD +5
            table.RecordMatch(new ClubId(1), new ClubId(2), 1, 0); // club 1: 3 pts, GD +1

            Assert.That(OrderOf(table), Is.EqualTo(new[] { 0, 1, 2 }).AsCollection);
        }

        [Test]
        public void Ordered_EqualPointsAndGoalDifference_RanksByGoalsFor()
        {
            // Both leaders sit on 3 points and +1. The one that scored more goes above, even though it
            // also conceded more — goals for, not goals against, is the third key.
            LeagueTable table = TableOf(3, 2, 1, 0);
            table.RecordMatch(new ClubId(0), new ClubId(2), 4, 3); // club 0: 3 pts, GD +1, GF 4
            table.RecordMatch(new ClubId(1), new ClubId(3), 1, 0); // club 1: 3 pts, GD +1, GF 1

            IReadOnlyList<LeagueTableRow> rows = table.Ordered();
            Assert.That(rows[0].Club.Value, Is.EqualTo(0));
            Assert.That(rows[1].Club.Value, Is.EqualTo(1));
            Assert.That(rows[0].Points, Is.EqualTo(rows[1].Points));
            Assert.That(rows[0].GoalDifference, Is.EqualTo(rows[1].GoalDifference));
            Assert.That(rows[0].GoalsFor, Is.GreaterThan(rows[1].GoalsFor));
        }

        [Test]
        public void Ordered_ClubsTiedOnEveryStatistic_RanksByTheLowerClubId()
        {
            // The tie-break the class doc calls its determinism guarantee, and the only one the rest of
            // the suite never reaches. Clubs 0 and 3 are indistinguishable — same points, same goal
            // difference, same goals for — as are clubs 1 and 2. Entered backwards (3,2,1,0), so the
            // expected order is the reverse of the input and cannot come from input order.
            LeagueTable table = TableOf(3, 2, 1, 0);
            table.RecordMatch(new ClubId(0), new ClubId(1), 2, 1);
            table.RecordMatch(new ClubId(3), new ClubId(2), 2, 1);

            IReadOnlyList<LeagueTableRow> rows = table.Ordered();
            for (int i = 0; i < 2; i++)
            {
                Assert.That(rows[i].Points, Is.EqualTo(3));
                Assert.That(rows[i].GoalDifference, Is.EqualTo(1));
                Assert.That(rows[i].GoalsFor, Is.EqualTo(2));
            }

            Assert.That(OrderOf(table), Is.EqualTo(new[] { 0, 3, 1, 2 }).AsCollection,
                "Clubs level on points, goal difference and goals for must order by the lower club id.");
        }

        [Test]
        public void Ordered_CalledRepeatedlyOnATableFullOfTies_ReturnsTheSameOrderEveryTime()
        {
            // Ordered() sorts with List<T>.Sort, which is an unstable introsort — with a genuine tie
            // anywhere in the comparator the order would be an implementation detail of the sort. The id
            // tie-break is what makes the comparator a total order, and this is the property that buys:
            // no two calls, and no two runs, can disagree (NON-NEGOTIABLE #2).
            LeagueTable table = TableOf(9, 8, 7, 6, 5, 4, 3, 2, 1, 0);
            IReadOnlyList<int> first = OrderOf(table);

            Assert.That(first, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }).AsCollection,
                "An untouched table is a ten-way tie, so it must come back in club-id order.");
            for (int i = 0; i < 5; i++)
            {
                Assert.That(OrderOf(table), Is.EqualTo(first).AsCollection);
            }
        }

        [Test]
        public void Ordered_DoesNotExposeTheTablesOwnRowList()
        {
            // Callers sort/hold the returned list; handing out the live list would let a caller reorder
            // the table itself.
            LeagueTable table = TableOf(0, 1);

            Assert.That(table.Ordered(), Is.Not.SameAs(table.Ordered()));
        }
    }
}
