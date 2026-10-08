using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Deck designer (Phase 28), opened from Create-a-Skater's BOARD ART page. The board lies across the top of the
    /// screen (tail on the left, nose on the right) with the grip tape under it. Four tabs:
    ///   BASE      the pattern, its two colours and the classic sticker;
    ///   STICKERS  up to four free stickers: pick, colour, move, size, turn, mirror (tap the board to drop one there);
    ///   PAINT     draw pixels in four colours with a pen or fill (drag on the board), erase, clear;
    ///   GRIP      a grip tape pattern in two colours.
    /// UNDO steps back, RANDOM rolls a new board, COPY / PASTE CODE share designs as RD: codes. Edits go to the
    /// Create-a-Skater working copy, so its SAVE / CANCEL still decide.
    /// </summary>
    public sealed class DeckDesignerView : MonoBehaviour
    {
        // Sized so the board and the 820-wide tools fit side by side in the narrowest safe area (about 2,000 units on
        // a notched iPhone in landscape).
        private const float BoardW = 1056f, BoardH = 264f;
        private const int MaxUndo = 30;

        private SkaterLook _look;
        private Action _changed;
        private RawImage _board, _grip;
        private Texture2D _boardTex, _gripTex;
        private Text _status, _layerInfo;
        private readonly List<(RectTransform page, Image tab)> _pages = new List<(RectTransform, Image)>();
        private RectTransform _stickerPage, _paintPage;
        private readonly List<Action> _refreshers = new List<Action>();
        private readonly List<(BoardArt art, GripArt grip, bool board, bool grip2)> _undo = new List<(BoardArt, GripArt, bool, bool)>();
        private int _layer;            // selected free sticker
        private int _brush = 1;        // 1-4 paint slot, 0 = eraser
        private bool _fill;
        private bool _strokeOpen;
        private float _clearArmed;

        public void Build(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 1f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "DECK DESIGNER", 52, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(40f, -60f), new Vector2(520f, 86f));

            // The board and the grip, drawn from the same 16 x 64 grids the game uses.
            var frame = UIFactory.Panel("BoardFrame", root, Theme.Ink);
            UIFactory.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -120f), new Vector2(BoardW + 24f, BoardH + 24f));
            _board = UIFactory.Rect("Board", frame.transform).gameObject.AddComponent<RawImage>();
            UIFactory.Place(_board.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BoardW, BoardH));
            _board.raycastTarget = true;
            _board.gameObject.AddComponent<BoardPointer>().Init(this);
            var tailLabel = UIFactory.Label("Tail", root, "TAIL", 24, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleLeft, false);
            UIFactory.Place(tailLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -BoardH - 150f), new Vector2(200f, 36f));
            var noseLabel = UIFactory.Label("Nose", root, "NOSE", 24, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleRight, false);
            UIFactory.Place(noseLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(BoardW - 180f, -BoardH - 150f), new Vector2(240f, 36f));

            var gripFrame = UIFactory.Panel("GripFrame", root, Theme.Ink);
            UIFactory.Place(gripFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -BoardH - 200f), new Vector2(BoardW * 0.75f + 16f, BoardH * 0.75f + 16f));
            _grip = UIFactory.Rect("Grip", gripFrame.transform).gameObject.AddComponent<RawImage>();
            UIFactory.Stretch(_grip.rectTransform, 8f);
            _grip.raycastTarget = false;
            var gripLabel = UIFactory.Label("GripLabel", root, "GRIP TAPE", 24, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperLeft, false);
            UIFactory.Place(gripLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(BoardW * 0.75f + 76f, -BoardH - 205f), new Vector2(240f, 36f));

            _boardTex = new Texture2D(DeckDesign.Height, DeckDesign.Width, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "DesignerBoard" };
            _gripTex = new Texture2D(DeckDesign.Height, DeckDesign.Width, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "DesignerGrip" };
            _board.texture = _boardTex;
            _grip.texture = _gripTex;

            // Tools on the right.
            var tabs = UIFactory.Rect("Tabs", root);
            UIFactory.Place(tabs, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(820f, 70f));
            var tl = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tl.spacing = 8f;
            tl.childControlWidth = tl.childControlHeight = false;
            tl.childAlignment = TextAnchor.MiddleRight;

            var basePage = AddPage(root, tabs, "BASE");
            Stepper(basePage, "PATTERN", () => _look.board.pattern, v => _look.board.pattern = v, LookPalette.PatternNames.Length, i => LookPalette.PatternNames[i], null);
            Stepper(basePage, "MAIN COLOUR", () => _look.board.primary, v => _look.board.primary = v, LookPalette.Colors.Length, null, i => LookPalette.Colors[i]);
            Stepper(basePage, "SECOND COLOUR", () => _look.board.secondary, v => _look.board.secondary = v, LookPalette.Colors.Length, null, i => LookPalette.Colors[i]);
            Stepper(basePage, "STICKER", () => _look.board.sticker, v => _look.board.sticker = NextUnlocked(_look.board.sticker, v), Stickers.All.Length, i => Stickers.Find(i).Name, null);
            Stepper(basePage, "STICKER COLOUR", () => _look.board.stickerColor, v => _look.board.stickerColor = v, LookPalette.Colors.Length, null, i => LookPalette.Colors[i]);
            Stepper(basePage, "STICKER SPOT", () => _look.board.stickerSpot, v => _look.board.stickerSpot = v, 3, i => LookPalette.SpotNames[i], null);

            _stickerPage = AddPage(root, tabs, "STICKERS");
            _layerInfo = UIFactory.Label("LayerInfo", _stickerPage, "", 28, Theme.Tape, TextAnchor.MiddleLeft, false);
            _layerInfo.rectTransform.sizeDelta = new Vector2(800f, 50f);
            var layerRow = Row(_stickerPage, 76f);
            SmallButton(layerRow, "< LAYER", 0f, 190f, () => { _layer = Mathf.Max(0, _layer - 1); RefreshAll(); });
            SmallButton(layerRow, "LAYER >", 200f, 190f, () => { _layer = Mathf.Min(Mathf.Max(0, Layers.Count - 1), _layer + 1); RefreshAll(); });
            SmallButton(layerRow, "+ ADD", 410f, 180f, AddLayer);
            SmallButton(layerRow, "REMOVE", 600f, 200f, RemoveLayer);
            Stepper(_stickerPage, "STICKER", () => Current?.sticker ?? 0, v => { if (Current != null) Current.sticker = NextUnlocked(Current.sticker, v, true); }, Stickers.All.Length, i => Stickers.Find(i).Name, null);
            Stepper(_stickerPage, "COLOUR", () => Current?.color ?? 0, v => { if (Current != null) Current.color = v; }, LookPalette.Colors.Length, null, i => LookPalette.Colors[i]);
            Stepper(_stickerPage, "SIZE", () => Current != null && Current.big ? 1 : 0, v => { if (Current != null && (v == 1) != Current.big) DeckDesign.ToggleBig(Current); }, 2, i => i == 1 ? "BIG" : "SMALL", null);
            Stepper(_stickerPage, "TURN", () => Current?.rot ?? 0, v => { if (Current != null) Current.rot = v; }, 4, i => (i * 90) + "°", null);
            Stepper(_stickerPage, "MIRROR", () => Current != null && Current.flip ? 1 : 0, v => { if (Current != null) Current.flip = v == 1; }, 2, i => i == 1 ? "ON" : "OFF", null);
            var move = Row(_stickerPage, 76f);
            UIFactory.Label("MoveLabel", move, "MOVE", 30, Theme.Cream, TextAnchor.MiddleLeft).rectTransform.sizeDelta = new Vector2(150f, 60f);
            SmallButton(move, "◄", 180f, 120f, () => MoveLayer(0, -2));
            SmallButton(move, "▲", 310f, 120f, () => MoveLayer(1, 0));
            SmallButton(move, "▼", 440f, 120f, () => MoveLayer(-1, 0));
            SmallButton(move, "►", 570f, 120f, () => MoveLayer(0, 2));

            _paintPage = AddPage(root, tabs, "PAINT");
            var brushes = Row(_paintPage, 96f);
            for (int b = 0; b <= DeckDesign.PaintColors; b++)
            {
                int slot = b;
                var btn = UIFactory.MakeButton("Brush" + b, brushes, b == 0 ? "ERASE" : "", new Vector2(130f, 86f), Theme.Cream, () => { _brush = slot; RefreshAll(); }, 26);
                UIFactory.Place((RectTransform)btn.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(b * 145f, 0f), new Vector2(130f, 86f));
                var img = btn.GetComponent<Image>();
                _refreshers.Add(() =>
                {
                    Color c = slot == 0 ? Theme.Cream : ToColor(LookPalette.Colors[DeckDesign.PaintColor(_look.board, slot - 1)]);
                    img.color = c;
                    btn.transform.localScale = _brush == slot ? Vector3.one * 1.08f : Vector3.one;
                    var o = btn.GetComponent<Outline>();
                    if (o == null) o = btn.gameObject.AddComponent<Outline>();
                    o.effectColor = Theme.Tape;
                    o.effectDistance = new Vector2(6f, -6f);
                    o.enabled = _brush == slot;
                });
            }
            Stepper(_paintPage, "BRUSH COLOUR", () => _brush > 0 ? DeckDesign.PaintColor(_look.board, _brush - 1) : 0,
                v => { if (_brush > 0) _look.board.paintColors[_brush - 1] = v; }, LookPalette.Colors.Length, null, i => LookPalette.Colors[i]);
            Stepper(_paintPage, "TOOL", () => _fill ? 1 : 0, v => _fill = v == 1, 2, i => i == 1 ? "FILL" : "PEN", null, false, false);
            var clearRow = Row(_paintPage, 86f);
            SmallButton(clearRow, "CLEAR PAINT", 0f, 330f, ClearPaint);
            var tip = UIFactory.Label("PaintTip", _paintPage, "DRAG ON THE BOARD TO PAINT", 26, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleLeft, false);
            tip.rectTransform.sizeDelta = new Vector2(800f, 50f);

            var gripPage = AddPage(root, tabs, "GRIP");
            Stepper(gripPage, "GRIP TAPE", () => _look.customGrip ? 1 : 0, v => _look.customGrip = v == 1, 2, i => i == 1 ? "MY DESIGN" : "PLAIN", null, true, false);
            Stepper(gripPage, "PATTERN", () => _look.grip.pattern, v => { _look.grip.pattern = v; _look.customGrip = true; }, DeckDesign.GripNames.Length, i => DeckDesign.GripNames[i], null, true, false);
            Stepper(gripPage, "TAPE COLOUR", () => _look.grip.color, v => { _look.grip.color = v; _look.customGrip = true; }, LookPalette.Colors.Length, null, i => LookPalette.Colors[i], true, false);
            Stepper(gripPage, "PATTERN COLOUR", () => _look.grip.accent, v => { _look.grip.accent = v; _look.customGrip = true; }, LookPalette.Colors.Length, null, i => LookPalette.Colors[i], true, false);

            // Bottom bar.
            _status = UIFactory.Label("Status", root, "", 28, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleLeft, false);
            UIFactory.Place(_status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 150f), new Vector2(BoardW, 50f));
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            var bar = UIFactory.Rect("Bar", root);
            UIFactory.Place(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1960f, 100f));
            var bl = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            bl.spacing = 24f;
            bl.childAlignment = TextAnchor.MiddleCenter;
            bl.childControlWidth = bl.childControlHeight = false;
            UIFactory.MakeButton("Undo", bar, "UNDO", new Vector2(240f, 96f), Theme.Cream, Undo, 38);
            UIFactory.MakeButton("Random", bar, "RANDOM", new Vector2(280f, 96f), Theme.Cream, Randomise, 38);
            UIFactory.MakeButton("Copy", bar, "COPY CODE", new Vector2(340f, 96f), Theme.Teal, CopyCode, 38);
            UIFactory.MakeButton("Paste", bar, "PASTE CODE", new Vector2(340f, 96f), Theme.Teal, PasteCode, 38);
            UIFactory.MakeButton("Done", bar, "DONE", new Vector2(300f, 100f), Theme.Tape, Close, 44);
        }

        /// <summary>Opens on <paramref name="look"/> (Create-a-Skater's working copy); <paramref name="changed"/> runs after every edit.</summary>
        public void Open(SkaterLook look, Action changed)
        {
            _look = look;
            _changed = changed;
            transform.SetAsLastSibling(); // over Create-a-Skater's buttons and note
            if (_look.grip == null) _look.grip = new GripArt();
            _look.board.Sanitize();
            _undo.Clear();
            _layer = 0;
            _status.text = "";
            gameObject.SetActive(true);
            ShowPage(_pages[0].page);
            RefreshAll();
        }

        private void Close()
        {
            gameObject.SetActive(false);
            _changed?.Invoke();
        }

        private void Update()
        {
            if (_clearArmed > 0f) _clearArmed -= Time.unscaledDeltaTime;
        }

        // ---------------------------------------------------------------- layers

        private List<StickerLayer> Layers => _look.board.layers;
        private StickerLayer Current => _layer >= 0 && _layer < Layers.Count ? Layers[_layer] : null;

        private void AddLayer()
        {
            if (Layers.Count >= DeckDesign.MaxLayers) { _status.text = $"UP TO {DeckDesign.MaxLayers} FREE STICKERS"; return; }
            Remember();
            Layers.Add(new StickerLayer { sticker = 1, color = 2, x = 4, y = 4 + Layers.Count * 14 });
            DeckDesign.Sanitize(_look.board);
            _layer = Layers.Count - 1;
            Edited();
        }

        private void RemoveLayer()
        {
            if (Current == null) return;
            Remember();
            Layers.RemoveAt(_layer);
            _layer = Mathf.Clamp(_layer, 0, Mathf.Max(0, Layers.Count - 1));
            Edited();
        }

        /// <param name="along">+ toward the nose.</param>
        /// <param name="across">+ toward the top edge in the preview.</param>
        private void MoveLayer(int across, int along)
        {
            if (Current == null) return;
            Remember();
            DeckDesign.Move(Current, across, along);
            Edited();
        }

        // ---------------------------------------------------------------- painting and tapping the board

        /// <summary>A touch on the board preview at (u, v) in 0-1 (u along the board from the tail, v across from the bottom).</summary>
        internal void OnBoardTouch(Vector2 uv, bool begin)
        {
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.x * DeckDesign.Height), 0, DeckDesign.Height - 1);
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.y * DeckDesign.Width), 0, DeckDesign.Width - 1);
            if (_paintPage.gameObject.activeSelf)
            {
                if (begin) { Remember(); _strokeOpen = true; }
                if (!_strokeOpen) return;
                bool changed = _fill ? begin && DeckDesign.Fill(_look.board, x, y, _brush) > 0 : DeckDesign.Paint(_look.board, x, y, _brush);
                if (changed) Edited(false);
            }
            else if (_stickerPage.gameObject.activeSelf && begin)
            {
                if (Current == null) AddLayer();
                if (Current == null) return;
                Remember();
                Current.x = x - Current.Size / 2;
                Current.y = y - Current.Size / 2;
                DeckDesign.Move(Current, 0, 0);
                Edited();
            }
        }

        internal void OnBoardRelease() => _strokeOpen = false;

        private void ClearPaint()
        {
            if (DeckDesign.PaintedCount(_look.board) == 0) { _status.text = "NOTHING PAINTED YET"; return; }
            if (_clearArmed <= 0f) { _clearArmed = 2.5f; _status.text = "TAP CLEAR PAINT AGAIN TO WIPE IT"; return; }
            _clearArmed = 0f;
            Remember();
            DeckDesign.ClearPaint(_look.board);
            _status.text = "PAINT CLEARED";
            Edited();
        }

        // ---------------------------------------------------------------- undo, random, codes

        private void Remember()
        {
            _undo.Add((_look.board.Clone(), _look.grip.Clone(), _look.customBoard, _look.customGrip));
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        }

        private void Undo()
        {
            if (_undo.Count == 0) { _status.text = "NOTHING TO UNDO"; return; }
            var last = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _look.board = last.art;
            _look.grip = last.grip;
            _look.customBoard = last.board;
            _look.customGrip = last.grip2;
            _layer = Mathf.Clamp(_layer, 0, Mathf.Max(0, Layers.Count - 1));
            Edited(boardEdit: false);
        }

        private void Randomise()
        {
            Remember();
            var r = new System.Random(Environment.TickCount);
            int colors = LookPalette.Colors.Length;
            var b = _look.board;
            b.pattern = r.Next(LookPalette.PatternNames.Length);
            b.primary = r.Next(colors);
            do b.secondary = r.Next(colors); while (b.secondary == b.primary);
            b.sticker = NextUnlocked(0, r.Next(1, Stickers.All.Length), true);
            b.stickerColor = r.Next(colors);
            b.stickerSpot = r.Next(3);
            b.layers.Clear();
            int n = r.Next(0, 3);
            for (int i = 0; i < n; i++)
                b.layers.Add(new StickerLayer { sticker = NextUnlocked(0, r.Next(1, Stickers.All.Length), true), color = r.Next(colors), x = r.Next(0, 9), y = r.Next(0, 57), big = r.Next(4) == 0, rot = r.Next(4), flip = r.Next(2) == 0 });
            DeckDesign.Sanitize(b);
            _look.grip.pattern = r.Next(DeckDesign.GripNames.Length);
            _look.grip.color = r.Next(2) == 0 ? 0 : r.Next(colors);
            _look.grip.accent = r.Next(colors);
            _layer = 0;
            _status.text = "ROLLED A NEW BOARD (UNDO TO GO BACK)";
            Edited();
        }

        private void CopyCode()
        {
            ShareService.Copy(DeckDesign.Encode(_look.board, _look.customGrip ? _look.grip : null, SaveManager.Data.settings.playerName));
            _status.text = "BOARD CODE COPIED: SEND IT TO A FRIEND";
        }

        private void PasteCode()
        {
            if (!DeckDesign.TryDecode(ShareService.Paste(), out var art, out var grip, out var from, out var error)) { _status.text = error; return; }
            Remember();
            int locked = DeckDesign.ReplaceLocked(art, Unlocked);
            _look.board = art;
            if (grip != null) { _look.grip = grip; _look.customGrip = true; }
            _look.customBoard = true;
            _layer = 0;
            string who = string.IsNullOrEmpty(from) ? "A FRIEND'S" : from + "'S";
            _status.text = locked > 0 ? $"{who} BOARD · {locked} LOCKED STICKER(S) SWAPPED FOR STARS" : $"{who} BOARD LOADED · SAVE TO KEEP IT";
            Edited();
        }

        // ---------------------------------------------------------------- stickers you've unlocked

        private static bool Unlocked(Sticker s) =>
            s.UnlockedFor(SaveManager.Data.career.ChaptersCompleted(), SaveManager.Data.crew.Level, SaveManager.Data.nightLines.Count);

        /// <summary>Skips locked stickers in the direction of travel (and NONE for free stickers).</summary>
        private static int NextUnlocked(int from, int to, bool skipNone = false)
        {
            int n = Stickers.All.Length;
            int dir = ((to - from + n) % n) == 1 ? 1 : -1;
            int v = to;
            for (int i = 0; i < n; i++)
            {
                if (!(skipNone && v == 0) && Unlocked(Stickers.Find(v))) return v;
                v = ((v + dir) % n + n) % n;
            }
            return skipNone ? 1 : 0;
        }

        // ---------------------------------------------------------------- drawing

        /// <summary>
        /// After a change: the board graphic is now yours (MY GRAPHIC). The skater behind the designer is only updated
        /// on DONE, so a long painting session doesn't make a texture per stroke.
        /// </summary>
        private void Edited(bool notify = true, bool boardEdit = true)
        {
            if (boardEdit) _look.customBoard = true;
            DeckDesign.Sanitize(_look.board);
            RefreshAll();
        }

        private void RefreshAll()
        {
            foreach (var r in _refreshers) r();
            DrawBoard();
            int locked = 0;
            foreach (var s in Stickers.All) if (!Unlocked(s)) locked++;
            _layerInfo.text = Current == null
                ? $"NO FREE STICKERS YET · TAP + ADD OR THE BOARD ({Layers.Count}/{DeckDesign.MaxLayers})"
                : $"LAYER {_layer + 1} OF {Layers.Count} · TAP THE BOARD TO PLACE IT";
            if (string.IsNullOrEmpty(_status.text) && locked > 0) _status.text = $"{locked} STICKERS UNLOCK THROUGH CAREER, CREW AND NIGHT LINES";
        }

        private void DrawBoard()
        {
            var px = new Color[DeckDesign.Height * DeckDesign.Width];
            var gx = new Color[px.Length];
            bool customGrip = _look.customGrip;
            Color plainGrip = SkaterLook.Pick(_look.gripColor, out var gc) ? ToColor(gc) : Theme.Ink;
            for (int y = 0; y < DeckDesign.Height; y++)
            for (int x = 0; x < DeckDesign.Width; x++)
            {
                int i = x * DeckDesign.Height + y; // texture is 64 wide (along) x 16 tall (across)
                px[i] = ToColor(LookPalette.Colors[DeckDesign.ColorAt(_look.board, x, y)]);
                gx[i] = customGrip ? ToColor(LookPalette.Colors[DeckDesign.GripColorAt(_look.grip, x, y)]) : plainGrip;
            }
            // Outline the selected free sticker while its tab is open.
            if (_stickerPage.gameObject.activeSelf && Current != null)
            {
                var l = Current;
                for (int k = 0; k < l.Size; k++)
                {
                    Mark(px, l.x + k, l.y); Mark(px, l.x + k, l.y + l.Size - 1);
                    Mark(px, l.x, l.y + k); Mark(px, l.x + l.Size - 1, l.y + k);
                }
            }
            _boardTex.SetPixels(px);
            _boardTex.Apply();
            _gripTex.SetPixels(gx);
            _gripTex.Apply();
        }

        private static void Mark(Color[] px, int x, int y)
        {
            if (x < 0 || y < 0 || x >= DeckDesign.Width || y >= DeckDesign.Height) return;
            int i = x * DeckDesign.Height + y;
            var c = px[i];
            px[i] = Color.Lerp(c, (c.r + c.g + c.b) > 1.5f ? Color.black : Color.white, 0.55f);
        }

        private static Color ToColor(Rgb c) => new Color(c.R, c.G, c.B, 1f);

        // ---------------------------------------------------------------- layout helpers

        private RectTransform AddPage(Transform root, RectTransform tabs, string name)
        {
            var page = UIFactory.Rect("Page_" + name, root);
            UIFactory.Place(page, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -120f), new Vector2(820f, 760f));
            var v = page.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10f;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = v.childControlHeight = false;
            var tab = UIFactory.MakeButton("Tab" + name, tabs, name, new Vector2(196f, 66f), Theme.Cream, () => ShowPage(page), 26).GetComponent<Image>();
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
            _strokeOpen = false;
            if (_look != null) DrawBoard();
        }

        private static RectTransform Row(RectTransform page, float height)
        {
            var row = UIFactory.Rect("Row", page);
            row.sizeDelta = new Vector2(800f, height);
            return row;
        }

        private void SmallButton(RectTransform row, string label, float x, float width, Action onClick)
        {
            var b = UIFactory.MakeButton(label, row, label, new Vector2(width, 70f), Theme.Cream, onClick, 28);
            UIFactory.Place((RectTransform)b.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, 70f));
        }

        /// <summary>A "&lt; value &gt;" row like Create-a-Skater's (narrower). Edits are remembered for UNDO.</summary>
        private void Stepper(RectTransform page, string label, Func<int> get, Action<int> set, int count, Func<int, string> name, Func<int, Rgb> color, bool undoable = true, bool boardEdit = true)
        {
            var row = Row(page, 76f);
            var l = UIFactory.Label("Label", row, label, 26, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(l.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(250f, 60f));
            var prev = (RectTransform)UIFactory.MakeButton("Prev", row, "<", new Vector2(80f, 68f), Theme.Cream, () => Step(get, set, count, -1, undoable, boardEdit), 40).transform;
            UIFactory.Place(prev, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(260f, 0f), new Vector2(80f, 68f));
            var valueBg = UIFactory.Panel("Value", row, new Color(1f, 1f, 1f, 0.08f));
            UIFactory.Place(valueBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(350f, 0f), new Vector2(340f, 68f));
            var swatch = UIFactory.Panel("Swatch", valueBg.transform, Color.white);
            UIFactory.Place(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(50f, 50f));
            var text = UIFactory.Label("Text", valueBg.transform, "", 26, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Stretch(text.rectTransform, 6f);
            var next = UIFactory.MakeButton("Next", row, ">", new Vector2(80f, 68f), Theme.Cream, () => Step(get, set, count, 1, undoable, boardEdit), 40);
            UIFactory.Place((RectTransform)next.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(700f, 0f), new Vector2(80f, 68f));
            _refreshers.Add(() =>
            {
                int v = get();
                swatch.gameObject.SetActive(color != null);
                if (color != null) swatch.color = ToColor(color(v));
                text.text = name?.Invoke(v) ?? (color != null ? "" : $"{v + 1}/{count}");
                text.rectTransform.offsetMin = new Vector2(color != null ? 70f : 6f, 6f);
            });
        }

        private void Step(Func<int> get, Action<int> set, int count, int dir, bool undoable, bool boardEdit)
        {
            if (undoable) Remember();
            set(((get() + dir) % count + count) % count);
            Edited(boardEdit: boardEdit);
        }

        /// <summary>Turns touches on the board preview into board coordinates (press and drag).</summary>
        private sealed class BoardPointer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            private DeckDesignerView _owner;
            private RectTransform _rt;

            public void Init(DeckDesignerView owner)
            {
                _owner = owner;
                _rt = (RectTransform)transform;
            }

            public void OnPointerDown(PointerEventData e) => Send(e, true);
            public void OnDrag(PointerEventData e) => Send(e, false);
            public void OnPointerUp(PointerEventData e) => _owner.OnBoardRelease();

            private void Send(PointerEventData e, bool begin)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, e.pressEventCamera, out var local)) return;
                var r = _rt.rect;
                var uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
                if (uv.x < 0f || uv.y < 0f || uv.x > 1f || uv.y > 1f) return;
                _owner.OnBoardTouch(uv, begin);
            }
        }
    }
}
