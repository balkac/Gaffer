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
    /// pace, and positioning; a centre-back on tackling, marking, and heading; a keeper on the keeping
    /// group. This marks a role's <em>importance</em>, not a player's value — the UI tints these rows,
    /// and the strength builder weights the same axes. The mapping lives in the pure Domain (no config
    /// asset yet); a data-driven <c>RoleSO</c> can replace it later without touching call sites.
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
