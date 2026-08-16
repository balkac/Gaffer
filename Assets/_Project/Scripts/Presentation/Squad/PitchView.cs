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

        private int _selected = -1;

        public PitchView(Action<int, int> onSwapRequested)
        {
            _onSwapRequested = onSwapRequested;
            _root.AddToClassList("pitch");
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

            card.RegisterCallback<ClickEvent>(_ => OnSlotTapped(slot));
            _slotCards.Add(card);
            return card;
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
                bool selected = card.userData is int slot && slot == _selected;
                card.EnableInClassList("slot--selected", selected);
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
