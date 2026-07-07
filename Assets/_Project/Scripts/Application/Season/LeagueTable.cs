using System.Collections.Generic;
using Gaffer.Domain.Clubs;

namespace Gaffer.Application.Season
{
    /// <summary>
    /// A league table: records each match by club and orders the rows by the usual tiebreaks
    /// (points, goal difference, goals for, then club id for a deterministic final order).
    /// </summary>
    public sealed class LeagueTable
    {
        private readonly List<LeagueTableRow> _rows;
        private readonly Dictionary<ClubId, LeagueTableRow> _byClub;

        public LeagueTable(IEnumerable<ClubId> clubs)
        {
            _rows = new List<LeagueTableRow>();
            _byClub = new Dictionary<ClubId, LeagueTableRow>();
            foreach (ClubId club in clubs)
            {
                var row = new LeagueTableRow(club);
                _rows.Add(row);
                _byClub[club] = row;
            }
        }

        public void RecordMatch(ClubId home, ClubId away, int homeGoals, int awayGoals)
        {
            _byClub[home].RecordResult(homeGoals, awayGoals);
            _byClub[away].RecordResult(awayGoals, homeGoals);
        }

        public IReadOnlyList<LeagueTableRow> Ordered()
        {
            var ordered = new List<LeagueTableRow>(_rows);
            ordered.Sort(CompareForTable);
            return ordered;
        }

        private static int CompareForTable(LeagueTableRow left, LeagueTableRow right)
        {
            if (left.Points != right.Points)
            {
                return right.Points.CompareTo(left.Points);
            }

            if (left.GoalDifference != right.GoalDifference)
            {
                return right.GoalDifference.CompareTo(left.GoalDifference);
            }

            if (left.GoalsFor != right.GoalsFor)
            {
                return right.GoalsFor.CompareTo(left.GoalsFor);
            }

            return left.Club.Value.CompareTo(right.Club.Value);
        }
    }
}
