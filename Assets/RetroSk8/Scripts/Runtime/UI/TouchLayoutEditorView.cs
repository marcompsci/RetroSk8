using System;
using RetroSk8.Core;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Drag the stick, JUMP and GRIND buttons wherever your thumbs like them; resize them and set stick
    /// sensitivity; one tap for a left-handed layout. Saved to settings and used by every run.
    /// </summary>
    public sealed class TouchLayoutEditorView : MonoBehaviour
    {
        private TouchLayout _layout;
        private RectTransform _area;
        private RectTransform _stick, _jump, _action;
        private Text _sensLabel, _sizeLabel;
        private Action _onClose;

        public void Build(RectTransform root, Action onClose)
        {
            _onClose = onClose;
            var bg = UIFactory.Panel("Backdrop", root, new Color(0.07f, 0.075f, 0.09f, 0.97f), true);
            UIFactory.Stretch(bg.rectTransform);
            _area = bg.rectTransform;

            var title = UIFactory.Label("Title", root, "DRAG THE CONTROLS WHERE YOUR THUMBS WANT THEM", 34, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1900f, 60f));

            _stick = Handle(root, "STEER", new Color(1f, 1f, 1f, 0.25f), 300f);
            _jump = Handle(root, "JUMP", new Color(0.95f, 0.76f, 0.19f, 0.85f), 260f);
            _action = Handle(root, "GRIND\nMANUAL", new Color(0.12f, 0.78f, 0.71f, 0.85f), 180f);

            // Centre column of controls, kept clear of the default thumb spots.
            var col = UIFactory.Rect("Controls", root);
            UIFactory.Place(col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(760f, 560f));
            var layoutGroup = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 16f;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = layoutGroup.childControlHeight = false;

            _sizeLabel = StepperRow(col, "Size", () => Nudge(ref _layout.scale, -0.05f, TouchLayout.MinScale, TouchLayout.MaxScale),
                () => Nudge(ref _layout.scale, 0.05f, TouchLayout.MinScale, TouchLayout.MaxScale));
            _sensLabel = StepperRow(col, "Sens", () => Nudge(ref _layout.sensitivity, -0.1f, TouchLayout.MinSensitivity, TouchLayout.MaxSensitivity),
                () => Nudge(ref _layout.sensitivity, 0.1f, TouchLayout.MinSensitivity, TouchLayout.MaxSensitivity));
            UIFactory.MakeButton("LeftHanded", col, "SWAP SIDES (LEFT-HANDED)", new Vector2(700f, 90f), Theme.Cream, () => { _layout.Mirror(); Refresh(); }, 34);
            UIFactory.MakeButton("Reset", col, "RESET LAYOUT", new Vector2(700f, 90f), Theme.Coral, () => { _layout = TouchLayout.Default(); Refresh(); }, 34);
            UIFactory.MakeButton("Done", col, "SAVE", new Vector2(700f, 100f), Theme.Tape, Save, 44);
        }

        private void OnEnable()
        {
            _layout = (SaveManager.Data.settings.touchLayout ?? TouchLayout.Default()).Clone();
            if (_area != null) Refresh();
        }

        private RectTransform Handle(RectTransform root, string label, Color color, float size)
        {
            var img = UIFactory.Panel("Handle_" + label, root, color, true);
            img.sprite = UIFactory.Circle;
            var t = UIFactory.Label("Label", img.transform, label, 30, Theme.Ink, TextAnchor.MiddleCenter, false);
            UIFactory.Stretch(t.rectTransform);
            var drag = img.gameObject.AddComponent<LayoutHandle>();
            drag.Init(this, label);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return img.rectTransform;
        }

        private Text StepperRow(Transform parent, string what, Action minus, Action plus)
        {
            var row = UIFactory.Rect(what + "Row", parent);
            row.sizeDelta = new Vector2(700f, 90f);
            var m = UIFactory.MakeButton("Minus", row, "-", new Vector2(110f, 90f), Theme.Cream, () => { minus(); Refresh(); }, 56);
            UIFactory.Place((RectTransform)m.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(110f, 90f));
            var p = UIFactory.MakeButton("Plus", row, "+", new Vector2(110f, 90f), Theme.Cream, () => { plus(); Refresh(); }, 56);
            UIFactory.Place((RectTransform)p.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(110f, 90f));
            var label = UIFactory.Label("Value", row, "", 36, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 90f));
            return label;
        }

        private static void Nudge(ref float v, float delta, float min, float max) => v = Mathf.Clamp(Mathf.Round((v + delta) * 100f) / 100f, min, max);

        internal void Drag(string handle, Vector2 screenPos, Camera cam)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screenPos, cam, out Vector2 local)) return;
            Vector2 f = Rect.PointToNormalized(_area.rect, local);
            switch (handle)
            {
                case "STEER": _layout.stickX = f.x; _layout.stickY = f.y; break;
                case "JUMP": _layout.jumpX = f.x; _layout.jumpY = f.y; break;
                default: _layout.actionX = f.x; _layout.actionY = f.y; break;
            }
            _layout.Clamp();
            Refresh();
        }

        private void Refresh()
        {
            Put(_stick, _layout.stickX, _layout.stickY, 300f);
            Put(_jump, _layout.jumpX, _layout.jumpY, 260f);
            Put(_action, _layout.actionX, _layout.actionY, 180f);
            _sizeLabel.text = $"BUTTON SIZE  {Mathf.RoundToInt(_layout.scale * 100f)}%";
            _sensLabel.text = $"STICK SENSITIVITY  {Mathf.RoundToInt(_layout.sensitivity * 100f)}%";
        }

        private void Put(RectTransform rt, float x, float y, float size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(x, y);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size) * _layout.scale;
        }

        private void Save()
        {
            _layout.Clamp();
            SaveManager.Data.settings.touchLayout = _layout.Clone();
            SaveManager.Save();
            _onClose?.Invoke();
        }
    }

    /// <summary>Forwards drags on one layout handle to the editor.</summary>
    public sealed class LayoutHandle : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        private TouchLayoutEditorView _editor;
        private string _id;

        public void Init(TouchLayoutEditorView editor, string id) { _editor = editor; _id = id; }
        public void OnPointerDown(PointerEventData e) => _editor.Drag(_id, e.position, e.pressEventCamera);
        public void OnDrag(PointerEventData e) => _editor.Drag(_id, e.position, e.pressEventCamera);
    }
}
