using System;
using System.Collections.Generic;
using System.Globalization;
using Gaffer.Application.Run;
using Gaffer.Application.Season;
using Gaffer.Common.Localization;
using Gaffer.Domain.Clubs;
using UnityEngine.UIElements;

namespace Gaffer.Presentation.Season
{
    /// <summary>
    /// Where the season stands — the third of the five screens (GDD §8: "table, fixtures, target"). Who
    /// is next, the table with the board's two numbers drawn through it, and those numbers said out loud.
    ///
    /// <para><b>Render-time queries only.</b> Every value comes off <see cref="RunSession.Standings"/>,
    /// <see cref="RunSession.NextFixture"/> and <see cref="RunSession.BoardTarget"/>; the screen asks
    /// what IS and never works out what changed (NON-NEGOTIABLE #4). Built whole each time it is opened:
    /// the table is a small list and a diff would be a second model of it.</para>
    ///
    /// <para><b>The zones are the board's own judgement.</b> Which position is promotion and which is the
    /// sack comes from <see cref="TableZones"/>, which asks the evaluator that will judge the season —
    /// so the line the table draws is by construction the line the board will read it by.</para>
    /// </summary>
    public sealed class SeasonScreen
    {
        private readonly RunSession _session;
        private readonly LocalizedStrings _text;
        private readonly Action _onClose;
        private readonly ScrollView _scroll = new ScrollView(ScrollViewMode.Vertical);

        public SeasonScreen(RunSession session, LocalizedStrings text, Action onClose)
        {
            _session = session;
            _text = text;
            _onClose = onClose;
        }

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("page");

            _scroll.AddToClassList("page__scroll");
            _scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragToScroll(_scroll);

            var body = new VisualElement();
            body.AddToClassList("page__body");
            body.Add(BuildNext());
            body.Add(BuildTable());
            body.Add(BuildTarget());
            _scroll.Add(body);
            root.Add(_scroll);

            // Pinned beneath the scroller, as the report's is — the same rule for the same reason.
            root.Add(CloseButton());
            return root;
        }

        // ----- Who is next --------------------------------------------------------------------------------

        private VisualElement BuildNext()
        {
            VisualElement card = Card();
            Fixture? next = _session.NextFixture();

            if (next == null)
            {
                card.Add(Eyebrow(_text.Or(UiTextKeys.SeasonOver)));
                return card;
            }

            Fixture fixture = next.Value;
            bool atHome = fixture.Home == _session.ManagedClub;
            ClubId opponent = atHome ? fixture.Away : fixture.Home;

            card.Add(Eyebrow(
                _text.Or(UiTextKeys.SeasonNext) + "  ·  "
                + _text.Or(UiTextKeys.SquadWeek) + " " + (fixture.Round + 1) + "/" + _session.RoundCount));

            var name = new Label(_session.ClubName(opponent));
            name.AddToClassList("headline");
            card.Add(name);

            var venue = new Label(_text.Or(atHome ? UiTextKeys.SeasonHome : UiTextKeys.SeasonAway));
            venue.AddToClassList("season__venue");
            card.Add(venue);

            return card;
        }

        // ----- The table ----------------------------------------------------------------------------------

        private VisualElement BuildTable()
        {
            VisualElement card = Card();
            card.Add(Eyebrow(_text.Or(UiTextKeys.SeasonTable)));
            card.Add(BuildHead());

            IReadOnlyList<LeagueTableRow> rows = _session.Standings();
            BoardTarget target = _session.BoardTarget;

            for (int i = 0; i < rows.Count; i++)
            {
                int position = i + 1;
                card.Add(BuildRow(position, rows[i]));

                if (TableZones.LineBelow(position, target, rows.Count))
                {
                    card.Add(BuildLine(position, target));
                }
            }

            return card;
        }

        // The column heads are the one place a single letter is the word (GameStrings says why). The club
        // column's head is blank: a heading over a list of names would only say "names".
        private VisualElement BuildHead()
        {
            var head = new VisualElement();
            head.AddToClassList("table__row");
            head.AddToClassList("table__head");
            head.Add(Cell(string.Empty, "table__pos"));
            head.Add(Cell(string.Empty, "table__club"));
            head.Add(Cell(_text.Or(UiTextKeys.SeasonPlayed), "table__stat"));
            head.Add(Cell(_text.Or(UiTextKeys.SeasonWon), "table__stat"));
            head.Add(Cell(_text.Or(UiTextKeys.SeasonDrawn), "table__stat"));
            head.Add(Cell(_text.Or(UiTextKeys.SeasonLost), "table__stat"));
            head.Add(Cell(_text.Or(UiTextKeys.SeasonGoalDifference), "table__stat"));
            head.Add(Cell(_text.Or(UiTextKeys.SeasonPoints), "table__pts"));
            return head;
        }

