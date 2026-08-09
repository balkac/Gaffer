using System;
using System.Collections.Generic;

namespace Gaffer.Common.Localization
{
    /// <summary>One locale's text for one key. A pair, nothing else — the entry owns the key.</summary>
    public readonly struct LocaleText
    {
        public LocaleText(string locale, string text)
        {
            Locale = locale;
            Text = text;
        }

        /// <summary>The locale code this text is written in ("en", "tr").</summary>
        public string Locale { get; }

        /// <summary>The text as authored, placeholders and all — never resolved, never formatted.</summary>
        public string Text { get; }
    }

    /// <summary>
    /// One row of the string table: a stable semantic key and the text written for it in each locale.
    /// This is deliberately the shape Unity's Localization CSV extension uses (a key column, then one
    /// column per locale) so Faz 7 can move the backend to <c>com.unity.localization</c> without
    /// touching a single key or template — the swap is a reader change, not a content change.
    /// </summary>
    public sealed class StringTableEntry
    {
        private readonly LocaleText[] _texts;

        public StringTableEntry(string key, IReadOnlyList<LocaleText> texts)
        {
            Key = key;
            if (texts == null)
            {
                _texts = Array.Empty<LocaleText>();
                return;
            }

            _texts = new LocaleText[texts.Count];
            for (int i = 0; i < texts.Count; i++)
            {
                _texts[i] = texts[i];
            }
        }

        /// <summary>The semantic key the core hands out ("drama.press_war.title").</summary>
        public string Key { get; }

        public IReadOnlyList<LocaleText> Texts => _texts;

        /// <summary>This entry's text in a locale, or null when the row has no column for it —
        /// tolerant, because the caller that cares (validation) wants to report every hole at once,
        /// not stop at the first.</summary>
        public string Find(string locale)
        {
            for (int i = 0; i < _texts.Length; i++)
            {
                if (string.Equals(_texts[i].Locale, locale, StringComparison.Ordinal))
                {
                    return _texts[i].Text;
                }
            }

            return null;
        }
    }
}
