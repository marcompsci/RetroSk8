using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Player;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Create-a-Skater (skin, hair, build, eyewear, shoes) and the board maker (pattern, colours, a pixel sticker
    /// and where it goes). Every change previews live on the turntable skater; SAVE keeps it.
    /// Some stickers unlock through Career chapters.
    /// </summary>
    public sealed class CreateSkaterPanelView : MonoBehaviour
    {
        private SkaterVisual _preview;
        private Action _onClose;
        private SkaterLook _look;
        private RectTransform _skaterPage, _boardPage;
        private readonly List<Action> _refreshers = new List<Action>();
        private Image _skaterTab, _boardTab;
        private RawImage _boardPreview;
        private Text _note;

        public void Build(RectTransform root, SkaterVisual preview, Action onClose)
        {
            _preview = preview;
            _onClose = onClose;

            var panel = UIFactory.Panel("SkaterPanel", root, new Color(0.07f, 0.075f, 0.09f, 0.97f), true);
            UIFactory.Place(panel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(1200f, 1000f));
            var title = UIFactory.TapeLabel("Title", panel.transform, "CREATE-A-SKATER", 52, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(30f, -10f), new Vector2(560f, 86f));

            var tabs = UIFactory.Rect("Tabs", panel.transform);
            UIFactory.Place(tabs, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(1150f, 76f));
            var tl = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tl.spacing = 12f;
            tl.childControlWidth = tl.childControlHeight = false;
            _skaterTab = UIFactory.MakeButton("TabSkater", tabs, "SKATER", new Vector2(300f, 70f), Theme.Tape, () => ShowPage(true), 32).GetComponent<Image>();
            _boardTab = UIFactory.MakeButton("TabBoard", tabs, "BOARD ART", new Vector2(300f, 70f), Theme.Cream, () => ShowPage(false), 32).GetComponent<Image>();

            _skaterPage = Page(panel.transform);
            Stepper(_skaterPage, "SKIN TONE", () => _look.skinTone, v => _look.skinTone = v, LookPalette.SkinTones.Length, i => null, i => LookPalette.SkinTones[i]);
            Stepper(_skaterPage, "HAIR", () => _look.hairStyle, v => _look.hairStyle = v, LookPalette.HairNames.Length, i => LookPalette.HairNames[i], null);
            Stepper(_skaterPage, "HAIR COLOUR", () => _look.hairColor, v => _look.hairColor = v, LookPalette.HairColors.Length, i => null, i => LookPalette.HairColors[i]);
            Stepper(_skaterPage, "BUILD", () => _look.build, v => _look.build = v, LookPalette.BuildNames.Length, i => LookPalette.BuildNames[i], null);
            Stepper(_skaterPage, "EYEWEAR", () => _look.eyewear, v => _look.eyewear = v, LookPalette.EyewearNames.Length, i => LookPalette.EyewearNames[i], null);
            Stepper(_skaterPage, "SHOES", () => _look.shoeColor, v => _look.shoeColor = v, LookPalette.Colors.Length, i => null, i => LookPalette.Colors[i]);

            _boardPage = Page(panel.transform);
            Stepper(_boardPage, "BOARD", () => _look.customBoard ? 1 : 0, v => _look.customBoard = v == 1, 2, i => i == 1 ? "MY GRAPHIC" : "SHOP DECK", null);
            Stepper(_boardPage, "PATTERN", () => _look.board.pattern, v => { _look.board.pattern = v; _look.customBoard = true; }, LookPalette.PatternNames.Length, i => LookPalette.PatternNames[i], null);
            Stepper(_boardPage, "MAIN COLOUR", () => _look.board.primary, v => { _look.board.primary = v; _look.customBoard = true; }, LookPalette.Colors.Length, i => null, i => LookPalette.Colors[i]);
            Stepper(_boardPage, "SECOND COLOUR", () => _look.board.secondary, v => { _look.board.secondary = v; _look.customBoard = true; }, LookPalette.Colors.Length, i => null, i => LookPalette.Colors[i]);
            Stepper(_boardPage, "STICKER", () => _look.board.sticker, v => { _look.board.sticker = NextUnlocked(_look.board.sticker, v); _look.customBoard = true; }, Stickers.All.Length, i => StickerLabel(i), null);
            Stepper(_boardPage, "STICKER COLOUR", () => _look.board.stickerColor, v => { _look.board.stickerColor = v; _look.customBoard = true; }, LookPalette.Colors.Length, i => null, i => LookPalette.Colors[i]);
            Stepper(_boardPage, "STICKER SPOT", () => _look.board.stickerSpot, v => { _look.board.stickerSpot = v; _look.customBoard = true; }, 3, i => LookPalette.SpotNames[i], null);

            var previewFrame = UIFactory.Panel("BoardFrame", _boardPage, Theme.Ink);
            UIFactory.Place(previewFrame.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -10f), new Vector2(150f, 540f));
            _boardPreview = UIFactory.Rect("Board", previewFrame.transform).gameObject.AddComponent<RawImage>();
            UIFactory.Stretch(_boardPreview.rectTransform, 10f);
            _boardPreview.raycastTarget = false;

            _note = UIFactory.Label("Note", panel.transform, "", 26, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleLeft, false);
            UIFactory.Place(_note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(330f, 50f), new Vector2(480f, 60f));
            _note.horizontalOverflow = HorizontalWrapMode.Wrap;

            var cancel = UIFactory.MakeButton("Cancel", panel.transform, "CANCEL", new Vector2(260f, 90f), Theme.Coral, Cancel, 38);
            UIFactory.Place((RectTransform)cancel.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(260f, 90f));
            var save = UIFactory.MakeButton("Save", panel.transform, "SAVE", new Vector2(320f, 100f), Theme.Tape, Save, 44);
            UIFactory.Place((RectTransform)save.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(320f, 100f));
        }

        /// <summary>Opens with a working copy of the saved look.</summary>
        public void Open()
        {
            _look = SaveManager.Data.look.Clone();
            gameObject.SetActive(true);
            ShowPage(true);
            RefreshAll();
        }

        private static RectTransform Page(Transform parent)
        {
            var page = UIFactory.Rect("Page", parent);
            UIFactory.Place(page, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1150f, 680f));
            var v = page.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 12f;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = v.childControlHeight = false;
            return page;
        }

        private void ShowPage(bool skater)
        {
            _skaterPage.gameObject.SetActive(skater);
            _boardPage.gameObject.SetActive(!skater);
            _skaterTab.color = skater ? Theme.Tape : Theme.Cream;
            _boardTab.color = skater ? Theme.Cream : Theme.Tape;
        }

        /// <summary>A "&lt; value &gt;" row. The value shows as a name, a colour swatch, or both.</summary>
        private void Stepper(RectTransform page, string label, Func<int> get, Action<int> set, int count, Func<int, string> name, Func<int, Rgb> color)
        {
            var row = UIFactory.Rect("Row_" + label, page);
            row.sizeDelta = new Vector2(900f, 82f);
            var l = UIFactory.Label("Label", row, label, 30, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(330f, 60f));
            var prev = (RectTransform)UIFactory.MakeButton("Prev", row, "<", new Vector2(90f, 74f), Theme.Cream, () => Step(get, set, count, -1), 44).transform;
            UIFactory.Place(prev, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(350f, 0f), new Vector2(90f, 74f));
            var valueBg = UIFactory.Panel("Value", row, new Color(1f, 1f, 1f, 0.08f));
            UIFactory.Place(valueBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(452f, 0f), new Vector2(330f, 74f));
            var swatch = UIFactory.Panel("Swatch", valueBg.transform, Color.white);
            UIFactory.Place(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(56f, 56f));
            var text = UIFactory.Label("Text", valueBg.transform, "", 28, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Stretch(text.rectTransform, 6f);
            var next = UIFactory.MakeButton("Next", row, ">", new Vector2(90f, 74f), Theme.Cream, () => Step(get, set, count, 1), 44);
            UIFactory.Place((RectTransform)next.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(794f, 0f), new Vector2(90f, 74f));

            _refreshers.Add(() =>
            {
                int v = get();
                swatch.gameObject.SetActive(color != null);
                if (color != null) { var c = color(v); swatch.color = new Color(c.R, c.G, c.B); }
                string n = name?.Invoke(v);
                text.text = n ?? $"{v + 1}/{count}";
                text.rectTransform.offsetMin = new Vector2(color != null ? 76f : 6f, 6f);
            });
        }

        private void Step(Func<int> get, Action<int> set, int count, int dir)
        {
            int v = ((get() + dir) % count + count) % count;
            set(v);
            _preview.ApplyLook(_look);
            RefreshAll();
        }

        /// <summary>Skips career-locked stickers in the direction of travel.</summary>
        private static int NextUnlocked(int from, int to)
        {
            int n = Stickers.All.Length;
            int dir = ((to - from + n) % n) == 1 ? 1 : -1;
            var career = SaveManager.Data.career;
            int v = to;
            for (int i = 0; i < n; i++)
            {
                if (career.StickerUnlocked(Stickers.Find(v))) return v;
                v = ((v + dir) % n + n) % n;
            }
            return 0;
        }

        private static string StickerLabel(int id)
        {
            var s = Stickers.Find(id);
            return s.Name;
        }

        private void RefreshAll()
        {
            foreach (var r in _refreshers) r();
            _boardPreview.texture = DeckTextures.Get(_look.board);
            int locked = 0;
            foreach (var s in Stickers.All) if (!SaveManager.Data.career.StickerUnlocked(s)) locked++;
            _note.text = locked > 0 ? $"{locked} STICKERS UNLOCK IN CAREER MODE" : "ALL STICKERS UNLOCKED";
        }

        private void Save()
        {
            SaveManager.Data.look = _look.Clone();
            SaveManager.Save();
            Close();
        }

        private void Cancel()
        {
            _preview.ApplyLook(SaveManager.Data.look);
            Close();
        }

        private void Close()
        {
            gameObject.SetActive(false);
            _onClose?.Invoke();
        }
    }
}
