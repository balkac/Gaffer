using System;
using System.Collections.Generic;

namespace Gaffer.Domain.Players
{
    /// <summary>
    /// The attributes each role emphasises, as display rows (GDD §4.2 / ART_STYLE §4.1): a striker leans on
    /// finishing, pace, and positioning; a centre-back on marking, heading, and strength; a full-back on pace
    /// and crossing; a winger on pace and dribbling; a keeper on the keeping group. This marks a role's
    /// <em>importance</em>, not a player's value — the UI tints these rows.
    /// <para>
    /// These rows are now <em>projected</em> from <see cref="RoleAttributeWeights"/> rather than typed out
    /// again beside it. The old doc admitted the invariant lived in prose ("its keys mirror the role's rating
    /// formula") and, as ARCHITECTURE §8a predicts, prose had already lost: the keeper's rows omitted aerial
    /// reach and the full-back's omitted positioning, both of which the rating weights, so the scout showed
    /// the wrong stats for those positions and nothing failed. Deriving the rows makes that drift
    /// unrepresentable — a role is defined once, in one table.
    /// </para>
    /// <para>
    /// Rows come out heaviest-weight first, matching the rating's own order. The projection runs once at type
    /// initialization into <c>static readonly</c> arrays (PERFORMANCE §8), so a lookup allocates nothing.
    /// </para>
    /// </summary>
    public static class RoleKeyAttributes
    {
        // Indexed by the role's enum value — built once from the weight table, so a role added there gets its
        // display rows for free and can never disagree with what it is scored on.
        private static readonly AttributeKey[][] KeysByRole = BuildKeysByRole();

        /// <summary>The attributes a specific role is scored on — the rows the UI tints for that player.</summary>
        public static IReadOnlyList<AttributeKey> For(PlayerRole role)
        {
            int index = (int)role;
            if (index >= 0 && index < KeysByRole.Length && KeysByRole[index] != null)
            {
                return KeysByRole[index];
            }

            return KeysByRole[(int)PlayerRole.Striker];
        }

        /// <summary>
        /// The rows to show when only the broad line is known — the line's representative role's rows, so a
        /// stand-in never disagrees with the role it stands in for (<see cref="PlayerRoles.Representative"/>).
        /// </summary>
        public static IReadOnlyList<AttributeKey> For(Position position)
        {
            return For(PlayerRoles.Representative(position));
        }

        private static AttributeKey[][] BuildKeysByRole()
        {
            var roles = (PlayerRole[])Enum.GetValues(typeof(PlayerRole));
            int highest = 0;
            for (int i = 0; i < roles.Length; i++)
            {
                int value = (int)roles[i];
                if (value > highest)
                {
                    highest = value;
                }
            }

            var byRole = new AttributeKey[highest + 1][];
            for (int i = 0; i < roles.Length; i++)
            {
                IReadOnlyList<RoleAttributeWeight> weights = RoleAttributeWeights.For(roles[i]);
                var keys = new AttributeKey[weights.Count];
                for (int k = 0; k < weights.Count; k++)
                {
                    keys[k] = new AttributeKey(weights[k].Attribute);
                }

                byRole[(int)roles[i]] = keys;
            }

            return byRole;
        }
    }
}
