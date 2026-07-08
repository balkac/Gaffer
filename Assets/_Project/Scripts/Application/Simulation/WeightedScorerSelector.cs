using System;
using System.Collections.Generic;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Credits a goal to a weighted-random player: a striker's finishing and positioning make him far the
    /// likeliest scorer, a midfielder less so, a defender rarely, a keeper almost never — but a small floor
    /// keeps the odd centre-back header alive. One rng draw per goal, so the same seed names the same
    /// scorers. The weights tune into a BalanceSO later.
    /// </summary>
    public sealed class WeightedScorerSelector : IScorerSelector
    {
        private const double MinWeight = 0.5;

        public PlayerId? SelectScorer(Squad squad, IRandom rng)
        {
            if (squad == null || squad.Count == 0)
            {
                return null;
            }

            IReadOnlyList<Player> players = squad.Players;

            double total = 0.0;
            for (int i = 0; i < players.Count; i++)
            {
                total += Weight(players[i]);
            }

            double roll = rng.NextDouble() * total;
            double cumulative = 0.0;
            for (int i = 0; i < players.Count; i++)
            {
                cumulative += Weight(players[i]);
                if (roll < cumulative)
                {
                    return players[i].Id;
                }
            }

            return players[players.Count - 1].Id;
        }

        private static double Weight(Player player)
        {
            Attributes a = player.Attributes;
            double scoring = (0.6 * a.Finishing) + (0.2 * a.Positioning) + (0.2 * a.Pace);
            return Math.Max(MinWeight, scoring * RoleMultiplier(player.Position));
        }

        private static double RoleMultiplier(Position position)
        {
            switch (position)
            {
                case Position.Forward:
                    return 1.0;
                case Position.Midfielder:
                    return 0.55;
                case Position.Defender:
                    return 0.18;
                case Position.Goalkeeper:
                    return 0.01;
                default:
                    return 0.3;
            }
        }
    }
}
