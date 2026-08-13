using System;
using Gaffer.Application.Narrative;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// The persisted representation of <see cref="CareerMomentKind"/> — the one place that turns a kind
    /// into the text a save carries and back, so no caller writes a bare cast or a bare
    /// <c>Enum.Parse</c>. The name is the wire format (NON-NEGOTIABLE #9): the vocabulary of moments is
    /// expected to GROW, and a name survives inserting a kind where an ordinal would silently re-label
    /// every moment in every save on every device.
    ///
    /// <para>Guarded exactly as <see cref="PersistedPlayerRole"/> is, and for the same reasons — each
    /// guard catches what the one before it lets through, and the last one refuses a bare ordinal
    /// (<c>"3"</c> parses to a defined member) so the numeric wire format cannot creep back in. Case
    /// SENSITIVE: this is a machine string, and a case-insensitive compare is the Turkish-i bug waiting
    /// for a Turkish-locale device (CONVENTIONS §6).</para>
    /// </summary>
    public static class PersistedCareerMomentKind
    {
        public static string ToName(CareerMomentKind kind)
        {
            return kind.ToString();
        }

        public static bool TryParse(string name, out CareerMomentKind kind)
        {
            if (!string.IsNullOrEmpty(name)
                && Enum.TryParse(name, out CareerMomentKind parsed)
                && Enum.IsDefined(typeof(CareerMomentKind), parsed)
                && string.Equals(ToName(parsed), name, StringComparison.Ordinal))
            {
                kind = parsed;
                return true;
            }

            kind = default;
            return false;
        }
    }
}
