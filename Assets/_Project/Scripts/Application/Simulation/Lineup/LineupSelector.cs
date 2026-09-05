using System;
using System.Collections.Generic;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Picks the starting eleven for a formation by filling each slot with the best player FOR THAT SLOT —
    /// his own-role rating charged for being out of position (<see cref="PlayerRatings.ForSlot"/>). Ties
    /// break on the lower player id, so the pick is deterministic: the natural default a manager adjusts,
    /// and the auto-pick for every club he does not run.
    ///
    /// <para><b>Why one ranked pass and not "natural first".</b> This used to try an exact-role match, then
    /// the same line, then anybody — which meant a 55-rated natural left-midfielder was always preferred to
    /// an 88-rated central midfielder, because the first pass found a match and never looked further. That
    /// is not how the position is judged: a compromise costs about a tenth of a player on the same line
    /// (<see cref="PositionalFitSettings"/>), so a much better footballer wins the slot and a marginally
    /// better one does not. Charging the penalty and then ranking once expresses that in one comparison and
    /// makes the auto-pick agree with what the match will actually reward.</para>
    ///
    /// <para><b>The one pairing it will not volunteer</b> is <see cref="PositionalFit.Impossible"/> — an
    /// outfielder in goal or a keeper anywhere else. The penalty alone would not stop it: a brilliant striker
    /// at 60% still out-rates a poor keeper, and the auto-pick would put him between the posts. A manager who
    /// wants that may still do it by hand and pay for it; the game will not do it behind his back. If a squad
    /// has no eligible player for a slot at all, the best remaining body fills it rather than leaving a gap.</para>
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
        private readonly List<SlottedPlayer> _chosen = new List<SlottedPlayer>(16);

        private readonly PositionalFitSettings _fit;

        /// <param name="fit">What being out of position costs; null takes the calibrated default.</param>
        public LineupSelector(PositionalFitSettings fit = null)
        {
            _fit = fit ?? PositionalFitSettings.Default;
        }

        /// <summary>
        /// The best eleven for this formation as a team sheet — each pick carries the slot it fills, so a
        /// caller never has to line two lists up by index (see <see cref="SlottedPlayer"/>).
        /// <para><b>Buffer lifetime:</b> the returned list is a buffer owned by this selector and is
        /// <b>valid only until the next <see cref="SelectBest"/> call on this instance</b> — the next
        /// call overwrites it in place. Consume it synchronously (read it, or copy out what you keep);
        /// a caller that stores the reference will silently see a later club's eleven. Give a caller
        /// that needs to retain an eleven its own <see cref="LineupSelector"/> or its own copy.</para>
        /// </summary>
        public IReadOnlyList<SlottedPlayer> SelectBest(Squad squad, Formation formation)
        {
            List<Player> available = _available;
            available.Clear();
            IReadOnlyList<Player> players = squad.Players;
            for (int i = 0; i < players.Count; i++)
            {
                available.Add(players[i]);
            }

            IReadOnlyList<PlayerRole> slots = formation.Slots;
            List<SlottedPlayer> chosen = _chosen;
            chosen.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                PlayerRole slotRole = slots[i];
                Player pick = PickForSlot(available, slotRole);
                if (pick != null)
                {
                    chosen.Add(new SlottedPlayer(i, slotRole, pick));
                    available.Remove(pick);
                }
            }

            return chosen;
        }

        private Player PickForSlot(List<Player> available, PlayerRole slotRole)
        {
            Player eligible = Best(available, slotRole, skipImpossible: true);
            if (eligible != null)
            {
                return eligible;
            }

            // No keeper in the squad (or nothing but keepers left). Fielding ten and a gap would be worse
            // than fielding somebody badly, so the ban lifts as a last resort.
            return Best(available, slotRole, skipImpossible: false);
        }

        private Player Best(List<Player> available, PlayerRole slotRole, bool skipImpossible)
        {
            Player best = null;
            double bestRating = double.MinValue;
            int bestId = int.MaxValue;
            for (int i = 0; i < available.Count; i++)
            {
                Player player = available[i];
                if (skipImpossible && PlayerRoles.FitFor(player.Role, slotRole) == PositionalFit.Impossible)
                {
                    continue;
                }

                if (IsBetter(player, slotRole, bestRating, bestId, out double rating))
                {
                    best = player;
                    bestRating = rating;
                    bestId = player.Id.Value;
                }
            }

            return best;
        }

        // Hands the rating back so the caller does not recompute it for the candidate it just accepted
        // — the comparison and the record-keeping were each evaluating the rating over the whole squad
        // (PERFORMANCE §6: the cost of a query is the contract of the API you asked it through). The
        // returned value is the same double the caller used to assign, so ordering is unchanged.
        private bool IsBetter(Player candidate, PlayerRole slotRole, double bestRating, int bestId, out double rating)
        {
            rating = PlayerRatings.ForSlot(candidate, slotRole, _fit);
            if (rating > bestRating)
            {
                return true;
            }

            return Math.Abs(rating - bestRating) < 1e-9 && candidate.Id.Value < bestId;
        }
    }
}
