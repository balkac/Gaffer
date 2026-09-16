using System.Collections.Generic;
using System.Globalization;
using Gaffer.Application.Drama;
using Gaffer.Application.Run;
using Gaffer.Common.Localization;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;

namespace Gaffer.Presentation.Drama
{
    /// <summary>How a line on the card is coloured: the one accent rule, applied to consequences.</summary>
    public enum DramaTone
    {
        /// <summary>Nothing moves, or the line is a note — drawn muted.</summary>
        Quiet = 0,
        /// <summary>Something the manager wants: a lift, money in, an heir.</summary>
        Gain = 1,
        /// <summary>Something that costs: a wound, money out, a man gone.</summary>
        Cost = 2,
    }

    /// <summary>One consequence, said in the manager's language and toned.</summary>
    public readonly struct DramaLine
    {
        public DramaLine(string text, DramaTone tone)
        {
            Text = text;
            Tone = tone;
        }

        public string Text { get; }
        public DramaTone Tone { get; }
    }

    /// <summary>
    /// What a choice WILL do and what a choice DID, as lines under it — the rule the drama card draws by,
    /// held apart from the card so <c>dotnet test</c> can hold it (CLAUDE.md, test bridge).
    ///
    /// <para><b>Why the preview exists.</b> The Faz 4 playtest said it plainly: "the +/− of a choice is
    /// not understood." The card showed a label and asked the manager to guess. Every consequence was
    /// already sitting in <see cref="DramaChoice.Effects"/> and was simply never drawn; this reads them
    /// out, one line per effect, in the order the choice declares them — stacked, never truncated, so a
    /// three-effect answer reads as three lines.</para>
    ///
    /// <para><b>Two effect kinds are priced on the spot.</b> A wage fine is one week of the subject's
    /// actual wage and a cash fraction scales to the money in the bank, so the euros the preview shows
    /// are the euros that will move — the same purse the resolver reads. The heir of a trait grant is
    /// picked inside the resolver, so the preview says a team-mate inherits and the aftermath names
    /// him.</para>
    ///
    /// <para><b>Copy.</b> Every sentence is a localization key (NON-NEGOTIABLE #8), and the templates
    /// carry one number each — the string table has one <c>{count}</c> slot, so a line that needs two
    /// figures (points AND weeks) is two templates joined, not one template with a suffix wished onto
    /// it. Money is written by <see cref="UiMoney"/> in the locale's decimal mark, then handed in as
    /// text.</para>
    /// </summary>
    public static class DramaLines
    {
        private const string Join = "  ·  ";

        /// <summary>What answering with this choice will do — one line per authored effect.</summary>
        /// <param name="cash">The club's cash now; a fraction effect is priced off it.</param>
        /// <param name="subjectWage">One week of the subject's wage, for a fine; ignored without a subject.</param>
        /// <param name="subjectFee">The subject's market fee, for a forced sale; ignored without a subject.</param>
        public static List<DramaLine> Preview(
            PendingDrama pending,
            DramaChoice choice,
            long cash,
            long subjectWage,
            long subjectFee,
            LocalizedStrings text)
        {
            var lines = new List<DramaLine>(4);
            IReadOnlyList<DramaEffect> effects = choice != null ? choice.Effects : null;
            if (effects == null || effects.Count == 0)
            {
                // A choice with no effects is legal: living with it is itself a decision.
                lines.Add(new DramaLine(text.Or(UiTextKeys.DramaNothing), DramaTone.Quiet));
                return lines;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                lines.Add(Describe(pending, effects[i], cash, subjectWage, subjectFee, text));
            }

            return lines;
        }

        private static DramaLine Describe(
            PendingDrama pending,
            DramaEffect effect,
            long cash,
            long subjectWage,
            long subjectFee,
            LocalizedStrings text)
        {
            Player subject = pending != null ? pending.Subject : null;
            switch (effect.Kind)
            {
                case DramaEffectKind.SubjectMorale:
                    return Morale(SubjectName(subject, text), effect.Magnitude, effect.DurationWeeks, text);

                case DramaEffectKind.TeamMorale:
                    return Morale(text.Or(UiTextKeys.DramaWholeSquad), effect.Magnitude, effect.DurationWeeks, text);

                case DramaEffectKind.Cash:
                    return Cash((long)effect.Magnitude, string.Empty, text);

                case DramaEffectKind.CashFraction:
                    // The resolver multiplies the club's current cash by the magnitude; so does this, off
                    // the same purse, so the euros shown are the euros that will move.
                    return Cash(
                        (long)(cash * effect.Magnitude),
                        text.Or(UiTextKeys.DramaCashShare, Count(Percent(effect.Magnitude))),
                        text);

                case DramaEffectKind.SubjectWageFine:
                    return subject == null
                        ? Broken(effect, text)
                        : Cash(subjectWage, text.Or(UiTextKeys.DramaWageFine, Named(subject.Name)), text);

                case DramaEffectKind.SellSubject:
                    return subject == null
                        ? Broken(effect, text)
                        : new DramaLine(
                            text.Or(UiTextKeys.DramaSale, new TextArguments(subject.Name, string.Empty, string.Empty, text.Money(subjectFee))),
                            DramaTone.Cost);

                case DramaEffectKind.GrantTraitToSuccessor:
                    return string.IsNullOrEmpty(effect.Trait.Value)
                        ? Broken(effect, text)
                        : new DramaLine(text.Or(UiTextKeys.DramaHeirPreview, Count(TraitName(effect.Trait, text))), DramaTone.Gain);

                default:
                    return Broken(effect, text);
            }
        }

