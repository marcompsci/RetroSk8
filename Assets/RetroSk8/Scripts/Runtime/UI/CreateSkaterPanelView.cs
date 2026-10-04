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
    /// Create-a-Skater: SKATER (skin, hair, build, eyewear, style), CLOTHES (shirt cut + colours, bottoms),
    /// SHOES (style, colour, soles, socks), BOARD (deck shape, wheels, trucks, grip) and BOARD ART (pattern,
    /// colours, a pixel sticker and where it goes). Every change previews live on the turntable skater; SAVE keeps it.
    /// Some stickers unlock through Career chapters.
    /// </summary>
    public sealed class CreateSkaterPanelView : MonoBehaviour
    {
        private SkaterVisual _preview;
        private Action _onClose;
        private SkaterLook _look;
        private RectTransform _skaterPage, _boardPage;
        private readonly List<Action> _refreshers = new List<Action>();
        private readonly List<(RectTransform page, Image tab)> _pages = new List<(RectTransform, Image)>();
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
            tl.spacing = 10f;
            tl.childControlWidth = tl.childControlHeight = false;

            _skaterPage = AddPage(panel.transform, tabs, "SKATER");
            Stepper(_skaterPage, "SKIN TONE", () => _look.skinTone, v => _look.skinTone = v, LookPalette.SkinTones.Length, i => null, i => LookPalette.SkinTones[i]);
            Stepper(_skaterPage, "HAIR", () => _look.hairStyle, v => _look.hairStyle = v, LookPalette.HairNames.Length, i => LookPalette.HairNames[i], null);
            Stepper(_skaterPage, "HAIR COLOUR", () => _look.hairColor, v => _look.hairColor = v, LookPalette.HairColors.Length, i => null, i => LookPalette.HairColors[i]);
            Stepper(_skaterPage, "BUILD", () => _look.build, v => _look.build = v, LookPalette.BuildNames.Length, i => LookPalette.BuildNames[i], null);
            Stepper(_skaterPage, "EYEWEAR", () => _look.eyewear, v => _look.eyewear = v, LookPalette.EyewearNames.Length, i => LookPalette.EyewearNames[i], null);
            Stepper(_skaterPage, "STYLE", () => _look.style, v => _look.style = v, StyleTricks.StyleNames.Length,
                i => StyleTricks.StyleNames[i] + ": " + StyleTricks.Signature(i).Name.ToUpperInvariant(), null);

            // Clothes: the cut always applies; colours left on SHOP use the equipped shop gear.
            var clothes = AddPage(panel.transform, tabs, "CLOTHES");
            Stepper(clothes, "SHIRT", () => _look.shirtStyle, v => _look.shirtStyle = v, LookPalette.ShirtNames.Length, i => LookPalette.ShirtNames[i], null);
            ColorChoice(clothes, "SHIRT COLOUR", () => _look.shirtColor, v => _look.shirtColor = v, "SHOP SHIRT");
            ColorChoice(clothes, "SHIRT TRIM", () => _look.shirtTrim, v => _look.shirtTrim = v, "SHOP TRIM");
            Stepper(clothes, "BOTTOMS", () => _look.bottomsStyle, v => _look.bottomsStyle = v, LookPalette.BottomsNames.Length, i => LookPalette.BottomsNames[i], null);
            ColorChoice(clothes, "BOTTOMS COLOUR", () => _look.bottomsColor, v => _look.bottomsColor = v, "SHOP PANTS");

            var shoes = AddPage(panel.transform, tabs, "SHOES");
            Stepper(shoes, "SHOES", () => _look.shoeStyle, v => _look.shoeStyle = v, LookPalette.ShoeNames.Length, i => LookPalette.ShoeNames[i], null);
            Stepper(shoes, "SHOE COLOUR", () => _look.shoeColor, v => _look.shoeColor = v, LookPalette.Colors.Length, i => null, i => LookPalette.Colors[i]);
            ColorChoice(shoes, "SOLES", () => _look.soleColor, v => _look.soleColor = v, "CLASSIC");
            ColorChoice(shoes, "SOCKS", () => _look.sockColor, v => _look.sockColor = v, "CLASSIC");

            var parts = AddPage(panel.transform, tabs, "BOARD");
            Stepper(parts, "DECK", () => _look.customBoard ? 1 : 0, v => _look.customBoard = v == 1, 2, i => i == 1 ? "MY GRAPHIC" : "SHOP DECK", null);
            Stepper(parts, "SHAPE", () => _look.deckShape, v => _look.deckShape = v, LookPalette.ShapeNames.Length, i => LookPalette.ShapeNames[i], null);
            ColorChoice(parts, "WHEELS", () => _look.wheelColor, v => _look.wheelColor = v, "SHOP WHEELS");
            ColorChoice(parts, "TRUCKS", () => _look.truckColor, v => _look.truckColor = v, "STEEL");
            ColorChoice(parts, "GRIP TAPE", () => _look.gripColor, v => _look.gripColor = v, "SHOP GRIP");

            _boardPage = AddPage(panel.transform, tabs, "BOARD ART");
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
            ShowPage(_skaterPage);
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

        private RectTransform AddPage(Transform panel, RectTransform tabs, string name)
        {
            var page = Page(panel);
            var tab = UIFactory.MakeButton("Tab" + name, tabs, name, new Vector2(222f, 70f), Theme.Cream, () => ShowPage(page), 28).GetComponent<Image>();
            _pages.Add((page, tab));
            return page;
        }

        private void ShowPage(RectTransform show)
        {
            foreach (var (page, tab) in _pages)
            {
                page.gameObject.SetActive(page == show);
                tab.color = page == show ? Theme.Tape : Theme.Cream;
            }
        }

        /// <summary>A colour row whose first choice is "shop gear / default" (saved as 0; colours are 1..n).</summary>
        private void ColorChoice(RectTransform page, string label, Func<int> get, Action<int> set, string defaultName)
        {
            var grey = new Rgb(0.35f, 0.36f, 0.4f);
            Stepper(page, label, get, set, LookPalette.Colors.Length + 1, i => i == 0 ? defaultName : null, i => i == 0 ? grey : LookPalette.Colors[i - 1]);
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
                if (Stickers.Find(v).UnlockedFor(career.ChaptersCompleted(), SaveManager.Data.crew.Level)) return v;
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
            int chapters = SaveManager.Data.career.ChaptersCompleted(), crewLevel = SaveManager.Data.crew.Level;
            foreach (var s in Stickers.All) if (!s.UnlockedFor(chapters, crewLevel)) locked++;
            _note.text = locked > 0 ? $"{locked} STICKERS UNLOCK THROUGH CAREER AND CREW LEVELS" : "ALL STICKERS UNLOCKED";
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
