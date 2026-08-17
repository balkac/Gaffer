using System;
using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Players;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaffer.Presentation.Squad
{
    /// <summary>
    /// The eleven as a shape rather than a list — ART_STYLE's signature screen, the tactics board of a
    /// match you never watch.
    ///
    /// <para><b>It draws a formation, not a fixed pitch.</b> Bands with nobody in them are not drawn at
    /// all, so 4-4-2 shows four rows and 3-5-2 shows six; a fixed grid with gaps would make every shape
    /// look the same, which is the one thing a tactics board must not do.</para>
    ///
    /// <para><b>Selection lives here, the swap does not.</b> A gesture reports "this player, that slot";
    /// what it MEANS — a straight swap, or benching whoever was there — is
    /// <c>RunSession.PlaceInSlot</c>'s rule. Re-deriving it here is how a UI starts disagreeing with the
    /// game.</para>
    ///
    /// <para><b>Dragging lives here and nowhere else</b>, because this is the only surface that does not
    /// scroll. Dragging a player OUT OF the bench was tried and removed: that list scrolls, so a press on
    /// a row already means "scroll", and making it also mean "pick up" put two gestures in one place —
    /// every threshold and hold-delay only changed which of them felt broken. A tap on a slot asks "who
    /// plays here" and the screen answers with a sheet. Drag survives where it is unambiguous and better;
    /// it is not forced on a surface that cannot support it.</para>
    /// </summary>
    public sealed class PitchView
    {
        private const float GhostWidth = 165f;
        private const float GhostLift = 140f;

        private readonly VisualElement _root = new VisualElement();
        private readonly List<VisualElement> _slotCards = new List<VisualElement>();
        private readonly Action<int, int> _onSwapRequested;
        private readonly Action<int> _onSlotChosen;

        private readonly VisualElement _ghost = new VisualElement();
        private readonly Label _ghostName = new Label();
        private VisualElement _overlay;

        private int _selected = -1;
        private int _draggedFrom = -1;
        private bool _dragging;

        // The run's own penalty, handed in with each draw rather than kept as a constant here — a screen
        // that carried its own copy of the number would be a second opinion about what a slot costs.
        private PositionalFitSettings _fit = PositionalFitSettings.Default;

        public PitchView(Action<int, int> onSwapRequested, Action<int> onSlotChosen)
        {
            _onSwapRequested = onSwapRequested;
            _onSlotChosen = onSlotChosen;
            _root.AddToClassList("pitch");

            _ghost.AddToClassList("slot");
            _ghost.AddToClassList("slot--ghost");
            _ghost.pickingMode = PickingMode.Ignore;
            _ghostName.AddToClassList("slot__name");
            _ghostName.pickingMode = PickingMode.Ignore;
            _ghost.Add(_ghostName);
            _ghost.style.display = DisplayStyle.None;
        }

        public VisualElement Root => _root;

        /// <summary>The slot the manager has picked up by tapping, or -1.</summary>
        public int SelectedSlot => _selected;

        /// <summary>
        /// Where the drag ghost draws. Handed in rather than found: the layer must be a descendant of the
        /// element carrying the theme, because a ghost parented to the panel root resolves none of the
        /// tokens and draws with no width and no colour — indistinguishable from not drawing, and exactly
        /// what "I cannot see what I am dragging" was.
        /// </summary>
        public void AttachOverlay(VisualElement overlay)
        {
            _overlay = overlay;
            _overlay.Add(_ghost);
        }

        public void ClearSelection()
        {
            _selected = -1;
            Restyle();
        }

        /// <summary>
        /// Draws the shape and who is in it. Rebuilt whole: eleven cards is nothing, and a diff would be a
        /// second model of the board to keep in step with the first.
        /// </summary>
        /// <param name="fit">What being out of position costs, so a card can show what the man in it is
        /// worth THERE rather than what he would be worth at home. Null takes the calibrated default.</param>
        public void Draw(Formation formation, IReadOnlyList<Player> slots, PositionalFitSettings fit)
        {
            _fit = fit ?? PositionalFitSettings.Default;
            _root.Clear();
            _slotCards.Clear();
            if (formation.Slots == null)
            {
                return;
            }

            var order = new List<int>(formation.Slots.Count);
            for (int i = 0; i < formation.Slots.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((left, right) => CompareSlots(formation, left, right));

            PitchBand? band = null;
            VisualElement row = null;
            for (int i = 0; i < order.Count; i++)
            {
                int slot = order[i];
                PitchBand slotBand = PitchBands.Of(formation.Slots[slot]);
                if (band == null || slotBand != band.Value)
                {
                    row = new VisualElement();
                    row.AddToClassList("pitch__band");
                    _root.Add(row);
                    band = slotBand;
                }

                row.Add(BuildSlotCard(slot, formation.Slots[slot], slot < slots.Count ? slots[slot] : null));
            }

            Restyle();
        }

        // ----- A drag that began somewhere else (a bench row) ---------------------------------------------

        /// <summary>
        /// The board lends its ghost and its hit-testing to a drag begun on the bench. It owns the slots,
        /// so it owns where a drop lands; and one ghost means one thing to keep looking right.
        /// </summary>
        public void BeginDragFromOutside(string label, Vector2 position)
        {
            if (_overlay == null)
            {
                return;
            }

            _ghostName.text = label;
            _ghost.style.display = DisplayStyle.Flex;
            _ghost.BringToFront();
            MoveGhost(position);
        }

        public void MoveDragGhost(Vector2 position)
        {
            MoveGhost(position);
        }

        public void EndDragFromOutside()
        {
            HideGhost();
        }

        /// <summary>Which slot is under a screen point, or -1.</summary>
        public int SlotUnder(Vector2 position)
        {
            VisualElement picked = _root.panel?.Pick(position);
            while (picked != null)
            {
                if (picked.userData is int slot)
                {
                    return slot;
                }

                picked = picked.parent;
            }

            return -1;
        }

        // ----- Cards --------------------------------------------------------------------------------------

        // Deepest band first — the goal at the top, the way a broadcast graphic draws it.
        private static int CompareSlots(Formation formation, int left, int right)
        {
            int byBand = PitchBands.Of(formation.Slots[left]).CompareTo(PitchBands.Of(formation.Slots[right]));
            if (byBand != 0)
            {
                return byBand;
            }

            int byWidth = PitchBands.WidthOrderOf(formation.Slots[left]).CompareTo(PitchBands.WidthOrderOf(formation.Slots[right]));
            return byWidth != 0 ? byWidth : left.CompareTo(right);
        }

        private VisualElement BuildSlotCard(int slot, PlayerRole role, Player player)
        {
            var card = new VisualElement();
            card.AddToClassList("slot");
            card.userData = slot;

            var roleLabel = new Label(Abbreviate(role));
            roleLabel.AddToClassList("slot__role");
            roleLabel.pickingMode = PickingMode.Ignore;
            card.Add(roleLabel);

            string label = player != null ? Surname(player.Name) : "—";
            var name = new Label(label);
            name.AddToClassList("slot__name");
            name.pickingMode = PickingMode.Ignore;
            card.Add(name);

            // WHAT HE IS WORTH HERE, not what he is worth at home.
            //
            // The board showed every man his own-role rating, so a centre-back moved up front kept the 78
            // he earns at the back and the tile said the team had just got stronger. The simulation had
            // already stopped agreeing with that. Charging the number is what makes the brightness ramp
            // tell the truth as well: a misplaced star visibly dims (ART_STYLE §4.1).
            double value = player != null ? PlayerRatings.ForSlot(player, role, _fit) : 0.0;
            var rating = new Label(player != null ? Rounded(value) : string.Empty);
            rating.AddToClassList("slot__rating");
            rating.pickingMode = PickingMode.Ignore;
            if (player != null)
            {
                rating.AddToClassList(AbilityBands.ClassOf(AbilityBands.Of(value)));
            }

            card.Add(rating);

            // And WHY it is lower, because a number that quietly shrinks is worse than no number: the
            // manager would see a weaker side and have nothing to blame.
            //
            // The label is always built and usually empty, so every tile in a band is the same height. One
            // taller card in a row of ten reads as a layout fault, and the manager would be looking at the
            // wobble instead of the team.
            var penalty = new Label();
            penalty.AddToClassList("slot__penalty");
            penalty.pickingMode = PickingMode.Ignore;
            card.Add(penalty);

            PositionalFit slotFit = player != null ? PlayerRoles.FitFor(player.Role, role) : PositionalFit.Natural;
            if (slotFit != PositionalFit.Natural)
            {
                card.AddToClassList(slotFit == PositionalFit.SameLine ? "slot--samline" : "slot--misfit");

                // A cost that rounds to nothing is not worth a mark of its own — the tinted edge already
                // says he is out of position, and "−0" only makes the manager wonder what he missed.
                int cost = Whole(PlayerRatings.ForRole(player)) - Whole(value);
                penalty.text = cost > 0 ? "−" + cost : string.Empty;
            }

            // No hold required: nothing on the board scrolls, so waiting would only feel slow.
            new DragGesture(
                card,
                requiresHold: false,
                onTap: () => OnSlotTapped(slot),
                onLift: position => Lift(slot, label, position),
                onMove: MoveGhost,
                onDrop: Drop,
                onCancel: AbandonDrag);

            _slotCards.Add(card);
            return card;
        }

        // ----- Gesture outcomes ---------------------------------------------------------------------------

        private void Lift(int slot, string label, Vector2 position)
        {
            _draggedFrom = slot;
            _selected = slot;
            _dragging = true;
            _ghostName.text = label;
            _ghost.style.display = DisplayStyle.Flex;
            _ghost.BringToFront();
            MoveGhost(position);
            Restyle();
        }

        private void Drop(Vector2 position)
        {
            int from = _draggedFrom;
            AbandonDrag();

            int target = SlotUnder(position);

            // Released over nothing, or back where it started: abandoned. Committing to the NEAREST slot
            // instead would move players the manager never aimed at.
            if (from >= 0 && target >= 0 && target != from)
            {
                _onSwapRequested(from, target);
            }
        }

        private void AbandonDrag()
        {
            _dragging = false;
            _draggedFrom = -1;
            _selected = -1;
            HideGhost();
            Restyle();
        }

        // Tap to pick up, tap again to put down. Tapping the same slot twice puts it back rather than
        // asking for a swap with itself — a gesture has to be undoable without a second control.
        private void OnSlotTapped(int slot)
        {
            // A tap on the board asks WHO SHOULD PLAY HERE. That is the question a manager actually has,
            // and answering it with a list is what let the bench stop being something to drag out of.
            _selected = slot;
            Restyle();
            _onSlotChosen(slot);
        }

        private void Restyle()
        {
            for (int i = 0; i < _slotCards.Count; i++)
            {
                VisualElement card = _slotCards[i];
                bool held = card.userData is int slot && slot == _selected;

                // Held by a tap reads as SELECTED (accent border, solid); held by a drag reads as SOURCE
                // (faded), because the card itself is under the finger. One state, two pictures, and the
                // difference is what stops a drag looking like a selection that got stuck.
                card.EnableInClassList("slot--selected", held && !_dragging);
                card.EnableInClassList("slot--source", held && _dragging);
            }
        }

        // ----- The ghost ----------------------------------------------------------------------------------

        private void MoveGhost(Vector2 position)
        {
            // Centred on the finger and lifted clear of it, so the card being moved is not under the thumb
            // moving it.
            _ghost.style.left = position.x - (GhostWidth / 2f);
            _ghost.style.top = position.y - GhostLift;
        }

        private void HideGhost()
        {
            _ghost.style.display = DisplayStyle.None;
        }

        // ----- Text ---------------------------------------------------------------------------------------

        // The last word of a name, which is what a board has room for. A single-word name stays whole.
        private static string Surname(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            int space = name.LastIndexOf(' ');
            return space >= 0 && space < name.Length - 1 ? name.Substring(space + 1) : name;
        }

        private static string Abbreviate(PlayerRole role)
        {
            string name = role.ToString();
            return name.Length <= 3 ? name.ToUpperInvariant() : name.Substring(0, 3).ToUpperInvariant();
        }

        private static string Rounded(double value)
        {
            return Whole(value).ToString();
        }

        // The number as the manager reads it. A cost is worked out from the ROUNDED pair rather than
        // rounded afterwards, so "78" next to "70" is always marked "−8" and never "−7" because the
        // unrounded difference was 7.6.
        private static int Whole(double value)
        {
            return Mathf.RoundToInt((float)value);
        }
    }
}
