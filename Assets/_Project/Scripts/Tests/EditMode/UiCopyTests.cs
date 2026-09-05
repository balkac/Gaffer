using System.Collections.Generic;
using Gaffer.Common.Localization;
using Gaffer.Domain.Players;
using Gaffer.Infrastructure.Localization;
using Gaffer.Presentation;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The screens' own copy. Guarded exactly as the drama and narrative copy is, and for a sharper
    /// reason: a missing interface string is a BLANK BUTTON on a device, in a language whoever wrote the
    /// key does not read. There is no stack trace and nothing in the log — it just looks like a layout bug.
    /// </summary>
    public sealed class UiCopyTests
    {
        [Test]
        public void EveryScreenKey_HasWordsInEveryShippedLocale()
        {
            StringTable table = GameStrings.Default;
            var missing = new List<string>();

            for (int i = 0; i < UiTextKeys.All.Count; i++)
            {
                string key = UiTextKeys.All[i];
                foreach (string locale in Locales.Shipped)
                {
                    if (string.IsNullOrEmpty(table.Find(key, locale)))
                    {
                        missing.Add($"{key} ({locale})");
                    }
                }
            }

            Assert.That(missing, Is.Empty, "These would draw blank: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryScreenKey_IsListedOnce()
        {
            // The list is what makes the guard above possible; a key added twice is a copy row nobody owns,
            // and a key added to neither is a key nothing checks.
            var seen = new HashSet<string>();
            for (int i = 0; i < UiTextKeys.All.Count; i++)
            {
                Assert.That(seen.Add(UiTextKeys.All[i]), Is.True, $"'{UiTextKeys.All[i]}' is listed twice.");
                Assert.That(
                    UiTextKeys.All[i].StartsWith("ui.")
                        || UiTextKeys.All[i].StartsWith("role.")
                        || UiTextKeys.All[i].StartsWith("tactics."),
                    Is.True,
                    UiTextKeys.All[i]);
            }
        }

        [Test]
        public void EveryScreenTemplate_IsValidAuthoredContent()
        {
            // The season screen is the first interface copy to interpolate a value ("Top {count}"), so the
            // rules the narrative rows are held to apply here too: every placeholder one the formatter
            // carries, every brace closed, no suffix hung off a value. Screens resolve through
            // UiWords.Or, which does not throw — so an invalid template would draw its own braces rather
            // than fail, and only this test would say so.
            StringTable table = GameStrings.Default;
            var problems = new List<string>();

            for (int i = 0; i < UiTextKeys.All.Count; i++)
            {
                string key = UiTextKeys.All[i];
                foreach (string locale in Locales.Shipped)
                {
                    string template = table.Find(key, locale);
                    if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0)
                    {
                        continue;
                    }

                    Gaffer.Common.Result validated = TextTemplate.Validate(template);
                    if (validated.IsFailure)
                    {
                        problems.Add($"{key} ({locale}): {validated.Error}");
                    }
                }
            }

            Assert.That(problems, Is.Empty, string.Join(" | ", problems));
        }

        [Test]
        public void EveryRoleAbbreviation_FitsTheColumnItIsDrawnIn()
        {
            // `.row__role` is a fixed 100px column; the label does not reflow, it clips. Three characters
            // is the shape every abbreviation was written to — the guard is here so a fourth is a DECISION
            // someone makes against this test, not a surprise on a phone in the language they don't read.
            StringTable table = GameStrings.Default;
            var wrong = new List<string>();

            foreach (PlayerRole role in System.Enum.GetValues(typeof(PlayerRole)))
            {
                string key = PlayerRoles.GetShortLabelKey(role);
                foreach (string locale in Locales.Shipped)
                {
                    string words = table.Find(key, locale);
                    if (!string.IsNullOrEmpty(words) && words.Length > 3)
                    {
                        wrong.Add($"{key} ({locale}): '{words}' is {words.Length} chars");
                    }
                }
            }

            Assert.That(wrong, Is.Empty, string.Join(" | ", wrong));
        }

        [Test]
        public void EveryRoleAbbreviation_IsDistinctWithinItsLocale()
        {
            // Two positions sharing a label is worse than a long one: the board looks right and reads
            // wrong, and nobody files it as a bug because nothing is visibly broken.
            StringTable table = GameStrings.Default;

            foreach (string locale in Locales.Shipped)
            {
                var seen = new Dictionary<string, PlayerRole>();
                foreach (PlayerRole role in System.Enum.GetValues(typeof(PlayerRole)))
                {
                    string words = table.Find(PlayerRoles.GetShortLabelKey(role), locale);
                    if (string.IsNullOrEmpty(words))
                    {
                        continue;
                    }

                    Assert.That(
                        seen.ContainsKey(words),
                        Is.False,
                        $"{locale}: {role} and {(seen.ContainsKey(words) ? seen[words].ToString() : string.Empty)} are both '{words}'.");
                    seen[words] = role;
                }
            }
        }

        [Test]
        public void EveryScreenString_IsShortEnoughForAPhone()
        {
            // Interface copy is not prose. A label that wraps to two lines on a narrow phone breaks a row
            // height the whole list is built on, and the failure shows up as clipped text rather than as a
            // string that was too long.
            StringTable table = GameStrings.Default;
            var tooLong = new List<string>();

            for (int i = 0; i < UiTextKeys.All.Count; i++)
            {
                string key = UiTextKeys.All[i];
                foreach (string locale in Locales.Shipped)
                {
                    string words = table.Find(key, locale);
                    if (!string.IsNullOrEmpty(words) && words.Length > 28)
                    {
                        tooLong.Add($"{key} ({locale}): {words.Length} chars");
                    }
                }
            }

            Assert.That(tooLong, Is.Empty, string.Join(" | ", tooLong));
        }
    }
}
