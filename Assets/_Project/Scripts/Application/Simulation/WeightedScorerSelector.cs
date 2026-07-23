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
    /// scorers. The weights come from an injected <see cref="ScorerWeights"/> (data-driven, NON-NEGOTIABLE #3).
    /// </summary>
    public sealed class WeightedScorerSelector : IScorerSelector
    {
        private readonly ScorerWeights _weights;

        public WeightedScorerSelector()
            : this(ScorerWeights.Default)
        {
        }

        /// <summary>Selects with specific attribution balance (from a config asset). Null falls back
        /// to the calibrated defaults.</summary>
        public WeightedScorerSelector(ScorerWeights weights)
        {
            _weights = weights ?? ScorerWeights.Default;
        }

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

        private double Weight(Player player)
        {
            ScorerWeights w = _weights;
            Attributes a = player.Attributes;
            double openPlay = ((w.OpenPlayFinishing * a.Finishing) + (w.OpenPlayPositioning * a.Positioning) + (w.OpenPlayPace * a.Pace)) * OpenPlayRole(player.Position);
            double aerial = ((w.AerialHeading * a.Heading) + (w.AerialJumping * a.Jumping) + (w.AerialStrength * a.Strength)) * AerialRole(player.Position);
            double weight = openPlay + aerial;

            // The floor keeps every outfielder a live threat; a keeper is exempt so his goal stays a
            // once-in-many-seasons event, not a regular one.
            return player.Position == Position.Goalkeeper ? weight : Math.Max(w.MinOutfielderWeight, weight);
        }

        // Open play favours strikers heavily; the air gives defenders a real set-piece threat while still
        // rewarding target men. Defence scores mostly through the aerial pathway (corner headers).
        private double OpenPlayRole(Position position)
        {
            switch (position)
            {
                case Position.Forward:
                    return _weights.OpenPlayForward;
                case Position.Midfielder:
                    return _weights.OpenPlayMidfielder;
                case Position.Defender:
                    return _weights.OpenPlayDefender;
                case Position.Goalkeeper:
                    return _weights.OpenPlayGoalkeeper;
                default:
                    return 0.3;
            }
        }

        private double AerialRole(Position position)
        {
            switch (position)
            {
                case Position.Forward:
                    return _weights.AerialForward;
                case Position.Midfielder:
                    return _weights.AerialMidfielder;
                case Position.Defender:
                    return _weights.AerialDefender;
                case Position.Goalkeeper:
                    return _weights.AerialGoalkeeper;
                default:
                    return 0.2;
            }
        }
    }
}
