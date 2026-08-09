using System;
using System.Collections.Generic;

namespace Gaffer.Domain.Players
{
    /// <summary>
    /// Facts about a <see cref="PlayerAttribute"/> that belong to the model rather than to any one caller:
    /// which attributes the body loses with age, and how an attribute names itself to the UI. The
    /// counterpart of <see cref="PlayerRoles"/> for the attribute enum.
    /// </summary>
    public static class PlayerAttributes
    {
        /// <summary>
        /// The attributes age wears down on their own, independently of the role — the extra athletic
        /// erosion a veteran suffers on top of the general slide in his rated attributes. A strict subset of
        /// the Physical &amp; Movement group: strength, balance and positioning are deliberately absent
        /// because they hold (or improve) as a player reads the game better, which is why a keeper or a
        /// positional centre-back ages gracefully while a winger who lived on pace falls off a cliff.
        /// <para>
        /// Every attribute in this list is weighted by at least one role in
        /// <see cref="RoleAttributeWeights"/>, so nothing age takes away is invisible to the ratings —
        /// pinned by <c>RoleAttributeWeightsTests</c>. It was not always so: acceleration, agility and
        /// jumping used to be eroded here and read by nothing, an asymmetry unifying the role tables made
        /// visible and 2026-08-07 closed by putting the three into the weights. Which roles read which is
        /// still a matter of football merit — a holder's rating carries none of the three — so this list
        /// deliberately does not mirror any one role's table; the invariant is coverage across all of them.
        /// The list is stated once so the erosion set cannot drift from this note the way the role tables
        /// used to drift from each other (ARCHITECTURE §8a).
        /// </para>
        /// <para>Order is load-bearing: it is the order decline draws from the rng (NON-NEGOTIABLE #2).</para>
        /// </summary>
        public static IReadOnlyList<PlayerAttribute> Athletic { get; } = new[]
        {
            PlayerAttribute.Pace,
            PlayerAttribute.Acceleration,
            PlayerAttribute.Agility,
            PlayerAttribute.Stamina,
            PlayerAttribute.Jumping,
        };

        /// <summary>
        /// The localization key for this attribute's short label — the string table resolves it to "FIN",
        /// "PAC", "TKL" in English and to whatever Turkish uses. Domain names the attribute and hands out a
        /// key; it never carries display text (NON-NEGOTIABLE #8), the same rule <c>TraitSO</c> and
        /// <c>DramaEvent</c> follow with their <c>NameKey</c> / <c>TitleKey</c> fields.
        /// </summary>
        public static string GetLabelKey(PlayerAttribute attribute)
        {
            switch (attribute)
            {
                case PlayerAttribute.Finishing:
                    return "attr.finishing.abbrev";
                case PlayerAttribute.Technique:
                    return "attr.technique.abbrev";
                case PlayerAttribute.FirstTouch:
                    return "attr.first_touch.abbrev";
                case PlayerAttribute.Dribbling:
                    return "attr.dribbling.abbrev";
                case PlayerAttribute.Passing:
                    return "attr.passing.abbrev";
                case PlayerAttribute.Crossing:
                    return "attr.crossing.abbrev";
                case PlayerAttribute.Heading:
                    return "attr.heading.abbrev";
                case PlayerAttribute.LongShots:
                    return "attr.long_shots.abbrev";
                case PlayerAttribute.Marking:
                    return "attr.marking.abbrev";
                case PlayerAttribute.Tackling:
                    return "attr.tackling.abbrev";
                case PlayerAttribute.Penalties:
                    return "attr.penalties.abbrev";
                case PlayerAttribute.FreeKicks:
                    return "attr.free_kicks.abbrev";
                case PlayerAttribute.Corners:
                    return "attr.corners.abbrev";
                case PlayerAttribute.LongThrows:
                    return "attr.long_throws.abbrev";
                case PlayerAttribute.Pace:
                    return "attr.pace.abbrev";
                case PlayerAttribute.Acceleration:
                    return "attr.acceleration.abbrev";
                case PlayerAttribute.Stamina:
                    return "attr.stamina.abbrev";
                case PlayerAttribute.Strength:
                    return "attr.strength.abbrev";
                case PlayerAttribute.Agility:
                    return "attr.agility.abbrev";
                case PlayerAttribute.Jumping:
                    return "attr.jumping.abbrev";
                case PlayerAttribute.Balance:
                    return "attr.balance.abbrev";
                case PlayerAttribute.Positioning:
                    return "attr.positioning.abbrev";
                case PlayerAttribute.Reflexes:
                    return "attr.reflexes.abbrev";
                case PlayerAttribute.Handling:
                    return "attr.handling.abbrev";
                case PlayerAttribute.AerialReach:
                    return "attr.aerial_reach.abbrev";
                case PlayerAttribute.CommandOfArea:
                    return "attr.command_of_area.abbrev";
                case PlayerAttribute.OneOnOnes:
                    return "attr.one_on_ones.abbrev";
                case PlayerAttribute.Kicking:
                    return "attr.kicking.abbrev";
                case PlayerAttribute.GkPositioning:
                    return "attr.gk_positioning.abbrev";
                default:
                    throw new ArgumentOutOfRangeException(nameof(attribute), attribute, "Unmapped player attribute.");
            }
        }

    }
}
