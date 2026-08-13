using System;
using System.Collections.Generic;
using System.Text;

namespace Gaffer.Common.Localization
{
    /// <summary>
    /// The entities a drama template may interpolate. Two are enough for the ten events; a third is a
    /// deliberate decision, because every name added here is a name every locale must be able to place
    /// WITHOUT inflecting it (see <see cref="TextTemplate"/>).
    /// </summary>
    public readonly struct TextArguments
    {
        public TextArguments(string player, string club)
            : this(player, club, null, null)
        {
        }

        /// <summary>Also carries the numbers a match line needs (Faz 5.4). Both are already TEXT: a
        /// number becomes words at the edge, where the locale that will read it is known, rather than
        /// inside a formatter that would have to guess at a culture (CONVENTIONS §6).</summary>
        public TextArguments(string player, string club, string minute, string count)
        {
            Player = player;
            Club = club;
            Minute = minute;
            Count = count;
        }

        /// <summary>The player the event happened to. Empty for a club-level event.</summary>
        public string Player { get; }

        /// <summary>The managed club's name.</summary>
        public string Club { get; }

        /// <summary>The minute, already rendered. Empty for a line that did not happen in a match.</summary>
        public string Minute { get; }

        /// <summary>The number the line names — goals, a milestone, a fee — already rendered.</summary>
        public string Count { get; }

        /// <summary>The value for a placeholder name, or false when this template asks for something
        /// the formatter does not carry — which is a template bug, never a blank.</summary>
        public bool TryGet(string name, out string value)
        {
            switch (name)
            {
                case TextTemplate.PlayerPlaceholder:
                    value = Player;
                    return true;
                case TextTemplate.ClubPlaceholder:
                    value = Club;
                    return true;
                case TextTemplate.MinutePlaceholder:
                    value = Minute;
                    return true;
                case TextTemplate.CountPlaceholder:
                    value = Count;
                    return true;
                default:
                    value = null;
                    return false;
            }
        }
    }

    /// <summary>
    /// Placeholder substitution for authored copy — <c>{player}</c> and <c>{club}</c>, filled from
    /// <see cref="TextArguments"/>.
    ///
    /// <para><b>THE RULE THIS TYPE EXISTS TO ENFORCE: an interpolated entity is ATOMIC and never takes
    /// a suffix.</b> Turkish is a launch locale (TDD §12.5) and a Turkish noun takes a case ending
    /// chosen by its own vowels — "Ali'yi", "Umut'u", "Ayşe'yi". A template written as
    /// <c>{player}'ı kadro dışı bırak</c> therefore cannot be filled correctly by substitution, EVER:
    /// no single suffix is right for every squad, and the bug is invisible in English. Copy is written
    /// around it instead — <c>{player} kadro dışı kalsın</c>, <c>Kadro dışı bırak — {player}</c> — and
    /// BOTH locales obey the same rule so an English template can be handed to a translator without
    /// being restructured first. <see cref="Validate"/> is where that stops being a note and becomes a
    /// build failure; a Turkish suffix engine is explicitly deferred (TDD §12.5, "sonraya, opsiyonel").</para>
    ///
    /// <para>Nothing here fails softly. An unknown placeholder, an unclosed brace or a suffix stuck to
    /// a placeholder is an authoring mistake in content that ships inside the build, so it is reported
    /// by name (ARCHITECTURE §11's strict posture for authored content) rather than rendered as a gap
    /// the player sees and nobody notices.</para>
    /// </summary>
    public static class TextTemplate
    {
        /// <summary>The player the event is about.</summary>
        public const string PlayerPlaceholder = "player";

        /// <summary>The club the manager runs.</summary>
        public const string ClubPlaceholder = "club";

        /// <summary>The minute a match moment happened in (Faz 5.4).</summary>
        public const string MinutePlaceholder = "minute";

