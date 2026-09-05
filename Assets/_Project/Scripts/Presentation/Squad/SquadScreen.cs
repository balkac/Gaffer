using System.Collections.Generic;
using Gaffer.Application.Run;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Common.Localization;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using Gaffer.Presentation.Matchday;
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
        private readonly VisualElement _overlay = new VisualElement();
        private readonly VisualElement _elevenCard = new VisualElement();
        private readonly VisualElement _eleven = new VisualElement();
        private readonly VisualElement _bench = new VisualElement();
        private readonly Label _message = new Label();

        // The lists ListView binds against. Held and refilled rather than replaced, so a rebind does not
        // hand the view a different collection every time (PERFORMANCE §8).
        //
        // The eleven is kept as the SHEET rather than as players, because every question the screen asks
        // about a starter — what is he worth, is he out of position, how much is that costing — is a
        // question about the slot he is in, and a bare list of players cannot answer any of them.
        private readonly List<SlottedPlayer> _sheet = new List<SlottedPlayer>();
        private readonly List<Player> _benched = new List<Player>();

        // What the shape asks for at each slot, kept from the last outcome. The sheet above only knows the
        // FILLED slots, and the one the manager most often taps is an empty one. Held rather than fetched:
        // asking the session for it re-derived a whole LineupOutcome — squad walk, bench walk and a strength
        // derivation — and the picker did that once PER ROW.
        private IReadOnlyList<PlayerRole> _slotRoles = System.Array.Empty<PlayerRole>();

        private bool _showingPitch = true;

        private readonly Button _modeToggle = new Button();
        private readonly VisualElement _listCard = new VisualElement();
        private readonly ScrollView _listScroll = new ScrollView(ScrollViewMode.Vertical);
        private readonly VisualElement _picker = new VisualElement();

        // The week's report. A sheet like the picker rather than a screen of its own, because there is no
        // navigation shell yet — and when there is one, this is the thing that moves, not MatchScreen.
        private readonly VisualElement _report = new VisualElement();
        private readonly VisualElement _reportPanel = new VisualElement();
        private readonly ScrollView _pickerScroll = new ScrollView(ScrollViewMode.Vertical);
        private readonly VisualElement _pickerList = new VisualElement();
        private int _pickerSlot = -1;
        private int _pickerPlayer = -1;

        public SquadScreen(RunSession session, VisualElement root, LocalizedStrings text)
        {
            _session = session;
            _root = root;
            _text = text;
            _pitch = new PitchView(OnSwapRequested, OpenPlayerPicker, text);
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

            // TWO MODES, AND NEITHER SCROLLS UNDER A DRAG.
            //
            // This is the shape shipped football managers use, and it was arrived at the long way round.
            // A board you drag on and a list you swipe cannot share a screen: every attempt — stopping
            // propagation, hold-to-lift, thresholds — only moved which gesture felt broken.
            //
            // So the board mode FILLS the screen and scrolls nothing: header, pitch, actions, and a sheet
            // for "who plays here". The list mode has no board at all and scrolls freely. One surface, one
            // gesture, no arbitration.
            //
            // The page used to scroll as a whole, and the tactics board sat inside it. That is what made
            // dragging a player fight the scroll: the same press-and-move means two things in the same
            // place, and no amount of stopping propagation makes that not confusing to use. It also put
            // the action buttons below the fold.
            //
            // So the board does not scroll, because a thing you drag on must not. The bench does, because
            // it is the only part that can be longer than the screen. Header and actions are pinned, so
            // "play the week" is always under the thumb.
            _root.Add(BuildHeader());
            _root.Add(BuildEleven());
            _root.Add(BuildListMode());
            _message.AddToClassList("body");
            _root.Add(_message);
            _root.Add(BuildActions());

            // The layer the drag ghost draws on. It sits ON the screen root — which is what carries the
            // theme — because an overlay parented to the panel would resolve none of the tokens and draw
            // as a zero-width, colourless nothing. That is exactly what "I cannot see what I am dragging"
            // was.
            _root.Add(BuildPicker());
            _root.Add(BuildReport());

            _overlay.AddToClassList("overlay");
            _overlay.pickingMode = PickingMode.Ignore;
            _root.Add(_overlay);
            _pitch.AttachOverlay(_overlay);

            // Stated rather than assumed. The initial mode used to rest on which elements happened to
            // have had a display set during construction, which is the sort of implicit start that
            // survives until somebody reorders two lines.
            ShowPitch(true);
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

            _slotRoles = lineup.Formation.Slots;
            RefillSheet(_sheet, lineup.Sheet);
            Refill(_benched, lineup.Bench);

            // The eleven is drawn from the SHEET, so each row can price the man where he actually stands.
            // The bench is drawn from a plain list, because a substitute stands nowhere yet and his own
            // role is the only honest number to show him.
            FillSheet(_eleven, _sheet);
            FillList(_bench, _benched, OnBenchTapped);
            _pitch.Draw(lineup.Formation, lineup.Slots, _session.PositionalFit);
        }

        private static void Refill(List<Player> into, IReadOnlyList<Player> from)
        {
            into.Clear();
            for (int i = 0; i < from.Count; i++)
            {
                into.Add(from[i]);
            }
        }

        private static void RefillSheet(List<SlottedPlayer> into, IReadOnlyList<SlottedPlayer> from)
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
            _elevenCard.AddToClassList("card--grow");
            _elevenCard.Add(_pitch.Root);
            return _elevenCard;
        }

        // The list mode: one scroller holding the eleven and the bench, and no board. Nothing here is
        // draggable, so the scroll is unambiguous — which is the entire point of it being a separate mode.
        private VisualElement BuildListMode()
        {
            _listCard.AddToClassList("card");
            _listCard.AddToClassList("card--grow");
            _listCard.style.display = DisplayStyle.None;

            _listScroll.AddToClassList("bench-scroll");

            // Drag the CONTENT to scroll, which is how a phone list works — a bar down the side is a
            // desktop affordance and a thumb never finds it.
            //
            // CLAMPED, not elastic: the bounce at the ends is a nice touch on a device and reads as judder
            // under a mouse, and the editor is where this gets looked at most. AlwaysVisible for the bar
            // for the same reason judder happens at all — an Auto bar appears when the content grows past
            // the frame, which narrows the content, which can shrink it back under the frame, which hides
            // the bar again. That oscillation is a real flicker and it has nothing to do with scrolling.
            _listScroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            // No bar at all. It was AlwaysVisible only to stop an Auto bar oscillating — appearing when
            // the content grew past the frame, narrowing the content, letting it fit again, hiding itself.
            // Hidden is just as stable and is what a phone list looks like: the content IS the control.
            _listScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

            // Content-drag scrolling, which UI Toolkit gives to touch only and not to a mouse. Without it
            // the list cannot be moved in the editor at all — the place this screen is looked at most.
            new DragToScroll(_listScroll);

            var elevenTitle = new Label(Say(UiTextKeys.SquadEleven));
            elevenTitle.AddToClassList("label");
            _listScroll.Add(elevenTitle);
            _listScroll.Add(_eleven);

            var benchTitle = new Label(Say(UiTextKeys.SquadBench));
            benchTitle.AddToClassList("label");
            benchTitle.style.marginTop = 24;
            _listScroll.Add(benchTitle);
            _listScroll.Add(_bench);

            _listCard.Add(_listScroll);
            return _listCard;
        }

        private void ShowPitch(bool pitch)
        {
            _showingPitch = pitch;
            _elevenCard.style.display = pitch ? DisplayStyle.Flex : DisplayStyle.None;
            _listCard.style.display = pitch ? DisplayStyle.None : DisplayStyle.Flex;
            _modeToggle.text = Say(pitch ? UiTextKeys.ViewList : UiTextKeys.ViewPitch);
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

            // The mode toggle lives in the HEADER, which neither mode hides.
            //
            // It used to sit inside the board's own card, so switching to the list hid the card — and the
            // button with it. There was no way back: a one-way door into a mode, which is the kind of
            // fault that makes an interface feel broken rather than merely awkward. A control that
            // switches between two things cannot belong to either of them.
            var strip = new VisualElement();
            strip.AddToClassList("header__strip");

            _shape.style.flexGrow = 1;
            strip.Add(_shape);

            _modeToggle.AddToClassList("button");
            _modeToggle.text = Say(UiTextKeys.ViewList);
            _modeToggle.clicked += () => ShowPitch(!_showingPitch);
            strip.Add(_modeToggle);

            card.Add(strip);
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
        private void FillList(VisualElement list, List<Player> source, System.Action<int> onTapped)
        {
            list.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                VisualElement row = MakePlayerRow(onTapped);
                BindPlayerRow(row, source, i);
                list.Add(row);
            }
        }

        // The eleven, each man priced where he stands. Same row as the bench uses, filled from the sheet
        // instead of a player list, so the two lists cannot end up saying different things about one man.
        private void FillSheet(VisualElement list, List<SlottedPlayer> sheet)
        {
            list.Clear();
            for (int i = 0; i < sheet.Count; i++)
            {
                VisualElement row = MakePlayerRow(OnStarterTapped);
                BindSlotRow(row, sheet[i], i);
                list.Add(row);
            }
        }

        // A starter's row: the slot's role (the shape asks for a right-back, and this is the right-back
        // row), his rating IN THAT SLOT, and what standing there costs him.
        private void BindSlotRow(VisualElement element, SlottedPlayer entry, int index)
        {
            Player player = entry.Player;
            element.userData = index;

            var name = (Label)element[0];
            var role = (Label)element[1];
            var rating = (Label)element[2];
            var penalty = (Label)element[3];

            name.text = player.Name;
            role.text = Abbreviate(entry.Role);

            // Both written unconditionally — the penalty label goes empty and the mark goes off when there
            // is nothing to say. An early return would leave whatever the last bind put there, which is the
            // stale-row bug SetRating already exists to avoid.
            double value = PlayerRatings.ForSlot(player, entry.Role, _session.PositionalFit);
            SetRating(rating, value);
            SetPenalty(penalty, PlayerRatings.ForRole(player), value);
            MarkFit(element, entry.Fit, markNatural: false);
        }

        // The drop, in the manager's own units, beside the number it was taken out of. Written only when
        // there IS a drop: a screen that prints "−0" for everyone teaches the reader to stop looking.
        private static void SetPenalty(Label penalty, double ownRole, double inSlot)
        {
            int cost = Whole(ownRole) - Whole(inSlot);
            penalty.text = cost > 0 ? "−" + cost : string.Empty;
        }

        // EVERY band class is removed before the right one is added — see BindPlayerRow.
        private static void SetRating(Label rating, double value)
        {
            rating.text = Rounded(value);
            foreach (AbilityBand band in (AbilityBand[])System.Enum.GetValues(typeof(AbilityBand)))
            {
                rating.RemoveFromClassList(AbilityBands.ClassOf(band));
            }

            rating.AddToClassList(AbilityBands.ClassOf(AbilityBands.Of(value)));
        }

        // See PitchView.Abbreviate — the same rule, and the same reason it is no longer the enum name.
        private string Abbreviate(PlayerRole role)
        {
            return _text.Get(PlayerRoles.GetShortLabelKey(role));
        }

        // Layout and look both come from the stylesheet; this only says what the parts ARE. Inline styles
        // here would be the palette leaking into C# one property at a time.
        /// <summary>
        /// Who can play in this slot. Tap a position, get the players.
        ///
        /// <para><b>This replaced dragging out of the bench.</b> That list scrolls, so a press on a row
        /// already means "scroll"; making it also mean "pick up" put two gestures in one place, and every
        /// threshold and hold-delay only changed which one felt broken. A sheet has no ambiguity — the
        /// list scrolls, a tap chooses. Dragging survives where it is unambiguous: slot to slot on the
        /// board, which scrolls nothing.</para>
        /// </summary>
        private void OpenPlayerPicker(int slot)
        {
            _pickerSlot = slot;
            PlayerRole wanted = slot >= 0 && slot < _slotRoles.Count ? _slotRoles[slot] : default;
            OpenSheet(Say(UiTextKeys.PickerWho), list =>
            {
                for (int i = 0; i < _benched.Count; i++)
                {
                    int index = i;
                    VisualElement row = MakePlayerRow(_ => ChoosePlayer(index));

                    // Priced FOR THIS SLOT. The sheet used to show each candidate his own-role rating, so
                    // a 78 who would be a 70 here still read as the best man available — the manager was
                    // comparing the wrong numbers, and the game knew it.
                    BindCandidateRow(row, _benched[index], wanted, index);
                    MarkFit(row, PlayerRoles.FitFor(_benched[index].Role, wanted), markNatural: true);
                    list.Add(row);
                }
            });
        }

        // A candidate's row: who he is, what he plays, and what he would be worth HERE — with the drop
        // spelled out, because a lower number the manager cannot account for is just a worse-looking player.
        private void BindCandidateRow(VisualElement element, Player player, PlayerRole slotRole, int index)
        {
            element.userData = index;

            var name = (Label)element[0];
            var role = (Label)element[1];
            var rating = (Label)element[2];
            var penalty = (Label)element[3];

            name.text = player.Name;
            role.text = Abbreviate(player.Role);

            double value = PlayerRatings.ForSlot(player, slotRole, _session.PositionalFit);
            SetRating(rating, value);
            SetPenalty(penalty, PlayerRatings.ForRole(player), value);
        }

        /// <summary>
        /// Where this player plays. Tap a player, get the positions — the mirror of the sheet above.
        ///
        /// <para><b>Symmetry is the point.</b> Tapping a bench player used to push him into the first free
        /// slot, which is a decision the game made and never explained: the manager taps a name and
        /// somebody he did not choose comes off. Both directions are a CHOICE now, and they are the same
        /// gesture answering the same question from either end.</para>
        /// </summary>
        private void OpenSlotPicker(int benchIndex)
        {
            _pickerPlayer = benchIndex;
            LineupOutcome lineup = _session.Lineup();
            Player candidate = benchIndex < _benched.Count ? _benched[benchIndex] : null;
            PlayerRole his = candidate != null ? candidate.Role : default;

            OpenSheet(Say(UiTextKeys.PickerWhere), list =>
            {
                IReadOnlyList<PlayerRole> slots = lineup.Formation.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    int slot = i;
                    Player occupant = slot < lineup.Slots.Count ? lineup.Slots[slot] : null;
                    VisualElement row = MakeSlotRow(slots[slot], occupant, candidate, () => ChooseSlot(slot));
                    MarkFit(row, PlayerRoles.FitFor(his, slots[slot]), markNatural: true);
                    list.Add(row);
                }
            });
        }

        /// <summary>
        /// How well a player fits a slot, read from the SAME rule the match charges him by
        /// (<see cref="PlayerRoles.FitFor"/>). Not decoration: a mark the screen worked out for itself would
        /// be a second opinion about what "out of position" means, and the two would drift the first time
        /// either moved — which is how a screen ends up promising a fit the simulation then penalises.
        ///
        /// <para><paramref name="markNatural"/> is the only difference between the two surfaces that use
        /// this. A sheet of CANDIDATES marks the good fit, because the manager is looking for it; the eleven
        /// he has already picked does not, because highlighting all eleven would wash the list in accent and
        /// say nothing. Both mark the bad fits identically, so one class means one thing everywhere.</para>
        /// </summary>
        private static void MarkFit(VisualElement row, PositionalFit fit, bool markNatural)
        {
            switch (fit)
            {
                case PositionalFit.Natural:
                    row.EnableInClassList("row--natural", markNatural);
                    break;
                case PositionalFit.SameLine:
                    row.AddToClassList("row--samline");
                    break;
                case PositionalFit.Impossible:
                    row.AddToClassList("row--wrong");
                    break;
                default:
                    row.AddToClassList("row--misfit");
                    break;
            }
        }

        private void ChoosePlayer(int index)
        {
            int slot = _pickerSlot;
            CloseSheet();
            if (slot >= 0 && index >= 0 && index < _benched.Count)
            {
                Apply(_session.PlaceInSlot(slot, _benched[index].Id));
            }
        }

        private void ChooseSlot(int slot)
        {
            int index = _pickerPlayer;
            CloseSheet();
            if (slot >= 0 && index >= 0 && index < _benched.Count)
            {
                Apply(_session.PlaceInSlot(slot, _benched[index].Id));
            }
        }

        private void OpenSheet(string title, System.Action<VisualElement> fill)
        {
            _pickerList.Clear();

            var heading = new Label(title);
            heading.AddToClassList("label");
            _pickerList.Add(heading);

            fill(_pickerList);

            _picker.style.display = DisplayStyle.Flex;
            _picker.BringToFront();
        }

        private void CloseSheet()
        {
            _pickerSlot = -1;
            _pickerPlayer = -1;
            _picker.style.display = DisplayStyle.None;
            _pitch.ClearSelection();
        }

        /// <summary>
        /// A row that names a POSITION and who is in it, so choosing where a substitute plays also says who
        /// he would displace. A choice that hid its cost would be the auto-assign problem wearing a sheet.
        ///
        /// <para>The number is what the CANDIDATE would be worth in this slot, not what the occupant is
        /// worth — the manager is choosing where to put one man, so the figure that changes as he reads down
        /// the sheet has to be that man's. The occupant is named beside it because displacing him is the
        /// other half of the price.</para>
        /// </summary>
        private VisualElement MakeSlotRow(PlayerRole role, Player occupant, Player candidate, System.Action onChosen)
        {
            var row = new VisualElement();
            row.AddToClassList("row");

            var roleLabel = new Label(Abbreviate(role));
            roleLabel.AddToClassList("row__role");
            roleLabel.pickingMode = PickingMode.Ignore;
            row.Add(roleLabel);

            var name = new Label(occupant != null ? occupant.Name : "—");
            name.AddToClassList("row__name");
            name.pickingMode = PickingMode.Ignore;
            row.Add(name);

            var rating = new Label();
            rating.AddToClassList("row__rating");
            rating.pickingMode = PickingMode.Ignore;
            row.Add(rating);

            // Child [3], the same shape every row in this screen has — SetPenalty writes into it.
            var penalty = new Label();
            penalty.AddToClassList("row__penalty");
            penalty.pickingMode = PickingMode.Ignore;
            row.Add(penalty);

            if (candidate != null)
            {
                double value = PlayerRatings.ForSlot(candidate, role, _session.PositionalFit);
                SetRating(rating, value);
                SetPenalty(penalty, PlayerRatings.ForRole(candidate), value);
            }

            row.RegisterCallback<ClickEvent>(_ => onChosen());
            return row;
        }

        private VisualElement BuildPicker()
        {
            _picker.AddToClassList("sheet");
            _picker.style.display = DisplayStyle.None;

            // Tapping the dimmed ground behind the sheet dismisses it — the way out has to be as obvious
            // as the way in, and a phone offers no back button on every device.
            var scrim = new VisualElement();
            scrim.AddToClassList("sheet__scrim");
            scrim.RegisterCallback<ClickEvent>(_ => CloseSheet());
            _picker.Add(scrim);

            var panel = new VisualElement();
            panel.AddToClassList("sheet__panel");
            _pickerScroll.AddToClassList("sheet__scroll");
            _pickerScroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            _pickerScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragToScroll(_pickerScroll);
            _pickerScroll.Add(_pickerList);
            panel.Add(_pickerScroll);
            _picker.Add(panel);

            return _picker;
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

            // Always built, usually empty. A penalty added only to the rows that have one would push the
            // rating column left on those rows alone, and a column that moves from line to line is harder
            // to read than the number it is trying to explain.
            var penalty = new Label();
            penalty.AddToClassList("row__penalty");
            penalty.pickingMode = PickingMode.Ignore;
            row.Add(penalty);

            return row;
        }

        // Not static: the role label is COPY now, and the words live on the instance.
        private void BindPlayerRow(VisualElement element, List<Player> source, int index)
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
            role.text = Abbreviate(player.Role);

            // A player with no slot is worth what his own role is worth — there is nothing to charge him
            // for yet. EVERY band class is removed before the right one is added (see SetRating): a reused
            // row carries whatever the last player left on it otherwise, and the bug shows up as one row in
            // a scrolled list wearing somebody else's brightness.
            SetRating(rating, PlayerRatings.ForRole(player));
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
            if (index >= 0 && index < _sheet.Count)
            {
                Apply(_session.ToggleStarter(_sheet[index].Player.Id));
            }
        }

        // Tapping a substitute asks WHERE HE SHOULD PLAY. It used to push him into the first free slot,
        // which is a decision the game made silently: the manager taps a name and somebody he did not
        // choose comes off.
        private void OnBenchTapped(int index)
        {
            if (index >= 0 && index < _benched.Count)
            {
                OpenSlotPicker(index);
            }
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

            // The result is SHOWN rather than summarised into one line. A manager who picks an eleven and
            // is told "Fairwood 2-1 Ashcombe" has been given the score of a match he had no part in; the
            // report is where the decision and the afternoon meet.
            ShowMessage(string.Empty);
            ShowReport(week.Value);
        }

        private void ShowReport(WeekOutcome week)
        {
            // The report brings its own scroller and its own pinned action, so the sheet only lends it a
            // panel to stand in. Rebuilt per week rather than refilled: it is shown once and a week is an
            // immutable record.
            _reportPanel.Clear();
            _reportPanel.Add(new MatchScreen(_session, _text, CloseReport).Build(week));
            _report.style.display = DisplayStyle.Flex;
            _report.BringToFront();
        }

        private void CloseReport()
        {
            _report.style.display = DisplayStyle.None;

            // Redrawn on the way out, not on the way in: the week that was just played changed the squad
            // underneath the board, and the manager returns to the board expecting it to be current.
            ShowPitch(true);
            Draw(_session.Lineup());
        }

        private VisualElement BuildReport()
        {
            _report.AddToClassList("sheet");
            _report.style.display = DisplayStyle.None;

            var scrim = new VisualElement();
            scrim.AddToClassList("sheet__scrim");
            scrim.RegisterCallback<ClickEvent>(_ => CloseReport());
            _report.Add(scrim);

            _reportPanel.AddToClassList("sheet__panel");
            _reportPanel.AddToClassList("sheet__panel--tall");
            _report.Add(_reportPanel);

            return _report;
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

        // A key's words, or the key itself when the table has no row for it — the policy lives in UiWords
        // so the screens cannot drift apart on it. Kept as a name because it reads better at the call site.
        private string Say(string key)
        {
            return _text.Or(key);
        }

        private void ShowMessage(string message)
        {
            _message.text = message;
            _message.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static string Rounded(double value)
        {
            return Whole(value).ToString();
        }

        // The number as the manager reads it. A cost is worked out from the ROUNDED pair rather than
        // rounded afterwards, so "78" next to "70" is always marked "−8" and never "−7" because the
        // unrounded difference happened to be 7.6.
        private static int Whole(double value)
        {
            return Mathf.RoundToInt((float)value);
        }
    }
}
