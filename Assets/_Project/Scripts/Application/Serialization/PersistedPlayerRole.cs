using System;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// The persisted representation of <see cref="PlayerRole"/> — the one place that turns a role into the
    /// text a save carries and back. It exists so no caller ever writes a bare cast or a bare
    /// <c>Enum.Parse</c>: from schema v5 the save stores the member NAME, because a name survives inserting
    /// or reordering a role while an ordinal silently re-roles every saved player (CONVENTIONS §6,
    /// UNITY.md §7).
    /// </summary>
    public static class PersistedPlayerRole
    {
        /// <summary>The text written into the save for a role. The name is wire format from v5 on — renaming
        /// a <see cref="PlayerRole"/> member breaks saves already on players' devices and needs its own
        /// migration step, exactly as Microsoft's data-contract versioning rules describe (CONVENTIONS §6).</summary>
        public static string ToName(PlayerRole role)
        {
            return role.ToString();
        }

        /// <summary>
        /// Reads a persisted role name. Guarded three times on purpose, because each guard catches what the
        /// one before it lets through: <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/> alone returns
        /// <c>true</c> for any numeric string (<c>"999"</c> parses to an undefined value) and, for a
        /// comma-separated list, to a bitwise combination that is not a member either; <see
        /// cref="Enum.IsDefined"/> rejects those but still accepts <c>"3"</c>, because ordinal 3 IS a defined
        /// member — which would quietly re-admit the very ordinal wire format v5 exists to retire. So the
        /// last guard requires the text to be exactly what <see cref="ToName"/> would have written. Case-
        /// SENSITIVE by design: this is a machine string, and a case-insensitive compare is the Turkish-i bug
        /// waiting for a Turkish-locale device (CONVENTIONS §6). The save path needs all three because the
        /// data is a file a player could have edited.
        /// </summary>
        public static bool TryParse(string name, out PlayerRole role)
        {
            // The parsed value is only published on success, so a caller that ignores the bool cannot end up
            // holding the undefined value TryParse produced for "999".
            if (!string.IsNullOrEmpty(name)
                && Enum.TryParse(name, out PlayerRole parsed)
                && Enum.IsDefined(typeof(PlayerRole), parsed)
                && string.Equals(ToName(parsed), name, StringComparison.Ordinal))
            {
                role = parsed;
                return true;
            }

            role = default;
            return false;
        }

        /// <summary>
        /// Reads a role from a pre-v5 save's raw ordinal, for the v4 → v5 migration step only. This works at
        /// all only because <see cref="PlayerRole"/>'s numeric values are pinned — that pinning is what keeps
        /// old saves readable, and is why the enum's values must never be reordered or reused. An ordinal
        /// that is not a defined member means the document is corrupt, and the caller surfaces it as a
        /// <c>Result</c> failure rather than defaulting to whichever role happens to sit at that number.
        /// </summary>
        public static bool TryFromLegacyOrdinal(int ordinal, out PlayerRole role)
        {
            if (!Enum.IsDefined(typeof(PlayerRole), ordinal))
            {
                role = default;
                return false;
            }

            role = (PlayerRole)ordinal;
            return true;
        }
    }
}
