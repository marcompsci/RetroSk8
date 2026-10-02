using System;
using RetroSk8.Audio;
using RetroSk8.Core;
using RetroSk8.Feedback;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroSk8.Game
{
    /// <summary>
    /// Create-a-Park editing session (the park scene loaded with <see cref="GameSession.EditPark"/>).
    /// The skater waits on the start pad, a map-style camera looks down at the lot (north is always up), and
    /// obstacles are placed, tapped to select, then moved one grid cell at a time with the arrow buttons
    /// (or the keyboard arrows), turned, resized, copied or deleted. Every change rebuilds only that piece.
    /// </summary>
    public sealed class ParkEditorController : MonoBehaviour
    {
        public const float TapSlopPixels = 14f;
        public const float MinHeight = 14f, MaxHeight = 70f;
        private const float Pitch = 62f;

        private CustomParkBuilder _builder;
        private PlayerController _player;
        private Camera _cam;
        private Behaviour[] _disabled = new Behaviour[0];
        private Transform _highlight;
        private Transform _highlightArrow;
        private Vector3 _focus;
        private float _height = 42f;
        private Vector2? _pressStart;
        private Vector2 _lastPointer;
        private bool _dragging;
        private InputAction _up, _down, _left, _right, _rotate, _delete;

        public CustomPark Park => _builder.Park;
        public int Selected { get; private set; } = -1;
        public bool Dirty { get; private set; }
        /// <summary>Screen points over editor buttons are ignored by the map (set by the view).</summary>
        public Func<Vector2, bool> IsOverUi;

        public event Action Changed;
        public event Action<string> Message;

        public void Init(CustomParkBuilder builder, PlayerController player)
        {
            _builder = builder;
            _player = player;
            _cam = Camera.main;

            // The skater stands frozen on the start pad as a size reference (SKATE IT reloads the park to ride).
            player.SetInputEnabled(false);
            player.Teleport(CustomParkBuilder.SpawnPosition, Vector3.forward);
            if (player.Body != null) player.Body.isKinematic = true;
            player.enabled = false;
            var safety = player.GetComponent<RespawnSafety>();
            if (safety != null) safety.enabled = false;

            var rig = FindFirstObjectByType<CameraRig>();
            var list = new System.Collections.Generic.List<Behaviour>();
            if (rig != null) list.Add(rig);
            if (_cam != null)
                foreach (var b in _cam.GetComponents<Behaviour>())
                    if (b != null && b.GetType().Name == "CinemachineBrain") list.Add(b);
            foreach (var b in list) b.enabled = false;
            _disabled = list.ToArray();

            _highlight = MakeHighlight();
            _focus = new Vector3(0f, 0f, -6f);

            _up = Key("<Keyboard>/upArrow", "<Keyboard>/w");
            _down = Key("<Keyboard>/downArrow", "<Keyboard>/s");
            _left = Key("<Keyboard>/leftArrow", "<Keyboard>/a");
            _right = Key("<Keyboard>/rightArrow", "<Keyboard>/d");
            _rotate = Key("<Keyboard>/r");
            _delete = Key("<Keyboard>/delete", "<Keyboard>/backspace");
            ApplyCamera();
            RefreshHighlight();
        }

        private static InputAction Key(params string[] bindings)
        {
            var a = new InputAction(type: InputActionType.Button);
            foreach (var b in bindings) a.AddBinding(b);
            a.Enable();
            return a;
        }

        private void OnDestroy()
        {
            foreach (var a in new[] { _up, _down, _left, _right, _rotate, _delete }) a?.Dispose();
            foreach (var b in _disabled) if (b != null) b.enabled = true;
        }

        // ---------------------------------------------------------------- editing (called by buttons, keys and tests)

        public bool AddPiece(PieceKind kind)
        {
            var cell = CustomPark.CellAt(_focus.x, _focus.z);
            if (cell.x < 0) cell = (CustomPark.GridCells / 2, CustomPark.GridCells / 2);
            int i = Park.Add(kind, cell.x, cell.z);
            if (i < 0)
            {
                Say(Park.pieces.Count >= CustomPark.MaxPieces ? $"PARK IS FULL ({CustomPark.MaxPieces} PIECES)" : "NO ROOM FOR THAT HERE");
                Feedback(false);
                return false;
            }
            _builder.PieceAdded();
            Select(i);
            MarkDirty();
            Feedback(true);
            return true;
        }

        /// <summary>Arrow buttons: one cell; up is north (+z) on the map.</summary>
        public bool Move(int dx, int dz)
        {
            if (Selected < 0) { Say("TAP AN OBSTACLE FIRST"); return false; }
            if (!Park.TryMove(Selected, dx, dz))
            {
                Say("BLOCKED");
                Feedback(false);
                return false;
            }
            _builder.RebuildPiece(Selected);
            KeepSelectionInView();
            MarkDirty();
            Feedback(true);
            return true;
        }

        public bool Rotate()
        {
            if (Selected < 0) return false;
            if (!Park.TryRotate(Selected)) { Say("NO ROOM TO TURN"); Feedback(false); return false; }
            _builder.RebuildPiece(Selected);
            MarkDirty();
            Feedback(true);
            return true;
        }

        public bool CycleSize()
        {
            if (Selected < 0) return false;
            Park.CycleSize(Selected);
            _builder.RebuildPiece(Selected);
            MarkDirty();
            Feedback(true);
            return true;
        }

        public bool Duplicate()
        {
            if (Selected < 0) return false;
            int i = Park.Duplicate(Selected);
            if (i < 0) { Say("NO ROOM FOR A COPY"); Feedback(false); return false; }
            _builder.PieceAdded();
            Select(i);
            MarkDirty();
            Feedback(true);
            return true;
        }

        public bool DeleteSelected()
        {
            if (Selected < 0) return false;
            int i = Selected;
            Park.Remove(i);
            _builder.PieceRemoved(i);
            // Later pieces shifted down one index; their objects keep working (names are cosmetic).
            Select(-1);
            MarkDirty();
            Feedback(true);
            return true;
        }

        public void CycleTheme()
        {
            Park.theme = (Park.theme + 1) % 3;
            MarkDirty();
            Say("THEME: " + ThemeName(Park.Theme) + " (SHOWS WHEN YOU SKATE IT)");
        }

        public void Rename(string name)
        {
            Park.name = string.IsNullOrWhiteSpace(name) ? Park.name : name.Trim().ToUpperInvariant();
            if (Park.name.Length > CustomPark.MaxNameLength) Park.name = Park.name.Substring(0, CustomPark.MaxNameLength);
            MarkDirty();
        }

        public void Save()
        {
            SaveManager.SaveCustomPark(Park);
            Dirty = false;
            Say("SAVED");
            if (CareerService.Check().Any)
            {
                var msgs = CareerService.TakePending();
                if (msgs.Count > 0) Say(msgs[msgs.Count - 1]);
            }
            Changed?.Invoke();
        }

        /// <summary>Save and reload the park to skate it (with its theme's lighting).</summary>
        public void SkateIt()
        {
            Save();
            GameSession.EditPark = false;
            GameSession.Mode = RunMode.FreeSkate;
            Time.timeScale = 1f;
            SceneRouter.LoadPark(Park.id, null);
        }

        public void ExitToMenu()
        {
            Save();
            GameSession.EditPark = false;
            Time.timeScale = 1f;
            SceneRouter.Load(SceneNames.MainMenu);
        }

        public void Select(int index)
        {
            Selected = Park.Valid(index) ? index : -1;
            RefreshHighlight();
            Changed?.Invoke();
        }

        public void Zoom(float dir) { _height = Mathf.Clamp(_height + dir * 8f, MinHeight, MaxHeight); ApplyCamera(); }

        public static string ThemeName(ParkTheme t) => t == ParkTheme.NeonNight ? "NEON NIGHT" : t == ParkTheme.Sunset ? "SUNSET" : "DAYLIGHT";

        // ---------------------------------------------------------------- input

        private void Update()
        {
            if (_builder == null) return;
            if (_up.WasPressedThisFrame()) Move(0, 1);
            if (_down.WasPressedThisFrame()) Move(0, -1);
            if (_left.WasPressedThisFrame()) Move(-1, 0);
            if (_right.WasPressedThisFrame()) Move(1, 0);
            if (_rotate.WasPressedThisFrame()) Rotate();
            if (_delete.WasPressedThisFrame()) DeleteSelected();

            var pointer = Pointer.current;
            if (pointer != null)
            {
                Vector2 pos = pointer.position.ReadValue();
                bool pressed = pointer.press.isPressed;
                if (pressed && !_pressStart.HasValue)
                {
                    if (IsOverUi == null || !IsOverUi(pos)) { _pressStart = pos; _lastPointer = pos; _dragging = false; }
                }
                else if (pressed && _pressStart.HasValue)
                {
                    if (!_dragging && (pos - _pressStart.Value).magnitude > TapSlopPixels) _dragging = true;
                    if (_dragging) Pan(pos - _lastPointer);
                    _lastPointer = pos;
                }
                else if (!pressed && _pressStart.HasValue)
                {
                    if (!_dragging) TapAt(pos);
                    _pressStart = null;
                }
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) Zoom(-Mathf.Sign(scroll));
            }
            AnimateHighlight();
        }

        /// <summary>Selects whatever obstacle is under a screen point (or clears the selection on empty ground).</summary>
        public void TapAt(Vector2 screen)
        {
            if (_cam == null) return;
            var ray = _cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return;
            float t = -ray.origin.y / ray.direction.y;
            if (t <= 0f) return;
            Vector3 hit = ray.origin + ray.direction * t;
            SelectAtWorld(hit.x, hit.z);
        }

        public void SelectAtWorld(float x, float z)
        {
            var cell = CustomPark.CellAt(x, z);
            int i = cell.x < 0 ? -1 : Park.PieceAt(cell.x, cell.z);
            Select(i);
            if (i >= 0)
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiClick);
                HapticsManager.Play(HapticKind.Selection);
            }
        }

        private void Pan(Vector2 deltaPixels)
        {
            float metresPerPixel = _height * 1.6f / Mathf.Max(1f, Screen.height);
            _focus -= new Vector3(deltaPixels.x, 0f, deltaPixels.y) * metresPerPixel;
            float lim = CustomPark.HalfSize;
            _focus.x = Mathf.Clamp(_focus.x, -lim, lim);
            _focus.z = Mathf.Clamp(_focus.z, -lim, lim);
            ApplyCamera();
        }

        private void KeepSelectionInView()
        {
            if (Selected < 0) return;
            var (x, z) = CustomPark.WorldCenter(Park.pieces[Selected]);
            var d = new Vector2(x - _focus.x, z - _focus.z);
            float keep = _height * 0.45f;
            if (d.magnitude > keep)
            {
                var c = new Vector2(_focus.x, _focus.z) + d - d.normalized * keep;
                _focus = new Vector3(c.x, 0f, c.y);
                ApplyCamera();
            }
        }

        private void ApplyCamera()
        {
            if (_cam == null) return;
            float back = _height / Mathf.Tan(Pitch * Mathf.Deg2Rad);
            _cam.transform.SetPositionAndRotation(_focus + new Vector3(0f, _height, -back), Quaternion.Euler(Pitch, 0f, 0f));
            _cam.fieldOfView = 50f;
        }

        // ---------------------------------------------------------------- selection highlight

        private Transform MakeHighlight()
        {
            var root = new GameObject("EditorSelection").transform;
            var frame = PrimitiveMeshes.CreateVisual("Frame", PrimitiveType.Cube, root, new Vector3(0f, 0.04f, 0f), Vector3.one, Palette.NeonCyan);
            var mat = PlaceholderMaterials.GetEmissive(Palette.NeonCyan, 1.4f);
            frame.GetComponent<MeshRenderer>().sharedMaterial = mat;
            _highlightArrow = PrimitiveMeshes.CreateVisual("Marker", PrimitiveType.Cube, root, new Vector3(0f, 4f, 0f), new Vector3(0.7f, 0.7f, 0.7f), Palette.TapeYellow).transform;
            _highlightArrow.GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(Palette.TapeYellow, 2f);
            _highlightArrow.localRotation = Quaternion.Euler(45f, 0f, 45f);
            return root;
        }

        private void RefreshHighlight()
        {
            if (_highlight == null) return;
            _highlight.gameObject.SetActive(Selected >= 0);
            if (Selected < 0) return;
            var p = Park.pieces[Selected];
            var (w, d) = CustomPark.Footprint(p);
            var (x, z) = CustomPark.WorldCenter(p);
            _highlight.position = new Vector3(x, 0f, z);
            _highlight.GetChild(0).localScale = new Vector3(w * CustomPark.CellSize, 0.06f, d * CustomPark.CellSize);
        }

        private void AnimateHighlight()
        {
            if (_highlightArrow == null || Selected < 0) return;
            _highlightArrow.localPosition = new Vector3(0f, 4f + Mathf.Sin(Time.unscaledTime * 4f) * 0.3f, 0f);
        }

        private void MarkDirty()
        {
            Dirty = true;
            RefreshHighlight();
            Changed?.Invoke();
        }

        private void Say(string text) => Message?.Invoke(text);

        private static void Feedback(bool ok)
        {
            AudioManager.Instance?.PlaySfx(ok ? SfxId.UiClick : SfxId.LandSketchy, ok ? 1f : 0.6f);
            HapticsManager.Play(ok ? HapticKind.Selection : HapticKind.Light);
        }
    }
}
