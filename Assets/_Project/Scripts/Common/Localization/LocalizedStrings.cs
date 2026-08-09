namespace Gaffer.Common.Localization
{
    /// <summary>
    /// A <see cref="StringTable"/> bound to one locale — what a view holds. This is the whole public
    /// surface of "turn a key into words": the core hands out keys, this hands back text, and the two
    /// never meet in the same assembly (NON-NEGOTIABLE #8).
    ///
    /// <para>It carries the same strictness the table does. <see cref="Get(string)"/> THROWS on a key
    /// with no text in this locale and does NOT fall back to <see cref="Locales.Reference"/>: a Turkish
    /// card that quietly renders English is precisely the bug that ships, because it looks fine to
    /// everyone who reads English. <see cref="Find(string)"/> is the tolerant door, for the editor
    /// dev-tools that must draw a visible marker rather than take the window down.</para>
    ///
    /// <para>A struct, and copied by value: resolving a label allocates the formatted string and
    /// nothing else, which matters on a card that rebuilds per repaint (PERFORMANCE §4).</para>
    /// </summary>
    public readonly struct LocalizedStrings
    {
        private readonly StringTable _table;

        public LocalizedStrings(StringTable table, string locale)
        {
            _table = table;
            Locale = locale;
        }

        /// <summary>The locale every lookup through this resolver reads.</summary>
        public string Locale { get; }

        /// <summary>Whether this resolver was built against a table at all — false for <c>default</c>.</summary>
        public bool IsBound => _table != null;

        /// <summary>The text for a key, LOUDLY: a key with no row, or a row with no text in this
        /// locale, throws with both named.</summary>
        public string Get(string key)
        {
            if (_table == null)
            {
                throw new System.InvalidOperationException(
                    $"Asked for localization key '{key}' before a string table was loaded.");
            }

            return _table.Get(key, Locale);
        }

        /// <summary>The text for a key with its placeholders filled. Same strictness, plus
        /// <see cref="TextTemplate.Format"/>'s: a placeholder the arguments cannot answer throws
        /// rather than rendering a gap.</summary>
        public string Get(string key, TextArguments arguments)
        {
            return TextTemplate.Format(Get(key), arguments);
        }

        /// <summary>The text for a key, or null when this locale has none — for callers that draw
        /// their own visible marker instead of failing.</summary>
        public string Find(string key)
        {
            return _table?.Find(key, Locale);
        }

        /// <summary>The text for a key with placeholders filled, or null when this locale has none.</summary>
        public string Find(string key, TextArguments arguments)
        {
            string template = Find(key);
            return template == null ? null : TextTemplate.Format(template, arguments);
        }

        public bool Has(string key)
        {
            return Find(key) != null;
        }
    }
}