        /// <summary>What answering did — every consequence the resolution reports, as lines. Morale
        /// entries are grouped by (points, weeks): one man is named, a team-wide effect is counted, because
        /// twenty-two identical lines would bury the one that is about a person.</summary>
        /// <param name="nameOf">Turns a player id into a name, for the men still on the roster. The men the
        /// resolution itself names — the one it sold, the heir it rebuilt — are read from the resolution
        /// first, because a morale entry can land on a man the same answer then sold, and the roster no
        /// longer holds him.</param>
        public static List<DramaLine> Aftermath(DramaResolution resolution, System.Func<PlayerId, string> nameOf, LocalizedStrings text)
        {
            var lines = new List<DramaLine>(4);
            if (resolution == null)
            {
                return lines;
            }

            AppendMorale(resolution, nameOf, lines, text);

            if (resolution.CashDelta != 0)
            {
                lines.Add(Cash(
                    resolution.CashDelta,
                    text.Or(UiTextKeys.DramaInTheBank, Count(text.Money(resolution.Finances.Cash))),
                    text));
            }

            if (resolution.SoldPlayer != null)
            {
                lines.Add(new DramaLine(
                    text.Or(UiTextKeys.DramaSold, new TextArguments(resolution.SoldPlayer.Name, string.Empty, string.Empty, text.Money(resolution.SaleFee))),
                    DramaTone.Cost));
            }

            Player heir = resolution.RebuiltPlayer ?? resolution.TraitGrantTarget;
            if (heir != null && !string.IsNullOrEmpty(resolution.GrantedTrait.Value))
            {
                lines.Add(new DramaLine(
                    text.Or(UiTextKeys.DramaHeir, new TextArguments(heir.Name, string.Empty, string.Empty, TraitName(resolution.GrantedTrait, text))),
                    DramaTone.Gain));
            }

            if (lines.Count == 0)
            {
                lines.Add(new DramaLine(text.Or(UiTextKeys.DramaNothingMoved), DramaTone.Quiet));
            }

            return lines;
        }

        private static void AppendMorale(DramaResolution resolution, System.Func<PlayerId, string> nameOf, List<DramaLine> into, LocalizedStrings text)
        {
            IReadOnlyList<MoraleChange> changes = resolution.MoraleChanges;
            if (changes == null || changes.Count == 0)
            {
                return;
            }

            var groups = new List<MoraleGroup>(4);
            for (int i = 0; i < changes.Count; i++)
            {
                MoraleChange change = changes[i];
                int found = -1;
                for (int g = 0; g < groups.Count; g++)
                {
                    if (groups[g].Points == change.Points && groups[g].Weeks == change.Weeks)
                    {
                        found = g;
                        break;
                    }
                }

                if (found < 0)
                {
                    groups.Add(new MoraleGroup { Points = change.Points, Weeks = change.Weeks, Count = 1, First = change.Player });
                    continue;
                }

                MoraleGroup group = groups[found];
                group.Count++;
                groups[found] = group;
            }

            for (int g = 0; g < groups.Count; g++)
            {
                MoraleGroup group = groups[g];
                string who = group.Count == 1
                    ? NameOf(group.First, resolution, nameOf) ?? text.Or(UiTextKeys.DramaSomeone)
                    : text.Or(UiTextKeys.DramaPlayersCount, Count(group.Count.ToString(CultureInfo.InvariantCulture)));
                into.Add(Morale(who, group.Points, group.Weeks, text));
            }
        }

