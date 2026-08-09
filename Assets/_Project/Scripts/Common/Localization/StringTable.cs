using System;
using System.Collections.Generic;
using System.Text;

namespace Gaffer.Common.Localization
{
    /// <summary>
    /// The words, keyed. A set of <see cref="StringTableEntry"/> rows plus the locales they cover —
    /// the pure half of the localization layer, so the copy check runs headless in <c>dotnet test</c>
    /// and the Unity asset (<c>StringTableSO</c>) is only an authoring surface that maps onto this,
    /// exactly as <c>TraitSO</c> maps onto <c>Trait</c>.
    ///
    /// <para><b>STRICTNESS POSTURE (ARCHITECTURE §11), stated rather than defaulted.</b> This content
    /// SHIPS INSIDE THE BUILD, so it takes the same posture <c>TraitCatalogSO.Load</c> already
    /// establishes for authored content: <see cref="Validate"/> is STRICT and names every hole, and
    /// <see cref="Get"/> THROWS on a key with no text. A key with no words is the same class of bug as
    /// a dangling trait slug — invisible at runtime, silently reducing something mechanical to a gap
    /// on a card. There is deliberately NO fallback to the reference locale on a miss: a Turkish card
    /// that quietly renders English is the silent failure this posture exists to remove, and it is the
    /// one bug a player would report and a developer would never see. <see cref="Find"/> stays
    /// tolerant for the one caller that wants to collect misses rather than trip over the first.</para>
    /// </summary>
    public sealed class StringTable
    {
        private readonly Dictionary<string, StringTableEntry> _byKey;
        private readonly StringTableEntry[] _entries;
        private readonly string[] _locales;

        public StringTable(IReadOnlyList<string> locales, IReadOnlyList<StringTableEntry> entries)
        {
            _locales = Copy(locales);
            _entries = new StringTableEntry[entries == null ? 0 : entries.Count];
            _byKey = new Dictionary<string, StringTableEntry>(_entries.Length, StringComparer.Ordinal);
            for (int i = 0; i < _entries.Length; i++)
            {
                StringTableEntry entry = entries[i];
                _entries[i] = entry;
                if (entry != null && !string.IsNullOrEmpty(entry.Key))
                {
                    // Last one wins here and Validate reports the duplicate — a table that refused to
                    // build could not tell the author WHICH key was doubled.
                    _byKey[entry.Key] = entry;
                }
            }
        }

        /// <summary>The locale codes this table carries a column for, in authored order. Named
        /// <c>LocaleCodes</c> and not <c>Locales</c> so it cannot shadow the <see cref="Localization.Locales"/>
        /// class this type checks itself against.</summary>
        public IReadOnlyList<string> LocaleCodes => _locales;

        public IReadOnlyList<StringTableEntry> Entries => _entries;

        public bool Contains(string key)
        {
            return !string.IsNullOrEmpty(key) && _byKey.ContainsKey(key);
        }

        /// <summary>The text for a key in a locale, or null when either is missing — tolerant, for
        /// validation and for the editor shim that wants to draw a visible marker instead of dying.</summary>
        public string Find(string key, string locale)
        {
            if (string.IsNullOrEmpty(key) || !_byKey.TryGetValue(key, out StringTableEntry entry))
            {
                return null;
            }

            string text = entry.Find(locale);
            return string.IsNullOrEmpty(text) ? null : text;
        }

        /// <summary>
        /// The text for a key in a locale, LOUDLY. A missing key or a locale with no text for it throws
        /// with both named — the shipped-content posture above: this is a broken invariant, not a
        /// recoverable failure, and the alternative (an empty card, or English inside Turkish) is
        /// exactly the silent bug the layer is here to prevent.
        /// </summary>
        public string Get(string key, string locale)
        {
            string text = Find(key, locale);
            if (text != null)
            {
                return text;
            }

            throw new KeyNotFoundException(
                Contains(key)
                    ? $"Localization key '{key}' has no text for locale '{locale}'."
                    : $"Localization key '{key}' is not in the string table (locale '{locale}').");
        }

        /// <summary>A resolver bound to one locale — what UI code holds.</summary>
        public LocalizedStrings For(string locale)
        {
            return new LocalizedStrings(this, locale);
        }

