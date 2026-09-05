using System;
using System.Collections.Generic;
using Gaffer.Application.Run;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common.Localization;
using Gaffer.Domain.Clubs;
using UnityEngine.UIElements;

namespace Gaffer.Presentation.Matchday
{
    /// <summary>
    /// What happened when the week was played — ART_STYLE §06, the signature screen. A score bug, a feed
    /// of the afternoon's beats, the setup that produced them, and the rest of the division.
    ///
    /// <para><b>It replays an outcome and asks the session nothing.</b> Every value on it comes off the
    /// <see cref="WeekOutcome"/> handed in; the screen never reaches back into the run to work out what
    /// changed (NON-NEGOTIABLE #4). The session is here only to turn ids into names, which is a lookup and
    /// not a second opinion about the week.</para>
    ///
    /// <para><b>It draws; it does not decide.</b> Which beats there are and in what order is
    /// <see cref="MatchBeats"/>'s job, and that lives apart so <c>dotnet test</c> can hold those rules.
    /// What is left here is a broadcast's shape: what is big, what is quiet, where the one accent goes.</para>
    /// </summary>
    public sealed class MatchScreen
    {
        private readonly RunSession _session;
        private readonly LocalizedStrings _text;
        private readonly Action _onContinue;

        // The report owns its own scroller so it can PIN the way out beneath it. Measured 2026-08-30: with
        // the button inside the scroll, a week carrying two career moments put it at y 2181 on a screen
        // 2108 tall — entirely off the bottom, with the scrollbar hidden so nothing said to scroll. The
        // squad screen already states the rule this broke: header and actions are pinned, so the action is
        // always under the thumb.
        private readonly ScrollView _scroll = new ScrollView(ScrollViewMode.Vertical);

        public MatchScreen(RunSession session, LocalizedStrings text, Action onContinue)
        {
            _session = session;
            _text = text;
            _onContinue = onContinue;
        }

        /// <summary>Builds the report for one played week. Built whole and thrown away: it is shown once
        /// per week and a diff would be a second model of an immutable record.</summary>
        public VisualElement Build(WeekOutcome week)
        {
            var root = new VisualElement();
            root.AddToClassList("report");

            _scroll.AddToClassList("report__scroll");
            _scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragToScroll(_scroll);
            _scroll.Add(BuildBody(week));

            root.Add(_scroll);

            // Outside the scroller, and last: the way out of a modal must not be something you have to go
            // looking for.
            root.Add(ContinueButton());
            return root;
        }

        private VisualElement BuildBody(WeekOutcome week)
        {
            var body = new VisualElement();
            body.AddToClassList("report__body");

            if (week.ManagedMatch == null)
            {
                body.Add(Card(Body(_text.Or(UiTextKeys.MessageNoFixture))));
                return body;
            }

            MatchResult match = week.ManagedMatch.Value;
            body.Add(BuildScoreBug(week, match));
            body.Add(BuildFeed(week, match));
            body.Add(BuildSetup());

            VisualElement elsewhere = BuildElsewhere(week, match);
            if (elsewhere != null)
            {
                body.Add(elsewhere);
            }

            return body;
        }

        // ----- The score bug ------------------------------------------------------------------------------

        private VisualElement BuildScoreBug(WeekOutcome week, MatchResult match)
        {
            VisualElement card = Card();
            card.AddToClassList("report__bug");

            var eyebrow = new Label(_text.Or(UiTextKeys.SquadWeek) + " " + (week.Round + 1) + "/" + week.RoundCount);
            eyebrow.AddToClassList("label");
            card.Add(eyebrow);

            var line = new VisualElement();
            line.AddToClassList("report__score");
            line.Add(ClubLabel(match.Home));
            line.Add(GoalsLabel(match));
            line.Add(ClubLabel(match.Away));
            card.Add(line);

            // The shot counts under the score, in the same left-to-right order as the clubs above them, so
            // the column a number sits in is the club it belongs to and no label has to say so.
            var shots = new VisualElement();
            shots.AddToClassList("report__shots");
            shots.Add(ShotsLabel(match.HomeShots));
            shots.Add(ShotsLabel(match.AwayShots));
            card.Add(shots);

            return card;
        }

        private Label ClubLabel(ClubId club)
        {
            var label = new Label(_session.ClubName(club));
            label.AddToClassList("report__club");
            return label;
        }

        /// <summary>
        /// The scoreline, coloured by what it was worth to the manager, in the sanctioned result classes
        /// rather than a parallel set of its own. This is the one place on the screen where the semantic
        /// colours are earned: ART_STYLE reserves win/loss/draw for a RESULT, and a result is what this is.
        /// </summary>
        private Label GoalsLabel(MatchResult match)
        {
            var label = new Label(match.HomeGoals + " – " + match.AwayGoals);
            label.AddToClassList("report__goals");

            bool weWereHome = match.Home.Value == _session.ManagedClub.Value;
            int ours = weWereHome ? match.HomeGoals : match.AwayGoals;
            int theirs = weWereHome ? match.AwayGoals : match.HomeGoals;

            if (ours > theirs)
            {
                label.AddToClassList("result--win");
            }
            else if (ours < theirs)
            {
                label.AddToClassList("result--loss");
            }
            else
            {
                label.AddToClassList("result--draw");
            }

            return label;
        }

        private Label ShotsLabel(int shots)
        {
            var label = new Label(shots + " " + _text.Or(UiTextKeys.MatchShots));
            label.AddToClassList("report__shot-count");
            return label;
        }

        // ----- The feed -----------------------------------------------------------------------------------

