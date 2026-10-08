using System;
using System.Collections.Generic;
using System.Text;

namespace RetroSk8.Core
{
    /// <summary>One extra sticker on the deck designer's board (Phase 28).</summary>
    [Serializable]
    public sealed class StickerLayer
    {
        /// <summary>Sticker id from <see cref="Stickers.All"/>.</summary>
        public int sticker = 1;
        /// <summary>Palette colour index.</summary>
        public int color = 1;
        /// <summary>Left column (0 .. 16 - size).</summary>
        public int x = 4;
        /// <summary>Bottom row, counted from the tail (0 .. 64 - size).</summary>
        public int y = 28;
        /// <summary>Drawn at double size (16 x 16, the full width of the deck).</summary>
        public bool big;
        /// <summary>Quarter turns clockwise (0-3).</summary>
        public int rot;
        /// <summary>Mirrored left to right.</summary>
        public bool flip;

        public StickerLayer Clone() => (StickerLayer)MemberwiseClone();
        public int Size => big ? 16 : 8;
    }

    /// <summary>Grip tape patterns. Saved by number: append only.</summary>
    public enum GripPattern { Solid = 0, Cutout = 1, Stripes = 2, Checker = 3, Dots = 4, Bolt = 5, Frame = 6 }

    /// <summary>The deck designer's grip tape: a pattern in two colours (Phase 28).</summary>
    [Serializable]
    public sealed class GripArt
    {
        public int pattern = (int)GripPattern.Cutout;
        /// <summary>Palette colour index of the tape.</summary>
        public int color = 0;
        /// <summary>Palette colour index of the pattern.</summary>
        public int accent = 3;

        public GripArt Clone() => (GripArt)MemberwiseClone();

        public void Sanitize()
        {
            pattern = SkaterLook.Wrap(pattern, DeckDesign.GripNames.Length);
            color = SkaterLook.Wrap(color, LookPalette.Colors.Length);
            accent = SkaterLook.Wrap(accent, LookPalette.Colors.Length);
        }
    }

    /// <summary>
    /// The deck designer (Phase 28): the board maker's pattern and sticker, plus up to four free stickers (move, size,
    /// turn, mirror), a pixel paint layer in four colours, and a grip tape design. Everything paints on the same
    /// 16 x 64 pixel grid the deck has always used, so the board looks the same in runs, replays and photos.
    /// Designs travel as RD: codes. Engine-free so it is unit-tested.
    /// </summary>
    public static class DeckDesign
    {
        public const int Width = BoardPainter.Width, Height = BoardPainter.Height;
        public const int MaxLayers = 4;
        public const int PaintCells = Width * Height;
        public const int PaintColors = 4;
        public const string CodePrefix = "RD:";
        public const int MaxCodeChars = 3000;
        private const int CodeVersion = 1;

        public static readonly string[] GripNames = { "SOLID", "CUTOUT", "STRIPES", "CHECKER", "DOTS", "BOLT", "FRAME" };
        public static readonly int[] DefaultPaintColors = { 1, 3, 4, 2 };

        // ---------------------------------------------------------------- painting the deck

        /// <summary>The palette colour of one deck pixel: pattern, then the classic sticker, then the free stickers in order, then paint.</summary>
        public static int ColorAt(BoardArt art, int x, int y)
        {
            int c = BoardPainter.Pixel(art, x, y) == BoardPainter.StickerInk ? art.stickerColor
                  : BoardPainter.Pattern((BoardPattern)art.pattern, x, y) ? art.secondary : art.primary;
            if (art.layers != null)
                for (int i = 0; i < art.layers.Count && i < MaxLayers; i++)
                    if (LayerInk(art.layers[i], x, y)) c = art.layers[i].color;
            int p = PaintAt(art, x, y);
            if (p > 0) c = PaintColor(art, p - 1);
            return SkaterLook.Wrap(c, LookPalette.Colors.Length);
        }

        /// <summary>True where a free sticker puts ink on deck pixel (x, y).</summary>
        public static bool LayerInk(StickerLayer l, int x, int y)
        {
            if (l == null || l.sticker <= 0) return false;
            int size = l.Size;
            int lx = x - l.x, ly = y - l.y;
            if (lx < 0 || ly < 0 || lx >= size || ly >= size) return false;
            if (l.big) { lx /= 2; ly /= 2; }
            // Sticker rows run top (nose side) to bottom: row 7 sits at the layer's bottom edge.
            int sx = lx, sy = 7 - ly;
            switch (((l.rot % 4) + 4) % 4)
            {
                case 1: { int t = sx; sx = 7 - sy; sy = t; break; }
                case 2: sx = 7 - sx; sy = 7 - sy; break;
                case 3: { int t = sx; sx = sy; sy = 7 - t; break; }
            }
            if (l.flip) sx = 7 - sx;
            return Stickers.Find(l.sticker).Pixel(sx, sy);
        }

