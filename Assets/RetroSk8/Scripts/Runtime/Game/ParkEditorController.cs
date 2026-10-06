using System;
using RetroSk8.Audio;
using RetroSk8.Core;
using RetroSk8.Feedback;
using RetroSk8.Input;
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
        private Transform _cursor;
        private Vector2 _padHeld;
        private float _padNext;
        private int _modeFrame = -1;

        /// <summary>Phase 21 controller: in map mode the pad drives the map (cursor, select, move); otherwise it drives the buttons.</summary>
        public bool MapMode { get; private set; } = true;
        /// <summary>Metres a second the cursor moves with the stick at the default zoom.</summary>
        public const float CursorSpeed = 18f;
        /// <summary>Where the pad cursor points (the map centre).</summary>
        public Vector3 Focus => _focus;

        public CustomPark Park => _builder.Park;
        public int Selected { get; private set; } = -1;
        public bool Dirty { get; private set; }
        /// <summary>Screen points over editor buttons are ignored by the map (set by the view).</summary>
        public Func<Vector2, bool> IsOverUi;

        public event Action Changed;
        /// <summary>Undo/redo (Phase 16).</summary>
        public readonly ParkHistory History = new ParkHistory();
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

            var rig = FindAnyObjectByType<CameraRig>();
            var list = new System.Collections.Generic.List<Behaviour>();
            if (rig != null) list.Add(rig);
            if (_cam != null)
                foreach (var b in _cam.GetComponents<Behaviour>())
                    if (b != null && b.GetType().Name == "CinemachineBrain") list.Add(b);
            foreach (var b in list) b.enabled = false;
            _disabled = list.ToArray();

            _highlight = MakeHighlight();
            _cursor = MakeCursor();
            PadNavigator.Suspend = () => this != null && MapMode && InputDeviceTracker.PadActive;
            PadNavigator.BackOverride = PadBack;
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
            PadNavigator.Suspend = null;
            PadNavigator.BackOverride = null;
        }

        // ---------------------------------------------------------------- editing (called by buttons, keys and tests)

        public bool AddPiece(PieceKind kind)
        {
            var cell = CustomPark.CellAt(_focus.x, _focus.z);
            if (cell.x < 0) cell = (CustomPark.GridCells / 2, CustomPark.GridCells / 2);
            var before = Park.Clone();
            int i = Park.Add(kind, cell.x, cell.z);
            if (i >= 0) History.Record(before);
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
            var before = Park.Clone();
            if (Park.TryMove(Selected, dx, dz)) History.Record(before);
            else
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
            var before = Park.Clone();
            if (!Park.TryRotate(Selected)) { Say("NO ROOM TO TURN"); Feedback(false); return false; }
            History.Record(before);
            _builder.RebuildPiece(Selected);
            MarkDirty();
            Feedback(true);
            return true;
        }

        public bool CycleSize()
        {
            if (Selected < 0) return false;
            History.Record(Park);
            Park.CycleSize(Selected);
            _builder.RebuildPiece(Selected);
            MarkDirty();
            Feedback(true);
            return true;
        }

        public bool Duplicate()
        {
            if (Selected < 0) return false;
            var before = Park.Clone();
            int i = Park.Duplicate(Selected);
            if (i < 0) { Say("NO ROOM FOR A COPY"); Feedback(false); return false; }
            History.Record(before);
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
            History.Record(Park);
            Park.Remove(i);
            _builder.PieceRemoved(i);
            // Later pieces shifted down one index; their objects keep working (names are cosmetic).
            Select(-1);
            MarkDirty();
            Feedback(true);
            return true;
        }

        /// <summary>Longer (+1) or shorter (-1): ledges, rails, quarters, banks, walls and pads (Phase 16).</summary>
        public bool Stretch(int delta)
        {
            if (Selected < 0) { Say("TAP AN OBSTACLE FIRST"); return false; }
            var p = Park.pieces[Selected];
            if (!CustomPark.Stretches(p.Kind)) { Say(CustomPark.DisplayName(p.Kind).ToUpperInvariant() + " HAS ONE LENGTH"); Feedback(false); return false; }
            var before = Park.Clone();
            if (!Park.TryStretch(Selected, delta))
            {
                Say(delta > 0 ? (p.length >= CustomPark.MaxLength ? "AS LONG AS IT GETS" : "NO ROOM TO STRETCH") : "AS SHORT AS IT GETS");
                Feedback(false);
                return false;
            }
            History.Record(before);
            _builder.RebuildPiece(Selected);
            MarkDirty();
            Feedback(true);
            return true;
        }

        /// <summary>Cycles a rail's curve (Phase 16).</summary>
        public bool Bend()
        {
            if (Selected < 0) { Say("TAP AN OBSTACLE FIRST"); return false; }
            var p = Park.pieces[Selected];
            if (!CustomPark.Bends(p.Kind)) { Say("ONLY RAILS BEND"); Feedback(false); return false; }
            var before = Park.Clone();
            if (!Park.TryBend(Selected)) { Say("NO ROOM TO BEND"); Feedback(false); return false; }
            History.Record(before);
            _builder.RebuildPiece(Selected);
            MarkDirty();
            Feedback(true);
            return true;
        }

        public bool Undo() => Restore(History.Undo(Park), "UNDO", "NOTHING TO UNDO");

        public bool Redo() => Restore(History.Redo(Park), "REDO", "NOTHING TO REDO");

        private bool Restore(CustomPark state, string done, string none)
        {
            if (state == null) { Say(none); Feedback(false); return false; }
            int keep = Selected;
            Park.pieces = state.pieces;
            Park.theme = state.theme;
            Park.name = state.name;
            _builder.RebuildAll();
            Select(Park.Valid(keep) ? keep : -1);
            MarkDirty();
            Say(done);
            Feedback(true);
            return true;
        }

        public void CycleTheme()
        {
            History.Record(Park);
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
            var pad = Gamepad.current;
            if (pad != null && InputDeviceTracker.PadActive) HandlePad(pad);
            AnimateHighlight();
            RefreshCursor();
        }

        // ---------------------------------------------------------------- controller (Phase 21)

        /// <summary>
        /// Map mode: left or right stick moves the cursor (the map centre), the D-pad nudges the selected piece one cell
        /// (or the cursor, with nothing selected), A selects what's under the cursor, X turns, Y copies, LB/RB undo/redo,
        /// the triggers zoom. B deselects, then switches to the buttons. Start or View flips between map and buttons.
        /// </summary>
        private void HandlePad(Gamepad pad)
        {
            if (Time.frameCount == _modeFrame) return; // the mode already changed this frame (B through the menu navigator)
            if (pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame) { SetMapMode(!MapMode); return; }
            if (!MapMode) return;
            if (pad.buttonEast.wasPressedThisFrame) { PadBack(); return; }

            Vector2 stick = pad.leftStick.ReadValue() + pad.rightStick.ReadValue();
            if (stick.sqrMagnitude > 0.04f) MoveCursor(Vector2.ClampMagnitude(stick, 1f) * CursorSpeed * (_height / 42f) * Time.unscaledDeltaTime);

            Vector2 d = pad.dpad.ReadValue();
            if (d.sqrMagnitude < 0.25f) _padHeld = Vector2.zero;
            else
            {
                d = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? new Vector2(Mathf.Sign(d.x), 0f) : new Vector2(0f, Mathf.Sign(d.y));
                float now = Time.unscaledTime;
                if (d != _padHeld || now >= _padNext)
                {
                    _padNext = now + (d == _padHeld ? 0.12f : 0.35f);
                    _padHeld = d;
                    Nudge((int)d.x, (int)d.y);
                }
            }

            if (pad.buttonSouth.wasPressedThisFrame) PadSelect();
            if (pad.buttonWest.wasPressedThisFrame) Rotate();
            if (pad.buttonNorth.wasPressedThisFrame) Duplicate();
            if (pad.leftShoulder.wasPressedThisFrame) Undo();
            if (pad.rightShoulder.wasPressedThisFrame) Redo();
            float zoom = pad.leftTrigger.ReadValue() - pad.rightTrigger.ReadValue(); // RT in, LT out
            if (Mathf.Abs(zoom) > 0.1f)
            {
                _height = Mathf.Clamp(_height + zoom * 40f * Time.unscaledDeltaTime, MinHeight, MaxHeight);
                ApplyCamera();
            }
        }

        /// <summary>Switches the controller between the map and the editor's buttons.</summary>
        public void SetMapMode(bool on)
        {
            if (MapMode == on) return;
            MapMode = on;
            _modeFrame = Time.frameCount;
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (on && es != null) es.SetSelectedGameObject(null); // A must not also press a button
            Say(on ? "MAP: A SELECT · D-PAD MOVE · X TURN · Y COPY" : "BUTTONS: START FOR THE MAP");
            Changed?.Invoke();
        }

        /// <summary>B: drop the selection, then go to the buttons; from the buttons, back to the map. Never leaves the editor.</summary>
        public bool PadBack()
        {
            if (this == null) return false;
            if (MapMode && Selected >= 0) { Select(-1); return true; }
            SetMapMode(!MapMode);
            return true;
        }

        /// <summary>A: select whatever is under the cursor (empty ground clears the selection).</summary>
        public void PadSelect() => SelectAtWorld(_focus.x, _focus.z);

        /// <summary>D-pad: the selected piece one cell, or the cursor one cell when nothing is selected.</summary>
        public void Nudge(int dx, int dz)
        {
            if (Selected >= 0) Move(dx, dz);
            else MoveCursor(new Vector2(dx, dz) * CustomPark.CellSize);
        }

        /// <summary>Moves the cursor (and the map with it) by metres, kept inside the lot.</summary>
        public void MoveCursor(Vector2 metres)
        {
            _focus += new Vector3(metres.x, 0f, metres.y);
            float lim = CustomPark.HalfSize;
            _focus.x = Mathf.Clamp(_focus.x, -lim, lim);
            _focus.z = Mathf.Clamp(_focus.z, -lim, lim);
            ApplyCamera();
        }

        private Transform MakeCursor()
        {
            var root = new GameObject("EditorCursor").transform;
            var mat = PlaceholderMaterials.GetEmissive(Palette.TapeYellow, 1.6f);
            foreach (var s in new[] { new Vector3(1.6f, 0.05f, 0.25f), new Vector3(0.25f, 0.05f, 1.6f) })
                PrimitiveMeshes.CreateVisual("Bar", PrimitiveType.Cube, root, new Vector3(0f, 0.08f, 0f), s, Palette.TapeYellow)
                    .GetComponent<MeshRenderer>().sharedMaterial = mat;
            root.gameObject.SetActive(false);
            return root;
        }

        private void RefreshCursor()
        {
            if (_cursor == null) return;
            bool show = MapMode && InputDeviceTracker.PadActive;
            if (_cursor.gameObject.activeSelf != show) _cursor.gameObject.SetActive(show);
            if (show) _cursor.position = new Vector3(_focus.x, 0f, _focus.z);
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
            float bob = SaveManager.Data.settings.reducedMotion ? 0f : Mathf.Sin(Time.unscaledTime * 4f) * 0.3f; // Phase 20
            _highlightArrow.localPosition = new Vector3(0f, 4f + bob, 0f);
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
