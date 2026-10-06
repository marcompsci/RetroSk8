using System;

namespace RetroSk8.Core
{
    /// <summary>Photo mode 2.0 looks (Phase 25). Saved by number: append only.</summary>
    public enum PhotoFilter { None = 0, Vhs = 1, FilmGrain = 2, BlackWhite = 3 }
    public enum PhotoFrame { None = 0, Tape = 1, Polaroid = 2, Comic = 3 }
    public enum PhotoStickers { None = 0, Stars = 1, Burst = 2 }

    /// <summary>
    /// Photo mode 2.0 (Phase 25): filters, frames and stickers applied to a captured photo as plain RGBA bytes
    /// (row 0 = bottom, like Unity textures). All art is drawn here from code: no text, logos or outside images.
    /// Deterministic for a seed, engine-free and unit-tested.
    /// </summary>
    public static class PhotoFx
    {
        public static string FilterName(PhotoFilter f) =>
            f == PhotoFilter.Vhs ? "VHS" : f == PhotoFilter.FilmGrain ? "FILM" : f == PhotoFilter.BlackWhite ? "B&W" : "NONE";
        public static string FrameName(PhotoFrame f) =>
            f == PhotoFrame.Tape ? "TAPE" : f == PhotoFrame.Polaroid ? "INSTANT" : f == PhotoFrame.Comic ? "COMIC" : "NONE";
        public static string StickerName(PhotoStickers s) => s == PhotoStickers.Stars ? "STARS" : s == PhotoStickers.Burst ? "BURST" : "NONE";

        public static T Next<T>(T v) where T : struct, Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            int i = Array.IndexOf(values, v);
            return values[(i + 1) % values.Length];
        }

        /// <summary>Applies filter, then frame, then stickers, in place.</summary>
        public static void Apply(byte[] rgba, int w, int h, PhotoFilter filter, PhotoFrame frame, PhotoStickers stickers, int seed)
        {
            if (rgba == null || w <= 0 || h <= 0 || rgba.Length < w * h * 4) throw new ArgumentException("Bad image");
            Filter(rgba, w, h, filter, seed);
            Frame(rgba, w, h, frame);
            Stickers(rgba, w, h, stickers, seed);
        }

        // ---------------------------------------------------------------- filters

