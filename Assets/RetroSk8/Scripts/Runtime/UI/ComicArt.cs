using UnityEngine;

namespace RetroSk8.UI
{
    /// <summary>
    /// Procedural comic-book art for the story panels, drawn once in code (no image files): a sunburst, a halftone dot
    /// tile, a speech-bubble tail and a starburst "action" sticker. All white, tinted by the UI.
    /// </summary>
    public static class ComicArt
    {
        private static Texture2D s_sunburst, s_halftone;
        private static Sprite s_tail, s_star;

        /// <summary>Alternating rays from the centre (tint it and spin it slowly behind a panel).</summary>
        public static Texture2D Sunburst
        {
            get
            {
                if (s_sunburst != null) return s_sunburst;
                const int n = 256, rays = 18;
                s_sunburst = NewTexture(n, TextureWrapMode.Clamp);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - n * 0.5f + 0.5f, dy = y - n * 0.5f + 0.5f;
                    float a = Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f; // 0..1 around
                    bool on = ((int)(a * rays * 2f)) % 2 == 0;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (n * 0.5f);
                    float fade = Mathf.Clamp01(1.15f - r) * Mathf.Clamp01(r * 4f); // soft centre and edge
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(on ? 255f * fade : 0f));
                }
                s_sunburst.SetPixels32(px);
                s_sunburst.Apply();
                return s_sunburst;
            }
        }

        /// <summary>A tile of round dots (comic halftone). Repeat it with RawImage.uvRect.</summary>
        public static Texture2D Halftone
        {
            get
            {
                if (s_halftone != null) return s_halftone;
                const int n = 32;
                s_halftone = NewTexture(n, TextureWrapMode.Repeat);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Two dots per tile on a diagonal grid.
                    float d1 = Dist(x, y, 8f, 8f), d2 = Dist(x, y, 24f, 24f);
                    float d = Mathf.Min(d1, d2);
                    byte a = (byte)(Mathf.Clamp01(4.5f - d) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
                s_halftone.SetPixels32(px);
                s_halftone.Apply();
                return s_halftone;
            }
        }

        /// <summary>A triangle pointing left (the tail of a speech bubble).</summary>
        public static Sprite Tail
        {
            get
            {
                if (s_tail != null) return s_tail;
                const int n = 64;
                var tex = NewTexture(n, TextureWrapMode.Clamp);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Point at (0, 24); base on the right edge from y = 8 to y = 56.
                    float t = x / (float)(n - 1);
                    float half = t * 24f;
                    bool inside = Mathf.Abs(y - (24f + t * 8f)) <= half;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(inside ? 255 : 0));
                }
                tex.SetPixels32(px);
                tex.Apply();
                s_tail = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0f, 0.5f), 100f);
                return s_tail;
            }
        }

        /// <summary>A 12-point starburst (for VS! and big moments).</summary>
        public static Sprite Star
        {
            get
            {
                if (s_star != null) return s_star;
                const int n = 256, points = 12;
                var tex = NewTexture(n, TextureWrapMode.Clamp);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - n * 0.5f + 0.5f, dy = y - n * 0.5f + 0.5f;
                    float a = Mathf.Atan2(dy, dx);
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (n * 0.5f);
                    // Radius swings between inner and outer every point (a zigzag around the circle).
                    float k = Mathf.Abs(Mathf.Repeat(a / (2f * Mathf.PI) * points * 2f, 2f) - 1f);
                    float edge = Mathf.Lerp(0.62f, 0.98f, k);
                    float alpha = Mathf.Clamp01((edge - r) * 60f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
                tex.SetPixels32(px);
                tex.Apply();
                s_star = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return s_star;
            }
        }

        private static float Dist(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        private static Texture2D NewTexture(int n, TextureWrapMode wrap) =>
            new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = wrap, filterMode = FilterMode.Bilinear };
    }
}
