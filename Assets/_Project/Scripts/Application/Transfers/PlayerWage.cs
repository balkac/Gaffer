using System;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Transfers
{
    /// <summary>
    /// A player's weekly wage — the recurring cost that keeps the run economy tense (GDD §4.4, the CM 01/02
    /// two-budget model: a fee is one-off, wages bite every week). Driven by current ability for the role:
    /// a star commands far more than a squad player, so you cannot simply hoard talent — the wage budget,
    /// not the transfer fee, is usually what stops you. The scale tunes into a BalanceSO.
    /// </summary>
    public static class PlayerWage
    {
        private const double MaxWeekly = 20_000.0;
        private const int RoundTo = 500;
        private const long Floor = 500;

        public static long Weekly(Player player)
        {
            double ability = PlayerRatings.ForRole(player) / 100.0;
            double raw = Math.Pow(Math.Max(0.0, ability), 2.0) * MaxWeekly;
            long rounded = (long)Math.Round(raw / RoundTo) * RoundTo;
            return Math.Max(Floor, rounded);
        }
    }
}
