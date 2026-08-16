using System;
using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Players;
using UnityEngine.UIElements;

namespace Gaffer.Presentation.Squad
{
    /// <summary>
    /// The eleven as a shape rather than a list — ART_STYLE's signature screen, the tactics board of a
    /// match you never watch.
    ///
    /// <para><b>It draws a formation, not a fixed pitch.</b> Bands with nobody in them are not drawn at
    /// all, so 4-4-2 shows four rows and 3-5-2 shows six; a fixed six-row grid with gaps would make every
    /// shape look the same, which is the one thing a tactics board must not do.</para>
    ///
    /// <para><b>Selection lives here, the swap does not.</b> Tapping a slot selects it; tapping a second
    /// one asks the caller to put the first player in the second slot. What that MEANS — a straight swap,
    /// or benching whoever was there — is <c>RunSession.PlaceInSlot</c>'s rule, and this view does not
    /// know it. Re-implementing it here is how a UI starts disagreeing with the game.</para>
    /// </summary>
    public sealed class PitchView
    {
        private readonly VisualElement _root = new VisualElement();
        private readonly List<VisualElement> _slotCards = new List<VisualElement>();
        private readonly Action<int, int> _onSwapRequested;

        // A press this far from where it started is a drag rather than a tap. Generous, because it is in
        // reference pixels on a 1080-wide board and because a thumb is not a mouse.
        private const float DragThreshold = 24f;
        private const float GhostWidth = 165f;
        private const float GhostLift = 140f;

        private int _selected = -1;
        private int _pressedSlot = -1;
        private bool _dragging;
        private UnityEngine.Vector2 _pressOrigin;

        // The card that follows the finger. One instance, moved and hidden rather than built per drag,
        // and parented to the panel root so it draws OVER the board instead of being clipped by the band
        // it started in.
        private readonly VisualElement _ghost = new VisualElement();
        private readonly Label _ghostName = new Label();

        public PitchView(Action<int, int> onSwapRequested)
        {
            _onSwapRequested = onSwapRequested;
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

        /// <summary>The slot the manager has picked up, or -1. A bench tap reads this to know where to go.</summary>
        public int SelectedSlot => _selected;

        public void ClearSelection()
        {
            _selected = -1;
            Restyle();
        }

        /// <summary>Draws the shape and who is in it. Rebuilt wholesale: eleven cards is nothing, and a
        /// diff would be a second model of the board to keep in step with the first.</summary>
        public void Draw(Formation formation, IReadOnlyList<Player> slots)
        {
            _root.Clear();
            _slotCards.Clear();

            if (formation.Slots == null)
            {
                return;
            }

            // Ordered by band, then across the width, so a band reads right-to-left the way it lines up.
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

        // Deepest band first — the goal is at the top of the board, the way a broadcast graphic draws it.
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

            var name = new Label(player != null ? Surname(player.Name) : "—");
            name.AddToClassList("slot__name");
            name.pickingMode = PickingMode.Ignore;
            card.Add(name);

            var rating = new Label(player != null ? Rounded(PlayerRatings.ForRole(player)) : string.Empty);
            rating.AddToClassList("slot__rating");
            rating.pickingMode = PickingMode.Ignore;
            if (player != null)
            {
                rating.AddToClassList(AbilityBands.ClassOf(AbilityBands.Of(PlayerRatings.ForRole(player))));
            }

            card.Add(rating);

            RegisterGestures(card, slot);
            _slotCards.Add(card);
            return card;
        }

        /// <summary>
        /// Both gestures on one card, because a manager will try both and neither should be the only way.
        ///
        /// <para><b>Drag</b> is the one that feels like a tactics board: press, move, release over another
        /// slot. It commits on release over a target and is cancelled by releasing anywhere else, so a drag
        /// begun by accident costs nothing.</para>
        ///
        /// <para><b>Tap</b> survives alongside it, and not as a fallback: on a phone held one-handed a drag
        /// across the board is genuinely awkward, and two taps are not. The two share one selection, so
        /// picking up by tap and putting down by drag works without either knowing about the other.</para>
        ///
        /// <para>A press only becomes a DRAG after the finger has travelled far enough
        /// (<see cref="DragThreshold"/>). Without that, every tap is a zero-length drag and the tap gesture
        /// never fires at all — fingers move a few pixels on the way up.</para>
        /// </summary>
        private void RegisterGestures(VisualElement card, int slot)
        {
            card.RegisterCallback<PointerDownEvent>(evt =>
            {
                _pressedSlot = slot;
                _pressOrigin = evt.position;
                _dragging = false;
                card.CapturePointer(evt.pointerId);

                // THE line that makes dragging work at all inside a scrolling page.
                //
                // Capture alone is not enough: the event still bubbles to the ScrollView, which treats the
                // same press-and-move as a scroll, takes the capture back, and the card's move handler then
                // sees HasPointerCapture return false and never starts a drag. From the outside that looks
                // exactly like drag being unimplemented — which is how it was reported, twice.
                //
                // The cost is deliberate and small: a drag begun ON a slot no longer scrolls the page.
                // There is room either side of the board to scroll from, and a board whose players cannot
                // be picked up is worse than one you cannot flick.
                evt.StopPropagation();
            });

            // The scroller can still take the capture in cases this does not foresee. Rather than leave a
            // half-finished drag on the board, treat losing it as the gesture being abandoned.
            card.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (_dragging)
                {
                    _dragging = false;
                    HideGhost();
                    _selected = -1;
                    Restyle();
                }

                _pressedSlot = -1;
            });