        // ---------------------------------------------------------------- the paint layer

        /// <summary>Paint at (x, y): 0 = none, 1-4 = paint colour slot + 1.</summary>
        public static int PaintAt(BoardArt art, int x, int y)
        {
            string p = art?.paint;
            if (string.IsNullOrEmpty(p) || x < 0 || y < 0 || x >= Width || y >= Height) return 0;
            int i = y * Width + x;
            if (i >= p.Length) return 0;
            int v = p[i] - '0';
            return v >= 1 && v <= PaintColors ? v : 0;
        }

        public static int PaintColor(BoardArt art, int slot)
        {
            var cols = art?.paintColors;
            int c = cols != null && slot >= 0 && slot < cols.Length ? cols[slot] : DefaultPaintColors[Math.Max(0, Math.Min(PaintColors - 1, slot))];
            return SkaterLook.Wrap(c, LookPalette.Colors.Length);
        }

        /// <summary>Paints one cell (value 0 erases). Returns true when anything changed.</summary>
        public static bool Paint(BoardArt art, int x, int y, int value)
        {
            if (art == null || x < 0 || y < 0 || x >= Width || y >= Height) return false;
            value = value >= 1 && value <= PaintColors ? value : 0;
            if (PaintAt(art, x, y) == value) return false;
            var chars = Cells(art);
            chars[y * Width + x] = (char)('0' + value);
            art.paint = Compact(chars);
            return true;
        }

        /// <summary>Flood-fills the paint layer from (x, y) across touching cells of the same paint value.</summary>
        public static int Fill(BoardArt art, int x, int y, int value)
        {
            if (art == null || x < 0 || y < 0 || x >= Width || y >= Height) return 0;
            value = value >= 1 && value <= PaintColors ? value : 0;
            var chars = Cells(art);
            char from = chars[y * Width + x], to = (char)('0' + value);
            if (from == to) return 0;
            int changed = 0;
            var stack = new Stack<int>();
            stack.Push(y * Width + x);
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                if (chars[i] != from) continue;
                chars[i] = to;
                changed++;
                int cx = i % Width, cy = i / Width;
                if (cx > 0) stack.Push(i - 1);
                if (cx < Width - 1) stack.Push(i + 1);
                if (cy > 0) stack.Push(i - Width);
                if (cy < Height - 1) stack.Push(i + Width);
            }
            art.paint = Compact(chars);
            return changed;
        }

        public static void ClearPaint(BoardArt art) { if (art != null) art.paint = ""; }

        public static int PaintedCount(BoardArt art)
        {
            if (string.IsNullOrEmpty(art?.paint)) return 0;
            int n = 0;
            foreach (char c in art.paint) if (c >= '1' && c <= '4') n++;
            return n;
        }

        private static char[] Cells(BoardArt art)
        {
            var chars = new char[PaintCells];
            string p = art.paint ?? "";
            for (int i = 0; i < PaintCells; i++)
            {
                char c = i < p.Length ? p[i] : '0';
                chars[i] = c >= '1' && c <= '4' ? c : '0';
            }
            return chars;
        }

        /// <summary>An unpainted board stores "" instead of 1,024 zeros.</summary>
        private static string Compact(char[] chars)
        {
            foreach (char c in chars) if (c != '0') return new string(chars);
            return "";
        }

        // ---------------------------------------------------------------- grip tape

        /// <summary>The palette colour of one grip pixel (same 16 x 64 grid, x across, y from the tail).</summary>
        public static int GripColorAt(GripArt g, int x, int y)
        {
            if (g == null) return 0;
            return GripAccent((GripPattern)g.pattern, x, y) ? g.accent : g.color;
        }

        public static bool GripAccent(GripPattern p, int x, int y)
        {
            switch (p)
            {
                case GripPattern.Cutout: return Math.Abs(y - 32) < 7 && Math.Abs(x - 8) + Math.Abs(y - 32) / 2 < 5;
                case GripPattern.Stripes: return ((x + y) / 3) % 3 == 0;
                case GripPattern.Checker: return y > 44 && ((x / 2) + (y / 2)) % 2 == 0;
                case GripPattern.Dots: return x % 5 == 2 && y % 6 == 3;
                case GripPattern.Bolt:
                {
                    int phase = y % 16;
                    int offset = phase < 8 ? phase : 16 - phase;
                    return y > 8 && y < 56 && Math.Abs(x - (4 + offset)) <= 1;
                }
                case GripPattern.Frame: return x == 1 || x == Width - 2 || y == 2 || y == Height - 3;
                default: return false;
            }
        }

        // ---------------------------------------------------------------- tidying

