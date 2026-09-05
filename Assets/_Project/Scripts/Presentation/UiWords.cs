using Gaffer.Common.Localization;

namespace Gaffer.Presentation
{
    /// <summary>
    /// How a screen asks for words when a missing row must not take the screen down.
    ///
    /// <para><see cref="LocalizedStrings.Get(string)"/> throws on a key this locale has no words for, and
    /// that strictness is right for the core: a Turkish card quietly rendering English is the bug that
    /// ships. A SCREEN needs the other answer. A label that throws takes the whole panel with it — the
    /// manager loses his squad because somebody forgot one row — so a screen draws the key instead. It
    /// looks wrong, it names its own fix, and everything around it still works.</para>
    ///
    /// <para>One place, because the bargain was being restated in four: the squad screen, the match
    /// report, the beat list and the moment line each had their own copy, and four copies of a policy are
    /// four chances for one of them to start throwing.</para>
    /// </summary>
    public static class UiWords
    {
        /// <summary>The words for a key, or the key itself when this locale has none.</summary>
        public static string Or(this LocalizedStrings text, string key)
        {
            string words = text.IsBound ? text.Find(key) : null;
            return string.IsNullOrEmpty(words) ? key : words;
        }

        /// <summary>The words for a key with its placeholders filled, or the key itself when this locale
        /// has none.</summary>
        public static string Or(this LocalizedStrings text, string key, TextArguments arguments)
        {
            string words = text.IsBound ? text.Find(key, arguments) : null;
            return string.IsNullOrEmpty(words) ? key : words;
        }
    }
}