        private static string NameOf(PlayerId player, DramaResolution resolution, System.Func<PlayerId, string> nameOf)
        {
            if (resolution.SoldPlayer != null && resolution.SoldPlayer.Id.Equals(player))
            {
                return resolution.SoldPlayer.Name;
            }

            if (resolution.RebuiltPlayer != null && resolution.RebuiltPlayer.Id.Equals(player))
            {
                return resolution.RebuiltPlayer.Name;
            }

            if (resolution.TraitGrantTarget != null && resolution.TraitGrantTarget.Id.Equals(player))
            {
                return resolution.TraitGrantTarget.Name;
            }

            return nameOf != null ? nameOf(player) : null;
        }

        private struct MoraleGroup
        {
            public double Points;
            public int Weeks;
            public int Count;
            public PlayerId First;
        }

        // ----- The pieces ---------------------------------------------------------------------------------

        // "{who}: morale +3  ·  4 weeks". The points template and the weeks template are two rows joined
        // here, because a template carries one {count} and this line needs two numbers.
        private static DramaLine Morale(string who, double points, int weeks, LocalizedStrings text)
        {
            string line = text.Or(UiTextKeys.DramaMorale, new TextArguments(who, string.Empty, string.Empty, Points(points)));
            string span = weeks == 1
                ? text.Or(UiTextKeys.DramaOneWeek)
                : text.Or(UiTextKeys.DramaWeeks, Count(weeks.ToString(CultureInfo.InvariantCulture)));
            return new DramaLine(line + Join + span, ToneOf(points, weeks));
        }

        // "Cash −€250k  ·  <aside>". A negative amount is a cost; zero is written and toned as nothing.
        private static DramaLine Cash(long amount, string aside, LocalizedStrings text)
        {
            string line = text.Or(UiTextKeys.DramaCash, Count(SignedMoney(amount, text)));
            if (!string.IsNullOrEmpty(aside))
            {
                line += Join + aside;
            }

            return new DramaLine(line, amount < 0 ? DramaTone.Cost : amount > 0 ? DramaTone.Gain : DramaTone.Quiet);
        }

        // The resolver throws on a kind it has no arm for. A card cannot throw at the manager, so it shows
        // the gap by name instead of quietly drawing one fewer line than the choice has.
        private static DramaLine Broken(DramaEffect effect, LocalizedStrings text)
        {
            return new DramaLine(text.Or(UiTextKeys.DramaUnknownEffect, Count(effect.Kind.ToString())), DramaTone.Quiet);
        }

        // MoraleLedger.Apply drops an entry with no weeks on the floor, so a line must not promise one.
        private static DramaTone ToneOf(double points, int weeks)
        {
            if (weeks <= 0 || System.Math.Abs(points) < 0.05)
            {
                return DramaTone.Quiet;
            }

            return points > 0.0 ? DramaTone.Gain : DramaTone.Cost;
        }

        private static string SubjectName(Player subject, LocalizedStrings text)
        {
            return subject != null ? subject.Name : text.Or(UiTextKeys.DramaSomeone);
        }

        /// <summary>The trait's written name, or its key when the table has no row for it.</summary>
        public static string TraitName(TraitId trait, LocalizedStrings text)
        {
            Trait defined = TraitCatalog.Default.Find(trait);
            return defined != null ? text.Or(defined.NameKey) : trait.Value;
        }

        /// <summary>Signed morale points: "+2", "−3", "−1.5". The sign is always written, and the minus is
        /// the typographic one the penalty column already uses, so the two read as the same kind of mark.</summary>
        public static string Points(double value)
        {
            double rounded = System.Math.Round(value, 1);
            if (rounded == 0.0)
            {
                return "0";
            }

            string number = rounded == System.Math.Round(rounded)
                ? ((long)System.Math.Abs(rounded)).ToString(CultureInfo.InvariantCulture)
                : System.Math.Abs(rounded).ToString("0.0", CultureInfo.InvariantCulture);
            return rounded > 0.0 ? "+" + number : "−" + number;
        }

        private static string SignedMoney(long amount, LocalizedStrings text)
        {
            if (amount == 0)
            {
                return text.Money(0);
            }

            return amount > 0 ? "+" + text.Money(amount) : "−" + text.Money(-amount);
        }

        private static string Percent(double fraction)
        {
            double percent = System.Math.Round(System.Math.Abs(fraction) * 100.0, 1);
            string number = percent == System.Math.Round(percent)
                ? ((long)percent).ToString(CultureInfo.InvariantCulture)
                : percent.ToString("0.0", CultureInfo.InvariantCulture);
            // The sign of the percent goes in the template — "20%" in English, "%20" in Turkish — so the
            // number is handed over bare.
            return number;
        }

        private static TextArguments Count(string value)
        {
            return new TextArguments(string.Empty, string.Empty, string.Empty, value);
        }

        private static TextArguments Named(string player)
        {
            return new TextArguments(player, string.Empty, string.Empty, string.Empty);
        }
    }
}
