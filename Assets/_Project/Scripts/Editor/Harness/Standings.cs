using System.Collections.Generic;

namespace Gaffer.Editor.Harness
{
    /// <summary>A season's league table: accumulates results by team and orders them by the usual tiebreaks.</summary>
    public sealed class Standings
    {
        private readonly List<StandingsRow> _rows;
        private readonly Dictionary<int, StandingsRow> _byRank;

        public Standings(IReadOnlyList<TeamProfile> teams)
        {
            _rows = new List<StandingsRow>(teams.Count);
            _byRank = new Dictionary<int, StandingsRow>(teams.Count);
            foreach (TeamProfile team in teams)
            {
                var row = new StandingsRow(team);
                _rows.Add(row);
                _byRank[team.Rank] = row;
            }
        }

        public StandingsRow ForRank(int rank)
        {
            return _byRank[rank];
        }

        public IReadOnlyList<StandingsRow> Ordered()
        {
            var ordered = new List<StandingsRow>(_rows);
            ordered.Sort(CompareForTable);
            return ordered;
        }

        private static int CompareForTable(StandingsRow left, StandingsRow right)
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

            // Deterministic final tiebreak so identical seeds always order the same.
            return left.Team.Rank.CompareTo(right.Team.Rank);
        }
    }
}
