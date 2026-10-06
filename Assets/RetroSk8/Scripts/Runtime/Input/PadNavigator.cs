using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RetroSk8.Input
{
    /// <summary>Which kind of device the player last used: a game controller, or touch / mouse.</summary>
    public static class InputDeviceTracker
    {
        public static bool PadActive { get; private set; }
        public static event Action<bool> Changed;

        public static bool PadConnected => Gamepad.current != null;

        internal static void Set(bool pad)
        {
            if (pad == PadActive) return;
            PadActive = pad;
            Changed?.Invoke(pad);
        }

        /// <summary>Tests and the editor can force a mode.</summary>
        public static void ForceForTests(bool pad) => Set(pad);
    }

    /// <summary>
    /// Lets a game controller (Xbox, PlayStation, MFi) drive every menu. The D-pad or left stick moves between the
    /// buttons you can actually see (anything covered by an open panel is skipped), A/Cross presses, B/Circle backs out,
    /// left/right adjusts sliders, and a tape-yellow frame shows where you are. Touch input hides the frame again.
    /// During a run only overlays (pause, maps, end screens) are navigable, so JUMP never presses a HUD button.
    /// </summary>
    public sealed class PadNavigator : MonoBehaviour
    {
        private const float RepeatDelay = 0.4f;
        private const float RepeatRate = 0.12f;
        private const float StickThreshold = 0.55f;
        private static readonly string[] Preferred = { "Resume", "Play", "Retry", "Accept", "Ready", "Rematch" };
        private static readonly string[] BackNames = { "Back", "Close", "Resume", "Cancel", "Home", "Exit", "Done", "Go", "Stay" }; // Phase 20: more screens

        private static PadNavigator s_instance;

        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private readonly List<Selectable> _candidates = new List<Selectable>();
        private readonly Vector3[] _corners = new Vector3[4];
        private PointerEventData _ped;
        private EventSystem _pedSystem;
        private RectTransform _frame;
        private RectTransform _frameCanvas;
        private Vector2 _heldDir;
        private float _nextRepeat;
        private float _nextCheck;
        private bool _skateScene;
        private UnityEngine.InputSystem.InputActionReference _savedMove;

        /// <summary>Phase 21: a screen that drives the controller itself (the park editor's map) pauses menu navigation.</summary>
        public static Func<bool> Suspend;
        /// <summary>Phase 21: B goes here first; return true when handled (so it doesn't press Exit/Back).</summary>
        public static Func<bool> BackOverride;

        public static void Ensure()
        {
            if (s_instance != null) return;
            var go = new GameObject("PadNavigator");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<PadNavigator>();
        }

        private void Awake()
        {
            BuildFrame();
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (s_instance == this) s_instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => _nextCheck = 0f;

        private void BuildFrame()
        {
            var canvasGo = new GameObject("PadFocus", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            _frameCanvas = (RectTransform)canvasGo.transform;

            var frameGo = new GameObject("Frame", typeof(RectTransform));
            frameGo.transform.SetParent(canvasGo.transform, false);
            _frame = (RectTransform)frameGo.transform;
            _frame.anchorMin = _frame.anchorMax = Vector2.zero;
            _frame.pivot = new Vector2(0.5f, 0.5f);
            var tape = new Color(0.95f, 0.76f, 0.19f, 1f);
            Edge(new Vector2(0f, 1f), new Vector2(1f, 1f), tape); // top
            Edge(new Vector2(0f, 0f), new Vector2(1f, 0f), tape); // bottom
            Edge(new Vector2(0f, 0f), new Vector2(0f, 1f), tape); // left
            Edge(new Vector2(1f, 0f), new Vector2(1f, 1f), tape); // right
            frameGo.SetActive(false);
        }

        private void Edge(Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject("Edge", typeof(RectTransform));
            go.transform.SetParent(_frame, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            const float t = 6f;
            bool horizontal = min.y == max.y;
            rt.sizeDelta = horizontal ? new Vector2(t * 2f, t) : new Vector2(t, t * 2f);
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private void Update()
        {
            TrackDevice();
            var es = EventSystem.current;
            if (es == null) { _frame.gameObject.SetActive(false); return; }

            if (!InputDeviceTracker.PadActive)
            {
                _frame.gameObject.SetActive(false);
                // Phase 20: give keyboard arrows back to the UI once the controller is put down.
                var kbModule = es.currentInputModule as UnityEngine.InputSystem.UI.InputSystemUIInputModule;
                if (kbModule != null && kbModule.move == null && _savedMove != null) kbModule.move = _savedMove;
                return;
            }

            if (Suspend != null && Suspend())
            {
                _frame.gameObject.SetActive(false);
                if (es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);
                _nextCheck = 0f;
                return;
            }

            if (Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + 0.25f;
                _skateScene = FindAnyObjectByType<PlayerInputRouter>() != null;
                var current = es.currentSelectedGameObject != null ? es.currentSelectedGameObject.GetComponent<Selectable>() : null;
                if (current == null || !Usable(current))
                {
                    var best = Best();
                    es.SetSelectedGameObject(best != null ? best.gameObject : null);
                }
                // This class does the moving (it skips covered buttons); the input module only presses.
                var module = es.currentInputModule as UnityEngine.InputSystem.UI.InputSystemUIInputModule;
                if (module == null) module = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (module != null && module.move != null) { _savedMove = module.move; module.move = null; }
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                Navigate(es, pad);
                if (pad.buttonEast.wasPressedThisFrame && (BackOverride == null || !BackOverride())) Back();
            }
            DrawFrame(es.currentSelectedGameObject);
        }

        private static void TrackDevice()
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                bool used = pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame
                    || pad.buttonNorth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame || pad.dpad.ReadValue().sqrMagnitude > 0.25f
                    || pad.leftStick.ReadValue().sqrMagnitude > StickThreshold * StickThreshold
                    || pad.rightStick.ReadValue().sqrMagnitude > StickThreshold * StickThreshold // Phase 21: editor camera
                    || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame;
                if (used) InputDeviceTracker.Set(true);
            }
            else if (InputDeviceTracker.PadActive) InputDeviceTracker.Set(false); // controller switched off

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) InputDeviceTracker.Set(false);
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) InputDeviceTracker.Set(false);
        }

        // ---------------------------------------------------------------- moving between buttons

        private void Navigate(EventSystem es, Gamepad pad)
        {
            Vector2 dir = pad.dpad.ReadValue();
            Vector2 stick = pad.leftStick.ReadValue();
            if (dir.sqrMagnitude < 0.25f && stick.sqrMagnitude > StickThreshold * StickThreshold) dir = stick;
            if (dir.sqrMagnitude < 0.25f)
            {
                _heldDir = Vector2.zero;
                return;
            }
            dir = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? new Vector2(Mathf.Sign(dir.x), 0f) : new Vector2(0f, Mathf.Sign(dir.y));
            float now = Time.unscaledTime;
            if (dir == _heldDir && now < _nextRepeat) return;
            _nextRepeat = now + (dir == _heldDir ? RepeatRate : RepeatDelay);
            _heldDir = dir;

            var current = es.currentSelectedGameObject != null ? es.currentSelectedGameObject.GetComponent<Selectable>() : null;
            if (current is Slider slider && dir.x != 0f && slider.interactable)
            {
                float step = (slider.maxValue - slider.minValue) * 0.05f;
                slider.value = Mathf.Clamp(slider.value + step * dir.x, slider.minValue, slider.maxValue);
                return;
            }
            var next = current != null && Usable(current) ? Neighbour(current, dir) : Best();
            if (next != null)
            {
                es.SetSelectedGameObject(next.gameObject);
                RetroSk8.Audio.AudioManager.Instance?.PlaySfx(RetroSk8.Audio.SfxId.UiClick, 0.4f, 1.3f);
            }
        }

        /// <summary>The nearest usable button in a direction: favours ones straight ahead over ones off to the side.</summary>
        private Selectable Neighbour(Selectable from, Vector2 dir)
        {
            Vector2 origin = Centre(from);
            Selectable best = null;
            float bestScore = float.MaxValue;
            Gather();
            foreach (var s in _candidates)
            {
                if (s == from) continue;
                Vector2 d = Centre(s) - origin;
                float along = Vector2.Dot(d, dir);
                if (along <= 1f) continue;
                float side = Mathf.Abs(dir.x != 0f ? d.y : d.x);
                float score = along + side * 2.5f;
                if (score < bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        /// <summary>The button to start on: a preferred one (PLAY, RESUME...) if visible, else the top-left-most.</summary>
        private Selectable Best()
        {
            Gather();
            foreach (var name in Preferred)
                foreach (var s in _candidates)
                    if (s.gameObject.name == name) return s;
            Selectable best = null;
            float bestScore = float.MaxValue;
            foreach (var s in _candidates)
            {
                if (s is InputField || s is Slider) continue; // don't open the keyboard or grab a slider by surprise
                Vector2 c = Centre(s);
                float score = -c.y * 2f + c.x;
                if (score < bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        private void Back()
        {
            Gather();
            foreach (var name in BackNames)
                foreach (var s in _candidates)
                    if (s.gameObject.name == name && s is Button b)
                    {
                        b.onClick.Invoke();
                        _nextCheck = 0f;
                        return;
                    }
        }

        private void Gather()
        {
            _candidates.Clear();
            var all = Selectable.allSelectablesArray;
            foreach (var s in all)
                if (s != null && Usable(s)) _candidates.Add(s);
        }

        /// <summary>Active, interactable, on screen, not covered by another panel, and (during a run) on an overlay.</summary>
        private bool Usable(Selectable s)
        {
            if (s == null || !s.isActiveAndEnabled || !s.IsInteractable()) return false;
            var canvas = s.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled) return false;
            canvas = canvas.rootCanvas;
            if (canvas == null || canvas.transform == _frameCanvas) return false;
            if (_skateScene && canvas.sortingOrder < 5) return false; // HUD and touch controls: never during play
            var group = s.GetComponentInParent<CanvasGroup>();
            if (group != null && (!group.interactable || !group.blocksRaycasts || group.alpha <= 0.01f)) return false;
            return TopmostAt(s);
        }

        private bool TopmostAt(Selectable s)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            Vector2 c = Centre(s);
            if (c.x < 0f || c.y < 0f || c.x > Screen.width || c.y > Screen.height) return false;
            if (_ped == null || _pedSystem != es) { _ped = new PointerEventData(es); _pedSystem = es; }
            _ped.position = c;
            _hits.Clear();
            es.RaycastAll(_ped, _hits);
            if (_hits.Count == 0) return false;
            var top = _hits[0].gameObject;
            return top == s.gameObject || top.transform.IsChildOf(s.transform);
        }

        private Vector2 Centre(Selectable s)
        {
            var rt = s.transform as RectTransform;
            if (rt == null) return new Vector2(-1f, -1f);
            rt.GetWorldCorners(_corners); // overlay canvases: world space is screen pixels
            return (_corners[0] + _corners[2]) * 0.5f;
        }

        private void DrawFrame(GameObject selected)
        {
            var rt = selected != null ? selected.transform as RectTransform : null;
            if (rt == null || !selected.activeInHierarchy)
            {
                _frame.gameObject.SetActive(false);
                return;
            }
            rt.GetWorldCorners(_corners);
            float scale = _frameCanvas.lossyScale.x > 0f ? _frameCanvas.lossyScale.x : 1f;
            Vector2 min = _corners[0], max = _corners[2];
            float pulse = 8f + 4f * Mathf.Sin(Time.unscaledTime * 6f);
            _frame.gameObject.SetActive(true);
            _frame.anchoredPosition = (min + max) * 0.5f / scale;
            _frame.sizeDelta = (max - min) / scale + new Vector2(pulse, pulse) * 2f;
        }
    }
}

namespace RetroSk8.Input
{
    /// <summary>Fades the on-screen touch controls out while a game controller is in use, and back in on the first touch.</summary>
    public sealed class TouchAutoHide : MonoBehaviour
    {
        private CanvasGroup _group;
        private TouchInputSource _touch;

        public void Init(TouchInputSource touch)
        {
            _touch = touch;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            InputDeviceTracker.Changed += Apply;
            Apply(InputDeviceTracker.PadActive);
        }

        private void OnDestroy() => InputDeviceTracker.Changed -= Apply;

        private void Apply(bool pad)
        {
            if (_group == null) return;
            _group.alpha = pad ? 0f : 1f;
            _group.blocksRaycasts = !pad;
            _group.interactable = !pad;
            if (pad) _touch?.ReleaseAll();
        }
    }
}
