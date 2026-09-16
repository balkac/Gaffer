using System;
using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Run;
using Gaffer.Common;
using Gaffer.Common.Localization;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;
using Gaffer.Presentation.Shell;
using UnityEngine.UIElements;

namespace Gaffer.Presentation.Drama
{
    /// <summary>
    /// The decision moment, on the shell's sheet (GDD §4.7 rule 2: a drama is a decision, not a
    /// notification). What happened, who it happened to, and two or three answers — each with what it
    /// will cost written underneath, because the Faz 4 playtest said the +/− of a choice was not
    /// understood and a label alone asks the manager to guess.
    ///
    /// <para><b>It asks the session for one thing: to answer.</b> Everything the card shows before the
    /// answer comes off the <see cref="PendingDrama"/> it was handed plus two prices (a wage, a fee) the
    /// session already knows how to quote; everything after comes off the <see cref="DramaResolution"/>
    /// the answer returned. The card never works out what changed by looking at the run
    /// (NON-NEGOTIABLE #4). Which lines there are and how they are toned is <see cref="DramaLines"/>'s
    /// job, held apart so <c>dotnet test</c> can hold it.</para>
    ///
    /// <para><b>The same card, twice.</b> Choosing does not close the sheet: the body is redrawn as
    /// "what happened" with one way out, so the consequence is read once, in full, before the roster
    /// carries it as a badge. A refused answer (a forced sale the squad cannot make) stays on the card as
    /// the core's own sentence, with the other answers still live — the manager can answer differently,
    /// which is a better beat than a silent no-op.</para>
    /// </summary>
    public sealed class DramaCard
    {
        private readonly RunSession _session;
        private readonly LocalizedStrings _text;
        private readonly IScreenHost _host;
        private readonly Action _onDone;

        private readonly VisualElement _body = new VisualElement();
        private readonly VisualElement _action = new VisualElement();
        private readonly Label _refusal = new Label();
        private PendingDrama _pending;

        public DramaCard(RunSession session, LocalizedStrings text, IScreenHost host, Action onDone)
        {
            _session = session;
            _text = text;
            _host = host;
            _onDone = onDone;
        }

        /// <summary>Builds the card for the drama waiting on an answer. Built whole and thrown away, like
        /// the report: a decision is shown once.</summary>
        public VisualElement Build(PendingDrama pending)
        {
            _pending = pending;

            var root = new VisualElement();
            root.AddToClassList("page");

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("page__scroll");
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragToScroll(scroll);

            _body.AddToClassList("page__body");
            DrawDecision();
            scroll.Add(_body);
            root.Add(scroll);

            // Empty until the answer lands: a decision has no "continue" — the choices are the way out.
            _action.AddToClassList("page__action-slot");
            root.Add(_action);
            return root;
        }

        // ----- The decision -------------------------------------------------------------------------------

        private void DrawDecision()
        {
            _body.Clear();
            _body.Add(Headline(UiTextKeys.DramaEyebrow, _pending.Event.TitleKey));

            var story = new Label(_text.Or(_pending.Event.BodyKey, Names()));
            story.AddToClassList("body");
            story.AddToClassList("drama__story");
            _body.Add(story);

            IReadOnlyList<DramaChoice> choices = _pending.Event.Choices;
            for (int i = 0; i < choices.Count; i++)
            {
                _body.Add(Choice(i, choices[i]));
            }

            // The core's own refusal, in the rare case an answer cannot be carried out. Hidden until then.
            _refusal.AddToClassList("card__refusal");
            _refusal.AddToClassList("body");
            _refusal.style.display = DisplayStyle.None;
            _body.Add(_refusal);
        }

