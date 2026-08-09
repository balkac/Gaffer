using System.Collections.Generic;
using Gaffer.Domain.Players;

namespace Gaffer.Editor.Harness
{
    /// <summary>
    /// Short English words for the localization keys the core hands out — <b>the one place the editor
    /// dev-tool windows are allowed to invent display text</b>.
    ///
    /// <para><b>Why this exists.</b> CLAUDE.md NON-NEGOTIABLE #8 forbids raw user-facing text: every string
    /// a player reads must come from the localization string table, keyed. The four windows in this
    /// assembly are developer tooling — they never ship, they are English-only by construction, and there
    /// is no string table to resolve against yet — so they are <em>exempt</em>. This class is what makes
    /// the exemption explicit rather than accidental: the exemption is stated once, here, and the words
    /// live in one file that is trivially deleted rather than scattered across four windows as
    /// <c>ToString()</c>-ish label logic.</para>
    ///
    /// <para><b>Why this stayed here when the string table arrived.</b> A localization layer now exists
    /// (<c>Common/Localization</c> + <c>Infrastructure/Localization</c>) and the drama copy moved into it,
    /// so the obvious next move looks like folding this class in too. It is the wrong move, because the two
    /// hold different KINDS of string. What is in here is <b>dev-tool chrome</b>: attribute abbreviations,
    /// role codes and column headings for four editor windows that never ship, are English by construction,
    /// and are read only by the people building the game. What went into the table is <b>player-facing
    /// content</b>: it ships, a player reads it, and it is written natively in Turkish as well as English.
    /// Putting "TKL" and "Four in the morning" in the same table would mean shipping — and asking someone
    /// to translate — forty rows of workbench furniture, and it would dissolve the one boundary that makes
    /// the exemption checkable: <em>this file, and nothing else</em>. Chrome stays here under the exemption;
    /// content goes through the table. When Presentation replaces these windows, this class is deleted
    /// whole rather than migrated.</para>
    ///
    /// <para><b>Runtime UI must not use this.</b> <c>Presentation</c> resolves
    /// <see cref="PlayerRoles.GetShortLabelKey"/>, <see cref="PlayerAttributes.GetLabelKey"/> and every
    /// other <c>…Key</c> the core emits against the string table, so the same role reads "ST" in English
    /// and whatever Turkish uses. The class is deliberately <c>internal</c> to <c>Gaffer.Editor</c>, which
    /// no shipped assembly references, so that rule is enforced by the compiler and not by memory.</para>
    /// </summary>
    internal static class HarnessLabels
    {
        // Key -> word, built once from the core's own key table, so a key the core renames stops
        // resolving here loudly (an unmapped key renders as itself) instead of silently drifting.
        private static readonly Dictionary<string, string> ByAttributeKey = BuildAttributeIndex();

        /// <summary>The role's short label, for a dev-tool window. "GK", "CB", "ST".</summary>
        internal static string RoleLabel(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper:
                    return "GK";
                case PlayerRole.RightBack:
                    return "RB";
                case PlayerRole.CentreBack:
                    return "CB";
                case PlayerRole.LeftBack:
                    return "LB";
                case PlayerRole.DefensiveMidfield:
                    return "DM";
                case PlayerRole.CentralMidfield:
                    return "CM";
                case PlayerRole.AttackingMidfield:
                    return "AM";
                case PlayerRole.RightMidfield:
                    return "RM";
                case PlayerRole.LeftMidfield:
                    return "LM";
                case PlayerRole.RightWing:
                    return "RW";
                case PlayerRole.LeftWing:
                    return "LW";
                default:
                    return "ST";
            }
        }

        /// <summary>The attribute's short label, for a dev-tool window. "FIN", "PAC", "TKL".</summary>
        internal static string AttributeLabel(PlayerAttribute attribute)
        {
            switch (attribute)
            {
                case PlayerAttribute.Finishing:
                    return "FIN";
                case PlayerAttribute.Technique:
                    return "TEC";
                case PlayerAttribute.FirstTouch:
                    return "FIR";
                case PlayerAttribute.Dribbling:
                    return "DRI";
                case PlayerAttribute.Passing:
                    return "PAS";
                case PlayerAttribute.Crossing:
                    return "CRO";
                case PlayerAttribute.Heading:
                    return "HEA";
                case PlayerAttribute.LongShots:
                    return "LON";
                case PlayerAttribute.Marking:
                    return "MAR";
                case PlayerAttribute.Tackling:
                    return "TKL";
                case PlayerAttribute.Penalties:
                    return "PEN";
                case PlayerAttribute.FreeKicks:
                    return "FRK";
                case PlayerAttribute.Corners:
                    return "COR";
                case PlayerAttribute.LongThrows:
                    return "LTH";
                case PlayerAttribute.Pace:
                    return "PAC";
                case PlayerAttribute.Acceleration:
                    return "ACC";
                case PlayerAttribute.Stamina:
                    return "STA";
                case PlayerAttribute.Strength:
                    return "STR";
                case PlayerAttribute.Agility:
                    return "AGI";
                case PlayerAttribute.Jumping:
                    return "JUM";
                case PlayerAttribute.Balance:
                    return "BAL";
                case PlayerAttribute.Positioning:
                    return "POS";
                case PlayerAttribute.Reflexes:
                    return "REF";
                case PlayerAttribute.Handling:
                    return "HAN";
                case PlayerAttribute.AerialReach:
                    return "AER";
                case PlayerAttribute.CommandOfArea:
                    return "CMD";
                case PlayerAttribute.OneOnOnes:
                    return "1v1";
                case PlayerAttribute.Kicking:
                    return "KIC";
                default:
                    return "GKP";
            }
        }

        /// <summary>The short label for a role's key-stat row. <c>in</c> keeps the struct from copying.</summary>
        internal static string AttributeLabel(in AttributeKey key)
        {
            return AttributeLabel(key.Attribute);
        }

        /// <summary>
        /// The short label for a localization key the core already resolved down to a string — a
        /// <see cref="Gaffer.Application.Transfers.AttributeEstimate.LabelKey"/> on a scout report, which
        /// carries the key rather than the word precisely because a report is core data. An unrecognised
        /// key renders as itself, so a rename shows up as a visible "attr.foo.abbrev" instead of blank.
        /// </summary>
        internal static string LabelForKey(string localizationKey)
        {
            if (string.IsNullOrEmpty(localizationKey))
            {
                return string.Empty;
            }

            return ByAttributeKey.TryGetValue(localizationKey, out string label) ? label : localizationKey;
        }

        private static Dictionary<string, string> BuildAttributeIndex()
        {
            var index = new Dictionary<string, string>(32);
            foreach (PlayerAttribute attribute in (PlayerAttribute[])System.Enum.GetValues(typeof(PlayerAttribute)))
            {
                index[PlayerAttributes.GetLabelKey(attribute)] = AttributeLabel(attribute);
            }

            return index;
        }
    }
}
