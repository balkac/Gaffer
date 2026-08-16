using System.Collections.Generic;
using Gaffer.Application.Run;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Common.Localization;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaffer.Presentation.Squad
{
    /// <summary>
    /// The squad and tactics screen — the first of the five, and the one the manager touches most.
    ///
    /// <para><b>A view over outcomes, not a second copy of the game.</b> It renders
    /// <see cref="LineupOutcome"/> and sends commands back; it never reaches into the run to diff state
    /// (NON-NEGOTIABLE #4). Every command it issues returns the outcome it should draw next, so the screen
    /// has no idea what changed and does not need one — which is what stops a UI drifting away from the
    /// simulation, the exact failure the two editor windows had before <c>RunSession</c> existed.</para>
    ///
    /// <para><b>Built in C# rather than UXML</b>, like the editor windows: the layout is entirely
    /// data-driven, so a markup file would be a near-empty shell plus a second place to keep names in
    /// step. The look is not here either — every colour and size comes from <c>Gaffer.uss</c>, and the
    /// only styling decision this file makes is which CLASS a thing wears.</para>
    ///
    /// <para><b>Mobile first.</b> The eleven and the bench are <see cref="ListView"/>s, so rows are
    /// recycled rather than built per player — a squad grows without bound (contracts are not written
    /// yet) and a market list will do the same. Every row is a 44px tap target.</para>
    /// </summary>
    public sealed class SquadScreen
    {
        private readonly RunSession _session;
        private readonly VisualElement _root;

        // The words. Injected rather than reached for: Presentation may not see the string table's
        // assembly (NON-NEGOTIABLE #8's layer half), and Composition is what binds a locale to a screen —
        // which is also what lets the language change without this class knowing there are languages.
        private readonly LocalizedStrings _text;

        private readonly Label _clubName = new Label();
        private readonly Label _standing = new Label();
        private readonly Label _shape = new Label();
        private readonly PitchView _pitch;
        private readonly ScrollView _page = new ScrollView(ScrollViewMode.Vertical);
        private readonly VisualElement _elevenCard = new VisualElement();
        private readonly VisualElement _eleven = new VisualElement();
        private readonly VisualElement _bench = new VisualElement();
        private readonly Label _message = new Label();

        // The lists ListView binds against. Held and refilled rather than replaced, so a rebind does not
        // hand the view a different collection every time (PERFORMANCE §8).
        private readonly List<Player> _starters = new List<Player>();
        private readonly List<Player> _benched = new List<Player>();

        private bool _showingPitch = true;
        private int _benchPressed = -1;
        private bool _benchDragging;
        private UnityEngine.Vector2 _benchOrigin;

        public SquadScreen(RunSession session, VisualElement root, LocalizedStrings text)
        {
            _session = session;
            _root = root;
            _text = text;
            _pitch = new PitchView(OnSwapRequested);
        }

        /// <summary>Builds the screen once and draws the run's current state into it.</summary>
        public void Build()
        {
            _root.Clear();

            // .theme carries the palette, .screen the ground it paints. Both, because a screen that wore
            // only the second would resolve every var() to nothing and fall back to Unity's default
            // runtime theme — which is exactly what a missing stylesheet looks like on a device.
            _root.AddToClassList("theme");
            _root.AddToClassList("screen");

            // ONE scroller, and it owns the page.
            //
            // Everything used to sit in a plain container that could not scroll, so the content simply ran
            // off the bottom, and the squad lists each brought a scroller of their own to compensate. That
            // is the worst of both: a phone-sized page you cannot reach the end of, and lists that fight
            // the page for the same drag. A screen scrolls; the things on it do not.
            _page.AddToClassList("page");
            _root.Add(_page);

            _page.Add(BuildHeader());
            _page.Add(BuildEleven());
            var benchCard = new VisualElement();
            benchCard.AddToClassList("card");
            var benchTitle = new Label(Say(UiTextKeys.SquadBench));
            benchTitle.AddToClassList("label");
            benchCard.Add(benchTitle);
            benchCard.Add(_bench);
            _page.Add(benchCard);
            _page.Add(BuildActions());

            _message.AddToClassList("body");
            _page.Add(_message);

            Draw(_session.Lineup());
        }

        // ----- Drawing ------------------------------------------------------------------------------------

        // Everything the screen shows, from one outcome. There is no other path in: a command's result
        // comes back through here too, so "what does the screen show" has one answer.
        private void Draw(LineupOutcome lineup)
        {
            _clubName.text = _session.ManagedClubName;
            string week = Say(UiTextKeys.SquadWeek) + " " + _session.PlayedRounds + "/" + _session.RoundCount;
            _standing.text = _session.TablePosition > 0
                ? Say(UiTextKeys.SquadPosition) + " " + _session.TablePosition + "  ·  " + week
                : week;

            TeamStrength strength = lineup.Strength;
            _shape.text = lineup.Formation.Name
                + "   " + Say(UiTextKeys.SquadAttack) + " " + Rounded(strength.Attack)
                + "   " + Say(UiTextKeys.SquadMidfield) + " " + Rounded(strength.Midfield)
                + "   " + Say(UiTextKeys.SquadDefence) + " " + Rounded(strength.Defence);

            Refill(_starters, lineup.Starters);
            Refill(_benched, lineup.Bench);
            FillList(_eleven, _starters, OnStarterTapped);
            FillList(_bench, _benched, OnBenchTapped, draggableOntoPitch: true);
            _pitch.Draw(lineup.Formation, lineup.Slots);
        }

        private static void Refill(List<Player> into, IReadOnlyList<Player> from)
        {
            into.Clear();
            for (int i = 0; i < from.Count; i++)
            {
                into.Add(from[i]);
            }
        }

        // The eleven, as a board or as a list. Both, because they answer different questions: the board
        // shows the SHAPE — is anybody covering the left? — and the list is how twenty-five names get
        // scanned on a phone. A toggle rather than one or the other, since neither answers both.
        private VisualElement BuildEleven()
        {
            _elevenCard.AddToClassList("card");

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;

            var title = new Label(Say(UiTextKeys.SquadEleven));
            title.AddToClassList("label");
            title.style.flexGrow = 1;
            head.Add(title);

            var toggle = new Button { text = Say(UiTextKeys.ViewList) };
            toggle.AddToClassList("button");
            toggle.clicked += () => ShowPitch(!_showingPitch, toggle);
            head.Add(toggle);

            _elevenCard.Add(head);
            _elevenCard.Add(_pitch.Root);

            _eleven.style.display = DisplayStyle.None;
            _elevenCard.Add(_eleven);

            return _elevenCard;
        }

        private void ShowPitch(bool pitch, Button toggle)
        {
            _showingPitch = pitch;
            _pitch.Root.style.display = pitch ? DisplayStyle.Flex : DisplayStyle.None;
            _eleven.style.display = pitch ? DisplayStyle.None : DisplayStyle.Flex;
            toggle.text = Say(pitch ? UiTextKeys.ViewList : UiTextKeys.ViewPitch);
            _pitch.ClearSelection();
        }

        // ----- Layout -------------------------------------------------------------------------------------

        private VisualElement BuildHeader()
        {
            var card = new VisualElement();
            card.AddToClassList("card");

            _clubName.AddToClassList("headline");
            _standing.AddToClassList("label");
            _shape.AddToClassList("body");

            card.Add(_clubName);
            card.Add(_standing);
            card.Add(_shape);
            return card;
        }

        /// <summary>
        /// Fills a list with one row per player, rebuilt whole.
        ///
        /// <para><b>Plain rows rather than a ListView, and that is a size judgement rather than a
        /// principle.</b> A ListView earns its recycling on the transfer market's fifty thousand; a squad
        /// is twenty-five, and there the virtualisation only bought a nested scroller that fought the page
        /// and a scrollbar nobody asked for. The market screen will still use one — this is the same
        /// decision made honestly for a different number.</para>
        /// </summary>
        private void FillList(VisualElement list, List<Player> source, System.Action<int> onTapped, bool draggableOntoPitch = false)
        {
            list.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                // A draggable row gets NO tap handler of its own: its own PointerUp already decides
                // between a tap and a drop, and a second handler would fire the tap again on every drop.
                VisualElement row = MakePlayerRow(draggableOntoPitch ? null : onTapped);
                BindPlayerRow(row, source, i);
                if (draggableOntoPitch)
                {
                    RegisterBenchDrag(row, i);
                }

                list.Add(row);
            }
        }

        // Layout and look both come from the stylesheet; this only says what the parts ARE. Inline styles
        // here would be the palette leaking into C# one property at a time.
        // A bench row can be dragged onto the board, which is the gesture a manager reaches for first:
        // pick a substitute up and drop him where he should play. The row reports the gesture and the
        // BOARD decides where it landed — it owns the slots, so it owns the hit-testing.
        private void RegisterBenchDrag(VisualElement row, int index)
        {
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                _benchPressed = index;
                _benchOrigin = evt.position;
                _benchDragging = false;
                row.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            row.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_benchPressed < 0 || !row.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                if (!_benchDragging && (evt.position - (UnityEngine.Vector3)_benchOrigin).magnitude >= 24f)
                {
                    _benchDragging = true;
                    _pitch.BeginDragFromOutside(index < _benched.Count ? _benched[index].Name : string.Empty);
                }

                if (_benchDragging)
                {
                    _pitch.MoveDragGhost(evt.position);
                    evt.StopPropagation();
                }
            });

            row.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                if (row.HasPointerCapture(evt.pointerId))
                {
                    row.ReleasePointer(evt.pointerId);
                }

                int player = _benchPressed;
                _benchPressed = -1;
                if (!_benchDragging)
                {
                    OnBenchTapped(player);
                    return;
                }

                _benchDragging = false;
                _pitch.EndDragFromOutside();

                int slot = _pitch.SlotUnder(evt.position);
                if (slot >= 0 && player >= 0 && player < _benched.Count)
                {
                    Apply(_session.PlaceInSlot(slot, _benched[player].Id));
                }
            });

            row.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (_benchDragging)
                {
                    _benchDragging = false;
                    _pitch.EndDragFromOutside();
                }

                _benchPressed = -1;
            });
        }

        private static VisualElement MakePlayerRow(System.Action<int> onTapped)
        {
            var row = new VisualElement();
            row.AddToClassList("row");

            // The click is registered on the ROW, and the row reads its own index.
            //
            // It used to be one handler on the ListView reading evt.target, and that was a real bug: the
            // target is the DEEPEST element under the finger, so tapping a player's NAME delivered the
            // Label — which carries no index — and the tap was swallowed. Only the gaps between the labels
            // worked, which reads as "it misses about half my taps".
            //
            // The children are picking-disabled as well, so the row is one target rather than four. Both
            // halves are needed: without the first the row never hears the tap, without the second the
            // label eats it before the row can.
            if (onTapped != null)
            {
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.currentTarget is VisualElement tapped && tapped.userData is int index)
                    {
                        onTapped(index);
                    }
                });
            }

            var name = new Label();
            name.AddToClassList("row__name");
            name.pickingMode = PickingMode.Ignore;
            row.Add(name);

            var role = new Label();
            role.AddToClassList("row__role");
            role.pickingMode = PickingMode.Ignore;
            row.Add(role);

            var rating = new Label();
            rating.AddToClassList("row__rating");
            rating.pickingMode = PickingMode.Ignore;
            row.Add(rating);

            return row;
        }

        private static void BindPlayerRow(VisualElement element, List<Player> source, int index)
        {
            if (index < 0 || index >= source.Count)
            {
                return;
            }

            Player player = source[index];
            element.userData = index;

            var name = (Label)element[0];
            var role = (Label)element[1];
            var rating = (Label)element[2];

            name.text = player.Name;
            role.text = player.Role.ToString().Substring(0, 3).ToUpperInvariant();

            double value = PlayerRatings.ForRole(player);
            rating.text = Rounded(value);

            // EVERY band class is removed before the right one is added. A reused row carries whatever the
            // last player left on it otherwise, and the bug shows up as one row in a scrolled list wearing
            // somebody else's brightness.
            foreach (AbilityBand band in (AbilityBand[])System.Enum.GetValues(typeof(AbilityBand)))
            {
                rating.RemoveFromClassList(AbilityBands.ClassOf(band));
            }

            rating.AddToClassList(AbilityBands.ClassOf(AbilityBands.Of(value)));
        }

        private VisualElement BuildActions()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            var autoPick = new Button(OnAutoPick) { text = Say(UiTextKeys.ActionAutoPick) };
            autoPick.AddToClassList("button");
            autoPick.style.flexGrow = 1;
            autoPick.style.marginRight = 8;

            var advance = new Button(OnAdvanceWeek) { text = Say(UiTextKeys.ActionPlayWeek) };
            advance.AddToClassList("button");
            advance.AddToClassList("button--primary");
            advance.style.flexGrow = 1;

            row.Add(autoPick);
            row.Add(advance);
            return row;
        }

        // ----- Commands -----------------------------------------------------------------------------------

        // Every one of these does the same thing: issue a command, draw what it returns, and say so when
        // it refuses. A refusal is a sentence the core wrote, not a code the screen interprets.

        private void OnStarterTapped(int index)
        {
            if (index >= 0 && index < _starters.Count)
            {
                Apply(_session.ToggleStarter(_starters[index].Id));
            }
        }

        // A bench tap means two different things, and which one is decided by whether a slot is held. With
        // one picked up it is "put HIM there", which is the whole point of the board; with nothing held it
        // falls back to the list behaviour of pushing him into the first free place.
        private void OnBenchTapped(int index)
        {
            if (index < 0 || index >= _benched.Count)
            {
                return;
            }

            int slot = _pitch.SelectedSlot;
            if (slot >= 0)
            {
                _pitch.ClearSelection();
                Apply(_session.PlaceInSlot(slot, _benched[index].Id));
                return;
            }

            Apply(_session.ToggleStarter(_benched[index].Id));
        }

        // Two slots tapped in turn. The core decides what that MEANS — a straight swap when both are
        // filled, benching the occupant when the mover came from elsewhere — and the screen only reports
        // the gesture. Re-deriving the rule here is how a UI starts disagreeing with the game.
        private void OnSwapRequested(int fromSlot, int toSlot)
        {
            IReadOnlyList<Player> slots = _session.Lineup().Slots;
            if (fromSlot < 0 || fromSlot >= slots.Count || slots[fromSlot] == null)
            {
                return;
            }

            Apply(_session.PlaceInSlot(toSlot, slots[fromSlot].Id));
        }

        private void OnAutoPick()
        {
            Apply(_session.AutoPickLineup());
        }

        private void OnAdvanceWeek()
        {
            Result<WeekOutcome> week = _session.AdvanceWeek();
            if (week.IsFailure)
            {
                ShowMessage(week.Error);
                return;
            }

            ShowMessage(Describe(week.Value));
            Draw(_session.Lineup());
        }

        private void Apply(Result<LineupOutcome> result)
        {
            if (result.IsFailure)
            {
                ShowMessage(result.Error);
                return;
            }

            ShowMessage(string.Empty);
            Draw(result.Value);
        }

        // A key's words, or the key itself when the table has no row for it. Visible-but-wrong beats blank:
        // a missing line shows up as "ui.squad.bench" on screen, which names its own fix, where an empty
        // label just looks like a layout bug.
        private string Say(string key)
        {
            string words = _text.IsBound ? _text.Find(key) : null;
            return string.IsNullOrEmpty(words) ? key : words;
        }

        private void ShowMessage(string message)
        {
            _message.text = message;
            _message.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private string Describe(WeekOutcome week)
        {
            if (week.ManagedMatch == null)
            {
                return Say(UiTextKeys.MessageNoFixture);
            }

            MatchResult match = week.ManagedMatch.Value;
            return _session.ClubName(match.Home) + " " + match.HomeGoals + "-" + match.AwayGoals + " " + _session.ClubName(match.Away);
        }

        private static string Rounded(double value)
        {
            return Mathf.RoundToInt((float)value).ToString();
        }
    }
}
