using UnityEngine;
using UnityEngine.UIElements;

namespace Gaffer.Presentation
{
    /// <summary>
    /// Makes a <see cref="ScrollView"/> scroll when its CONTENT is dragged, not only its scrollbar.
    ///
    /// <para><b>Why this has to be written by hand.</b> UI Toolkit's ScrollView drag-scrolls on TOUCH
    /// input only — Unity's own answer on the matter is that "the drag scroll is a behavior present in the
    /// ScrollView only with the touch input, and not the mouse inputs", by design, on the reasoning that a
    /// mouse has a wheel. uGUI's ScrollRect does support it, which is why a list built this way feels
    /// broken to anyone who has used a Canvas one: the editor is where the screen gets looked at most, and
    /// there the list simply does not move.</para>
    ///
    /// <para><b>It never steals a tap.</b> Nothing is captured until the finger has travelled past a
    /// threshold; up to that point a press still belongs to whatever row is under it, so tapping a player
    /// works exactly as before. Once it IS a scroll, the pointer is captured and the row's click is
    /// suppressed — which is what stops a flick through a list from selecting somebody on the way past.</para>
    /// </summary>
    public sealed class DragToScroll
    {
        /// <summary>How far a finger travels before a press becomes a scroll rather than a tap.</summary>
        public const float Threshold = 20f;

        private readonly ScrollView _view;

        private Vector2 _origin;
        private Vector2 _offsetAtPress;
        private bool _pressed;
        private bool _scrolling;

        public DragToScroll(ScrollView view)
        {
            _view = view;

            // Registered in the TRICKLE-DOWN phase so the scroller sees a press before the row does, and
            // can decide it is a scroll without the row having already acted on it.
            _view.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            _view.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            _view.RegisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
        }

        private void OnDown(PointerDownEvent evt)
        {
            _pressed = true;
            _scrolling = false;
            _origin = evt.position;
            _offsetAtPress = _view.scrollOffset;
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if (!_pressed)
            {
                return;
            }

            Vector2 travelled = (Vector2)evt.position - _origin;
            if (!_scrolling)
            {
                // Vertical intent only: a sideways drag on a vertical list is not a scroll, and treating it
                // as one would swallow horizontal gestures a screen may want later.
                if (Mathf.Abs(travelled.y) < Threshold || Mathf.Abs(travelled.y) < Mathf.Abs(travelled.x))
                {
                    return;
                }

                _scrolling = true;
                _view.CapturePointer(evt.pointerId);
            }

            _view.scrollOffset = new Vector2(_offsetAtPress.x, _offsetAtPress.y - travelled.y);
            evt.StopPropagation();
        }

        private void OnUp(PointerUpEvent evt)
        {
            if (!_pressed)
            {
                return;
            }

            _pressed = false;
            if (!_scrolling)
            {
                return;
            }

            _scrolling = false;
            if (_view.HasPointerCapture(evt.pointerId))
            {
                _view.ReleasePointer(evt.pointerId);
            }

            // Swallowed so the row under the finger does not read the end of a scroll as a choice.
            evt.StopPropagation();
        }
    }
}