        /// <summary>
        /// The number the line is about — goals in the match, the milestone reached, the fee. One
        /// placeholder rather than one per kind, because a template only ever needs the number its own
        /// sentence names, and a second would only ever go unused.
        /// </summary>
        public const string CountPlaceholder = "count";

        /// <summary>The apostrophes a suffix is attached with. The straight one is what a keyboard
        /// gives you; the typographic one is what a word processor silently turns it into, and a rule
        /// that only knew about the first would be trivially bypassed by pasting from one.</summary>
        private static readonly char[] SuffixMarks = { '\'', '’', 'ʼ' };

        private static readonly string[] SupportedNames =
        {
            PlayerPlaceholder, ClubPlaceholder, MinutePlaceholder, CountPlaceholder,
        };

        /// <summary>Every placeholder a template may use.</summary>
        public static IReadOnlyList<string> Supported => SupportedNames;

        /// <summary>
        /// Checks a template as authored content: every placeholder is closed, is one the formatter
        /// supports, and is not followed by a suffix mark. Collects every problem in one message so a
        /// bad row is fixed once rather than one brace per run.
        /// </summary>
        public static Result Validate(string template)
        {
            if (string.IsNullOrEmpty(template))
            {
                return Result.Success();
            }

            var problems = new List<string>();
            int index = 0;
            while (index < template.Length)
            {
                int open = template.IndexOf('{', index);
                if (open < 0)
                {
                    break;
                }

                int close = template.IndexOf('}', open + 1);
                if (close < 0)
                {
                    problems.Add($"placeholder opened at {open} is never closed");
                    break;
                }

                string name = template.Substring(open + 1, close - open - 1);
                if (!IsSupported(name))
                {
                    problems.Add($"'{{{name}}}' is not a placeholder the formatter fills (have: {string.Join(", ", SupportedNames)})");
                }

                if (close + 1 < template.Length && IsSuffixMark(template[close + 1]))
                {
                    problems.Add(
                        $"'{{{name}}}' is followed by '{template[close + 1]}' — an interpolated name must stay atomic, " +
                        "because a Turkish case ending depends on the name's own vowels and cannot be substituted in; " +
                        "write around it instead");
                }

                index = close + 1;
            }

            if (problems.Count == 0)
            {
                return Result.Success();
            }

            var message = new StringBuilder();
            for (int i = 0; i < problems.Count; i++)
            {
                if (i > 0)
                {
                    message.Append("; ");
                }

                message.Append(problems[i]);
            }

            return Result.Failure(message.ToString());
        }

        /// <summary>
        /// Fills a template's placeholders. A placeholder the arguments cannot answer, or a brace that
        /// is never closed, throws with the template quoted: this is a broken invariant of shipped
        /// content (CONVENTIONS §4), and <see cref="Validate"/> plus the copy coverage test are what
        /// stop it reaching a player.
        /// </summary>
        public static string Format(string template, TextArguments arguments)
        {
            if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0)
            {
                return template;
            }

            var built = new StringBuilder(template.Length + 16);
            int index = 0;
            while (index < template.Length)
            {
                int open = template.IndexOf('{', index);
                if (open < 0)
                {
                    built.Append(template, index, template.Length - index);
                    break;
                }

                built.Append(template, index, open - index);
                int close = template.IndexOf('}', open + 1);
                if (close < 0)
                {
                    throw new FormatException($"Localized template has an unclosed placeholder: \"{template}\".");
                }

                string name = template.Substring(open + 1, close - open - 1);
                if (!arguments.TryGet(name, out string value))
                {
                    throw new FormatException(
                        $"Localized template asks for '{{{name}}}', which the formatter does not fill: \"{template}\".");
                }

                built.Append(value);
                index = close + 1;
            }

            return built.ToString();
        }

        private static bool IsSupported(string name)
        {
            for (int i = 0; i < SupportedNames.Length; i++)
            {
                if (string.Equals(SupportedNames[i], name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSuffixMark(char candidate)
        {
            for (int i = 0; i < SuffixMarks.Length; i++)
            {
                if (SuffixMarks[i] == candidate)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
