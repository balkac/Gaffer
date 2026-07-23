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
    /// the ability, sell high. The scale comes from an injected <see cref="EconomySettings"/> (data-driven,
    /// NON-NEGOTIABLE #3); the shape (steep at the top, age-adjusted) is the point and stays in code.
    /// </summary>
    public static class PlayerValuation
    {
        public static long Value(Player player)
        {
            return Value(player, EconomySettings.Default);
        }

        /// <summary>Values against specific economy balance (from a config asset).</summary>
        public static long Value(Player player, EconomySettings economy)
        {
            double ability = PlayerRatings.ForRole(player) / 100.0;
            double raw = Math.Pow(Math.Max(0.0, ability), 3.0) * economy.ValuationCeiling * AgeMultiplier(player.Age, economy);
            long rounded = (long)Math.Round(raw / economy.ValuationRounding) * economy.ValuationRounding;
            return Math.Max(0, rounded);
        }

        // Value peaks in a player's mid-twenties and tails off with age; the young are a touch cheaper
        // (unproven), the old much cheaper (little resale, declining).
        private static double AgeMultiplier(int age, EconomySettings economy)
        {
            if (age <= 18)
            {
                return economy.ValueFactorTo18;
            }

            if (age <= 21)
            {
                return economy.ValueFactorTo21;
            }

            if (age <= 27)
            {
                return economy.ValueFactorTo27;
            }

            if (age <= 30)
            {
                return economy.ValueFactorTo30;
            }

            if (age <= 32)
            {
                return economy.ValueFactorTo32;
            }

            return economy.ValueFactorVeteran;
        }
    }
}
