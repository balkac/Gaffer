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
    /// scorers. The weights come from an injected <see cref="ScorerWeights"/> (data-driven, NON-NEGOTIABLE #3),
    /// which — as its own docs require — is treated as immutable once built, so a squad's weight vector
    /// can be memoised across the goals of a match.
    /// </summary>
    public sealed class WeightedScorerSelector : IScorerSelector
    {
        // Cumulative weight vectors, memoised per squad (PERFORMANCE §6). A player's weight is a pure
        // function of (his attributes, ScorerWeights) — both constant for the life of a Squad instance,
        // which is immutable, so a roster change hands over a *new* instance and a stale vector can
        // never be read. Two slots because a match alternates between exactly two squads; a goal used
        // to walk the whole roster twice (up to 2 × 25 Weight() calls, each copying the 29-field
        // Attributes struct by value) — ~50,000 evaluations over a season's ~1,000 goals. Bounded at
        // two entries on purpose: a dictionary keyed by squad would grow with every transfer (§16).
        // The core is synchronous and single-threaded, so unsynchronised slots are safe.
        private const int CacheSlots = 2;

        private readonly Squad[] _cachedSquads = new Squad[CacheSlots];
        private readonly double[][] _cachedCumulative = new double[CacheSlots][];
        private readonly int[] _cachedCounts = new int[CacheSlots];
        private int _nextSlot;

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
            double[] cumulative = CumulativeWeightsOf(squad, players, out int count);

            // The vector is accumulated in roster order from 0.0, exactly as the old two-pass scan
            // accumulated its running total, so the last entry is bit-identical to that scan's total —
            // and so is every partial sum the roll is compared against. The draw stays in the same
            // place and still consumes exactly one value.
            double total = cumulative[count - 1];
            double roll = rng.NextDouble() * total;

            // Binary search for the first index whose cumulative weight exceeds the roll — precisely
            // the index the linear scan returned (same strict `roll < cumulative` boundary, and the
            // same "no index qualifies → the last player" fallback, since the search converges on
            // count - 1 in that case). O(log n) per goal instead of O(n) with a struct copy per step.
            int lo = 0;
            int hi = count - 1;
            while (lo < hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                if (roll < cumulative[mid])
                {
                    hi = mid;
                }
                else
                {
                    lo = mid + 1;
                }
            }

            return players[lo].Id;
        }

        // Returns this squad's cumulative weight vector, building it on a miss. Slots are replaced
        // round-robin, which for the two squads of a match means each keeps its own.
        private double[] CumulativeWeightsOf(Squad squad, IReadOnlyList<Player> players, out int count)
        {
            for (int slot = 0; slot < CacheSlots; slot++)
            {
                if (ReferenceEquals(_cachedSquads[slot], squad))
                {
                    count = _cachedCounts[slot];
                    return _cachedCumulative[slot];
                }
            }

            int target = _nextSlot;
            _nextSlot = (_nextSlot + 1) % CacheSlots;

            double[] buffer = _cachedCumulative[target];
            if (buffer == null || buffer.Length < players.Count)
            {
                buffer = new double[players.Count];
                _cachedCumulative[target] = buffer;
            }

            double running = 0.0;
            for (int i = 0; i < players.Count; i++)
            {
                running += Weight(players[i]);
                buffer[i] = running;
            }

            _cachedSquads[target] = squad;
            _cachedCounts[target] = players.Count;
            count = players.Count;
            return buffer;
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
