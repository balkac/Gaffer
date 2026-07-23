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
            long rounded = (long)Math.Round(raw / economy.WageRounding) * economy.WageRounding;
            return Math.Max(economy.WageFloor, rounded);
        }
    }
}
