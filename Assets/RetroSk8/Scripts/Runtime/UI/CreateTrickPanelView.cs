using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Trick Book → CREATE-A-TRICK (Phase 27): four slots for your own tricks. Pick a flip, a board spin, a grab and
    /// a body spin, name it, put it on an air gesture (it replaces that gesture's built-in trick until you switch it
    /// OFF), and share it as an RT: code.
    /// </summary>
    public sealed class CreateTrickPanelView : MonoBehaviour
    {
        private ContentRegistry _content;
        private readonly List<Text> _slotLabels = new List<Text>();
        private readonly List<Image> _slotBgs = new List<Image>();
        private InputField _name;
        private Text _flip, _shove, _grab, _body, _gesture, _info, _status;
        private CustomTrick _edit;
        private int _slot = 1;

        public void Build(RectTransform root, ContentRegistry content, Action onClose)
        {
            _content = content;
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.97f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "CREATE-A-TRICK", 60, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -24f), new Vector2(560f, 100f));
            var sub = UIFactory.Label("Sub", root, "BUILD UP TO 4 TRICKS · PUT ONE ON AN AIR SWIPE AND IT REPLACES THAT TRICK IN YOUR RUNS (NOT IN S.K.A.T.E. OR LESSONS)", 24, Theme.Cream, TextAnchor.MiddleLeft, false);
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(660f, -30f), new Vector2(1600f, 80f));

            // Slots (left).
            var slots = UIFactory.Rect("Slots", root);
            UIFactory.Place(slots, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -150f), new Vector2(640f, 520f));
            var v = slots.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 14f;
            v.childControlWidth = v.childControlHeight = false;
            for (int s = 1; s <= CustomTricks.MaxTricks; s++)
            {
                int slot = s;
                var b = UIFactory.MakeButton("Slot" + s, slots, "", new Vector2(640f, 112f), Theme.InkSoft, () => Select(slot), 28);
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(24f, 0f);
                _slotLabels.Add(label);
                _slotBgs.Add(b.GetComponent<Image>());
            }

            // Editor (right).
            var ed = UIFactory.Panel("Editor", root, Theme.InkSoft);
            UIFactory.Place(ed.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -150f), new Vector2(1500f, 790f));
            var nameTag = UIFactory.Label("NameTag", ed.transform, "NAME", 28, Theme.Teal, TextAnchor.MiddleLeft);
            UIFactory.Place(nameTag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -24f), new Vector2(200f, 70f));
            _name = Field(ed.transform, new Vector2(250f, -24f), new Vector2(760f, 70f));
            _name.onEndEdit.AddListener(n => { _edit.name = Gallery.SafeName(n, CustomTricks.MaxNameLength, CustomTricks.SuggestName(_edit)); Refresh(); });
            var suggest = UIFactory.MakeButton("Suggest", ed.transform, "AUTO NAME", new Vector2(360f, 70f), Theme.Cream, () => { _edit.name = CustomTricks.SuggestName(_edit); Refresh(); }, 28);
            UIFactory.Place((RectTransform)suggest.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1040f, -24f), new Vector2(360f, 70f));

            _flip = Row(ed.transform, "FLIP", -112f, d => _edit.flip = Step(_edit.flip, d, CustomTricks.FlipNames.Length));
            _shove = Row(ed.transform, "BOARD", -194f, d => _edit.shove = Step(_edit.shove, d, CustomTricks.ShoveNames.Length));
            _grab = Row(ed.transform, "GRAB", -276f, d => _edit.grab = Step(_edit.grab, d, CustomTricks.GrabNames.Length));
            _body = Row(ed.transform, "BODY", -358f, d => _edit.body = Step(_edit.body, d, CustomTricks.BodyNames.Length));
            _gesture = Row(ed.transform, "SWIPE", -440f, d => _edit.gesture = StepGesture(_edit.gesture, d));

            _info = UIFactory.Label("Info", ed.transform, "", 30, Theme.Cream, TextAnchor.UpperLeft, false);
            _info.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -530f), new Vector2(1420f, 110f));

            var buttons = UIFactory.Rect("Buttons", ed.transform);
            UIFactory.Place(buttons, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1420f, 90f));
            var h = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            UIFactory.MakeButton("Save", buttons, "SAVE", new Vector2(300f, 86f), Theme.Tape, Save, 36);
            UIFactory.MakeButton("Delete", buttons, "DELETE", new Vector2(240f, 86f), Theme.Coral, Delete, 32);
            UIFactory.MakeButton("Copy", buttons, "COPY CODE", new Vector2(300f, 86f), Theme.Teal, CopyCode, 32);
            UIFactory.MakeButton("Paste", buttons, "PASTE CODE", new Vector2(300f, 86f), Theme.Cream, PasteCode, 32);

            _status = UIFactory.Label("Status", root, "", 28, Theme.Tape, TextAnchor.MiddleRight);
            UIFactory.Place(_status.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(1500f, 60f));
            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 30f), new Vector2(300f, 90f));
            Select(1);
        }

        private void OnEnable()
        {
            if (_name == null) return;
            Select(_slot);
        }

        private static InputField Field(Transform parent, Vector2 pos, Vector2 size)
        {
            var bg = UIFactory.Panel("NameField", parent, new Color(1f, 1f, 1f, 0.1f), true);
            UIFactory.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            var text = UIFactory.Label("Text", bg.transform, "", 34, Theme.Tape, TextAnchor.MiddleLeft, false);
            UIFactory.Stretch(text.rectTransform, 16f);
            var field = bg.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.characterLimit = CustomTricks.MaxNameLength;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        /// <summary>A "LABEL  &lt;  value  &gt;" row; <paramref name="change"/> gets -1 or +1.</summary>
        private Text Row(Transform parent, string label, float y, Action<int> change)
        {
            var tag = UIFactory.Label(label + "Tag", parent, label, 28, Theme.Teal, TextAnchor.MiddleLeft);
            UIFactory.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(200f, 70f));
            var prev = UIFactory.MakeButton(label + "Prev", parent, "<", new Vector2(90f, 70f), Theme.Cream, () => { change(-1); Refresh(); }, 40);
            UIFactory.Place((RectTransform)prev.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, y), new Vector2(90f, 70f));
            var value = UIFactory.Label(label + "Value", parent, "", 32, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(350f, y), new Vector2(940f, 70f));
            var next = UIFactory.MakeButton(label + "Next", parent, ">", new Vector2(90f, 70f), Theme.Cream, () => { change(1); Refresh(); }, 40);
            UIFactory.Place((RectTransform)next.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1300f, y), new Vector2(90f, 70f));
            return value;
        }

        private static int Step(int v, int d, int count) => ((v + d) % count + count) % count;

        /// <summary>Cycles OFF → 0..11 → OFF.</summary>
        private static int StepGesture(int g, int d)
        {
            int i = Step(g + 1, d, CustomTricks.GestureCount + 1);
            return i - 1;
        }

        private void Select(int slot)
        {
            _slot = slot;
            var saved = CustomTrickService.Find(slot);
            _edit = saved != null ? saved.Clone() : new CustomTrick { slot = slot, flip = (int)TrickFlip.Kick, shove = (int)TrickShove.Full, gesture = -1 };
            _edit.slot = slot;
            if (saved == null) _edit.name = CustomTricks.SuggestName(_edit);
            _status.text = "";
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < _slotLabels.Count; i++)
            {
                var t = CustomTrickService.Find(i + 1);
                _slotLabels[i].text = t == null ? $"{i + 1}.  EMPTY" : $"{i + 1}.  {t.name}\n     {CustomTricks.GestureName(t.gesture)}";
                _slotBgs[i].color = i + 1 == _slot ? Theme.Tape : Theme.InkSoft;
                _slotLabels[i].color = i + 1 == _slot ? Theme.Ink : Theme.Cream;
            }
            _name.text = _edit.name;
            _flip.text = CustomTricks.FlipNames[_edit.flip];
            _shove.text = CustomTricks.ShoveNames[_edit.shove];
            _grab.text = CustomTricks.GrabNames[_edit.grab];
            _body.text = CustomTricks.BodyNames[_edit.body];
            _gesture.text = CustomTricks.GestureName(_edit.gesture);

            var shape = CustomTricks.Shape(_edit);
            string replaces = "";
            if (_edit.gesture >= 0)
            {
                var lib = _content != null ? _content.trickLibrary : null;
                var builtIn = lib != null ? lib.GetAir(CustomTricks.FamilyOf(_edit.gesture), _edit.gesture % 4, false) : null;
                var owner = CustomTricks.ForGesture(CustomTrickService.Tricks, _edit.gesture);
                replaces = builtIn != null ? $"\nREPLACES {builtIn.displayName.ToUpperInvariant()} IN YOUR RUNS (NOT ON DAYS IT'S THE DAILY TRICK)" : "";
                if (owner != null && owner.slot != _edit.slot) replaces += $" · TAKES THIS SWIPE FROM {owner.name}";
            }
            _info.text = CustomTricks.IsEmpty(_edit)
                ? "PICK AT LEAST ONE PART."
                : $"{shape.Points:N0} POINTS · {shape.Duration:0.00} S IN THE AIR (LAND BEFORE IT FINISHES AND YOU BAIL){replaces}";
        }

        private void Save()
        {
            if (CustomTricks.IsEmpty(_edit)) { _status.text = "PICK AT LEAST ONE PART FIRST"; return; }
            _edit.name = Gallery.SafeName(_name.text, CustomTricks.MaxNameLength, CustomTricks.SuggestName(_edit));
            CustomTrickService.Save(_edit);
            _status.text = $"SAVED {_edit.name}" + (_edit.gesture < 0 ? " (OFF: PICK A SWIPE TO USE IT)" : "");
            Refresh();
        }

        private void Delete()
        {
            CustomTrickService.Delete(_slot);
            Select(_slot);
            _status.text = "SLOT CLEARED";
        }

        private void CopyCode()
        {
            if (CustomTricks.IsEmpty(_edit)) { _status.text = "NOTHING TO SHARE YET"; return; }
            ShareService.Copy(CustomTricks.Encode(_edit, SaveManager.Data.settings.playerName));
            _status.text = "TRICK CODE COPIED: SEND IT TO A FRIEND";
        }

        private void PasteCode()
        {
            if (!CustomTricks.TryDecode(ShareService.Paste(), out var trick, out var from, out var error)) { _status.text = error; return; }
            trick.slot = _slot;
            trick.gesture = _edit.gesture;
            _edit = trick;
            _status.text = (string.IsNullOrEmpty(from) ? "A FRIEND'S" : from + "'S") + $" {trick.name} · PICK A SWIPE AND SAVE";
            Refresh();
        }
    }
}