        /// <summary>Clamps everything a save or a code could put out of range.</summary>
        public static void Sanitize(BoardArt art)
        {
            if (art == null) return;
            if (art.layers == null) art.layers = new List<StickerLayer>();
            art.layers.RemoveAll(l => l == null);
            if (art.layers.Count > MaxLayers) art.layers.RemoveRange(MaxLayers, art.layers.Count - MaxLayers);
            foreach (var l in art.layers)
            {
                l.sticker = SkaterLook.Wrap(l.sticker, Stickers.All.Length);
                l.color = SkaterLook.Wrap(l.color, LookPalette.Colors.Length);
                l.rot = SkaterLook.Wrap(l.rot, 4);
                l.x = Math.Max(0, Math.Min(Width - l.Size, l.x));
                l.y = Math.Max(0, Math.Min(Height - l.Size, l.y));
            }
            if (art.paintColors == null || art.paintColors.Length != PaintColors) art.paintColors = (int[])DefaultPaintColors.Clone();
            for (int i = 0; i < PaintColors; i++) art.paintColors[i] = SkaterLook.Wrap(art.paintColors[i], LookPalette.Colors.Length);
            if (!string.IsNullOrEmpty(art.paint)) art.paint = Compact(Cells(art));
        }

        /// <summary>Moves a layer by whole pixels, keeping it on the deck.</summary>
        public static void Move(StickerLayer l, int dx, int dy)
        {
            if (l == null) return;
            l.x = Math.Max(0, Math.Min(Width - l.Size, l.x + dx));
            l.y = Math.Max(0, Math.Min(Height - l.Size, l.y + dy));
        }

        /// <summary>Toggles double size, keeping the layer's centre where it was.</summary>
        public static void ToggleBig(StickerLayer l)
        {
            if (l == null) return;
            int cx = l.x + l.Size / 2, cy = l.y + l.Size / 2;
            l.big = !l.big;
            l.x = cx - l.Size / 2;
            l.y = cy - l.Size / 2;
            Move(l, 0, 0);
        }

        /// <summary>A short fingerprint of everything that changes the deck graphic (texture cache key).</summary>
        public static string Key(BoardArt art)
        {
            if (art == null) return "none";
            var sb = new StringBuilder();
            sb.Append(art.pattern).Append('_').Append(art.primary).Append('_').Append(art.secondary).Append('_')
              .Append(art.sticker).Append('_').Append(art.stickerColor).Append('_').Append(art.stickerSpot);
            if (art.layers != null)
                foreach (var l in art.layers)
                    if (l != null) sb.Append("|").Append(l.sticker).Append(',').Append(l.color).Append(',').Append(l.x).Append(',').Append(l.y)
                                    .Append(',').Append(l.big ? 1 : 0).Append(',').Append(l.rot).Append(',').Append(l.flip ? 1 : 0);
            if (!string.IsNullOrEmpty(art.paint))
            {
                sb.Append("|p");
                if (art.paintColors != null) foreach (int c in art.paintColors) sb.Append(c).Append(',');
                sb.Append(Fnv(art.paint).ToString("x8"));
            }
            return sb.ToString();
        }

        public static string GripKey(GripArt g) => g == null ? "grip_none" : $"grip_{g.pattern}_{g.color}_{g.accent}";

        private static uint Fnv(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h;
        }

        // ---------------------------------------------------------------- RD: codes

        public static bool IsCode(string text) => text != null && text.TrimStart().StartsWith(CodePrefix, StringComparison.Ordinal);

        /// <summary>A code for a whole board: graphic, stickers, paint and (when <paramref name="grip"/> isn't null) grip tape.</summary>
        public static string Encode(BoardArt art, GripArt grip, string from)
        {
            if (art == null) throw new ArgumentNullException(nameof(art));
            var a = art.Clone();
            Sanitize(a);
            bool hasGrip = grip != null;
            var g = (grip ?? new GripArt()).Clone();
            g.Sanitize();
            var w = new ShareCodes.BitWriter();
            w.Write(CodeVersion, 4);
            w.Write(a.pattern, 5);
            w.Write(a.primary, 5);
            w.Write(a.secondary, 5);
            w.Write(a.sticker, 6);
            w.Write(a.stickerColor, 5);
            w.Write(a.stickerSpot, 2);
            w.Write(a.layers.Count, 3);
            foreach (var l in a.layers)
            {
                w.Write(l.sticker, 6);
                w.Write(l.color, 5);
                w.Write(l.x, 4);
                w.Write(l.y, 6);
                w.Write(l.big ? 1 : 0, 1);
                w.Write(l.rot, 2);
                w.Write(l.flip ? 1 : 0, 1);
            }
            for (int i = 0; i < PaintColors; i++) w.Write(a.paintColors[i], 5);
            // Paint as runs: 3 bits of value, 6 bits of length (1-64).
            var cells = Cells(a);
            bool painted = PaintedCount(a) > 0;
            w.Write(painted ? 1 : 0, 1);
            if (painted)
            {
                int i = 0;
                while (i < PaintCells)
                {
                    char v = cells[i];
                    int run = 1;
                    while (i + run < PaintCells && run < 64 && cells[i + run] == v) run++;
                    w.Write(v - '0', 3);
                    w.Write(run - 1, 6);
                    i += run;
                }
            }
            w.Write(hasGrip ? 1 : 0, 1);
            if (hasGrip)
            {
                w.Write(g.pattern, 4);
                w.Write(g.color, 5);
                w.Write(g.accent, 5);
            }
            ShareCodes.WriteName(w, from ?? "", ShareCodes.MaxFromLength);
            return GhostCodes.Seal(CodePrefix, w.ToBytes());
        }

