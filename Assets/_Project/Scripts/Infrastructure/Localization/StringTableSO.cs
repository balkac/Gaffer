using System;
using System.Collections.Generic;
using Gaffer.Common;
using Gaffer.Common.Localization;
using UnityEngine;

namespace Gaffer.Infrastructure.Localization
{
    /// <summary>
    /// One authored row of the string table: a key and its text in each shipped locale, side by side
    /// in the Inspector so a translator sees the English he is writing against.
    ///
    /// <para>The locale fields are deliberately EXPLICIT rather than a nested list. A nested list draws
    /// as two collapsed foldouts per row in the Inspector, which is the difference between copy that
    /// gets read and copy that gets skipped; and <see cref="Locales.Shipped"/> is two entries long by
    /// decision, not by accident (CLAUDE.md: launch is <c>en</c> + <c>tr</c>). A third launch locale is
    /// a deliberate edit here — and <c>StringTableShapeTests</c> fails the moment
    /// <see cref="Locales.Shipped"/> grows past what this row can hold, so it cannot be forgotten.</para>
    /// </summary>
    [Serializable]
    public sealed class LocalizedStringRow
    {
        [Tooltip("The key the core hands out, e.g. drama.press_war.title.")]
        [SerializeField] private string key = string.Empty;

        [Tooltip("English — the reference locale. Written first; every other locale is written against it.")]
        [SerializeField] [TextArea(1, 4)] private string english = string.Empty;

        [Tooltip("Türkçe — native, not a translation of the English. Interpolated names never take a suffix.")]
        [SerializeField] [TextArea(1, 4)] private string turkish = string.Empty;

        public string Key => key;

        /// <summary>Fills the row — used by the editor bootstrap when it materialises the built-in copy.</summary>
        public void Author(string rowKey, string en, string tr)
        {
            key = rowKey;
            english = en;
            turkish = tr;
        }

        public StringTableEntry ToEntry()
        {
            return new StringTableEntry(
                key,
                new List<LocaleText>(2)
                {
                    new LocaleText(Locales.Reference, english),
                    new LocaleText(Locales.Turkish, turkish),
                });
        }
    }

    /// <summary>
    /// The Unity authoring surface for the words: a list of <see cref="LocalizedStringRow"/> mapped to
    /// the pure <see cref="StringTable"/>, the same way <c>TraitCatalogSO</c> maps to
    /// <c>TraitCatalog</c>. Config-as-override — an empty asset (or no asset at all) means
    /// <see cref="GameStrings.Default"/>, so the built-in copy is always the floor and an authored
    /// table replaces it wholesale (ARCHITECTURE §7).
    ///
    /// <para>
    /// STRICTNESS POSTURE (ARCHITECTURE §11), stated rather than defaulted: this content SHIPS INSIDE
    /// THE BUILD, so it takes the posture <c>TraitCatalogSO.Load</c> already established for authored
    /// content. <see cref="Load"/> is STRICT — a key with no text in a shipped locale, a key authored
    /// twice, or a template that breaks the placeholder rules fails the load with the key NAMED. A key
    /// with no words is the same class of bug as a dangling trait slug: invisible at runtime, and it
    /// costs the card the only thing it was there to say. There is deliberately no fallback to English
    /// on a missing Turkish row either; see <see cref="StringTable"/> for that argument in full.
    /// </para>
    ///
    /// <para><b>Loud in three places, not one.</b> The copy-coverage test fails in CI on a key the
    /// catalog references and this table has no words for; <see cref="Load"/> refuses the asset at the
    /// composition root; and the editor windows draw a visible marker for the one key they could not
    /// resolve rather than a blank line. A missing key never renders as nothing.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Content/String Table", fileName = "StringTable")]
    public sealed class StringTableSO : ScriptableObject
    {
        [Tooltip("The player-facing copy. Empty = the built-in table in GameStrings.")]
        [SerializeField] private List<LocalizedStringRow> rows = new List<LocalizedStringRow>();

        /// <summary>Points the asset at a set of rows — used by the editor tooling when it
        /// materialises the built-in copy.</summary>
        public void Author(List<LocalizedStringRow> authored)
        {
            rows = new List<LocalizedStringRow>(authored);
        }

        /// <summary>
        /// The validating entry point — VALIDATE ON LOAD, per the posture above. Maps the rows, then
        /// checks the result as shipped content against <paramref name="requiredKeys"/> (the keys the
        /// core actually hands out, from <c>DramaCatalog.CopyKeys()</c>), so a hole fails here — at the
        /// composition root, where a load can still be refused — instead of on a card.
        /// </summary>
        public Result<StringTable> Load(IReadOnlyList<string> requiredKeys)
        {
            StringTable table = Map();
            Result validation = table.Validate(requiredKeys);
            return validation.IsFailure
                ? Result<StringTable>.Failure($"{name}: {validation.Error}")
                : Result<StringTable>.Success(table);
        }

        /// <summary>
        /// Compatibility shim for call sites that cannot yet refuse a load — the editor dev-tool
        /// windows. It runs the SAME validation and reports a failure as a Unity error (which the Test
        /// Runner fails a test on, so it is not silent) but still hands back the table, so a session
        /// with one bad row stays usable and the bad row shows as a marker on the card. Prefer
        /// <see cref="Load"/>: it is the one that lets a caller decline to run on broken content.
        /// </summary>
        public StringTable ToTable(IReadOnlyList<string> requiredKeys)
        {
            Result<StringTable> loaded = Load(requiredKeys);
            if (loaded.IsSuccess)
            {
                return loaded.Value;
            }

            Debug.LogError(loaded.Error, this);
            return Map();
        }

        /// <summary>Rows to the pure table, no validation — an empty or all-null list is the
        /// config-as-override fallback to the built-in copy, because "author nothing" is a valid
        /// answer; "author it wrong" is what <see cref="Load"/> refuses.</summary>
        private StringTable Map()
        {
            if (rows == null || rows.Count == 0)
            {
                return GameStrings.Default;
            }

            var mapped = new List<StringTableEntry>(rows.Count);
            foreach (LocalizedStringRow row in rows)
            {
                if (row != null)
                {
                    mapped.Add(row.ToEntry());
                }
            }

            return mapped.Count == 0 ? GameStrings.Default : new StringTable(Locales.Shipped, mapped);
        }
    }
}