        /// <summary>
        /// Checks the table as authored content that ships in the build: every shipped locale has a
        /// column, every row has a non-empty key and non-empty text in EVERY locale, no key is authored
        /// twice, every template obeys the placeholder rules (<see cref="TextTemplate.Validate"/>), and
        /// every key in <paramref name="requiredKeys"/> — the keys the core actually hands out — is
        /// present. Reports every problem at once, each naming its key, so a new event that ships with
        /// no words fails on the commit that introduces it instead of on the card.
        /// </summary>
        public Result Validate(IReadOnlyList<string> requiredKeys)
        {
            var problems = new List<string>();

            for (int i = 0; i < _locales.Length; i++)
            {
                // A column nobody ships is not an error — it is a language in progress. A SHIPPED
                // locale with no column is, and that is the direction this check runs.
                if (string.IsNullOrEmpty(_locales[i]))
                {
                    problems.Add($"The string table has a locale column with no code at index {i}.");
                }
            }

            for (int i = 0; i < Localization.Locales.Shipped.Count; i++)
            {
                string shipped = Localization.Locales.Shipped[i];
                if (!HasLocale(shipped))
                {
                    problems.Add($"The string table has no column for shipped locale '{shipped}'.");
                }
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _entries.Length; i++)
            {
                CollectEntryProblems(_entries[i], i, seen, problems);
            }

            if (requiredKeys != null)
            {
                for (int i = 0; i < requiredKeys.Count; i++)
                {
                    string key = requiredKeys[i];
                    if (string.IsNullOrEmpty(key))
                    {
                        problems.Add("Something asked the string table for an empty key.");
                    }
                    else if (!Contains(key))
                    {
                        problems.Add($"Key '{key}' is referenced but has no row in the string table — it would render as nothing.");
                    }
                }
            }

            return Describe(problems);
        }

        /// <summary>
        /// The keys this table has words for that nothing in <paramref name="referencedKeys"/> asks
        /// for — dead copy, usually the other half of a rename.
        ///
        /// <para><b>This REPORTS; it does not fail.</b> <see cref="Validate"/> is the strict direction
        /// (a referenced key with no words would render as nothing, so it breaks the build); an orphan
        /// renders as nothing at all, costs nothing, and is a normal intermediate state — copy is
        /// routinely written before the screen that shows it. A caller that owns a whole key prefix
        /// can still be strict about ITS OWN prefix, which is what the drama copy test does: every
        /// <c>drama.*</c> row must be reachable from the catalog, because there the catalog IS the
        /// complete list of callers.</para>
        /// </summary>
        public IReadOnlyList<string> FindOrphanKeys(IReadOnlyList<string> referencedKeys)
        {
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            if (referencedKeys != null)
            {
                for (int i = 0; i < referencedKeys.Count; i++)
                {
                    if (!string.IsNullOrEmpty(referencedKeys[i]))
                    {
                        referenced.Add(referencedKeys[i]);
                    }
                }
            }

            var orphans = new List<string>();
            for (int i = 0; i < _entries.Length; i++)
            {
                StringTableEntry entry = _entries[i];
                if (entry != null && !string.IsNullOrEmpty(entry.Key) && !referenced.Contains(entry.Key))
                {
                    orphans.Add(entry.Key);
                }
            }

            return orphans;
        }

        private void CollectEntryProblems(StringTableEntry entry, int index, HashSet<string> seen, List<string> problems)
        {
            if (entry == null)
            {
                problems.Add($"The string table has a missing row at index {index}.");
                return;
            }

            if (string.IsNullOrEmpty(entry.Key))
            {
                problems.Add($"The string table row at index {index} has no key.");
                return;
            }

            if (!seen.Add(entry.Key))
            {
                problems.Add($"Key '{entry.Key}' is authored more than once.");
            }

            for (int i = 0; i < _locales.Length; i++)
            {
                string locale = _locales[i];
                string text = entry.Find(locale);
                if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
                {
                    problems.Add($"Key '{entry.Key}' has no '{locale}' text.");
                    continue;
                }

                Result template = TextTemplate.Validate(text);
                if (template.IsFailure)
                {
                    problems.Add($"Key '{entry.Key}' ({locale}): {template.Error}.");
                }
            }
        }

        private bool HasLocale(string locale)
        {
            for (int i = 0; i < _locales.Length; i++)
            {
                if (string.Equals(_locales[i], locale, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            var copied = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                copied[i] = values[i];
            }

            return copied;
        }

        // One report per load, the shape TraitCatalog.Describe already set for authored content — it is
        // reimplemented rather than shared because Common cannot reference Domain (ARCHITECTURE §1).
        private static Result Describe(List<string> problems)
        {
            if (problems.Count == 0)
            {
                return Result.Success();
            }

            var message = new StringBuilder();
            message.Append("String table is invalid: ");
            for (int i = 0; i < problems.Count; i++)
            {
                if (i > 0)
                {
                    message.Append(' ');
                }

                message.Append(problems[i]);
            }

            return Result.Failure(message.ToString());
        }
    }
}