        // One answer: what it says, and under it what it does. The whole block is the tap target — the
        // lines are part of the choice, not a footnote beside it — and the children ignore picking so the
        // tap lands on the block whichever word the thumb hits (see SquadScreen.MakePlayerRow).
        private VisualElement Choice(int index, DramaChoice choice)
        {
            var block = new VisualElement();
            block.AddToClassList("choice");
            block.RegisterCallback<ClickEvent>(_ => Choose(index));

            var label = new Label(_text.Or(choice.LabelKey, Names()));
            label.AddToClassList("choice__label");
            label.pickingMode = PickingMode.Ignore;
            block.Add(label);

            Player subject = _pending.Subject;
            List<DramaLine> lines = DramaLines.Preview(
                _pending,
                choice,
                _session.Finances.Cash,
                subject != null ? _session.WeeklyWageOf(subject) : 0L,
                subject != null ? _session.FeeOf(subject) : 0L,
                _text);

            for (int i = 0; i < lines.Count; i++)
            {
                block.Add(Line(lines[i]));
            }

            return block;
        }

        private void Choose(int index)
        {
            Result<DramaResolution> answered = _session.ResolveDrama(index);
            if (answered.IsFailure)
            {
                _refusal.text = answered.Error;
                _refusal.style.display = DisplayStyle.Flex;
                return;
            }

            _host.RunChanged();
            DrawAftermath(answered.Value);
        }

        // ----- What happened ------------------------------------------------------------------------------

        private void DrawAftermath(DramaResolution resolution)
        {
            _body.Clear();
            _body.Add(Headline(UiTextKeys.DramaAftermath, _pending.Event.TitleKey));

            VisualElement card = new VisualElement();
            card.AddToClassList("card");
            List<DramaLine> lines = DramaLines.Aftermath(resolution, NameOf, _text);
            for (int i = 0; i < lines.Count; i++)
            {
                card.Add(Line(lines[i]));
            }

            if (resolution.MoraleChanges != null && resolution.MoraleChanges.Count > 0)
            {
                // Where the consequence goes on living: the badge on the roster is this line's other half.
                var note = new Label(_text.Or(UiTextKeys.DramaOnRoster));
                note.AddToClassList("drama__note");
                card.Add(note);
            }

            _body.Add(card);

            var done = new Button(_onDone) { text = _text.Or(UiTextKeys.ActionContinue) };
            done.AddToClassList("button");
            done.AddToClassList("button--primary");
            done.AddToClassList("page__action");
            _action.Clear();
            _action.Add(done);
        }

        // A morale entry can land on a man the same resolution then sold, so he is looked up rather than
        // assumed to be on the roster; the sold man is named from the resolution itself.
        private string NameOf(PlayerId player)
        {
            IReadOnlyList<Player> squad = _session.Squad.Players;
            for (int i = 0; i < squad.Count; i++)
            {
                if (squad[i].Id.Equals(player))
                {
                    return squad[i].Name;
                }
            }

            return null;
        }

        // ----- Parts --------------------------------------------------------------------------------------

        private VisualElement Headline(string eyebrowKey, string titleKey)
        {
            var head = new VisualElement();
            head.AddToClassList("drama__head");

            var eyebrow = new Label(_text.Or(eyebrowKey));
            eyebrow.AddToClassList("label");
            head.Add(eyebrow);

            var title = new Label(_text.Or(titleKey, Names()));
            title.AddToClassList("headline");
            title.AddToClassList("drama__title");
            head.Add(title);
            return head;
        }

        private static Label Line(DramaLine line)
        {
            var label = new Label(line.Text);
            label.AddToClassList("choice__line");
            label.AddToClassList(ToneClass(line.Tone));
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        private static string ToneClass(DramaTone tone)
        {
            switch (tone)
            {
                case DramaTone.Gain:
                    return "choice__line--gain";
                case DramaTone.Cost:
                    return "choice__line--cost";
                default:
                    return "choice__line--quiet";
            }
        }

        // The two entities a template may name. The subject is empty for a club-level event, which is
        // correct rather than missing: those events' copy names the club instead.
        private TextArguments Names()
        {
            string player = _pending.Subject != null ? _pending.Subject.Name : string.Empty;
            return new TextArguments(player, _session.ManagedClubName, string.Empty, string.Empty);
        }
    }
}
