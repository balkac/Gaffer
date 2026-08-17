using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaffer.Presentation
{
    /// <summary>
    /// Press, hold, drag, drop — the one place that decides what a finger meant.
    ///
    /// <para><b>Why it exists.</b> The same logic was written twice, once for the tactics board and once
    /// for the bench, and the two drifted immediately: one could be dragged out of a scrolling list and
    /// the other could not, and neither agreed about when a press became a drag. A gesture is a rule, and
    /// a rule with two implementations has none (ARCHITECTURE §8a).</para>
    ///
    /// <para><b>The hold exists for surfaces that scroll, and the squad screen decided not to have any.</b>
    /// A list you can both swipe and rearrange has one gesture meaning two things in one place; the hold
    /// is the usual compromise, and it was tried on the bench. It works, but it is still a compromise —
    /// the manager has to know the trick before the list behaves. The squad screen dropped it for a sheet
    /// ("who plays here"), which needs no trick at all. The hold stays supported because a list that must
    /// be reordered IN PLACE will want it, and that is a different problem from choosing a player.</para>
    /// </summary>
    public sealed class DragGesture
    {
        /// <summary>How long a finger must stay put before a press in a scrolling list becomes a lift.</summary>
        public const long HoldMilliseconds = 220;

        /// <summary>How far a finger may travel and still be a tap. A thumb is not a mouse.</summary>
        public const float MoveThreshold = 24f;

        private readonly VisualElement _element;
        private readonly bool _requiresHold;
        private readonly Action _onTap;
        private readonly Action<Vector2> _onLift;
        private readonly Action<Vector2> _onMove;
        private readonly Action<Vector2> _onDrop;
        private readonly Action _onCancel;

        private IVisualElementScheduledItem _hold;
        private Vector2 _origin;
        private bool _pressed;
        private bool _lifted;

        /// <param name="requiresHold">True inside anything that scrolls, false where nothing does.</param>
        public DragGesture(
            VisualElement element,
            bool requiresHold,
            Action onTap,
            Action<Vector2> onLift,
            Action<Vector2> onMove,
            Action<Vector2> onDrop,
            Action onCancel)
        {
            _element = element;
            _requiresHold = requiresHold;
            _onTap = onTap;
            _onLift = onLift;
            _onMove = onMove;
            _onDrop = onDrop;
            _onCancel = onCancel;

            _element.RegisterCallback<PointerDownEvent>(OnDown);
            _element.RegisterCallback<PointerMoveEvent>(OnMove);
            _element.RegisterCallback<PointerUpEvent>(OnUp);
            _element.RegisterCallback<PointerCaptureOutEvent>(_ => Cancel());
        }

        private void OnDown(PointerDownEvent evt)
        {
            _pressed = true;
            _lifted = false;
            _origin = evt.position;

            if (_requiresHold)
            {
                // The pointer is NOT captured and the event is NOT stopped yet: until the hold completes
                // this press still belongs to the scroller, which is what lets a quick swipe scroll.
                _hold = _element.schedule.Execute(() => Lift(_origin)).StartingIn(HoldMilliseconds);
                return;
            }

            _element.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if (!_pressed)
            {
                return;
            }

            Vector2 position = evt.position;
            if (_lifted)
            {
                _onMove(position);
                evt.StopPropagation();
                return;
            }

            if ((position - _origin).magnitude < MoveThreshold)
            {
                return;
            }

            if (_requiresHold)
            {
                // Moved before the hold finished, so this was a swipe. Stand down and leave the scroller
                // to it — the alternative is a list that fights every attempt to scroll it.
                Cancel();
                return;
            }

            Lift(position);
            _onMove(position);
            evt.StopPropagation();
        }

        private void OnUp(PointerUpEvent evt)
        {
            if (!_pressed)
            {
                return;
            }

            bool lifted = _lifted;
            Vector2 position = evt.position;
            Release(evt.pointerId);

            if (lifted)
            {
                evt.StopPropagation();
                _onDrop(position);
                return;
            }

            // A press that never became a lift is a tap, whether or not a hold was required.
            _onTap?.Invoke();
        }

        private void Lift(Vector2 position)
        {
            if (_lifted || !_pressed)
            {
                return;
            }

            _hold?.Pause();
            _hold = null;
            _lifted = true;

            // Captured only NOW. Taking the pointer at press time is what made a draggable row impossible
            // to scroll: the scroller never saw the move at all.
            _element.CaptureMouse();
            _onLift(position);
        }

        private void Cancel()
        {
            bool lifted = _lifted;
            Release(PointerId.mousePointerId);
            if (lifted)
            {
                _onCancel();
            }
        }

        private void Release(int pointerId)
        {
            _hold?.Pause();
            _hold = null;
            _pressed = false;
            _lifted = false;

            if (_element.HasPointerCapture(pointerId))
            {
                _element.ReleasePointer(pointerId);
            }
            else if (_element.HasMouseCapture())
            {
                _element.ReleaseMouse();
            }
        }
    }
}
