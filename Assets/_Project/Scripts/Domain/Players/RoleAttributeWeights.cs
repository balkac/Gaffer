using System.Collections.Generic;

namespace Gaffer.Domain.Players
{
    /// <summary>
    /// The single owner of "which attributes constitute this role, and how much each one counts" (GDD §4.2 /
    /// TDD §4.2). Everything role-shaped derives from this one table: <c>PlayerRatings.ForRole</c> sums it,
    /// <c>PlayerDevelopment.AdjustRoleAttributes</c> iterates it to decide which stats grow and erode, and
    /// <see cref="RoleKeyAttributes"/> projects the scout's display rows from it. Before this table the same
    /// fact was written out three times and kept in step only by prose — exactly the failure ARCHITECTURE
    /// §8a describes: each copy is a legal, self-consistent piece of code, only their *relationship* is
    /// wrong, so adding or re-weighting a role silently grew attributes the rating never read and showed the
    /// wrong stats in the scout. Now a role is defined once and cannot drift.
    /// <para>
    /// The weights stay in code rather than a ScriptableObject on purpose (decision 2026-07-23): they are the
    /// role *model* — which attributes constitute a role, normalized to sum 1 — not a balance dial. Retuning
    /// them renormalizes every rating, valuation and wage at once, so it is a design change, not a tweak;
    /// balance tuning lives in the injected settings objects. That argument is precisely why the table must
    /// have one owner: if the weights are the model, everything else has to be derived from them.
    /// </para>
    /// <para>
    /// The table must also cover everything age takes away: every attribute in
    /// <see cref="PlayerAttributes.Athletic"/> is weighted by at least one role, asserted by
    /// <c>RoleAttributeWeightsTests</c>. Unifying the three role tables into this one exposed the opposite —
    /// decline eroded acceleration, agility and jumping while no role's rating read them, so a player's body
    /// went and his OVR did not notice (fixed 2026-08-07). Coverage is not uniformity: the holder and the
    /// deep playmaker carry none of the three, because a role that does not sprint, leap or turn should not
    /// be made to. Each of the three was carved out of the coarser attribute that had been standing in for
    /// it — acceleration out of pace, jumping out of heading / aerial reach, agility out of dribbling /
    /// reflexes — so each role's group budgets (its speed, its aerial duel) are unchanged and the table is
    /// resolved finer rather than re-weighted. It still moves ratings: an ageing player now loses OVR where
    /// he used to lose only hidden numbers, which is what the decline model always meant.
    /// </para>
    /// <para>
    /// Each role's weights sum to 1, so a uniform attribute sheet collapses to that value whatever the role
    /// (see <c>RoleAttributeWeightsTests</c>). Row order is load-bearing twice over: it fixes the
    /// floating-point summation order of the rating (so the last bits of every pinned balance number stay
    /// put) and the order development draws from the rng, so it must never be shuffled — determinism is a
    /// project NON-NEGOTIABLE (#2). The tables are <c>static readonly</c> and handed out as
    /// <see cref="IReadOnlyList{T}"/>, so a lookup on the hot path (~60k ratings a season) allocates nothing
    /// and an indexed read does not box (PERFORMANCE §8).
    /// </para>
    /// </summary>
    public static class RoleAttributeWeights
    {
        // A keeper is scored on the keeping group alone: shot-stopping first, then the box work. Agility is
        // the dive behind the reflex and jumping the leap behind the reach, so each is carved out of the
        // coarse attribute that used to stand in for it (reflexes 0.30 -> 0.25 + agility 0.05; aerial reach
        // 0.10 -> 0.05 + jumping 0.05) rather than added on top.
        private static readonly RoleAttributeWeight[] GoalkeeperWeights =
        {
            new RoleAttributeWeight(PlayerAttribute.Reflexes, 0.25),
            new RoleAttributeWeight(PlayerAttribute.Handling, 0.20),
            new RoleAttributeWeight(PlayerAttribute.OneOnOnes, 0.20),
            new RoleAttributeWeight(PlayerAttribute.CommandOfArea, 0.15),
            new RoleAttributeWeight(PlayerAttribute.AerialReach, 0.05),
            new RoleAttributeWeight(PlayerAttribute.Jumping, 0.05),
            new RoleAttributeWeight(PlayerAttribute.Agility, 0.05),
            new RoleAttributeWeight(PlayerAttribute.GkPositioning, 0.05),
        };

        // An aerial stopper: the tackle and the duel, with reading the game behind it — no pace at all, which
        // is why a centre-back still ages more gracefully than a winger. The aerial duel is the same trio
        // WeightedScorerSelector heads a corner with (heading/jumping/strength), so the 0.35 that group
        // already carried is now split across all three (heading 0.20 -> 0.15, strength 0.15 -> 0.10,
        // jumping 0.10) instead of leaving the leap itself unread.
        private static readonly RoleAttributeWeight[] CentreBackWeights =
        {
            new RoleAttributeWeight(PlayerAttribute.Marking, 0.25),
            new RoleAttributeWeight(PlayerAttribute.Tackling, 0.25),
            new RoleAttributeWeight(PlayerAttribute.Heading, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Positioning, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Jumping, 0.10),
            new RoleAttributeWeight(PlayerAttribute.Strength, 0.10),
        };

        // A modern full-back: up and down the flank and a cross on the end of it, defending second. The
        // flank's speed budget stays 0.20 and is resolved into its two halves — top speed for the recovery
        // run (0.15) and the burst that starts the overlap (0.05).
        private static readonly RoleAttributeWeight[] FullBackWeights =
        {
            new RoleAttributeWeight(PlayerAttribute.Crossing, 0.20),
            new RoleAttributeWeight(PlayerAttribute.Tackling, 0.20),
            new RoleAttributeWeight(PlayerAttribute.Pace, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Marking, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Stamina, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Positioning, 0.10),
            new RoleAttributeWeight(PlayerAttribute.Acceleration, 0.05),
        };

