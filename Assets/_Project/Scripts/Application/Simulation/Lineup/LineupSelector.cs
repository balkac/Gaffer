using System;
using System.Collections.Generic;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Picks the starting eleven for a formation by filling each role slot with the best available player:
    /// first an exact-role match (a real right-back for the right-back slot), then the best of the same
    /// broad line, then anyone left. Rating is <see cref="PlayerRatings.ForRole(Player)"/> — role-specific, so
    /// a full-back is judged on pace and crossing and a centre-back on heading, and a player filling a
    /// same-line slot out of his natural role is rated on his own role, not the slot's. Ties break on the
    /// lower player id, so the pick is deterministic — the natural default a manager adjusts, and the
    /// auto-pick for AI clubs.
    /// </summary>
    public sealed class LineupSelector
    {
        // Scratch pool of not-yet-picked players, reused across calls (cleared each time) so the
        // per-club-per-match auto-pick allocates nothing (PERFORMANCE §8). The core is synchronous
        // and single-threaded, so one buffer is safe.
        private readonly List<Player> _available = new List<Player>(32);

        // The returned eleven, on the same reuse contract as _available — the class already owned a
        // scratch buffer and then allocated a fresh List per call for the result, which is the same
        // per-action allocation the buffer exists to avoid (PERFORMANCE §4 "reuse scratch
        // collections"). Pre-sized past the eleven slots any formation asks for, so it never grows.
        private readonly List<Player> _chosen = new List<Player>(16);

        /// <summary>
        /// The best eleven for this formation, best-first by slot order.
        /// <para><b>Buffer lifetime:</b> the returned list is a buffer owned by this selector and is
        /// <b>valid only until the next <see cref="SelectBest"/> call on this instance</b> — the next
        /// call overwrites it in place. Consume it synchronously (read it, or copy out what you keep);
        /// a caller that stores the reference will silently see a later club's eleven. Give a caller
        /// that needs to retain an eleven its own <see cref="LineupSelector"/> or its own copy.</para>
        /// </summary>
        public IReadOnlyList<Player> SelectBest(Squad squad, Formation formation)
        {
            List<Player> available = _available;
            available.Clear();
            IReadOnlyList<Player> players = squad.Players;
            for (int i = 0; i < players.Count; i++)
            {
                available.Add(players[i]);
            }

            IReadOnlyList<PlayerRole> slots = formation.Slots;
            List<Player> chosen = _chosen;
            chosen.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                Player pick = PickForSlot(available, slots[i]);
                if (pick != null)
                {
                    chosen.Add(pick);
                    available.Remove(pick);
                }
            }

            return chosen;
        }

        private static Player PickForSlot(List<Player> available, PlayerRole slot)
        {
            Player exact = BestWhere(available, slot, matchLine: false);
            if (exact != null)
            {
                return exact;
            }

            Player sameLine = BestWhere(available, slot, matchLine: true);
            if (sameLine != null)
            {
                return sameLine;
            }

            return BestAny(available);
        }

        private static Player BestWhere(List<Player> available, PlayerRole slot, bool matchLine)
        {
            Position line = PlayerRoles.Line(slot);
            Player best = null;
            double bestRating = double.MinValue;
            int bestId = int.MaxValue;
            foreach (Player player in available)
            {
                bool matches = matchLine ? player.Position == line : player.Role == slot;
                if (!matches)
                {
                    continue;
                }

                if (IsBetter(player, bestRating, bestId, out double rating))
                {
                    best = player;
                    bestRating = rating;
                    bestId = player.Id.Value;
                }
            }

            return best;
        }

        private static Player BestAny(List<Player> available)
        {
            Player best = null;
            double bestRating = double.MinValue;
            int bestId = int.MaxValue;
            foreach (Player player in available)
            {
                if (IsBetter(player, bestRating, bestId, out double rating))
                {
                    best = player;
                    bestRating = rating;
                    bestId = player.Id.Value;
                }
            }

            return best;
        }

        // Hands the rating back so the caller does not recompute it for the candidate it just accepted
        // — the comparison and the record-keeping were each evaluating ForRole over the whole squad
        // (PERFORMANCE §6: the cost of a query is the contract of the API you asked it through). The
        // returned value is the same double the caller used to assign, so ordering is unchanged.
        private static bool IsBetter(Player candidate, double bestRating, int bestId, out double rating)
        {
            rating = PlayerRatings.ForRole(candidate);
            if (rating > bestRating)
            {
                return true;
            }

            return Math.Abs(rating - bestRating) < 1e-9 && candidate.Id.Value < bestId;
        }
    }
}
