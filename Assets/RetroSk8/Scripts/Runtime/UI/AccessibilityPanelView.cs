using System;
using RetroSk8.Core;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Controls and accessibility: touch layout editor, reduced motion, larger text, colour-vision palette, low effects,
    /// frame rate; and (Phase 26) the ASSISTS column: game speed, balance assist, landing window.
    /// </summary>
    public sealed class AccessibilityPanelView : MonoBehaviour
    {
        private Text _motion, _text, _color, _fx, _frames;
        private Text _speed, _balance, _landing, _assistNote;
        private Image _swatchBad, _swatchGood, _swatchAccent;
        private GameObject _editor;
        private Action _onClose;

        public void Build(RectTransform root, Action onClose)
        {
            _onClose = onClose;
            var dim = UIFactory.Panel("Dim", root, Theme.InkSoft, true);
            UIFactory.Stretch(dim.rectTransform);
            var panel = UIFactory.Panel("Access", root, Theme.Ink, true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1900f, 960f));
            var title = UIFactory.TapeLabel("Title", panel.transform, "CONTROLS & ACCESS", 54, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 96f));

            // Left: controls and accessibility.
            var left = Column(panel.transform, "Rows", -470f, "ACCESSIBILITY");
            UIFactory.MakeButton("TouchLayout", left, "EDIT TOUCH LAYOUT", new Vector2(820f, 90f), Theme.Tape, OpenEditor, 38);
            _motion = Toggle(left, "Motion", () => { var s = SaveManager.Data.settings; s.reducedMotion = !s.reducedMotion; });
            _text = Toggle(left, "Text", () => { var s = SaveManager.Data.settings; s.largeText = !s.largeText; });
            _color = Toggle(left, "Color", () =>
            {
                var s = SaveManager.Data.settings;
                s.colorVision = (int)VisionPalette.Next(VisionPalette.FromSaved(s.colorVision));
            });
            // Phase 26: a live preview of the palette's fail / success / highlight colours.
            var swatches = UIFactory.Rect("Swatches", left);
            swatches.sizeDelta = new Vector2(820f, 44f);
            _swatchBad = Swatch(swatches, "Bad", -260f, "MISSED");
            _swatchGood = Swatch(swatches, "Good", 0f, "LANDED");
            _swatchAccent = Swatch(swatches, "Accent", 260f, "TAPE");
            _fx = Toggle(left, "Fx", () => { var s = SaveManager.Data.settings; s.lowEffects = !s.lowEffects; });
            // Phase 23: frame rate / battery saver.
            _frames = Toggle(left, "Frames", () =>
            {
                var s = SaveManager.Data.settings;
                s.frameRateMode = (int)RetroSk8.Core.PowerPolicy.Next((RetroSk8.Core.FrameRateMode)s.frameRateMode);
                RetroSk8.Game.DevicePerformance.Instance?.RefreshPlan();
            });

            // Right: Phase 26 difficulty assists.
            var right = Column(panel.transform, "Assists", 470f, "ASSISTS");
            _speed = Toggle(right, "Speed", () => { var s = SaveManager.Data.settings; s.gameSpeed = Assists.Next(s.gameSpeed, Assists.MaxSpeed); });
            _balance = Toggle(right, "Balance", () => { var s = SaveManager.Data.settings; s.balanceAssist = Assists.Next(s.balanceAssist, Assists.MaxBalance); });
            _landing = Toggle(right, "Landing", () => { var s = SaveManager.Data.settings; s.landingAssist = Assists.Next(s.landingAssist, Assists.MaxLanding); });
            _assistNote = UIFactory.Label("AssistNote", right, "", 28, Theme.Cream, TextAnchor.UpperLeft, false);
            _assistNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            _assistNote.verticalOverflow = VerticalWrapMode.Truncate;
            _assistNote.resizeTextForBestFit = true; // Larger Text would otherwise push it into the footer
            _assistNote.resizeTextMaxSize = _assistNote.fontSize;
            _assistNote.resizeTextMinSize = 20;
            _assistNote.rectTransform.sizeDelta = new Vector2(820f, 300f);

            var note = UIFactory.Label("Note", panel.transform, "Text and colour changes apply from the next screen; assists from the next run.", 28, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter, false);
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1700f, 40f));
            var back = UIFactory.MakeButton("Back", panel.transform, "DONE", new Vector2(360f, 100f), Theme.Tape, Close, 48);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(360f, 100f));

            var editor = UIFactory.Rect("TouchLayoutEditor", root);
            UIFactory.Stretch(editor);
            editor.gameObject.SetActive(false); // Build before first enable so OnEnable has a layout to show
            editor.gameObject.AddComponent<TouchLayoutEditorView>().Build(editor, () => _editor.SetActive(false));
            _editor = editor.gameObject;
            Refresh();
        }

        private static RectTransform Column(Transform panel, string name, float x, string heading)
        {
            var head = UIFactory.Label(name + "Heading", panel, heading, 36, Theme.Teal, TextAnchor.MiddleLeft);
            UIFactory.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -96f), new Vector2(820f, 50f));
            var col = UIFactory.Rect(name, panel);
            UIFactory.Place(col, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -150f), new Vector2(820f, 620f));
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            return col;
        }

        private static Image Swatch(RectTransform row, string name, float x, string label)
        {
            var chip = UIFactory.Panel(name, row, Color.white);
            UIFactory.Place(chip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x - 70f, 0f), new Vector2(40f, 40f));
            var text = UIFactory.Label(name + "Label", row, label, 26, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x + 30f, 0f), new Vector2(150f, 40f));
            return chip;
        }

        private Text Toggle(Transform parent, string name, Action flip)
        {
            Text label = null;
            var b = UIFactory.MakeButton(name, parent, "", new Vector2(820f, 76f), Theme.Teal, () => { flip(); Refresh(); }, 32);
            label = b.GetComponentInChildren<Text>();
            return label;
        }

        private void OpenEditor() => _editor.SetActive(true);

        private void Refresh()
        {
            var s = SaveManager.Data.settings;
            _motion.text = s.reducedMotion ? "REDUCED MOTION: ON" : "REDUCED MOTION: OFF";
            _text.text = s.largeText ? "LARGER TEXT: ON" : "LARGER TEXT: OFF";
            var vision = VisionPalette.FromSaved(s.colorVision);
            _color.text = VisionPalette.Name(vision);
            var colors = VisionPalette.For(vision);
            _swatchBad.color = new Color(colors.Bad.R, colors.Bad.G, colors.Bad.B);
            _swatchGood.color = new Color(colors.Good.R, colors.Good.G, colors.Good.B);
            _swatchAccent.color = new Color(colors.Accent.R, colors.Accent.G, colors.Accent.B);
            _fx.text = s.lowEffects ? "VISUAL EFFECTS: LOW" : "VISUAL EFFECTS: FULL";
            string why = RetroSk8.Game.DevicePerformance.Instance != null ? RetroSk8.Game.DevicePerformance.Instance.Plan.Reason : "";
            _frames.text = RetroSk8.Core.PowerPolicy.ModeName((RetroSk8.Core.FrameRateMode)s.frameRateMode)
                           + (s.frameRateMode == 0 && !string.IsNullOrEmpty(why) ? " · 30 NOW (" + why + ")" : "");
            _speed.text = Assists.SpeedName(s.gameSpeed);
            _balance.text = Assists.BalanceName(s.balanceAssist);
            _landing.text = Assists.LandingName(s.landingAssist);
            _assistNote.text = (s.Assists.Any
                ? "ASSISTS ON. RUNS STILL EARN TOKENS, STORY STEPS AND ACHIEVEMENTS, BUT STAY OFF THE GAME CENTER LEADERBOARDS."
                : "ALL OFF. TURN ANY ON TO MAKE THE GAME MORE FORGIVING.")
                + "\n\nSTEADY CALMS THE GRIND AND MANUAL METER; AUTO KEEPS IT LEVEL UNLESS YOU STEER OFF. NOT USED IN ONLINE S.K.A.T.E.";
        }

        private void Close()
        {
            SaveManager.Save();
            Theme.ApplySettings(SaveManager.Data.settings);
            _onClose?.Invoke();
        }
    }
}
