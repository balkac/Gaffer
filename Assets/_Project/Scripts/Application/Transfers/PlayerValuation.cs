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
            int step = RoundingStep(economy.ValuationRounding);
            long rounded = (long)Math.Round(raw / step) * step;
            return Math.Max(0, rounded);
        }

        // The rounding step is a config-editable divisor, and a zero one does not throw: raw/0 is
        // Infinity, the cast to long collapses it, and every player in the game is suddenly worth 0 —
        // free transfers, with nothing raised at the source (CONVENTIONS §6). A step below one currency
        // unit has no meaning, so 1 is the true floor and the calibrated 50 000 passes through untouched.
        private static int RoundingStep(int configured)
        {
            return configured > 0 ? configured : 1;
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
