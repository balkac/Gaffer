using System;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Transfers
{
    /// <summary>
    /// A player's weekly wage — the recurring cost that keeps the run economy tense (GDD §4.4, the CM 01/02
    /// two-budget model: a fee is one-off, wages bite every week). Driven by current ability for the role:
    /// a star commands far more than a squad player, so you cannot simply hoard talent — the wage budget,
    /// not the transfer fee, is usually what stops you. The scale comes from an injected
    /// <see cref="EconomySettings"/> (data-driven, NON-NEGOTIABLE #3).
    /// </summary>
    public static class PlayerWage
    {
        public static long Weekly(Player player)
        {
            return Weekly(player, EconomySettings.Default);
        }

        /// <summary>Prices against specific economy balance (from a config asset).</summary>
        public static long Weekly(Player player, EconomySettings economy)
        {
            double ability = PlayerRatings.ForRole(player) / 100.0;
            double raw = Math.Pow(Math.Max(0.0, ability), 2.0) * economy.WageCeiling;
            int step = RoundingStep(economy.WageRounding);
            long rounded = (long)Math.Round(raw / step) * step;
            return Math.Max(economy.WageFloor, rounded);
        }

        // The rounding step is a config-editable divisor, and a zero one does not throw: raw/0 is
        // Infinity and the cast to long collapses every wage to the floor, silently (CONVENTIONS §6).
        // A step below one currency unit has no meaning, so 1 is the true floor and the calibrated 500
        // passes through untouched.
        private static int RoundingStep(int configured)
        {
            return configured > 0 ? configured : 1;
        }
    }
}
