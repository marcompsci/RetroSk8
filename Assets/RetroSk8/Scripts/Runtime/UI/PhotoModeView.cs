using System;
using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Photo mode (from the pause menu): the game stays frozen, the HUD hides, and you orbit a free camera
    /// around the skater with a drag; buttons zoom, raise/lower and tilt. Take the shot with the phone's own
    /// screenshot. EXIT puts the gameplay camera back exactly as it was.
    /// </summary>
    public sealed class PhotoModeView : MonoBehaviour
    {
        private PlayerController _player;
        private System.Collections.Generic.IList<GameObject> _hide;
        private readonly System.Collections.Generic.List<GameObject> _hidden = new System.Collections.Generic.List<GameObject>();
        private Action _exit;
        private Camera _cam;
        private Behaviour[] _disabled = new Behaviour[0];
        private Vector3 _savedPos;
        private Quaternion _savedRot;
        private float _savedFov;

        private float _yaw, _pitch = 14f, _distance = 5f, _lift = 1f, _fov = 55f;
        private Vector2? _lastPointer;
        private GameObject _controls;

        public bool Active { get; private set; }

        public const float MinDistance = 1.5f, MaxDistance = 16f;

        // Phase 25: photo mode 2.0.
        public PhotoFilter Filter { get; private set; }
        public PhotoFrame FrameStyle { get; private set; }
        public PhotoStickers StickerStyle { get; private set; }
        private Text _filterLabel, _frameLabel, _stickerLabel, _toast;
        private RawImage _preview;
        private Texture2D _lastPhoto;
        private float _toastUntil;
        private bool _capturing;

        public void Build(RectTransform root, PlayerController player, System.Collections.Generic.IList<GameObject> hideWhileActive, Action exit)
        {
            _player = player;
            _hide = hideWhileActive;
            _exit = exit;

            _controls = UIFactory.Rect("PhotoControls", root).gameObject;
            UIFactory.Stretch((RectTransform)_controls.transform);
            var hint = UIFactory.Label("Hint", _controls.transform, "PHOTO MODE · DRAG TO ORBIT · PICK A LOOK, THEN SNAP", 26, Theme.Cream, TextAnchor.UpperCenter);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1400f, 40f));

            var col = UIFactory.Rect("Buttons", _controls.transform);
            UIFactory.Place(col, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(150f, 800f));
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = layout.childControlWidth = false;
            UIFactory.MakeButton("ZoomIn", col, "+", new Vector2(130f, 100f), Theme.Cream, () => Zoom(-1f), 60);
            UIFactory.MakeButton("ZoomOut", col, "-", new Vector2(130f, 100f), Theme.Cream, () => Zoom(1f), 60);
            UIFactory.MakeButton("Up", col, "UP", new Vector2(130f, 90f), Theme.Cream, () => _lift = Mathf.Clamp(_lift + 0.4f, 0.2f, 4f), 34);
            UIFactory.MakeButton("Down", col, "DOWN", new Vector2(130f, 90f), Theme.Cream, () => _lift = Mathf.Clamp(_lift - 0.4f, 0.2f, 4f), 30);
            UIFactory.MakeButton("Wide", col, "WIDE", new Vector2(130f, 90f), Theme.Teal, () => _fov = Mathf.Clamp(_fov + 8f, 30f, 95f), 30);
            UIFactory.MakeButton("Tight", col, "TIGHT", new Vector2(130f, 90f), Theme.Teal, () => _fov = Mathf.Clamp(_fov - 8f, 30f, 95f), 30);

            var exitButton = UIFactory.MakeButton("Exit", _controls.transform, "EXIT", new Vector2(240f, 90f), Theme.Tape, Exit, 40);
            UIFactory.Place((RectTransform)exitButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(240f, 90f));
            var hide = UIFactory.MakeButton("HideUi", _controls.transform, "HIDE BUTTONS", new Vector2(300f, 70f), Theme.Cream, HideButtonsBriefly, 28);
            UIFactory.Place((RectTransform)hide.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(300f, 70f));

            // Phase 25: look pickers and SNAP along the bottom.
            var looks = UIFactory.Rect("Looks", _controls.transform);
            UIFactory.Place(looks, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(120f, 30f), new Vector2(1300f, 90f));
            var lh = looks.gameObject.AddComponent<HorizontalLayoutGroup>();
            lh.spacing = 14f;
            lh.childAlignment = TextAnchor.MiddleCenter;
            lh.childControlHeight = lh.childControlWidth = false;
            _filterLabel = UIFactory.MakeButton("Filter", looks, "", new Vector2(280f, 84f), Theme.Teal, () => { Filter = PhotoFx.Next(Filter); RefreshLooks(); }, 28).GetComponentInChildren<Text>();
            _frameLabel = UIFactory.MakeButton("Frame", looks, "", new Vector2(300f, 84f), Theme.Teal, () => { FrameStyle = PhotoFx.Next(FrameStyle); RefreshLooks(); }, 28).GetComponentInChildren<Text>();
            _stickerLabel = UIFactory.MakeButton("Stickers", looks, "", new Vector2(300f, 84f), Theme.Teal, () => { StickerStyle = PhotoFx.Next(StickerStyle); RefreshLooks(); }, 28).GetComponentInChildren<Text>();
            UIFactory.MakeButton("Snap", looks, "SNAP", new Vector2(220f, 84f), Theme.Tape, Snap, 40);

            _preview = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>();
            _preview.transform.SetParent(root, false);
            UIFactory.Place(_preview.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -140f), new Vector2(480f, 222f));
            _preview.raycastTarget = false;
            _preview.gameObject.SetActive(false);
            _toast = UIFactory.Label("PhotoToast", root, "", 34, Theme.Tape, TextAnchor.UpperLeft);
            UIFactory.Place(_toast.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -372f), new Vector2(900f, 50f));
            RefreshLooks();

            gameObject.SetActive(false);
        }

        private void RefreshLooks()
        {
            _filterLabel.text = "FILTER: " + PhotoFx.FilterName(Filter);
            _frameLabel.text = "FRAME: " + PhotoFx.FrameName(FrameStyle);
            _stickerLabel.text = "STICKERS: " + PhotoFx.StickerName(StickerStyle);
        }

        /// <summary>Hides the buttons for one frame, captures the screen with the chosen look and saves it.</summary>
        public void Snap()
        {
            if (!Active || _capturing) return;
            StartCoroutine(SnapRoutine());
        }

        private System.Collections.IEnumerator SnapRoutine()
        {
            _capturing = true;
            _controls.SetActive(false);
            _preview.gameObject.SetActive(false);
            _toast.text = "";
            yield return null; // one frame drawn without the buttons
            string message = null;
            Texture2D photo = null;
            yield return PhotoSaver.Capture(Filter, FrameStyle, StickerStyle, (m, t) => { message = m; photo = t; });
            if (_lastPhoto != null) Destroy(_lastPhoto);
            _lastPhoto = photo;
            _preview.texture = photo;
            _preview.gameObject.SetActive(photo != null);
            _toast.text = message ?? "";
            _toastUntil = Time.unscaledTime + 4f;
            RetroSk8.Audio.AudioManager.Instance?.PlaySfx(RetroSk8.Audio.SfxId.UiClick, 1f, 0.7f);
            _controls.SetActive(true);
            _capturing = false;
        }

        private void OnDestroy() { if (_lastPhoto != null) Destroy(_lastPhoto); }

        public void Enter()
        {
            if (Active) return;
            _cam = Camera.main;
            if (_cam == null || _player == null) return;
            Active = true;
            gameObject.SetActive(true);
            _controls.SetActive(true);
            _hidden.Clear();
            foreach (var go in _hide)
            {
                if (go == null || !go.activeSelf) continue;
                go.SetActive(false);
                _hidden.Add(go);
            }

            _savedPos = _cam.transform.position;
            _savedRot = _cam.transform.rotation;
            _savedFov = _cam.fieldOfView;
            _fov = _savedFov;
            // Anything else steering the camera (our rig, or a Cinemachine brain when installed) pauses.
            var rig = FindAnyObjectByType<CameraRig>();
            var list = new System.Collections.Generic.List<Behaviour>();
            if (rig != null && rig.enabled) list.Add(rig);
            foreach (var b in _cam.GetComponents<Behaviour>())
                if (b != null && b.enabled && b.GetType().Name == "CinemachineBrain") list.Add(b);
            foreach (var b in list) b.enabled = false;
            _disabled = list.ToArray();

            // Start from where the gameplay camera was looking.
            var toCam = _savedPos - Focus();
            _distance = Mathf.Clamp(toCam.magnitude, MinDistance, MaxDistance);
            _yaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(toCam.y / Mathf.Max(0.01f, toCam.magnitude), -1f, 1f)) * Mathf.Rad2Deg, -10f, 80f);
            _lastPointer = null;
            Apply();
        }

        public void Exit()
        {
            if (!Active) return;
            Active = false;
            _capturing = false; // a snap in progress stops with the view
            _toastUntil = 0f;
            if (_toast != null) _toast.text = "";
            if (_preview != null) _preview.gameObject.SetActive(false);
            foreach (var b in _disabled) if (b != null) b.enabled = true;
            _disabled = new Behaviour[0];
            if (_cam != null)
            {
                _cam.transform.SetPositionAndRotation(_savedPos, _savedRot);
                _cam.fieldOfView = _savedFov;
            }
            foreach (var go in _hidden) if (go != null) go.SetActive(true);
            _hidden.Clear();
            gameObject.SetActive(false);
            _exit?.Invoke();
        }

        private void Zoom(float dir) => _distance = Mathf.Clamp(_distance + dir * 0.8f, MinDistance, MaxDistance);

        private float _hiddenUntil;

        private void HideButtonsBriefly()
        {
            _controls.SetActive(false);
            _hiddenUntil = Time.unscaledTime + 4f;
        }

        private Vector3 Focus() => _player.transform.position + Vector3.up * _lift;

        private void Update()
        {
            if (!Active) return;
            if (!_capturing && !_controls.activeSelf && Time.unscaledTime >= _hiddenUntil) _controls.SetActive(true);
            if (_toastUntil > 0f)
            {
                if (_toast.text == "SAVING TO PHOTOS…")
                {
                    int st = PhotoSaver.PhotosState;
                    if (st == 2) _toast.text = "SAVED TO PHOTOS";
                    else if (st == 3) _toast.text = "PHOTOS ACCESS IS OFF: SAVED IN THE APP (SETTINGS > PRIVACY > PHOTOS)";
                }
                if (Time.unscaledTime >= _toastUntil) { _toastUntil = 0f; _toast.text = ""; _preview.gameObject.SetActive(false); }
            }

            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
            {
                Vector2 pos = pointer.position.ReadValue();
                if (_lastPointer.HasValue)
                {
                    Vector2 d = pos - _lastPointer.Value;
                    float k = 180f / Mathf.Max(1f, Screen.width);
                    _yaw += d.x * k * 1.6f;
                    _pitch = Mathf.Clamp(_pitch - d.y * k * 1.2f, -10f, 80f);
                }
                _lastPointer = pos;
            }
            else _lastPointer = null;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Zoom(-Mathf.Sign(scroll));
            }

            // Phase 20: a controller's right stick orbits the camera and the triggers zoom (the D-pad still
            // moves between the photo buttons).
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.rightStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f)
                {
                    float dt = Time.unscaledDeltaTime;
                    _yaw += stick.x * 120f * dt;
                    _pitch = Mathf.Clamp(_pitch - stick.y * 80f * dt, -10f, 80f);
                }
                if (pad.rightTrigger.wasPressedThisFrame) Zoom(-1f);
                if (pad.leftTrigger.wasPressedThisFrame) Zoom(1f);
            }
            Apply();
        }

        private void Apply()
        {
            if (_cam == null) return;
            float yaw = _yaw * Mathf.Deg2Rad, pitch = _pitch * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
            var focus = Focus();
            var pos = focus + dir * _distance;
            // Don't sink the camera through the ground or a wall.
            if (Physics.Linecast(focus, pos, out var hit, ~0, QueryTriggerInteraction.Ignore))
                pos = hit.point + (focus - pos).normalized * 0.2f;
            _cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(focus - pos));
            _cam.fieldOfView = _fov;
        }
    }
}
