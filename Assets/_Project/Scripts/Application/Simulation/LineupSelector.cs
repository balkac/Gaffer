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
        public IReadOnlyList<Player> SelectBest(Squad squad, Formation formation)
        {
            var available = new List<Player>(squad.Players);
            var chosen = new List<Player>(formation.Slots.Count);

            foreach (PlayerRole slot in formation.Slots)
            {
                Player pick = PickForSlot(available, slot);
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

                if (IsBetter(player, bestRating, bestId))
                {
                    best = player;
                    bestRating = PlayerRatings.ForRole(player);
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
                if (IsBetter(player, bestRating, bestId))
                {
                    best = player;
                    bestRating = PlayerRatings.ForRole(player);
                    bestId = player.Id.Value;
                }
            }

            return best;
        }

        private static bool IsBetter(Player candidate, double bestRating, int bestId)
        {
            double rating = PlayerRatings.ForRole(candidate);
            if (rating > bestRating)
            {
                return true;
            }

            return Math.Abs(rating - bestRating) < 1e-9 && candidate.Id.Value < bestId;
        }
    }
}
