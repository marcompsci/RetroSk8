using System;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Controls and accessibility: touch layout editor, reduced motion, larger text, colour-safe HUD, low effects.</summary>
    public sealed class AccessibilityPanelView : MonoBehaviour
    {
        private Text _motion, _text, _color, _fx, _frames;
        private GameObject _editor;
        private Action _onClose;

        public void Build(RectTransform root, Action onClose)
        {
            _onClose = onClose;
            var dim = UIFactory.Panel("Dim", root, Theme.InkSoft, true);
            UIFactory.Stretch(dim.rectTransform);
            var panel = UIFactory.Panel("Access", root, Theme.Ink, true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 900f));
            var title = UIFactory.TapeLabel("Title", panel.transform, "CONTROLS & ACCESS", 54, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 96f));

            var col = UIFactory.Rect("Rows", panel.transform);
            UIFactory.Place(col, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(900f, 640f));
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;

            UIFactory.MakeButton("TouchLayout", col, "EDIT TOUCH LAYOUT", new Vector2(820f, 100f), Theme.Tape, OpenEditor, 40);
            _motion = Toggle(col, "Motion", () => { var s = SaveManager.Data.settings; s.reducedMotion = !s.reducedMotion; });
            _text = Toggle(col, "Text", () => { var s = SaveManager.Data.settings; s.largeText = !s.largeText; });
            _color = Toggle(col, "Color", () => { var s = SaveManager.Data.settings; s.colorSafe = !s.colorSafe; });
            _fx = Toggle(col, "Fx", () => { var s = SaveManager.Data.settings; s.lowEffects = !s.lowEffects; });
            // Phase 23: frame rate / battery saver.
            _frames = Toggle(col, "Frames", () =>
            {
                var s = SaveManager.Data.settings;
                s.frameRateMode = (int)RetroSk8.Core.PowerPolicy.Next((RetroSk8.Core.FrameRateMode)s.frameRateMode);
                RetroSk8.Game.DevicePerformance.Instance?.RefreshPlan();
            });

            var note = UIFactory.Label("Note", panel.transform, "Text and colour changes apply from the next screen.", 28, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter, false);
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1000f, 40f));
            var back = UIFactory.MakeButton("Back", panel.transform, "DONE", new Vector2(360f, 100f), Theme.Tape, Close, 48);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(360f, 100f));

            var editor = UIFactory.Rect("TouchLayoutEditor", root);
            UIFactory.Stretch(editor);
            editor.gameObject.SetActive(false); // Build before first enable so OnEnable has a layout to show
            editor.gameObject.AddComponent<TouchLayoutEditorView>().Build(editor, () => _editor.SetActive(false));
            _editor = editor.gameObject;
            Refresh();
        }

        private Text Toggle(Transform parent, string name, Action flip)
        {
            Text label = null;
            var b = UIFactory.MakeButton(name, parent, "", new Vector2(820f, 80f), Theme.Teal, () => { flip(); Refresh(); }, 32);
            label = b.GetComponentInChildren<Text>();
            return label;
        }

        private void OpenEditor() => _editor.SetActive(true);

        private void Refresh()
        {
            var s = SaveManager.Data.settings;
            _motion.text = s.reducedMotion ? "REDUCED MOTION: ON" : "REDUCED MOTION: OFF";
            _text.text = s.largeText ? "LARGER TEXT: ON" : "LARGER TEXT: OFF";
            _color.text = s.colorSafe ? "COLOUR-SAFE HUD: ON" : "COLOUR-SAFE HUD: OFF";
            _fx.text = s.lowEffects ? "VISUAL EFFECTS: LOW" : "VISUAL EFFECTS: FULL";
            string why = RetroSk8.Game.DevicePerformance.Instance != null ? RetroSk8.Game.DevicePerformance.Instance.Plan.Reason : "";
            _frames.text = RetroSk8.Core.PowerPolicy.ModeName((RetroSk8.Core.FrameRateMode)s.frameRateMode)
                           + (s.frameRateMode == 0 && !string.IsNullOrEmpty(why) ? " · 30 NOW (" + why + ")" : "");
        }

        private void Close()
        {
            SaveManager.Save();
            Theme.ApplySettings(SaveManager.Data.settings);
            _onClose?.Invoke();
        }
    }
}
