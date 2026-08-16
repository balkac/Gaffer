using System.Collections.Generic;
using Gaffer.Common.Localization;
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
                Assert.That(UiTextKeys.All[i], Does.StartWith("ui."), UiTextKeys.All[i]);
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