        private VisualElement BuildRow(int position, LeagueTableRow row)
        {
            var line = new VisualElement();
            line.AddToClassList("table__row");

            // Only the managed club is marked. Every other row is the default, because a table where every
            // row wore a class that styled nothing would be weight with no meaning.
            if (row.Club == _session.ManagedClub)
            {
                line.AddToClassList("table__row--ours");
            }

            line.Add(Cell(Number(position), "table__pos"));
            line.Add(Cell(_session.ClubName(row.Club), "table__club"));
            line.Add(Cell(Number(row.Played), "table__stat"));
            line.Add(Cell(Number(row.Won), "table__stat"));
            line.Add(Cell(Number(row.Drawn), "table__stat"));
            line.Add(Cell(Number(row.Lost), "table__stat"));
            line.Add(Cell(Signed(row.GoalDifference), "table__stat"));
            line.Add(Cell(Number(row.Points), "table__pts"));
            return line;
        }

        /// <summary>
        /// The line under a position, captioned with the zone it belongs to — and the caption sits on the
        /// zone's side of the line, because "PROMOTION" printed under a line reads as the start of what
        /// follows. So the promotion caption is drawn ABOVE its line, closing the zone, and the relegation
        /// caption BELOW its line, opening one. A line that closes the safe zone directly onto the drop —
        /// the two numbers coinciding, or a board that asks for promotion or nothing — is captioned by the
        /// fall rather than the rise, because that is the one a manager needs to see.
        /// </summary>
        private VisualElement BuildLine(int position, BoardTarget target)
        {
            var line = new VisualElement();
            line.AddToClassList("table__line");

            bool sackBelow = TableZones.Of(position + 1, target) == SeasonVerdict.Sacked;
            line.AddToClassList(sackBelow ? "table__line--opens" : "table__line--closes");

            var caption = new Label(_text.Or(sackBelow ? UiTextKeys.SeasonRelegation : UiTextKeys.SeasonPromotion));
            caption.AddToClassList("table__line-caption");
            line.Add(caption);
            return line;
        }

        // ----- What the board asks for --------------------------------------------------------------------

        /// <summary>The two numbers as a sentence, because the lines through the table show where they fall
        /// but not that they are the whole of the job.</summary>
        private VisualElement BuildTarget()
        {
            VisualElement card = Card();
            card.Add(Eyebrow(_text.Or(UiTextKeys.SeasonTarget)));

            BoardTarget target = _session.BoardTarget;
            var axes = new VisualElement();
            axes.AddToClassList("report__axes");
            axes.Add(Axis(UiTextKeys.SeasonPromotion, UiTextKeys.SeasonTargetUp, target.PromotionPosition));
            axes.Add(Axis(UiTextKeys.SeasonJob, UiTextKeys.SeasonTargetStay, target.SurvivalPosition));
            card.Add(axes);

            return card;
        }

        private VisualElement Axis(string nameKey, string valueKey, int position)
        {
            var axis = new VisualElement();
            axis.AddToClassList("report__axis");

            var name = new Label(_text.Or(nameKey));
            name.AddToClassList("report__axis-name");
            axis.Add(name);

            // The number is rendered here, at the edge, where the locale is known — the template only
            // says where it goes (TextTemplate).
            var value = new Label(_text.Or(valueKey, new TextArguments(string.Empty, string.Empty, string.Empty, Number(position))));
            value.AddToClassList("report__axis-value");
            axis.Add(value);

            return axis;
        }

        // ----- Parts --------------------------------------------------------------------------------------

        private Button CloseButton()
        {
            var button = new Button(() => _onClose?.Invoke()) { text = _text.Or(UiTextKeys.ActionClose) };
            button.AddToClassList("button");
            button.AddToClassList("button--primary");
            button.AddToClassList("page__action");
            return button;
        }

        private static Label Cell(string words, string cls)
        {
            var label = new Label(words);
            label.AddToClassList(cls);
            return label;
        }

        private static Label Eyebrow(string words)
        {
            var label = new Label(words);
            label.AddToClassList("label");
            return label;
        }

        private static VisualElement Card()
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            return card;
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        // A goal difference carries its sign, so +7 and -7 cannot be misread in a column that mixes them.
        private static string Signed(int value)
        {
            return value > 0 ? "+" + Number(value) : Number(value);
        }
    }
}