        public static void Filter(byte[] p, int w, int h, PhotoFilter filter, int seed)
        {
            if (filter == PhotoFilter.None) return;
            var rng = new Random(seed);
            if (filter == PhotoFilter.Vhs)
            {
                // Red shifted right a few pixels (colour bleed), every third row darker (scanlines), a little noise.
                int shift = Math.Max(1, w / 400);
                for (int y = 0; y < h; y++)
                {
                    int row = y * w * 4;
                    for (int x = w - 1; x >= 0; x--)
                    {
                        int i = row + x * 4;
                        int src = row + Math.Max(0, x - shift) * 4;
                        p[i] = p[src];
                    }
                    float line = y % 3 == 0 ? 0.78f : 1f;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + x * 4;
                        int n = rng.Next(-10, 11);
                        p[i] = Clamp(p[i] * line * 1.06f + n);
                        p[i + 1] = Clamp(p[i + 1] * line + n);
                        p[i + 2] = Clamp(p[i + 2] * line * 1.04f + n);
                    }
                }
                return;
            }
            float cx = w * 0.5f, cy = h * 0.5f, maxD = (float)Math.Sqrt(cx * cx + cy * cy);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    float r = p[i], g = p[i + 1], b = p[i + 2];
                    if (filter == PhotoFilter.BlackWhite)
                    {
                        float l = 0.299f * r + 0.587f * g + 0.114f * b;
                        l = (l - 128f) * 1.25f + 128f; // a little more contrast
                        r = g = b = l;
                    }
                    else // film grain: warm tint, soft vignette, grain
                    {
                        float dx = x - cx, dy = y - cy;
                        float v = 1f - 0.35f * (float)Math.Pow(Math.Sqrt(dx * dx + dy * dy) / maxD, 2.2);
                        float grain = rng.Next(-18, 19);
                        r = r * 1.06f * v + grain;
                        g = g * 1.0f * v + grain;
                        b = b * 0.88f * v + grain;
                    }
                    p[i] = Clamp(r);
                    p[i + 1] = Clamp(g);
                    p[i + 2] = Clamp(b);
                }
        }

        // ---------------------------------------------------------------- frames

        public static void Frame(byte[] p, int w, int h, PhotoFrame frame)
        {
            int s = Math.Min(w, h);
            switch (frame)
            {
                case PhotoFrame.Tape:
                {
                    // Tape-yellow bands top and bottom with a thin ink edge (the game's tape motif).
                    int band = s / 14, edge = Math.Max(1, s / 200);
                    Rect(p, w, h, 0, 0, w, band, 242, 194, 48);
                    Rect(p, w, h, 0, h - band, w, band, 242, 194, 48);
                    Rect(p, w, h, 0, band, w, edge, 18, 19, 23);
                    Rect(p, w, h, 0, h - band - edge, w, edge, 18, 19, 23);
                    break;
                }
                case PhotoFrame.Polaroid:
                {
                    // Cream border, deeper at the bottom like an instant print.
                    int side = s / 22, bottom = s / 7;
                    Rect(p, w, h, 0, 0, w, bottom, 242, 236, 222);
                    Rect(p, w, h, 0, h - side, w, side, 242, 236, 222);
                    Rect(p, w, h, 0, 0, side, h, 242, 236, 222);
                    Rect(p, w, h, w - side, 0, side, h, 242, 236, 222);
                    break;
                }
                case PhotoFrame.Comic:
                {
                    // A heavy ink panel border with a halftone dot strip along the bottom.
                    int t = s / 30;
                    Rect(p, w, h, 0, 0, w, t, 18, 19, 23);
                    Rect(p, w, h, 0, h - t, w, t, 18, 19, 23);
                    Rect(p, w, h, 0, 0, t, h, 18, 19, 23);
                    Rect(p, w, h, w - t, 0, t, h, 18, 19, 23);
                    int dot = Math.Max(2, s / 120), gap = dot * 3;
                    for (int y = t + gap; y < t + gap * 4; y += gap)
                        for (int x = t + gap; x < w - t - gap; x += gap)
                            Disc(p, w, h, x, y, dot * (1f - (y - t) / (float)(gap * 5)), 255, 90, 78);
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- stickers

        public static void Stickers(byte[] p, int w, int h, PhotoStickers stickers, int seed)
        {
            int s = Math.Min(w, h);
            var rng = new Random(seed ^ 0x5eed);
            if (stickers == PhotoStickers.Stars)
            {
                // Five-point stars in the corners, in the game's colours.
                var spots = new[] { (0.1f, 0.85f), (0.9f, 0.82f), (0.12f, 0.2f), (0.88f, 0.18f), (0.82f, 0.9f) };
                var colors = new[] { (242, 194, 48), (31, 199, 181), (255, 90, 78), (242, 232, 209), (150, 90, 230) };
                for (int i = 0; i < spots.Length; i++)
                {
                    float r = s * (0.045f + 0.025f * (float)rng.NextDouble());
                    var (cr, cg, cb) = colors[i % colors.Length];
                    Star(p, w, h, spots[i].Item1 * w, spots[i].Item2 * h, r, (float)rng.NextDouble(), (byte)cr, (byte)cg, (byte)cb);
                }
            }
            else if (stickers == PhotoStickers.Burst)
            {
                // A comic sunburst in the top-right corner: alternating rays round a solid disc.
                float cx = w * 0.86f, cy = h * 0.8f, inner = s * 0.06f, outer = s * 0.14f;
                for (int y = (int)(cy - outer); y <= (int)(cy + outer); y++)
                    for (int x = (int)(cx - outer); x <= (int)(cx + outer); x++)
                    {
                        if (x < 0 || y < 0 || x >= w || y >= h) continue;
                        float dx = x - cx, dy = y - cy, d = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (d > outer) continue;
                        double a = Math.Atan2(dy, dx);
                        bool ray = ((int)Math.Floor((a + Math.PI) / (Math.PI / 8.0))) % 2 == 0;
                        if (d <= inner) Set(p, w, x, y, 255, 90, 78);
                        else if (ray && d <= outer * (0.75f + 0.25f * (float)Math.Cos(a * 8))) Set(p, w, x, y, 242, 194, 48);
                    }
            }
        }

        // ---------------------------------------------------------------- drawing helpers

        private static void Rect(byte[] p, int w, int h, int x0, int y0, int rw, int rh, byte r, byte g, byte b)
        {
            for (int y = Math.Max(0, y0); y < Math.Min(h, y0 + rh); y++)
                for (int x = Math.Max(0, x0); x < Math.Min(w, x0 + rw); x++)
                    Set(p, w, x, y, r, g, b);
        }

        private static void Disc(byte[] p, int w, int h, float cx, float cy, float radius, byte r, byte g, byte b)
        {
            if (radius <= 0f) return;
            for (int y = (int)(cy - radius); y <= (int)(cy + radius); y++)
                for (int x = (int)(cx - radius); x <= (int)(cx + radius); x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    float dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= radius * radius) Set(p, w, x, y, r, g, b);
                }
        }

        /// <summary>A filled five-point star (point-in-polygon against its ten corners).</summary>
        private static void Star(byte[] p, int w, int h, float cx, float cy, float radius, float spin, byte r, byte g, byte b)
        {
            var px = new float[10];
            var py = new float[10];
            for (int i = 0; i < 10; i++)
            {
                double a = spin + i * Math.PI / 5.0 + Math.PI / 2.0;
                float rad = i % 2 == 0 ? radius : radius * 0.45f;
                px[i] = cx + rad * (float)Math.Cos(a);
                py[i] = cy + rad * (float)Math.Sin(a);
            }
            for (int y = (int)(cy - radius); y <= (int)(cy + radius); y++)
                for (int x = (int)(cx - radius); x <= (int)(cx + radius); x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    bool inside = false;
                    for (int i = 0, j = 9; i < 10; j = i++)
                        if ((py[i] > y) != (py[j] > y) && x < (px[j] - px[i]) * (y - py[i]) / (py[j] - py[i]) + px[i]) inside = !inside;
                    if (inside) Set(p, w, x, y, r, g, b);
                }
        }

        private static void Set(byte[] p, int w, int x, int y, byte r, byte g, byte b)
        {
            int i = (y * w + x) * 4;
            p[i] = r; p[i + 1] = g; p[i + 2] = b; p[i + 3] = 255;
        }

        private static byte Clamp(float v) => v <= 0f ? (byte)0 : v >= 255f ? (byte)255 : (byte)(v + 0.5f);
    }
}
