using RetroSk8.Core;
using RetroSk8.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// One-thumb-per-side touch layout. Left half: floating steer stick. Right half: swipe pad with JUMP (hold/release)
    /// and ACTION buttons on top. A swipe that starts on JUMP also counts, so "hold, release, flick" is one motion.
    /// </summary>
    public sealed class TouchControlsView : MonoBehaviour
    {
        public const float SwipeDistance = 70f; // reference pixels

        public const float BaseStickRadius = 120f;

        public void Build(RectTransform safe, TouchInputSource touch, Canvas canvas)
        {
            // Positions, size and stick sensitivity come from the player's layout (Settings → Controls).
            var layout = RetroSk8.Save.SaveManager.Data.settings.touchLayout ?? TouchLayout.Default();
            float k = layout.scale;
            bool stickRight = layout.StickOnRight;

            // Steer zone (45% of the width, on the stick's side)
            var steerZone = UIFactory.Panel("SteerZone", safe, new Color(0, 0, 0, 0), true);
            var sz = steerZone.rectTransform;
            float z0 = stickRight ? 0.55f : 0f;
            sz.anchorMin = new Vector2(z0, 0f);
            sz.anchorMax = new Vector2(z0 + 0.45f, 0.75f);
            sz.offsetMin = sz.offsetMax = Vector2.zero;
            var stickBase = UIFactory.Panel("StickBase", sz, new Color(1f, 1f, 1f, 0.12f));
            stickBase.sprite = UIFactory.Circle;
            var stickAnchor = new Vector2(Mathf.Clamp01((layout.stickX - z0) / 0.45f), Mathf.Clamp01(layout.stickY / 0.75f));
            UIFactory.Place(stickBase.rectTransform, stickAnchor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f) * k);
            var knob = UIFactory.Panel("StickKnob", stickBase.transform, new Color(0.95f, 0.91f, 0.82f, 0.55f));
            knob.sprite = UIFactory.Circle;
            UIFactory.Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 130f) * k);
            var stick = steerZone.gameObject.AddComponent<VirtualStick>();
            stick.Init(touch, canvas, stickBase.rectTransform, knob.rectTransform, layout.StickRadius(BaseStickRadius) * k);

            // Swipe pad (the other 55%)
            var pad = UIFactory.Panel("SwipePad", safe, new Color(0, 0, 0, 0), true);
            var pr = pad.rectTransform;
            pr.anchorMin = new Vector2(stickRight ? 0f : 0.45f, 0f);
            pr.anchorMax = new Vector2(stickRight ? 0.55f : 1f, 0.82f);
            pr.offsetMin = pr.offsetMax = Vector2.zero;
            pad.gameObject.AddComponent<SwipePad>().Init(touch, canvas);
            var hint = UIFactory.Label("SwipeHint", pr, "SWIPE FOR TRICKS", 26, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleCenter, false);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(600f, 40f));

            // Jump
            var jump = UIFactory.Panel("Jump", safe, new Color(0.95f, 0.76f, 0.19f, 0.85f), true);
            jump.sprite = UIFactory.Circle;
            UIFactory.Place(jump.rectTransform, new Vector2(layout.jumpX, layout.jumpY), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 260f) * k);
            var jl = UIFactory.Label("Label", jump.transform, "JUMP", 44, Theme.Ink, TextAnchor.MiddleCenter, false);
            UIFactory.Stretch(jl.rectTransform);
            jump.gameObject.AddComponent<JumpButton>().Init(touch, canvas);

            // Action (grind / manual)
            var action = UIFactory.Panel("Action", safe, new Color(0.12f, 0.78f, 0.71f, 0.85f), true);
            action.sprite = UIFactory.Circle;
            UIFactory.Place(action.rectTransform, new Vector2(layout.actionX, layout.actionY), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 180f) * k);
            var al = UIFactory.Label("Label", action.transform, "GRIND\nMANUAL", 26, Theme.Ink, TextAnchor.MiddleCenter, false);
            UIFactory.Stretch(al.rectTransform);
            action.gameObject.AddComponent<ActionButton>().Init(touch);
        }

        internal static float ToCanvas(Canvas canvas, float screenPixels) => screenPixels / Mathf.Max(0.01f, canvas.scaleFactor);
    }

    /// <summary>Floating stick: appears where the thumb lands, re-centres on release.</summary>
    public sealed class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private float _radius = TouchControlsView.BaseStickRadius;
        private TouchInputSource _touch;
        private Canvas _canvas;
        private RectTransform _base;
        private RectTransform _knob;
        private Vector2 _restPos;
        private int _pointer = int.MinValue;

        public void Init(TouchInputSource touch, Canvas canvas, RectTransform stickBase, RectTransform knob, float radius)
        {
            _radius = Mathf.Max(30f, radius);
            _touch = touch;
            _canvas = canvas;
            _base = stickBase;
            _knob = knob;
            _restPos = stickBase.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointer != int.MinValue) return;
            _pointer = e.pointerId;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out Vector2 local))
            {
                // The base is anchored at its resting spot; move it to where the thumb landed.
                var zone = (RectTransform)transform;
                Vector2 anchorPoint = zone.rect.min + Vector2.Scale(zone.rect.size, _base.anchorMin);
                _base.anchoredPosition = local - anchorPoint;
            }
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointer) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_base, e.position, e.pressEventCamera, out Vector2 local)) return;
            Vector2 v = Vector2.ClampMagnitude(local, _radius);
            _knob.anchoredPosition = v;
            _touch.SetSteer(v / _radius);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointer) return;
            _pointer = int.MinValue;
            _knob.anchoredPosition = Vector2.zero;
            _base.anchoredPosition = _restPos;
            _touch.SetSteer(Vector2.zero);
        }

        private void OnDisable() => _touch?.ReleaseAll();
    }

    /// <summary>Shared swipe recognition: fires once per touch as soon as the drag passes the threshold.</summary>
    public abstract class SwipeRecognizer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        protected TouchInputSource Touch;
        protected Canvas Canvas;
        private Vector2 _start;
        private bool _swiped;
        private int _pointer = int.MinValue;

        public virtual void OnPointerDown(PointerEventData e)
        {
            if (_pointer != int.MinValue) return;
            _pointer = e.pointerId;
            _start = e.position;
            _swiped = false;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointer || _swiped) return;
            Vector2 d = e.position - _start;
            float dx = TouchControlsView.ToCanvas(Canvas, d.x);
            float dy = TouchControlsView.ToCanvas(Canvas, d.y);
            var dir = GestureRules.ClassifySwipe(dx, dy, TouchControlsView.SwipeDistance);
            if (dir == SwipeDirection.None) return;
            _swiped = true;
            Touch.PushSwipe(dir);
        }

        public virtual void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointer) return;
            _pointer = int.MinValue;
        }
    }

    public sealed class SwipePad : SwipeRecognizer
    {
        public void Init(TouchInputSource touch, Canvas canvas) { Touch = touch; Canvas = canvas; }
    }

    public sealed class JumpButton : SwipeRecognizer
    {
        public void Init(TouchInputSource touch, Canvas canvas) { Touch = touch; Canvas = canvas; }

        public override void OnPointerDown(PointerEventData e)
        {
            base.OnPointerDown(e);
            Touch.SetJumpHeld(true);
            transform.localScale = Vector3.one * 0.92f;
        }

        public override void OnPointerUp(PointerEventData e)
        {
            base.OnPointerUp(e);
            Touch.SetJumpHeld(false);
            transform.localScale = Vector3.one;
        }
    }

    public sealed class ActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private TouchInputSource _touch;
        public void Init(TouchInputSource touch) => _touch = touch;

        public void OnPointerDown(PointerEventData e)
        {
            _touch.PressAction();
            transform.localScale = Vector3.one * 0.9f;
        }

        public void OnPointerUp(PointerEventData e) => transform.localScale = Vector3.one;
    }
}
