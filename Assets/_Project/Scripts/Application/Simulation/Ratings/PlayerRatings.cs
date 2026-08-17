using System.Collections.Generic;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Rates a player on his specific role from his attributes — the shared scoring both the strength builder
    /// (to average a line) and the lineup selector (to pick the best per slot) rely on. The weights come from
    /// <see cref="RoleAttributeWeights"/>, the one table that says which attributes constitute a role; this
    /// class only sums them. Weights sum to 1 per role, so the rating stays on the 0–100 scale (uniform
    /// attributes collapse to that value, whatever the role). The split is the point: a full-back is valued
    /// on pace and crossing, a centre-back on marking and heading, a winger on pace and dribbling — so the
    /// same attribute sheet is worth different amounts in different roles, and a player slotted out of
    /// position rates below a natural fit. Decision (2026-07-23): those weight tables stay in code — they
    /// are the role *model* (which attributes constitute a role, normalized to sum 1), not a balance dial;
    /// retuning them renormalizes every rating, valuation, and wage at once, so it is a design change, not a
    /// tweak. Balance tuning lives in the injected settings objects (TacticsSettings, ScorerWeights,
    /// EconomySettings, and the per-domain *Settings/*BalanceSO pairs).
    /// </summary>
    public static class PlayerRatings
    {
        /// <summary>
        /// Sums the role's weighted attributes, heaviest first. The accumulation walks the table in stored
        /// order and starts from 0.0, which reproduces the left-to-right summation the hand-written
        /// expressions used bit for bit — floating-point addition is not associative, and the balance
        /// figures the suite pins are measured to the last digit (NON-NEGOTIABLE #2). Indexed loop over
        /// <see cref="IReadOnlyList{T}"/> and no delegates, so a rating allocates nothing (PERFORMANCE §8);
        /// <c>in</c> passes the 32-byte sheet by reference on a path walked ~60,000 times a season.
        /// </summary>
        public static double ForRole(PlayerRole role, in Attributes attributes)
        {
            IReadOnlyList<RoleAttributeWeight> weights = RoleAttributeWeights.For(role);
            double rating = 0.0;
            for (int i = 0; i < weights.Count; i++)
            {
                RoleAttributeWeight weighted = weights[i];
                rating += weighted.Weight * attributes.ValueOf(weighted.Attribute);
            }

            return rating;
        }

        /// <summary>How good a player is in his own role — the rating on the axis matching his specific role.</summary>
        public static double ForRole(Player player)
        {
            return ForRole(player.Role, player.Attributes);
        }

        /// <summary>
        /// How good a player is IN A GIVEN SLOT: his own-role rating, charged for being out of position
        /// (<see cref="PlayerRoles.FitFor"/> × <see cref="PositionalFitSettings"/>).
        ///
        /// <para><b>Why the base stays his own role and is not re-weighted on the slot's.</b> Rating a
        /// centre-back on a striker's attributes would ask what kind of striker he would be, which the
        /// attribute sheet cannot answer — and it would reward the wrong thing, because a quick centre-back
        /// would score well as a winger and the game would start recommending it. What playing out of
        /// position actually costs is positioning and decisions, not talent; FM models it the same way, as a
        /// penalty on the man rather than a re-reading of him.</para>
        /// </summary>
        public static double ForSlot(Player player, PlayerRole slotRole, PositionalFitSettings fit)
        {
            double rating = ForRole(player.Role, player.Attributes);
            if (player.Role == slotRole)
            {
                return rating;
            }

            PositionalFitSettings settings = fit ?? PositionalFitSettings.Default;
            return rating * settings.MultiplierFor(PlayerRoles.FitFor(player.Role, slotRole));
        }
    }
}
