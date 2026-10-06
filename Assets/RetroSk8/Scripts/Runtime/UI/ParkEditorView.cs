using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Create-a-Park editor screen: obstacle palette (left), selected-piece tools (bottom), a big arrow pad that
    /// moves the selected obstacle one grid cell per tap (hold to keep moving), zoom, theme, save and SKATE IT.
    /// </summary>
    public sealed class ParkEditorView : MonoBehaviour
    {
        private ParkEditorController _editor;
        private readonly List<RectTransform> _blocking = new List<RectTransform>();
        private Text _selected;
        private Text _count;
        private Text _toast;
        private float _toastTimer;
        private InputField _name;
        private GameObject _tools;
        private Text _theme;
        private Button _undo, _redo, _longer, _shorter, _bend;
        private Text _hint;
        public const string TouchHint = "TAP AN OBSTACLE TO SELECT IT · DRAG THE MAP TO LOOK AROUND";
        public const string PadMapHint = "STICK: CURSOR · A: SELECT · D-PAD: MOVE · X: TURN · Y: COPY · LB/RB: UNDO/REDO · START: BUTTONS";
        public const string PadButtonsHint = "D-PAD: CHOOSE A BUTTON · A: PRESS · START OR B: BACK TO THE MAP";

        private static readonly PieceKind[] Palette =
        {
            PieceKind.Ledge, PieceKind.FlatRail, PieceKind.Stairs, PieceKind.Kicker,
            PieceKind.QuarterPipe, PieceKind.Bank, PieceKind.Funbox, PieceKind.MiniRamp,
            PieceKind.Bowl, PieceKind.ManualPad, PieceKind.Bench, PieceKind.Wall,
        };

        public static readonly string[] SizeNames = { "LOW", "MID", "HIGH" };

        public void Build(RectTransform safe, ParkEditorController editor)
        {
            _editor = editor;
            editor.IsOverUi = IsOverUi;

            // ---- palette (left)
            var palette = UIFactory.Panel("Palette", safe, Theme.InkSoft, true);
            UIFactory.Place(palette.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -10f), new Vector2(390f, 860f));
            _blocking.Add(palette.rectTransform);
            var header = UIFactory.Label("Header", palette.transform, "ADD", 34, Theme.Tape, TextAnchor.UpperCenter);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(360f, 44f));
            var grid = UIFactory.Rect("Grid", palette.transform);
            UIFactory.Place(grid, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(360f, 780f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(172f, 116f);
            layout.spacing = new Vector2(12f, 14f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            foreach (var kind in Palette)
            {
                var k = kind;
                UIFactory.MakeButton("Add_" + kind, grid, CustomPark.DisplayName(kind).ToUpperInvariant(), new Vector2(172f, 116f),
                    Theme.Cream, () => editor.AddPiece(k), 26);
            }

            // ---- top bar
            var top = UIFactory.Panel("TopBar", safe, Theme.InkSoft, true);
            UIFactory.Place(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1880f, 104f));
            _blocking.Add(top.rectTransform);
            _name = MakeNameField(top.transform);
            _count = UIFactory.Label("Count", top.transform, "", 30, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(_count.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(560f, 0f), new Vector2(200f, 60f));
            var row = UIFactory.Rect("Actions", top.transform);
            UIFactory.Place(row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(1100f, 84f));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14f;
            h.childAlignment = TextAnchor.MiddleRight;
            h.childControlWidth = h.childControlHeight = false;
            // Phase 16: undo / redo.
            _undo = UIFactory.MakeButton("Undo", row, "UNDO", new Vector2(130f, 80f), Theme.Cream, () => editor.Undo(), 30);
            _redo = UIFactory.MakeButton("Redo", row, "REDO", new Vector2(130f, 80f), Theme.Cream, () => editor.Redo(), 30);
            var theme = UIFactory.MakeButton("Theme", row, "", new Vector2(240f, 80f), Theme.Teal, () => { editor.CycleTheme(); Refresh(); }, 26);
            _theme = theme.GetComponentInChildren<Text>();
            UIFactory.MakeButton("Save", row, "SAVE", new Vector2(150f, 80f), Theme.Cream, editor.Save, 32);
            UIFactory.MakeButton("Skate", row, "SKATE IT", new Vector2(220f, 80f), Theme.Tape, editor.SkateIt, 34);
            UIFactory.MakeButton("Exit", row, "EXIT", new Vector2(150f, 80f), Theme.Coral, editor.ExitToMenu, 32);

            // ---- selected-piece tools (bottom centre)
            var tools = UIFactory.Panel("Tools", safe, Theme.InkSoft, true);
            UIFactory.Place(tools.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 16f), new Vector2(1180f, 190f));
            _blocking.Add(tools.rectTransform);
            _tools = tools.gameObject;
            _selected = UIFactory.Label("Selected", tools.transform, "", 32, Theme.Tape, TextAnchor.UpperCenter);
            UIFactory.Place(_selected.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1000f, 46f));
            var toolRow = UIFactory.Rect("ToolRow", tools.transform);
            UIFactory.Place(toolRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1150f, 100f));
            var th = toolRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.spacing = 12f;
            th.childAlignment = TextAnchor.MiddleCenter;
            th.childControlWidth = th.childControlHeight = false;
            var b = new Vector2(152f, 96f);
            UIFactory.MakeButton("Rotate", toolRow, "TURN", b, Theme.Teal, () => editor.Rotate(), 30);
            UIFactory.MakeButton("Size", toolRow, "HEIGHT", b, Theme.Teal, () => editor.CycleSize(), 28);
            _longer = UIFactory.MakeButton("Longer", toolRow, "LONGER", b, Theme.Teal, () => editor.Stretch(1), 28);
            _shorter = UIFactory.MakeButton("Shorter", toolRow, "SHORTER", b, Theme.Teal, () => editor.Stretch(-1), 26);
            _bend = UIFactory.MakeButton("Bend", toolRow, "BEND", b, Theme.Tape, () => editor.Bend(), 30);
            UIFactory.MakeButton("Copy", toolRow, "COPY", b, Theme.Cream, () => editor.Duplicate(), 30);
            UIFactory.MakeButton("Delete", toolRow, "DELETE", b, Theme.Coral, () => editor.DeleteSelected(), 28);

            // ---- arrow pad (bottom right): move the selected obstacle one cell per tap; hold to repeat
            var pad = UIFactory.Panel("ArrowPad", safe, Theme.InkSoft, true);
            UIFactory.Place(pad.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 16f), new Vector2(420f, 420f));
            pad.sprite = UIFactory.Circle;
            _blocking.Add(pad.rectTransform);
            Arrow(pad.rectTransform, new Vector2(0f, 130f), 0f, () => editor.Move(0, 1));
            Arrow(pad.rectTransform, new Vector2(0f, -130f), 180f, () => editor.Move(0, -1));
            Arrow(pad.rectTransform, new Vector2(-130f, 0f), 90f, () => editor.Move(-1, 0));
            Arrow(pad.rectTransform, new Vector2(130f, 0f), -90f, () => editor.Move(1, 0));
            var move = UIFactory.Label("Move", pad.transform, "MOVE", 26, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Place(move.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 40f));

            // ---- zoom (right edge)
            var zoom = UIFactory.Rect("Zoom", safe);
            UIFactory.Place(zoom, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 150f), new Vector2(110f, 240f));
            _blocking.Add(zoom);
            var zl = zoom.gameObject.AddComponent<VerticalLayoutGroup>();
            zl.spacing = 16f;
            zl.childControlWidth = zl.childControlHeight = false;
            UIFactory.MakeButton("ZoomIn", zoom, "+", new Vector2(110f, 110f), Theme.Cream, () => editor.Zoom(-1f), 60);
            UIFactory.MakeButton("ZoomOut", zoom, "-", new Vector2(110f, 110f), Theme.Cream, () => editor.Zoom(1f), 60);

            // ---- hint + toast
            var hint = UIFactory.Label("Hint", safe, TouchHint, 24, Theme.Cream, TextAnchor.UpperCenter);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(1400f, 36f));
            _toast = UIFactory.Label("Toast", safe, "", 40, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Place(_toast.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 60f));

            _hint = hint;
            RetroSk8.Input.InputDeviceTracker.Changed += OnDeviceChanged;
            editor.Changed += Refresh;
            editor.Message += text => { _toast.text = text; _toastTimer = 2f; };
            Refresh();
        }

        private InputField MakeNameField(Transform parent)
        {
            var bg = UIFactory.Panel("Name", parent, new Color(1f, 1f, 1f, 0.12f), true);
            UIFactory.Place(bg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(520f, 78f));
            var text = UIFactory.Label("Text", bg.transform, "", 40, Theme.Tape, TextAnchor.MiddleLeft, false);
            UIFactory.Stretch(text.rectTransform, 14f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.raycastTarget = false;
            var field = bg.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.characterLimit = CustomPark.MaxNameLength;
            field.text = _editor.Park.name;
            field.onEndEdit.AddListener(v => { _editor.Rename(v); field.text = _editor.Park.name; });
            return field;
        }

        private void Arrow(RectTransform pad, Vector2 pos, float angle, Action onPress)
        {
            var b = UIFactory.Panel("Arrow", pad, Theme.Tape, true);
            b.sprite = UIFactory.Circle;
            UIFactory.Place(b.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(130f, 130f));
            var hold = b.gameObject.AddComponent<HoldRepeatButton>();
            hold.OnPress = onPress;
            hold.Target = b;
            // Chevron drawn from two bars so it never depends on a font glyph.
            var glyph = UIFactory.Rect("Chevron", b.rectTransform);
            UIFactory.Place(glyph, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
            glyph.localRotation = Quaternion.Euler(0f, 0f, angle);
            foreach (float s in new[] { -1f, 1f })
            {
                var bar = UIFactory.Panel("Bar", glyph, Theme.Ink);
                UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(s * 15f, 4f), new Vector2(16f, 52f));
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, s * 45f);
            }
        }

        private bool IsOverUi(Vector2 screen)
        {
            foreach (var r in _blocking)
                if (r != null && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, screen, null)) return true;
            return false;
        }

        private void OnDeviceChanged(bool pad) => Refresh();
        private void OnDestroy() => RetroSk8.Input.InputDeviceTracker.Changed -= OnDeviceChanged;

        private void Refresh()
        {
            if (_hint != null)
                _hint.text = !RetroSk8.Input.InputDeviceTracker.PadActive ? TouchHint : _editor.MapMode ? PadMapHint : PadButtonsHint;
            var park = _editor.Park;
            _count.text = $"{park.pieces.Count}/{CustomPark.MaxPieces} PIECES{(_editor.Dirty ? "  *" : "")}";
            _theme.text = ParkEditorController.ThemeName(park.Theme);
            _undo.interactable = _editor.History.CanUndo;
            _redo.interactable = _editor.History.CanRedo;
            int sel = _editor.Selected;
            _tools.SetActive(sel >= 0);
            if (sel >= 0)
            {
                var p = park.pieces[sel];
                bool stretches = CustomPark.Stretches(p.Kind), bends = CustomPark.Bends(p.Kind);
                _longer.interactable = stretches && p.length < CustomPark.MaxLength;
                _shorter.interactable = stretches && p.length > 0;
                _bend.gameObject.SetActive(bends);
                string extra = (stretches ? $" · LENGTH {p.length + 1}/{CustomPark.MaxLength + 1}" : "") + (bends ? " · " + BendName(p.bend) : "");
                _selected.text = $"{CustomPark.DisplayName(p.Kind).ToUpperInvariant()} · {SizeNames[Mathf.Clamp(p.size, 0, 2)]}{extra} · ARROWS MOVE IT";
            }
        }

        public static string BendName(int bend) =>
            bend == 0 ? "STRAIGHT" : (Mathf.Abs(bend) == 2 ? "BIG CURVE " : "CURVE ") + (bend > 0 ? "RIGHT" : "LEFT");

        private void Update()
        {
            if (_toastTimer <= 0f) return;
            _toastTimer -= Time.unscaledDeltaTime;
            var c = _toast.color;
            c.a = Mathf.Clamp01(_toastTimer * 2f);
            _toast.color = c;
        }
    }

    /// <summary>Fires once on press, then repeats while held (for nudging obstacles across the grid).</summary>
    public sealed class HoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public const float FirstDelay = 0.35f, RepeatEvery = 0.12f;
        public Action OnPress;
        public Image Target;
        private bool _held;
        private float _next;
        private Color _base;

        public void OnPointerDown(PointerEventData e)
        {
            _held = true;
            _next = Time.unscaledTime + FirstDelay;
            if (Target != null) { _base = Target.color; Target.color = _base * 0.8f; }
            OnPress?.Invoke();
        }

        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) => Release();

        private void Release()
        {
            if (!_held) return;
            _held = false;
            if (Target != null) Target.color = _base;
        }

        private void OnDisable() => Release();

        private void Update()
        {
            if (!_held || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + RepeatEvery;
            OnPress?.Invoke();
        }
    }
}
