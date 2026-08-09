using System.Collections.Generic;

namespace Gaffer.Common.Localization
{
    /// <summary>
    /// The locale codes the game ships, in one place. Codes, never words — this file names languages,
    /// it does not contain any (CLAUDE.md NON-NEGOTIABLE #8: <c>Common</c>/<c>Domain</c>/<c>Application</c>
    /// carry keys, the string table carries text).
    /// <para>
    /// <b>English is the reference locale</b> (TDD §12.5): it is written first, everything else is
    /// written against it, and <see cref="StringTable.Validate"/> treats a locale that does not cover
    /// the reference as a hole rather than as a language that is simply behind. Turkish ships at
    /// launch and is written NATIVELY — a Turkish row that reads like a translation of the English
    /// one is a bug this file cannot catch, but the coverage test can at least prove the row exists.
    /// </para>
    /// </summary>
    public static class Locales
    {
        /// <summary>The source locale. Copy is authored here first and every other locale is written against it.</summary>
        public const string Reference = "en";

        /// <summary>The second launch locale (TDD §12.5), written native rather than translated.</summary>
        public const string Turkish = "tr";

        private static readonly string[] ShippedCodes = { Reference, Turkish };

        /// <summary>Every locale that must be complete before the game ships. Adding one here makes
        /// every coverage check demand it, which is the point.</summary>
        public static IReadOnlyList<string> Shipped => ShippedCodes;

        /// <summary>Whether this code is one the game ships — ordinal, because a locale code is an
        /// identifier and not a word in any language.</summary>
        public static bool IsShipped(string locale)
        {
            for (int i = 0; i < ShippedCodes.Length; i++)
            {
                if (string.Equals(ShippedCodes[i], locale, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