        public static bool TryDecode(string code, out BoardArt art, out GripArt grip, out string from, out string error)
        {
            art = null;
            grip = null;
            from = null;
            if (string.IsNullOrWhiteSpace(code)) { error = "NO CODE FOUND"; return false; }
            if (code.Length > MaxCodeChars) { error = "THAT BOARD CODE IS TOO LONG"; return false; }
            if (!IsCode(code)) { error = "THAT ISN'T A BOARD CODE"; return false; }
            var bytes = GhostCodes.Unseal(CodePrefix, code);
            if (bytes == null) { error = "THAT BOARD CODE IS INCOMPLETE (COPY ALL OF IT)"; return false; }
            try
            {
                var r = new ShareCodes.BitReader(bytes);
                if (r.Read(4) != CodeVersion) { error = "THAT BOARD NEEDS A NEWER VERSION"; return false; }
                var a = new BoardArt
                {
                    pattern = r.Read(5), primary = r.Read(5), secondary = r.Read(5),
                    sticker = r.Read(6), stickerColor = r.Read(5), stickerSpot = r.Read(2),
                    layers = new List<StickerLayer>(),
                };
                int layers = r.Read(3);
                if (layers > MaxLayers) { error = "THAT BOARD NEEDS A NEWER VERSION"; return false; }
                for (int i = 0; i < layers; i++)
                    a.layers.Add(new StickerLayer { sticker = r.Read(6), color = r.Read(5), x = r.Read(4), y = r.Read(6), big = r.Read(1) == 1, rot = r.Read(2), flip = r.Read(1) == 1 });
                a.paintColors = new int[PaintColors];
                for (int i = 0; i < PaintColors; i++) a.paintColors[i] = r.Read(5);
                if (r.Read(1) == 1)
                {
                    var cells = new char[PaintCells];
                    int i = 0;
                    while (i < PaintCells)
                    {
                        int v = r.Read(3), run = r.Read(6) + 1;
                        if (v > PaintColors || i + run > PaintCells) { error = "THAT BOARD CODE IS DAMAGED"; return false; }
                        for (int k = 0; k < run; k++) cells[i++] = (char)('0' + v);
                    }
                    a.paint = Compact(cells);
                }
                if (a.pattern >= LookPalette.PatternNames.Length || a.sticker >= Stickers.All.Length) { error = "THAT BOARD NEEDS A NEWER VERSION"; return false; }
                GripArt g = null;
                if (r.Read(1) == 1)
                {
                    g = new GripArt { pattern = r.Read(4), color = r.Read(5), accent = r.Read(5) };
                    if (g.pattern >= GripNames.Length) { error = "THAT BOARD NEEDS A NEWER VERSION"; return false; }
                }
                from = Gallery.SafeName(ShareCodes.ReadName(r, ShareCodes.MaxFromLength), ShareCodes.MaxFromLength, "");
                a.Sanitize();
                g?.Sanitize();
                art = a;
                grip = g;
                error = null;
                return true;
            }
            catch (Exception)
            {
                error = "THAT BOARD CODE IS DAMAGED";
                return false;
            }
        }

        /// <summary>
        /// Stickers a pasted board uses that you haven't unlocked yet. The board still loads (it's a friend's design),
        /// but those stickers are swapped for STAR so locked art can't be skipped to.
        /// </summary>
        public static int ReplaceLocked(BoardArt art, Func<Sticker, bool> unlocked)
        {
            if (art == null || unlocked == null) return 0;
            int n = 0;
            if (art.sticker != 0 && !unlocked(Stickers.Find(art.sticker))) { art.sticker = 1; n++; }
            if (art.layers != null)
                foreach (var l in art.layers)
                    if (l != null && l.sticker != 0 && !unlocked(Stickers.Find(l.sticker))) { l.sticker = 1; n++; }
            return n;
        }
    }
}
