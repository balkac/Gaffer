using System;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Transfers
{
    /// <summary>
    /// A player's market value — what the transfer market thinks he is worth (GDD §4.4). Driven by his
    /// current ability for his role and his age, not his hidden potential: the market cannot see the
    /// ceiling a scout estimates, so a young gem sits cheap until he grows into it. That gap is the
    /// discover-grow-sell flip's whole reward — buy on scouted potential the market has not priced, raise
    /// the ability, sell high. The scale tunes into a BalanceSO; the shape (steep at the top, age-adjusted)
    /// is the point.
    /// </summary>
    public static class PlayerValuation
    {
        private const double MaxValue = 40_000_000.0;
        private const int RoundTo = 50_000;

        public static long Value(Player player)
        {
            double ability = PlayerRatings.ForRole(player) / 100.0;
            double raw = Math.Pow(Math.Max(0.0, ability), 3.0) * MaxValue * AgeMultiplier(player.Age);
            long rounded = (long)Math.Round(raw / RoundTo) * RoundTo;
            return Math.Max(0, rounded);
        }

        // Value peaks in a player's mid-twenties and tails off with age; the young are a touch cheaper
        // (unproven), the old much cheaper (little resale, declining).
        private static double AgeMultiplier(int age)
        {
            if (age <= 18)
            {
                return 0.80;
            }

            if (age <= 21)
            {
                return 0.92;
            }

            if (age <= 27)
            {
                return 1.0;
            }

            if (age <= 30)
            {
                return 0.82;
            }

            if (age <= 32)
            {
                return 0.58;
            }

            return 0.32;
        }
    }
}
