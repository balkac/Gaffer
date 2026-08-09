using System.Collections.Generic;
using System.Globalization;
using Gaffer.Application.Drama;
using Gaffer.Application.Run;
using Gaffer.Common.Localization;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;
using Gaffer.Editor.Content;
using Gaffer.Infrastructure.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaffer.Editor.Harness
{
    /// <summary>One line of drama copy and the tone it is written in. Text plus colour, nothing else —
    /// the window that asked for it owns the font, the spacing and the element.</summary>
    internal readonly struct DramaLine
    {
        internal DramaLine(string text, Color tone)
        {
            Text = text;
            Tone = tone;
        }

        internal string Text { get; }

        internal Color Tone { get; }
    }

    /// <summary>
    /// What a drama choice <em>does</em>, in words and exact numbers — the answer to the owner's
    /// "dramalarda verdiğim kararlar neyi etkiliyor kesinlikle anlamıyorum" (PROGRESS, "Seçimin +'sı/-'si").
    /// A decision card used to show a verb ("Fine him") and a generic sentence; every number behind it was
    /// already sitting in <see cref="DramaChoice.Effects"/> and was simply never drawn.
    ///
    /// <para><b>Resolved, not raw.</b> Two of the seven kinds are computed from live state rather than
    /// authored as constants: <see cref="DramaEffectKind.SubjectWageFine"/> is one week of the subject's
    /// actual wage and <see cref="DramaEffectKind.CashFraction"/> scales to the money in the bank. Both are
    /// read back through the same <see cref="RunSession"/> queries the resolver uses
    /// (<see cref="RunSession.WeeklyWageOf"/>, <see cref="RunSession.Finances"/>,
    /// <see cref="RunSession.FeeOf"/> — all on the run's own economy balance), so the preview quotes the
    /// number the manager will actually get instead of "-10% of cash".</para>
    ///
    /// <para><b>Nothing is skipped silently.</b> Every kind has an arm, an effect list that is empty says so
    /// ("living with it IS a decision"), and an unmapped kind renders as a loud line rather than vanishing —
    /// mirroring the resolver's own rule that a kind with no handler is a bug, not a no-op.</para>
    ///
    /// <para>Dev-tool English, under the exemption stated once in <see cref="HarnessLabels"/>. Money goes
    /// through <see cref="HarnessMoney"/>.</para>
    /// </summary>
    internal static class HarnessDrama
    {
        /// <summary>Below this a morale entry rounds to nothing, so it is not worth a badge.</summary>
        private const double MoraleEpsilon = 0.05;

        /// <summary>
        /// Fills <paramref name="into"/> with one line per authored effect, in the order the choice
        /// declares them — stacked, never truncated, so a three-effect answer reads as three lines.
        /// </summary>
        internal static void Preview(PendingDrama pending, DramaChoice choice, RunSession session, List<DramaLine> into)
        {
            into.Clear();
            IReadOnlyList<DramaEffect> effects = choice.Effects;
            if (effects == null || effects.Count == 0)
            {
                // A choice with no effects is legal: living with it is itself a decision.
                into.Add(new DramaLine("Nothing changes — you live with it.", HarnessPalette.Muted));
                return;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                into.Add(Describe(pending, effects[i], session));
            }
        }

        private static DramaLine Describe(PendingDrama pending, DramaEffect effect, RunSession session)
        {
            switch (effect.Kind)
            {
                case DramaEffectKind.SubjectMorale:
                    return new DramaLine(
                        SubjectName(pending) + ": morale " + Points(effect.Magnitude) + "  " + Spell(effect.DurationWeeks),
                        MoraleTone(effect.Magnitude, effect.DurationWeeks));

                case DramaEffectKind.TeamMorale:
                    return new DramaLine(
                        "Squad (" + SquadCount(pending) + "): morale " + Points(effect.Magnitude) + " each  " +
                        Spell(effect.DurationWeeks),
                        MoraleTone(effect.Magnitude, effect.DurationWeeks));

                case DramaEffectKind.Cash:
                    return CashLine((long)effect.Magnitude, string.Empty);

                case DramaEffectKind.CashFraction:
                    // The resolver multiplies the club's current cash by the magnitude; so does this, off
                    // the same purse, so the euros shown are the euros that will move.
                    return CashLine(
                        (long)(session.Finances.Cash * effect.Magnitude),
                        "  (" + Percent(effect.Magnitude) + " of " + HarnessMoney.Format(session.Finances.Cash) + " in the bank)");

                case DramaEffectKind.SubjectWageFine:
                    return pending.Subject == null
                        ? Broken("a wage fine with nobody to fine")
                        : CashLine(
                            session.WeeklyWageOf(pending.Subject),
                            "  (one week of " + pending.Subject.Name + "'s wage)");

                case DramaEffectKind.SellSubject:
                    return pending.Subject == null
                        ? Broken("a forced sale with nobody to sell")
                        : new DramaLine(
                            pending.Subject.Name + " leaves — sold at his market fee, " +
                            HarnessMoney.Signed(session.FeeOf(pending.Subject)) + " in.",
                            HarnessPalette.Loss);

                case DramaEffectKind.GrantTraitToSuccessor:
                    // The heir is picked inside the resolver (strongest young team-mate, deterministically)
                    // and this window has no query for him, so it says who decides rather than guessing at
                    // a name it could get wrong. The aftermath names him from the resolution.
                    return string.IsNullOrEmpty(effect.Trait.Value)
                        ? Broken("a trait grant with no trait")
                        : new DramaLine(
                            "A team-mate is anointed and inherits " + Humanize(effect.Trait.Value) +
                            " — the dressing room's heir; you learn who the moment you decide.",
                            HarnessPalette.Win);

                default:
                    // The resolver throws on a kind it has no arm for. A view cannot throw at the manager,
                    // so it shows the gap instead of quietly drawing one fewer line than the choice has.
                    return Broken("effect '" + effect.Kind + "' — this window has no words for it yet");
            }
        }

        /// <summary>
        /// What the answer actually did, in the same terms the preview promised — replayed entirely off
        /// <see cref="DramaResolution"/> (morale entries, cash, the sale and its fee, the trait and the
        /// team-mate who got it), never by diffing state.
        /// </summary>
        internal static void Aftermath(DramaResolution resolution, RunSession session, List<DramaLine> into)
        {
            into.Clear();

            AppendMorale(resolution, session, into);

            if (resolution.CashDelta != 0)
            {
                into.Add(new DramaLine(
                    "Cash " + HarnessMoney.Signed(resolution.CashDelta) + "  ·  " +
                    HarnessMoney.Format(resolution.Finances.Cash) + " in the bank now.",
                    resolution.CashDelta < 0 ? HarnessPalette.Loss : HarnessPalette.Win));
            }

            if (resolution.SoldPlayer != null)
            {
                into.Add(new DramaLine(
                    resolution.SoldPlayer.Name + " is out of the squad — sold for " +
                    HarnessMoney.Format(resolution.SaleFee) + ", and back on the market.",
                    HarnessPalette.Loss));
            }

            Player heir = resolution.RebuiltPlayer ?? resolution.TraitGrantTarget;
            if (heir != null && !string.IsNullOrEmpty(resolution.GrantedTrait.Value))
            {
                into.Add(new DramaLine(
                    heir.Name + " is the heir — he carries " + Humanize(resolution.GrantedTrait.Value) + " from now on.",
                    HarnessPalette.Win));
            }

            if (into.Count == 0)
            {
                into.Add(new DramaLine("Nothing moved — you lived with it.", HarnessPalette.Muted));
            }
        }

        // The ledger entries as they landed, grouped by (points, weeks) in first-seen order: one man named,
        // a team-wide effect counted. Twenty-two identical lines would bury the one that is about a person.
        private static void AppendMorale(DramaResolution resolution, RunSession session, List<DramaLine> into)
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
                    ? NameOf(group.First, resolution, session)
                    : group.Count + " players";
                string each = group.Count == 1 ? string.Empty : " each";
                into.Add(new DramaLine(
                    who + ": morale " + Points(group.Points) + each + "  " + Spell(group.Weeks) +
                    " — carried on the roster until it fades.",
                    MoraleTone(group.Points, group.Weeks)));
            }
        }

        private struct MoraleGroup
        {
            public double Points;
            public int Weeks;
            public int Count;
            public PlayerId First;
        }

        // A morale entry can land on a man the same resolution then sold, so the roster no longer holds him.
        private static string NameOf(PlayerId player, DramaResolution resolution, RunSession session)
        {
            string name = session.PlayerName(session.ManagedClub, player);
            if (name.Length > 0)
            {
                return name;
            }

            if (resolution.SoldPlayer != null && resolution.SoldPlayer.Id == player)
            {
                return resolution.SoldPlayer.Name;
            }

            return "A player who has left";
        }

        private static DramaLine CashLine(long amount, string aside)
        {
            return new DramaLine(
                "Cash " + HarnessMoney.Signed(amount) + aside,
                amount < 0 ? HarnessPalette.Loss : HarnessPalette.Win);
        }

        private static DramaLine Broken(string what)
        {
            return new DramaLine("Cannot preview " + what + " — report it.", HarnessPalette.Draw);
        }

        private static string SubjectName(PendingDrama pending)
        {
            return pending.Subject != null ? pending.Subject.Name : "The subject (none — report it)";
        }

        private static int SquadCount(PendingDrama pending)
        {
            IReadOnlyList<Player> squad = pending.Context.Squad;
            return squad != null ? squad.Count : 0;
        }

        // MoraleLedger.Apply drops an entry with no weeks on the floor, so a preview must not promise one.
        private static Color MoraleTone(double points, int weeks)
        {
            if (weeks <= 0 || (points > -MoraleEpsilon && points < MoraleEpsilon))
            {
                return HarnessPalette.Muted;
            }

            return points < 0 ? HarnessPalette.Loss : HarnessPalette.Win;
        }

        private static string Spell(int weeks)
        {
            if (weeks <= 0)
            {
                return "(no weeks — it never lands)";
            }

            return weeks == 1 ? "(1 week)" : "(" + weeks + " weeks)";
        }

        /// <summary>Signed morale points: "+2", "-3", "-1.5". The sign is always written.</summary>
        internal static string Points(double value)
        {
            double rounded = System.Math.Round(value, 1);
            if (rounded == 0.0)
            {
                return "0";
            }

            string number = rounded == System.Math.Round(rounded)
                ? ((long)rounded).ToString(CultureInfo.InvariantCulture)
                : rounded.ToString("0.0", CultureInfo.InvariantCulture);
            return rounded > 0.0 ? "+" + number : number;
        }

        private static string Percent(double fraction)
        {
            double percent = System.Math.Round(fraction * 100.0, 1);
            string number = percent == System.Math.Round(percent)
                ? ((long)percent).ToString(CultureInfo.InvariantCulture)
                : percent.ToString("0.0", CultureInfo.InvariantCulture);
            return (percent > 0.0 ? "+" : string.Empty) + number + "%";
        }

        /// <summary>
        /// A slug turned into words — for the TRAIT ids this file prints in effect lines, which are
        /// domain identifiers with no copy of their own yet. It is no longer used for drama titles or
        /// choice labels: those are written copy now and come through <see cref="HarnessCopy"/>.
        /// Dev-tool English under the exemption stated in <see cref="HarnessLabels"/>.
        /// </summary>
        internal static string Humanize(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return string.Empty;
            }

            string spaced = slug.Replace('-', ' ').Replace('_', ' ');
            return char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
        }

        // ----- Written copy ---------------------------------------------------------------------------------
        // The three strings a card is actually made of. Each is authored text resolved from the event's own
        // key — NOT the identifier prettied up, which is what used to be here and is what made the cards read
        // as machine output ("NIGHT CLUB SCANDAL", "Fine", no body at all).

        /// <summary>The event's headline, resolved from <see cref="DramaEvent.TitleKey"/>.</summary>
        internal static string Title(PendingDrama pending, RunSession session)
        {
            return HarnessCopy.Resolve(pending.Event.TitleKey, Arguments(pending, session));
        }

        /// <summary>What happened, resolved from <see cref="DramaEvent.BodyKey"/> — the one string on the
        /// card that has never been rendered in any window until now.</summary>
        internal static string Body(PendingDrama pending, RunSession session)
        {
            return HarnessCopy.Resolve(pending.Event.BodyKey, Arguments(pending, session));
        }

        /// <summary>The decision on the button, resolved from <see cref="DramaChoice.LabelKey"/>.</summary>
        internal static string ChoiceLabel(DramaChoice choice, PendingDrama pending, RunSession session)
        {
            return choice == null ? string.Empty : HarnessCopy.Resolve(choice.LabelKey, Arguments(pending, session));
        }

        // The two entities a template may name. The subject is empty for a club-level event, which is
        // correct rather than missing: those events' copy names the club instead.
        private static TextArguments Arguments(PendingDrama pending, RunSession session)
        {
            string player = pending != null && pending.Subject != null ? pending.Subject.Name : string.Empty;
            string club = session != null ? session.ManagedClubName : string.Empty;
            return new TextArguments(player, club);
        }
    }

    /// <summary>
    /// The dev-tool's door to the string table: it loads <c>StringTable.asset</c> (creating it from
    /// <see cref="GameStrings"/> on first use, via <see cref="ContentAssets.Strings"/>) and resolves the
    /// keys the drama events carry.
    ///
    /// <para><b>This is not the <see cref="HarnessLabels"/> exemption.</b> That class holds dev-tool
    /// CHROME — column headings and attribute abbreviations for windows that never ship. Drama titles,
    /// bodies and choice labels are player-facing CONTENT: they ship, they are translated, and they go
    /// through the table like everything else will. The two are kept apart deliberately; merging them
    /// would quietly re-open the door NON-NEGOTIABLE #8 closes.</para>
    ///
    /// <para><b>A missing key is loud here too, in the way a window can be loud.</b> The strict door
    /// (<c>StringTableSO.Load</c>, and the copy-coverage test in <c>dotnet test</c>) is what stops a
    /// hole ever reaching a build. This is the dev-tool shim: it validates on load and reports a bad
    /// table as a Unity error, then draws the one unresolvable key as a visible «marker» on the card
    /// rather than taking the window down or — far worse — leaving a blank line nobody notices.</para>
    /// </summary>
    internal static class HarnessCopy
    {
        /// <summary>Which locale the dev windows read. Persisted per developer, so reviewing the Turkish
        /// copy is a menu item and not a code edit — the fastest way to catch a Turkish line that reads
        /// like a translation is to play a season in it.</summary>
        private const string LocalePreference = "Gaffer.Harness.Locale";

        // Cached for the domain reload, not for the session: a re-import of the asset resets the domain
        // and with it this field, so an edited line shows up on the next repaint.
        private static StringTable _table;

        internal static string Locale
        {
            get
            {
                string stored = EditorPrefs.GetString(LocalePreference, Locales.Reference);
                return Locales.IsShipped(stored) ? stored : Locales.Reference;
            }
        }

        [MenuItem("Gaffer/Content/Toggle Copy Locale (EN ↔ TR)")]
        internal static void ToggleLocale()
        {
            string next = string.Equals(Locale, Locales.Reference, System.StringComparison.Ordinal)
                ? Locales.Turkish
                : Locales.Reference;
            EditorPrefs.SetString(LocalePreference, next);
            Debug.Log("Gaffer dev windows now read copy in '" + next + "'. Reopen a window to see it.");
        }

        /// <summary>The text for a key with its placeholders filled, or a visible marker naming the key
        /// when this locale has no words for it — never a blank, never an exception at the manager.</summary>
        internal static string Resolve(string key, TextArguments arguments)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "«this event has no key here — report it»";
            }

            try
            {
                string text = Table().For(Locale).Find(key, arguments);
                return text ?? Missing(key);
            }
            catch (System.FormatException problem)
            {
                // A template that asks for a placeholder the formatter has no value for. Validate() and the
                // copy tests catch this before it can ship; the window still has to draw something.
                return "«" + key + " — " + problem.Message + "»";
            }
        }

        private static string Missing(string key)
        {
            return "«" + key + " — no '" + Locale + "' text»";
        }

        private static StringTable Table()
        {
            if (_table != null)
            {
                return _table;
            }

            // Required keys are the built-in catalog's, which is what both dev windows run on. An event
            // authored only into DramaCatalog.asset therefore shows its holes as markers on the card
            // rather than as a load error — the same information, at the only place a dev tool can put it.
            _table = ContentAssets.Strings().ToTable(DramaCatalog.Default.CopyKeys());
            return _table;
        }
    }

    /// <summary>
    /// The drama layer made visible on the roster: a chip showing a player's live morale points, so a
    /// dressing room a decision wounded is something you can SEE for the weeks it lasts, not something you
    /// infer from results (PROGRESS, "Seçimin +'sı/-'si" — "etkinin sonraki haftalara dokunduğu görünmüyor").
    ///
    /// <para><b>Made once, shown many times.</b> <see cref="Show"/> writes every mutable thing about the
    /// chip — text, colour, border, visibility — on every call, and zero morale hides it. That total reset is
    /// what a recycled <c>ListView</c> row needs: a row rebound to a calm player must not keep the previous
    /// occupant's wound (PERFORMANCE.md §4).</para>
    /// </summary>
    internal static class HarnessMorale
    {
        private const double Epsilon = 0.05;

        /// <summary>A hidden chip, ready to be bound. Created once per row, never per repaint.</summary>
        internal static Label MakeBadge()
        {
            var badge = new Label(string.Empty);
            badge.style.fontSize = 9;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.whiteSpace = WhiteSpace.NoWrap;
            badge.style.marginLeft = 8;
            badge.style.paddingLeft = 4;
            badge.style.paddingRight = 4;
            badge.style.borderLeftWidth = 1;
            badge.style.borderRightWidth = 1;
            badge.style.borderTopWidth = 1;
            badge.style.borderBottomWidth = 1;
            badge.style.borderTopLeftRadius = 3;
            badge.style.borderTopRightRadius = 3;
            badge.style.borderBottomLeftRadius = 3;
            badge.style.borderBottomRightRadius = 3;
            badge.style.display = DisplayStyle.None;
            return badge;
        }

        /// <summary>
        /// A chip already bound to a player's morale — for the rows that are rebuilt wholesale rather than
        /// recycled. A recycled row must keep its chip and call <see cref="Show"/> on every bind instead.
        /// </summary>
        internal static Label MakeBadgeFor(double points)
        {
            Label badge = MakeBadge();
            Show(badge, points);
            return badge;
        }

        /// <summary>Binds the chip to a player's live morale, or hides it when he carries none.</summary>
        internal static void Show(Label badge, double points)
        {
            if (points > -Epsilon && points < Epsilon)
            {
                badge.text = string.Empty;
                badge.tooltip = string.Empty;
                badge.style.display = DisplayStyle.None;
                return;
            }

            Color tone = points < 0.0 ? HarnessPalette.Loss : HarnessPalette.Win;
            badge.text = "MORALE " + HarnessDrama.Points(points);
            badge.tooltip = points < 0.0
                ? "Live drama morale: it is pulling his rating down until it fades."
                : "Live drama morale: it is lifting his rating until it fades.";
            badge.style.color = tone;
            badge.style.borderLeftColor = tone;
            badge.style.borderRightColor = tone;
            badge.style.borderTopColor = tone;
            badge.style.borderBottomColor = tone;
            badge.style.display = DisplayStyle.Flex;
        }
    }
}
