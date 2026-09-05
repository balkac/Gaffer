using System;
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
            return Judge(FindPosition(finalTable, managedClub), target);
        }

        /// <summary>
        /// What finishing in a given 1-based position would earn against the target. The rule itself, in
        /// one place: the season screen paints the table's zones with it, so what the table shows as
        /// "promotion" is by construction what the board would call promotion — a screen that re-derived
        /// the two comparisons would be the one place they could drift apart.
        /// </summary>
        public static SeasonVerdict Judge(int position, BoardTarget target)
        {
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

        // The league position of a club that is not in the league has no sensible answer at all, so this
        // throws rather than returning one (CONVENTIONS §4 — "throw when there is no answer"). The old
        // fallback returned the last position, which flowed straight into SeasonVerdict.Sacked: a
        // mis-wired managed club ended the player's run and looked exactly like a real relegation.
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

            throw new ArgumentException(
                $"Club {managedClub.Value} is not in the final table ({ordered.Count} clubs), so it has no league position to judge.",
                nameof(managedClub));
        }
    }
}
