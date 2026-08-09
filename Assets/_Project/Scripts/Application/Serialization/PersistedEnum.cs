using System;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// Turns any enum that crosses the save boundary into the text a save carries, and back
    /// (NON-NEGOTIABLE #9). It is <see cref="PersistedPlayerRole"/>'s rule made reusable, for the enums
    /// that arrived with schema v6 — the four tactical axes — so that no caller ever writes a bare cast or
    /// a bare <c>Enum.Parse</c> against a string that came out of a file.
    /// <para>
    /// <see cref="PersistedPlayerRole"/> stays its own type rather than calling this: it also has to read a
    /// pre-v5 ORDINAL, which is a legacy affordance that must not be offered to a new enum. A generic
    /// "read the ordinal too" here would quietly re-admit the wire format v5 exists to retire.
    /// </para>
    /// </summary>
    public static class PersistedEnum
    {
        /// <summary>The text written into the save for a member. The name is wire format: renaming a member
        /// breaks saves already on players' devices and needs its own migration step (CONVENTIONS §6).</summary>
        public static string ToName<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
            return value.ToString();
        }

        /// <summary>
        /// Reads a persisted member name. Guarded three times for the reasons
        /// <see cref="PersistedPlayerRole.TryParse"/> spells out and which are worth not re-deriving:
        /// <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/> alone accepts any numeric string
        /// (<c>"999"</c> parses to an undefined value) and a comma-separated list (a bitwise combination
        /// that is no member either); <see cref="Enum.IsDefined"/> rejects those but still accepts
        /// <c>"3"</c>, because ordinal 3 IS a member — which would let the ordinal back in through the side
        /// door. So the last guard requires the text to be exactly what <see cref="ToName{TEnum}"/> would
        /// have written. Case-SENSITIVE by design: this is a machine string, and a case-insensitive compare
        /// is the Turkish-i bug waiting for a Turkish-locale device (CONVENTIONS §6).
        /// </summary>
        public static bool TryParse<TEnum>(string name, out TEnum value)
            where TEnum : struct, Enum
        {
            // The parsed value is only published on success, so a caller that ignores the bool cannot end
            // up holding the undefined value TryParse produced for "999".
            if (!string.IsNullOrEmpty(name)
                && Enum.TryParse(name, out TEnum parsed)
                && Enum.IsDefined(typeof(TEnum), parsed)
                && string.Equals(ToName(parsed), name, StringComparison.Ordinal))
            {
                value = parsed;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// The tolerant read the save path wants: the persisted member, or <paramref name="fallback"/> when
        /// the text is missing or is not a member this build knows. Tolerant is correct HERE and nowhere
        /// near authored content (ARCHITECTURE §11): a save outlives the build, and a tactical axis this
        /// build cannot read should cost the manager one dropdown, not the run. A field whose default would
        /// be a lie — a player's role, say — must use <see cref="TryParse{TEnum}"/> and report the failure
        /// instead.
        /// </summary>
        public static TEnum ParseOr<TEnum>(string name, TEnum fallback)
            where TEnum : struct, Enum
        {
            return TryParse(name, out TEnum parsed) ? parsed : fallback;
        }
    }
}
