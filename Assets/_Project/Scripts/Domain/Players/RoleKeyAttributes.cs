using System;
using System.Collections.Generic;

namespace Gaffer.Domain.Players
{
    /// <summary>
    /// One attribute a role leans on — a short label and a reader over <see cref="Attributes"/>. Pairing
    /// the two lets callers show a role's key stats generically without a switch per attribute.
    /// </summary>
    public readonly struct AttributeKey
    {
        public AttributeKey(string label, Func<Attributes, byte> read)
        {
            Label = label;
            Read = read;
        }

        public string Label { get; }

        public Func<Attributes, byte> Read { get; }
    }

    /// <summary>
    /// The attributes each role emphasises (GDD §4.2 / ART_STYLE §4.1): a striker leans on finishing,
    /// pace, and positioning; a centre-back on marking, heading, and strength; a full-back on pace and
    /// crossing; a winger on pace and dribbling; a keeper on the keeping group. This marks a role's
    /// <em>importance</em>, not a player's value — the UI tints these rows, and the rating weights the same
    /// attributes. The <see cref="For(PlayerRole)"/> overload is the specific mapping (its keys mirror the
    /// role's rating formula); <see cref="For(Position)"/> keeps a broad-line default. The mapping lives in
    /// the pure Domain (no config asset yet); a data-driven <c>RoleSO</c> can replace it without touching
    /// call sites.
    /// </summary>
    public static class RoleKeyAttributes
    {
        private static readonly AttributeKey[] ForwardKeys =
        {
            new AttributeKey("FIN", a => a.Finishing),
            new AttributeKey("PAC", a => a.Pace),
            new AttributeKey("POS", a => a.Positioning),
            new AttributeKey("TEC", a => a.Technique),
            new AttributeKey("DRI", a => a.Dribbling),
        };

        private static readonly AttributeKey[] MidfielderKeys =
        {
            new AttributeKey("PAS", a => a.Passing),
            new AttributeKey("TEC", a => a.Technique),
            new AttributeKey("FIR", a => a.FirstTouch),
            new AttributeKey("POS", a => a.Positioning),
            new AttributeKey("STA", a => a.Stamina),
        };

        private static readonly AttributeKey[] DefenderKeys =
        {
            new AttributeKey("TKL", a => a.Tackling),
            new AttributeKey("MAR", a => a.Marking),
            new AttributeKey("HEA", a => a.Heading),
            new AttributeKey("STR", a => a.Strength),
            new AttributeKey("POS", a => a.Positioning),
        };

        private static readonly AttributeKey[] GoalkeeperKeys =
        {
            new AttributeKey("REF", a => a.Reflexes),
            new AttributeKey("HAN", a => a.Handling),
            new AttributeKey("1v1", a => a.OneOnOnes),
            new AttributeKey("CMD", a => a.CommandOfArea),
            new AttributeKey("GKP", a => a.GkPositioning),
        };

        // Role-specific key sets — their attributes and order mirror each role's rating formula in
        // PlayerRatings.ForRole, so the tinted rows are exactly what that role is scored on.
        private static readonly AttributeKey[] FullBackKeys =
        {
            new AttributeKey("PAC", a => a.Pace),
            new AttributeKey("CRO", a => a.Crossing),
            new AttributeKey("TKL", a => a.Tackling),
            new AttributeKey("MAR", a => a.Marking),
            new AttributeKey("STA", a => a.Stamina),
        };

        private static readonly AttributeKey[] DefensiveMidfieldKeys =
        {
            new AttributeKey("TKL", a => a.Tackling),
            new AttributeKey("MAR", a => a.Marking),
            new AttributeKey("POS", a => a.Positioning),
            new AttributeKey("PAS", a => a.Passing),
            new AttributeKey("STA", a => a.Stamina),
        };

        private static readonly AttributeKey[] AttackingMidfieldKeys =
        {
            new AttributeKey("PAS", a => a.Passing),
            new AttributeKey("TEC", a => a.Technique),
            new AttributeKey("DRI", a => a.Dribbling),
            new AttributeKey("FIR", a => a.FirstTouch),
            new AttributeKey("LON", a => a.LongShots),
        };

        private static readonly AttributeKey[] WideMidfieldKeys =
        {
            new AttributeKey("CRO", a => a.Crossing),
            new AttributeKey("PAC", a => a.Pace),
            new AttributeKey("STA", a => a.Stamina),
            new AttributeKey("PAS", a => a.Passing),
            new AttributeKey("DRI", a => a.Dribbling),
        };

        private static readonly AttributeKey[] WingKeys =
        {
            new AttributeKey("PAC", a => a.Pace),
            new AttributeKey("DRI", a => a.Dribbling),
            new AttributeKey("CRO", a => a.Crossing),
            new AttributeKey("TEC", a => a.Technique),
            new AttributeKey("FIN", a => a.Finishing),
        };

        private static readonly AttributeKey[] StrikerKeys =
        {
            new AttributeKey("FIN", a => a.Finishing),
            new AttributeKey("POS", a => a.Positioning),
            new AttributeKey("PAC", a => a.Pace),
            new AttributeKey("TEC", a => a.Technique),
            new AttributeKey("HEA", a => a.Heading),
        };

        /// <summary>The attributes a specific role is scored on — the rows the UI tints for that player.</summary>
        public static IReadOnlyList<AttributeKey> For(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper:
                    return GoalkeeperKeys;
                case PlayerRole.CentreBack:
                    return DefenderKeys;
                case PlayerRole.RightBack:
                case PlayerRole.LeftBack:
                    return FullBackKeys;
                case PlayerRole.DefensiveMidfield:
                    return DefensiveMidfieldKeys;
                case PlayerRole.CentralMidfield:
                    return MidfielderKeys;
                case PlayerRole.AttackingMidfield:
                    return AttackingMidfieldKeys;
                case PlayerRole.RightMidfield:
                case PlayerRole.LeftMidfield:
                    return WideMidfieldKeys;
                case PlayerRole.RightWing:
                case PlayerRole.LeftWing:
                    return WingKeys;
                default: // Striker
                    return StrikerKeys;
            }
        }

        public static IReadOnlyList<AttributeKey> For(Position position)
        {
            switch (position)
            {
                case Position.Forward:
                    return ForwardKeys;
                case Position.Midfielder:
                    return MidfielderKeys;
                case Position.Defender:
                    return DefenderKeys;
                case Position.Goalkeeper:
                    return GoalkeeperKeys;
                default:
                    return DefenderKeys;
            }
        }
    }
}
