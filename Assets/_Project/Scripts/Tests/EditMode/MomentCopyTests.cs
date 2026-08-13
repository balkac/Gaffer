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
