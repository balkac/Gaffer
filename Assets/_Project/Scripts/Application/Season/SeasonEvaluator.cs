using System.Collections.Generic;
using Gaffer.Domain.Clubs;

namespace Gaffer.Application.Season
{
    /// <summary>
    /// Judges a finished season for the managed club against the board target: its final position
    /// decides promotion, retention, or the sack (GDD §2 — "meet the target or get sacked").
    /// </summary>
    public sealed class SeasonEvaluator
    {
        public SeasonVerdict Evaluate(LeagueTable finalTable, ClubId managedClub, BoardTarget target)
        {
            int position = FindPosition(finalTable, managedClub);

            if (position <= target.PromotionPosition)
            {
                return SeasonVerdict.Promoted;
            }

            if (position <= target.SurvivalPosition)
            {
                return SeasonVerdict.Retained;
            }

            return SeasonVerdict.Sacked;
        }

        private static int FindPosition(LeagueTable finalTable, ClubId managedClub)
        {
            IReadOnlyList<LeagueTableRow> ordered = finalTable.Ordered();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Club == managedClub)
                {
                    return i + 1;
                }
            }

            return ordered.Count;
        }
    }
}
