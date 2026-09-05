using System;
using System.Collections.Generic;
using Gaffer.Application.Narrative;
using Gaffer.Common;
using Gaffer.Common.Localization;
using Gaffer.Infrastructure.Localization;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The narrative copy, guarded the way the drama copy is. A missing line here is not a crash — it is
    /// a blank in the middle of somebody's career, which is exactly the kind of fault that ships.
    /// </summary>
    public sealed class MomentCopyTests
    {
        private static readonly CareerMomentKind[] AllKinds =
            (CareerMomentKind[])Enum.GetValues(typeof(CareerMomentKind));

        [Test]
        public void EveryMomentKind_HasWordsInEveryShippedLocale()
        {
            StringTable table = GameStrings.Default;
            var missing = new List<string>();

            foreach (CareerMomentKind kind in AllKinds)
            {
                string key = MomentTextKeys.For(kind);
                foreach (string locale in Locales.Shipped)
                {
                    if (string.IsNullOrEmpty(table.Find(key, locale)))
                    {
                        missing.Add($"{key} ({locale})");
                    }
                }
            }

            Assert.That(missing, Is.Empty,
                "A career would print a blank line for these: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryMomentKind_HasItsOwnKey()
        {
            // The switch in MomentTextKeys has a default that returns a key with no words, so a kind added
            // without copy fails the test above. This one catches the other half: two kinds sharing a key
            // would pass that test while telling two different moments with one sentence.
            var seen = new Dictionary<string, CareerMomentKind>();
            foreach (CareerMomentKind kind in AllKinds)
            {
                string key = MomentTextKeys.For(kind);
                Assert.That(key, Does.StartWith(MomentTextKeys.Prefix), kind.ToString());
                Assert.That(seen.ContainsKey(key), Is.False,
                    $"{kind} and {(seen.TryGetValue(key, out CareerMomentKind other) ? other.ToString() : "?")} share the key '{key}'.");
                seen[key] = kind;
            }
        }

        [Test]
        public void EveryMomentTemplate_IsValidAuthoredContent()
        {
            // TextTemplate's rules, applied to the narrative rows: every placeholder closed, every name
            // one the formatter carries, and no suffix hung off an interpolated name — the Turkish
            // constraint that has to be a build failure rather than a note, because a suffix that agrees
            // with one name disagrees with the next.
            StringTable table = GameStrings.Default;
            var problems = new List<string>();

            foreach (CareerMomentKind kind in AllKinds)
            {
                string key = MomentTextKeys.For(kind);
                foreach (string locale in Locales.Shipped)
                {
                    string template = table.Find(key, locale);
                    if (string.IsNullOrEmpty(template))
                    {
                        continue;
                    }

                    Result validated = TextTemplate.Validate(template);
                    if (validated.IsFailure)
                    {
                        problems.Add($"{key} ({locale}): {validated.Error}");
                    }
                }
            }

            Assert.That(problems, Is.Empty, string.Join(" | ", problems));
        }

        // ----- The short forms, read under a goal --------------------------------------------------------

        [Test]
        public void EveryFoldedKind_HasWordsInEveryShippedLocale_AndTheyAreValid()
        {
            StringTable table = GameStrings.Default;
            var problems = new List<string>();

            foreach (CareerMomentKind kind in AllKinds)
            {
                string key = MomentTextKeys.Folded(kind);
                if (key == null)
                {
                    continue;
                }

                Assert.That(key, Is.EqualTo(MomentTextKeys.For(kind) + MomentTextKeys.FoldedSuffix), kind.ToString());
                foreach (string locale in Locales.Shipped)
                {
                    string template = table.Find(key, locale);
                    if (string.IsNullOrEmpty(template))
                    {
                        problems.Add($"{key} ({locale}): no words");
                        continue;
                    }

                    Result validated = TextTemplate.Validate(template);
                    if (validated.IsFailure)
                    {
                        problems.Add($"{key} ({locale}): {validated.Error}");
                    }
                }
            }

            Assert.That(problems, Is.Empty, string.Join(" | ", problems));
        }

        [Test]
        public void EveryLineThatWritesTheMinute_HasAShortFormToReadUnderTheGoal()
        {
            // A full line that names the minute is a line written to stand alone — and a moment with a
            // minute is one that folds under a goal on the report, where the row above has said the
            // minute already. So every such kind needs the short form, or the duplication this exists to
            // remove comes back the day somebody adds a kind.
            StringTable table = GameStrings.Default;
            var missing = new List<string>();

            foreach (CareerMomentKind kind in AllKinds)
            {
                string full = table.Find(MomentTextKeys.For(kind), Locales.Reference);
                if (!string.IsNullOrEmpty(full)
                    && full.Contains("{" + TextTemplate.MinutePlaceholder + "}")
                    && MomentTextKeys.Folded(kind) == null)
                {
                    missing.Add(kind.ToString());
                }
            }

            Assert.That(missing, Is.Empty, "These write the minute and have no short form: " + string.Join(", ", missing));
        }

        [Test]
        public void NoShortForm_RepeatsWhatTheGoalRowAlreadySays()
        {
            // The whole reason the short form exists. The row it sits under carries the minute and the
            // scorer; a short form that wrote either would be the full line under another key.
            StringTable table = GameStrings.Default;
            var offenders = new List<string>();

            foreach (CareerMomentKind kind in AllKinds)
            {
                string key = MomentTextKeys.Folded(kind);
                if (key == null)
                {
                    continue;
                }

                foreach (string locale in Locales.Shipped)
                {
                    string line = table.Find(key, locale) ?? string.Empty;
                    if (line.Contains("{" + TextTemplate.MinutePlaceholder + "}")
                        || line.Contains("{" + TextTemplate.PlayerPlaceholder + "}"))
                    {
                        offenders.Add($"{key} ({locale})");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, "These repeat the minute or the name: " + string.Join(", ", offenders));
        }

        [Test]
        public void EveryMomentLine_IsASingleSentence()
        {
            // These stack: a career prints a dozen of them in a column, and a second sentence in each
            // turns a timeline into an essay. Cheap to check, and it is the rule most easily forgotten
            // when a new kind is added months from now.
            StringTable table = GameStrings.Default;
            var offenders = new List<string>();

            foreach (CareerMomentKind kind in AllKinds)
            {
                string key = MomentTextKeys.For(kind);
                foreach (string locale in Locales.Shipped)
                {
                    string line = table.Find(key, locale);
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    int stops = 0;
                    for (int i = 0; i < line.Length; i++)
                    {
                        if (line[i] == '.' || line[i] == '!' || line[i] == '?')
                        {
                            stops++;
                        }
                    }

                    // An ordinal in Turkish carries a full stop of its own ("25. maç"), so the budget is
                    // two rather than one for a line that names a number.
                    int allowed = line.Contains("{count}") ? 2 : 1;
                    if (stops > allowed)
                    {
                        offenders.Add($"{key} ({locale}): {stops} sentence stops");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join(" | ", offenders));
        }
    }
}