        // The holder reads the game and screens the back four; nothing about the job is a sprint, a leap or a
        // change of direction, so he carries none of the three athletic attributes — the erosion set is not
        // spread over every role for the sake of symmetry.
        private static readonly RoleAttributeWeight[] DefensiveMidfieldWeights =
        {
            new RoleAttributeWeight(PlayerAttribute.Tackling, 0.25),
            new RoleAttributeWeight(PlayerAttribute.Marking, 0.20),
            new RoleAttributeWeight(PlayerAttribute.Positioning, 0.20),
            new RoleAttributeWeight(PlayerAttribute.Passing, 0.20),
            new RoleAttributeWeight(PlayerAttribute.Stamina, 0.15),
        };

        // The deep playmaker: a passing range and a first touch under pressure. Like the holder he asks
        // nothing of raw athleticism beyond the lungs, so none of the three lands here either.
        private static readonly RoleAttributeWeight[] CentralMidfieldWeights =
        {
            new RoleAttributeWeight(PlayerAttribute.Passing, 0.30),
            new RoleAttributeWeight(PlayerAttribute.Technique, 0.25),
            new RoleAttributeWeight(PlayerAttribute.FirstTouch, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Positioning, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Stamina, 0.15),
        };

        // The number ten works in a crowd: agility is how he turns out of a tackle, so a slice of the
        // dribbling weight (0.20 -> 0.15) names the change of direction it was standing in for.
        private static readonly RoleAttributeWeight[] AttackingMidfieldWeights =
        {
            new RoleAttributeWeight(PlayerAttribute.Passing, 0.25),
            new RoleAttributeWeight(PlayerAttribute.Technique, 0.25),
            new RoleAttributeWeight(PlayerAttribute.Dribbling, 0.15),
            new RoleAttributeWeight(PlayerAttribute.FirstTouch, 0.15),
            new RoleAttributeWeight(PlayerAttribute.LongShots, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Agility, 0.05),
        };

        // A flank runner rather than a one-v-one winger, so his 0.20 of speed splits the same way the
        // full-back's does: mostly the run, a little of the burst.
        private static readonly RoleAttributeWeight[] WideMidfieldWeights =
        {
            new RoleAttributeWeight(PlayerAttribute.Crossing, 0.25),
            new RoleAttributeWeight(PlayerAttribute.Stamina, 0.20),
            new RoleAttributeWeight(PlayerAttribute.Passing, 0.20),
            new RoleAttributeWeight(PlayerAttribute.Pace, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Dribbling, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Acceleration, 0.05),
        };

        // The winger is the most athletic role on the sheet and the one age punishes hardest: his 0.25 of
        // speed is as much the first yard as the top end (pace 0.15 + acceleration 0.10), and beating a
        // full-back is a change of direction, so agility takes a slice of the dribbling (0.25 -> 0.20).
        private static readonly RoleAttributeWeight[] WingWeights =
        {
            new RoleAttributeWeight(PlayerAttribute.Dribbling, 0.20),
            new RoleAttributeWeight(PlayerAttribute.Crossing, 0.20),
            new RoleAttributeWeight(PlayerAttribute.Pace, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Technique, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Finishing, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Acceleration, 0.10),
            new RoleAttributeWeight(PlayerAttribute.Agility, 0.05),
        };

        // A striker's speed is the half-yard in the box, so his 0.20 splits evenly between the top end and
        // the burst; and the target man's aerial weight splits between the header and the leap that wins it
        // (heading 0.10 -> 0.05 + jumping 0.05), matching WeightedScorerSelector's aerial pathway.
        private static readonly RoleAttributeWeight[] StrikerWeights =
        {
            new RoleAttributeWeight(PlayerAttribute.Finishing, 0.35),
            new RoleAttributeWeight(PlayerAttribute.Positioning, 0.20),
            new RoleAttributeWeight(PlayerAttribute.Technique, 0.15),
            new RoleAttributeWeight(PlayerAttribute.Pace, 0.10),
            new RoleAttributeWeight(PlayerAttribute.Acceleration, 0.10),
            new RoleAttributeWeight(PlayerAttribute.Heading, 0.05),
            new RoleAttributeWeight(PlayerAttribute.Jumping, 0.05),
        };

        /// <summary>
        /// The attributes this role is made of, heaviest first — the shared table the rating, the
        /// development curve, and the scout's key stats all read. The returned list is the shared preset
        /// instance; treat it as immutable and read it by index (never <c>foreach</c> it on a hot path,
        /// which boxes the enumerator — PERFORMANCE §8).
        /// </summary>
        public static IReadOnlyList<RoleAttributeWeight> For(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper:
                    return GoalkeeperWeights;
                case PlayerRole.CentreBack:
                    return CentreBackWeights;
                case PlayerRole.RightBack:
                case PlayerRole.LeftBack:
                    return FullBackWeights;
                case PlayerRole.DefensiveMidfield:
                    return DefensiveMidfieldWeights;
                case PlayerRole.CentralMidfield:
                    return CentralMidfieldWeights;
                case PlayerRole.AttackingMidfield:
                    return AttackingMidfieldWeights;
                case PlayerRole.RightMidfield:
                case PlayerRole.LeftMidfield:
                    return WideMidfieldWeights;
                case PlayerRole.RightWing:
                case PlayerRole.LeftWing:
                    return WingWeights;
                default: // Striker
                    return StrikerWeights;
            }
        }
    }
}