            card.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_pressedSlot < 0 || !card.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                if (!_dragging && Distance(evt.position, _pressOrigin) >= DragThreshold)
                {
                    _dragging = true;
                    _selected = _pressedSlot;
                    Restyle();
                    ShowGhost(card);
                }

                if (_dragging)
                {
                    MoveGhost(evt.position);
                    evt.StopPropagation();
                }
            });

            card.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                if (card.HasPointerCapture(evt.pointerId))
                {
                    card.ReleasePointer(evt.pointerId);
                }

                int from = _pressedSlot;
                _pressedSlot = -1;

                if (!_dragging)
                {
                    OnSlotTapped(slot);
                    return;
                }

                _dragging = false;
                HideGhost();
                int target = SlotUnderInternal(evt.position);
                _selected = -1;
                Restyle();

                // Released over nothing, or back where it started: the drag is simply abandoned. A gesture
                // that committed to the nearest slot instead would move players the manager never aimed at.
                if (target >= 0 && target != from)
                {
                    _onSwapRequested(from, target);
                }
            });
        }

        // The ghost is what makes a drag READABLE. Without something under the finger a drag is
        // indistinguishable from a tap that did nothing — which is exactly how it was reported.
        private void ShowGhost(VisualElement card)
        {
            VisualElement layer = _root.panel?.visualTree;
            if (layer == null)
            {
                return;
            }

            if (_ghost.parent != layer)
            {
                layer.Add(_ghost);
            }

            _ghostName.text = NameIn(card);
            _ghost.style.display = DisplayStyle.Flex;
            _ghost.BringToFront();
        }

        private void MoveGhost(UnityEngine.Vector2 position)
        {
            // Centred on the finger and lifted clear of it, so the card being moved is not under the thumb
            // that is moving it.
            _ghost.style.left = position.x - (GhostWidth / 2f);
            _ghost.style.top = position.y - GhostLift;
        }

        private void HideGhost()
        {
            _ghost.style.display = DisplayStyle.None;
        }

        private static string NameIn(VisualElement card)
        {
            for (int i = 0; i < card.childCount; i++)
            {
                if (card[i] is Label label && label.ClassListContains("slot__name"))
                {
                    return label.text;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Starts a drag that began somewhere else — a bench row. The board owns the ghost and the
        /// hit-testing because it owns the slots; the bench only reports the gesture. Two ghosts, one per
        /// source, would be two things to keep looking the same.
        /// </summary>
        public void BeginDragFromOutside(string label)
        {
            VisualElement layer = _root.panel?.visualTree;
            if (layer == null)
            {
                return;
            }

            if (_ghost.parent != layer)
            {
                layer.Add(_ghost);
            }

            _ghostName.text = label;
            _ghost.style.display = DisplayStyle.Flex;
            _ghost.BringToFront();
        }

        public void MoveDragGhost(UnityEngine.Vector2 position)
        {
            MoveGhost(position);
        }

        public void EndDragFromOutside()
        {
            HideGhost();
        }

        /// <summary>Which slot is under a screen point, or -1. Public so a bench drag can ask where it
        /// was released.</summary>
        public int SlotUnder(UnityEngine.Vector2 position)
        {
            return SlotUnderInternal(position);
        }

        // Which slot is under a screen point, or -1. The pick lands on whichever label happens to be
        // there, so it walks up until it finds the card that carries a slot index.
        private int SlotUnderInternal(UnityEngine.Vector2 position)
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

        private static float Distance(UnityEngine.Vector2 one, UnityEngine.Vector2 other)
        {
            return (one - other).magnitude;
        }

        // Tap to pick up, tap again to put down. Tapping the same slot twice puts it back rather than
        // asking for a swap with itself — the gesture has to be undoable without a second control.
        private void OnSlotTapped(int slot)
        {
            if (_selected < 0)
            {
                _selected = slot;
                Restyle();
                return;
            }

            if (_selected == slot)
            {
                ClearSelection();
                return;
            }

            int from = _selected;
            _selected = -1;
            _onSwapRequested(from, slot);
        }

        private void Restyle()
        {
            for (int i = 0; i < _slotCards.Count; i++)
            {
                VisualElement card = _slotCards[i];
                bool held = card.userData is int slot && slot == _selected;

                // Held by a tap reads as SELECTED (accent border, still solid); held by a drag reads as
                // SOURCE (faded), because the card itself is under the finger. Same state, two pictures,
                // and the difference is what stops a drag looking like a selection that got stuck.
                card.EnableInClassList("slot--selected", held && !_dragging);
                card.EnableInClassList("slot--source", held && _dragging);
            }
        }

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
            return UnityEngine.Mathf.RoundToInt((float)value).ToString();
        }
    }
}