        private VisualElement BuildFeed(WeekOutcome week, MatchResult match)
        {
            VisualElement card = Card();
            List<MatchBeat> beats = MatchBeats.Of(week, match, _session, _text);

            if (beats.Count == 0)
            {
                // Said rather than left blank. A goalless week with nobody's career touched is a real
                // outcome and the screen should own it, not look like it failed to load.
                card.Add(Body(_text.Or(UiTextKeys.MatchQuiet)));
                return card;
            }

            for (int i = 0; i < beats.Count; i++)
            {
                // The rule is dividers BETWEEN beats, and USS has no :first-child to say so — the pseudo
                // classes Unity supports are the interaction ones. Said here instead, because a line under
                // the card's own edge separates the first beat from nothing.
                card.Add(BuildBeat(beats[i], divided: i > 0));
            }

            return card;
        }

        private VisualElement BuildBeat(MatchBeat beat, bool divided)
        {
            var row = new VisualElement();
            row.AddToClassList("report__beat");
            if (divided)
            {
                row.AddToClassList("report__beat--divided");
            }

            // Only the opposition's beats are marked. Ours are the default because they are most of the
            // feed, and a class on every row that styles nothing is weight with no meaning.
            if (!beat.IsOurs)
            {
                row.AddToClassList("report__beat--theirs");
            }

            if (beat.HasStory)
            {
                row.AddToClassList("report__beat--story");
            }

            var minute = new Label(beat.Minute + "'");
            minute.AddToClassList("report__minute");
            row.Add(minute);

            var body = new VisualElement();
            body.AddToClassList("report__beat-body");

            var headline = new Label(beat.Headline);
            headline.AddToClassList("report__headline");
            body.Add(headline);

            if (beat.HasStory)
            {
                var story = new Label(beat.Story);
                story.AddToClassList("report__story");
                body.Add(story);

                var tag = new Label(_text.Or(UiTextKeys.MatchJournal));
                tag.AddToClassList("report__tag");
                body.Add(tag);
            }

            row.Add(body);
            return row;
        }

        // ----- The setup ----------------------------------------------------------------------------------

        /// <summary>
        /// What was set up to produce that. The four axes pay measurably in the simulation, so the report
        /// names them: a result the manager cannot connect to a decision teaches him nothing.
        /// </summary>
        private VisualElement BuildSetup()
        {
            VisualElement card = Card();

            var eyebrow = new Label(_text.Or(UiTextKeys.MatchSetup));
            eyebrow.AddToClassList("label");
            card.Add(eyebrow);

            var shape = new Label(_session.Formation.Name);
            shape.AddToClassList("report__shape");
            card.Add(shape);

            var axes = new VisualElement();
            axes.AddToClassList("report__axes");

            Tactics tactics = _session.Tactics;
            axes.Add(Axis(UiTextKeys.MatchMentality, TacticsTextKeys.For(tactics.Mentality)));
            axes.Add(Axis(UiTextKeys.MatchTempo, TacticsTextKeys.For(tactics.Tempo)));
            axes.Add(Axis(UiTextKeys.MatchPressing, TacticsTextKeys.For(tactics.Pressing)));
            axes.Add(Axis(UiTextKeys.MatchApproach, TacticsTextKeys.For(tactics.Approach)));
            card.Add(axes);

            return card;
        }

        private VisualElement Axis(string labelKey, string valueKey)
        {
            var axis = new VisualElement();
            axis.AddToClassList("report__axis");

            var name = new Label(_text.Or(labelKey));
            name.AddToClassList("report__axis-name");
            axis.Add(name);

            var value = new Label(_text.Or(valueKey));
            value.AddToClassList("report__axis-value");
            axis.Add(value);

            return axis;
        }

        // ----- The rest of the division -------------------------------------------------------------------

        private VisualElement BuildElsewhere(WeekOutcome week, MatchResult match)
        {
            VisualElement card = Card();

            var eyebrow = new Label(_text.Or(UiTextKeys.MatchElsewhere));
            eyebrow.AddToClassList("label");
            card.Add(eyebrow);

            int shown = 0;
            for (int i = 0; i < week.Matches.Count; i++)
            {
                MatchResult other = week.Matches[i];
                if (other.Home.Value == match.Home.Value && other.Away.Value == match.Away.Value)
                {
                    continue;
                }

                card.Add(OtherResult(other));
                shown++;
            }

            // A two-club division plays one fixture a round, and an empty card headed ELSEWHERE would be
            // the screen promising something it has not got.
            return shown == 0 ? null : card;
        }

        private VisualElement OtherResult(MatchResult other)
        {
            var row = new VisualElement();
            row.AddToClassList("report__other");

            var home = new Label(_session.ClubName(other.Home));
            home.AddToClassList("report__other-home");
            row.Add(home);

            var score = new Label(other.HomeGoals + "–" + other.AwayGoals);
            score.AddToClassList("report__other-score");
            row.Add(score);

            var away = new Label(_session.ClubName(other.Away));
            away.AddToClassList("report__other-away");
            row.Add(away);

            return row;
        }

        // ----- Parts --------------------------------------------------------------------------------------

        private Button ContinueButton()
        {
            var button = new Button(() => _onContinue?.Invoke()) { text = _text.Or(UiTextKeys.ActionContinue) };
            button.AddToClassList("button");
            button.AddToClassList("button--primary");
            button.AddToClassList("report__action");
            return button;
        }

        private static VisualElement Card(VisualElement child = null)
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            if (child != null)
            {
                card.Add(child);
            }

            return card;
        }

        private static Label Body(string words)
        {
            var label = new Label(words);
            label.AddToClassList("body");
            return label;
        }
    }
}
