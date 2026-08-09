using Gaffer.Domain.Clubs;

namespace Gaffer.Application.Season
{
    /// <summary>One club's running record in a league table.</summary>
    public sealed class LeagueTableRow
    {
        public LeagueTableRow(ClubId club)
        {
            Club = club;
        }

        public ClubId Club { get; }

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
