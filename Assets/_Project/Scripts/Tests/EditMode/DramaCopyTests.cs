using System.Collections.Generic;
using Gaffer.Common;
using Gaffer.Common.Localization;
using Gaffer.Domain.Drama;
using Gaffer.Infrastructure.Localization;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The guard on the words. Drama copy is content that ships inside the build, so it takes the strict
    /// posture (ARCHITECTURE §11) the trait and drama catalogs already take: a hole fails the build with
    /// the offending key NAMED, rather than reaching a player as a blank line on a card.
    ///
    /// <para>Three separate things are pinned here, and they fail for three different reasons:</para>
    /// <list type="bullet">
    /// <item><b>Coverage.</b> Every key <c>DramaCatalog.Default</c> hands out has words in EVERY shipped
    /// locale. This is the one that stops a new event shipping with no copy — the exact state all ten
    /// events were in before this test existed, when <c>TitleKey</c> and <c>BodyKey</c> were read by
    /// nothing and the card showed the id instead.</item>
    /// <item><b>The placeholder rule.</b> An interpolated name stays ATOMIC and never takes a suffix,
    /// in BOTH locales. Turkish is a launch locale and a Turkish case ending is chosen by the name's own
    /// vowels ("Ali'yi", "Umut'u"), so <c>{player}'ı kadro dışı bırak</c> cannot be filled by
    /// substitution for every squad — ever. English does not have the problem and obeys the rule anyway,
    /// so an English template can be handed to a translator without being restructured first.</item>
    /// <item><b>Loudness.</b> A missing key throws with the key named and does NOT fall back to English.
    /// A Turkish card that quietly renders English is the bug that ships, because it looks correct to
    /// everyone who can read the fallback.</item>
    /// </list>
    /// </summary>
    public sealed class DramaCopyTests
    {
        private const string DramaPrefix = "drama.";

        // ----- Coverage: the guard that stops an event shipping with no words --------------------------

        [Test]
        public void Validate_TheShippedCopy_HasWordsForEveryKeyTheDramaCatalogHandsOut()
        {
            Result validation = GameStrings.Default.Validate(DramaCatalog.Default.CopyKeys());

            Assert.That(validation.IsSuccess, Is.True, validation.Error);
        }

        [Test]
        public void Validate_AKeyTheCatalogReferencesWithNoRow_FailsNamingThatKey()
        {
            var table = new StringTable(Locales.Shipped, new[] { Row("drama.press_war.title", "Four minutes", "Dört dakika") });

            Result validation = table.Validate(new[] { "drama.press_war.title", "drama.press_war.body" });

            Assert.That(validation.IsFailure, Is.True);
            Assert.That(validation.Error, Does.Contain("drama.press_war.body"));
        }

        [Test]
        public void Validate_ARowWithNoTurkishText_FailsNamingTheKeyAndTheLocale()
        {
            var table = new StringTable(Locales.Shipped, new[] { Row("drama.budget_cut.title", "The board has been through the books", string.Empty) });

            Result validation = table.Validate(new[] { "drama.budget_cut.title" });

            Assert.That(validation.IsFailure, Is.True);
            Assert.That(validation.Error, Does.Contain("drama.budget_cut.title"));
            Assert.That(validation.Error, Does.Contain(Locales.Turkish));
        }

        [Test]
        public void Copy_TurkishCoverage_EqualsEnglishCoverage()
        {
            var missing = new List<string>();
            IReadOnlyList<StringTableEntry> entries = GameStrings.Default.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                StringTableEntry entry = entries[i];
                bool english = !string.IsNullOrEmpty(entry.Find(Locales.Reference));
                bool turkish = !string.IsNullOrEmpty(entry.Find(Locales.Turkish));
                if (english != turkish)
                {
                    missing.Add(entry.Key + (english ? " has no Turkish" : " has no English"));
                }
            }

            Assert.That(missing, Is.Empty, string.Join("; ", missing));
        }

        [Test]
        public void Copy_TheDramaCatalog_ReferencesEveryDramaRowInTheTable()
        {
            // Strict for `drama.*` only, and deliberately so. The catalog IS the complete list of callers
            // for that prefix, so a `drama.` row nothing references is dead copy — the other half of a
            // rename. Keys outside the prefix are REPORTED, not failed: copy is routinely written before
            // the screen that shows it, and failing that would punish working ahead.
            IReadOnlyList<string> orphans = GameStrings.Default.FindOrphanKeys(DramaCatalog.Default.CopyKeys());

            var deadDramaCopy = new List<string>();
            var notYetUsed = new List<string>();
            for (int i = 0; i < orphans.Count; i++)
            {
                if (orphans[i].StartsWith(DramaPrefix, System.StringComparison.Ordinal))
                {
                    deadDramaCopy.Add(orphans[i]);
                }
                else
                {
                    notYetUsed.Add(orphans[i]);
                }
            }

            if (notYetUsed.Count > 0)
            {
                TestContext.WriteLine("Copy written ahead of its screen (not an error): " + string.Join(", ", notYetUsed));
            }

            Assert.That(deadDramaCopy, Is.Empty, "Dead drama copy: " + string.Join(", ", deadDramaCopy));
        }

        [Test]
        public void Copy_EveryEventTitle_IsWrittenRatherThanDerivedFromItsId()
        {
            // The failure this replaces: the card used to print the event id with its dashes removed, so
            // "night-club-scandal" became the headline "NIGHT CLUB SCANDAL". A title that still matches its
            // own slug is that failure written down rather than fixed.
            LocalizedStrings english = GameStrings.Default.For(Locales.Reference);
            IReadOnlyList<DramaEvent> events = DramaCatalog.Default.Events;
            for (int i = 0; i < events.Count; i++)
            {
                string slug = events[i].Id.Value.Replace('-', ' ');
                string title = english.Get(events[i].TitleKey);
                Assert.That(
                    title,
                    Is.Not.EqualTo(slug).IgnoreCase,
                    events[i].Id.Value + " has no written headline — it is still its own id.");
            }
        }

        // ----- The placeholder rule ---------------------------------------------------------------------

        [Test]
        public void Copy_NoTemplateInAnyLocale_PutsASuffixOnAnInterpolatedName()
        {
            var problems = new List<string>();
            IReadOnlyList<StringTableEntry> entries = GameStrings.Default.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                IReadOnlyList<LocaleText> texts = entries[i].Texts;
                for (int t = 0; t < texts.Count; t++)
                {
                    Result checkedTemplate = TextTemplate.Validate(texts[t].Text);
                    if (checkedTemplate.IsFailure)
                    {
                        problems.Add(entries[i].Key + " (" + texts[t].Locale + "): " + checkedTemplate.Error);
                    }
                }
            }

            Assert.That(problems, Is.Empty, string.Join(" | ", problems));
        }

        [Test]
        public void Validate_APlaceholderFollowedByATurkishCaseEnding_FailsWithTheRuleNamed()
        {
            // The template a translator writes on instinct, and the reason the rule is a build failure and
            // not a comment: "Ali'yi" and "Umut'u" take different endings, so no single suffix is right.
            Result validation = TextTemplate.Validate("{player}'ı kadro dışı bırak");

            Assert.That(validation.IsFailure, Is.True);
            Assert.That(validation.Error, Does.Contain("atomic"));
        }

        [Test]
        public void Validate_APlaceholderFollowedByATypographicApostrophe_AlsoFails()
        {
            // Pasting from a word processor turns ' into ’. A rule that only knew the straight one would be
            // bypassed by a copy-paste, which is exactly how this gets into a table.
            Result validation = TextTemplate.Validate("{player}’nin sözleşmesi");

            Assert.That(validation.IsFailure, Is.True);
        }

        [Test]
        public void Validate_ThePermittedWayAround_Passes()
        {
            Assert.That(TextTemplate.Validate("{player} kadro dışı kalsın").IsSuccess, Is.True);
            Assert.That(TextTemplate.Validate("Kadro dışı bırak — {player}").IsSuccess, Is.True);
        }

        [Test]
        public void Validate_APlaceholderTheFormatterDoesNotFill_FailsNamingIt()
        {
            Result validation = TextTemplate.Validate("{manager} has decided");

            Assert.That(validation.IsFailure, Is.True);
            Assert.That(validation.Error, Does.Contain("manager"));
        }

        [Test]
        public void Copy_EveryPlaceholderUsed_IsOneTheFormatterSupports()
        {
            // Coverage's other half: a key with words is still broken if the words ask for a name the
            // formatter cannot produce. Formatting every row proves the whole table is fillable.
            var arguments = new TextArguments("Ali Yılmaz", "Karabük Demir");
            IReadOnlyList<string> keys = DramaCatalog.Default.CopyKeys();
            for (int locale = 0; locale < Locales.Shipped.Count; locale++)
            {
                LocalizedStrings strings = GameStrings.Default.For(Locales.Shipped[locale]);
                for (int i = 0; i < keys.Count; i++)
                {
                    string key = keys[i];
                    Assert.That(() => strings.Get(key, arguments), Throws.Nothing, key + " (" + Locales.Shipped[locale] + ")");
                }
            }
        }

        [Test]
        public void Format_ATemplateWithBothPlaceholders_FillsThemWithoutTouchingTheName()
        {
            string filled = TextTemplate.Format(
                "{player} kadro dışı kalsın — {club} kararı",
                new TextArguments("Ali Yılmaz", "Karabük Demir"));

            Assert.That(filled, Is.EqualTo("Ali Yılmaz kadro dışı kalsın — Karabük Demir kararı"));
        }

        // ----- Loudness -----------------------------------------------------------------------------------

        [Test]
        public void Get_AKeyWithNoRow_ThrowsNamingTheKey()
        {
            Assert.That(
                () => GameStrings.Default.Get("drama.transfer_request.beg", Locales.Reference),
                Throws.TypeOf<KeyNotFoundException>().And.Message.Contains("drama.transfer_request.beg"));
        }

        [Test]
        public void Get_AKeyWithNoTextInThisLocale_ThrowsRatherThanFallingBackToEnglish()
        {
            var table = new StringTable(Locales.Shipped, new[] { Row("drama.press_war.title", "Four minutes on a live microphone", string.Empty) });

            Assert.That(
                () => table.Get("drama.press_war.title", Locales.Turkish),
                Throws.TypeOf<KeyNotFoundException>().And.Message.Contains(Locales.Turkish));
        }

        [Test]
        public void Locales_TheShippedSet_IsStillTheTwoTheAuthoringRowCanHold()
        {
            // LocalizedStringRow serialises `english` and `turkish` as explicit fields for a readable
            // Inspector. That is a deliberate trade, and this is what makes it safe: a third launch locale
            // fails here, at the sentence that says what to change, instead of shipping a locale with a
            // column nothing writes to.
            Assert.That(Locales.Shipped, Is.EqualTo(new[] { "en", "tr" }));
        }

        private static StringTableEntry Row(string key, string english, string turkish)
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
}
