namespace Gaffer.Tools.SeasonHarness
{
    /// <summary>One team's running record within a single season's table.</summary>
    public sealed class StandingsRow
    {
        public StandingsRow(TeamProfile team)
        {
            Team = team;
        }

        public TeamProfile Team { get; }

        public int Played { get; private set; }

        public int Won { get; private set; }

        public int Drawn { get; private set; }

        public int Lost { get; private set; }

        public int GoalsFor { get; private set; }

        public int GoalsAgainst { get; private set; }

        public int GoalDifference => GoalsFor - GoalsAgainst;

        public int Points => (Won * 3) + Drawn;

        public void RecordResult(int goalsFor, int goalsAgainst)
        {
            Played++;
            GoalsFor += goalsFor;
            GoalsAgainst += goalsAgainst;

            if (goalsFor > goalsAgainst)
            {
                Won++;
            }
            else if (goalsFor < goalsAgainst)
            {
                Lost++;
            }
            else
            {
                Drawn++;
            }
        }
    }
}
