using System;
using System.Collections.Generic;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Credits a goal to a weighted-random player through two pathways: open play (finishing, positioning,
    /// pace) where a striker dominates, and the air (heading, jumping, strength) where a target man and a
    /// tall centre-back both threaten — that second pathway is how a defender scores from a corner. So a
    /// striker is far the likeliest, a midfielder less so, a defender occasionally (mostly with his head),
    /// a keeper almost never — but not never: a keeper up for a last-minute corner is a rare, legendary
    /// beat the game wants to keep possible. One rng draw per goal, so the same seed names the same
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
            double openPlay = ((0.6 * a.Finishing) + (0.2 * a.Positioning) + (0.2 * a.Pace)) * OpenPlayRole(player.Position);
            double aerial = ((0.6 * a.Heading) + (0.25 * a.Jumping) + (0.15 * a.Strength)) * AerialRole(player.Position);
            double weight = openPlay + aerial;

            // The floor keeps every outfielder a live threat; a keeper is exempt so his goal stays a
            // once-in-many-seasons event, not a regular one.
            return player.Position == Position.Goalkeeper ? weight : Math.Max(MinWeight, weight);
        }

        // Open play favours strikers heavily; the air gives defenders a real set-piece threat while still
        // rewarding target men. Defence scores mostly through the aerial pathway (corner headers).
        private static double OpenPlayRole(Position position)
        {
            switch (position)
            {
                case Position.Forward:
                    return 1.0;
                case Position.Midfielder:
                    return 0.55;
                case Position.Defender:
                    return 0.10;
                case Position.Goalkeeper:
                    return 0.003;
                default:
                    return 0.3;
            }
        }

        private static double AerialRole(Position position)
        {
            switch (position)
            {
                case Position.Forward:
                    return 0.45;
                case Position.Midfielder:
                    return 0.25;
                case Position.Defender:
                    return 0.30;
                case Position.Goalkeeper:
                    return 0.004;
                default:
                    return 0.2;
            }
        }
    }
}
