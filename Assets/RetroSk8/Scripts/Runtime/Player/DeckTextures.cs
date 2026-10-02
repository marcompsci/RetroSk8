using System.Collections.Generic;
using RetroSk8.Data;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>Generates the original procedural deck graphics as small point-filtered textures (pixel-hybrid look).</summary>
    public static class DeckTextures
    {
        private const int W = 16;
        private const int H = 64;
        private static readonly Dictionary<string, Texture2D> s_cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(DeckPattern pattern, Color a, Color b)
        {
            string key = $"{pattern}_{ColorUtility.ToHtmlStringRGB(a)}_{ColorUtility.ToHtmlStringRGB(b)}";
            if (s_cache.TryGetValue(key, out var tex) && tex != null) return tex;

            tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Deck_" + key,
            };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                px[y * W + x] = Sample(pattern, x, y) ? b : a;
            tex.SetPixels(px);
            tex.Apply();
            s_cache[key] = tex;
            return tex;
        }

        /// <summary>The board maker's graphic (pattern, two colours and a sticker), same 16 x 64 pixel look.</summary>
        public static Texture2D Get(RetroSk8.Core.BoardArt art)
        {
            string key = "art_" + art.Key;
            if (s_cache.TryGetValue(key, out var tex) && tex != null) return tex;
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Deck_" + key,
            };
            Color C(int i) { var c = RetroSk8.Core.LookPalette.Colors[i]; return new Color(c.R, c.G, c.B); }
            var colors = new[] { C(art.primary), C(art.secondary), C(art.stickerColor) };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                px[y * W + x] = colors[RetroSk8.Core.BoardPainter.Pixel(art, x, y)];
            tex.SetPixels(px);
            tex.Apply();
            s_cache[key] = tex;
            return tex;
        }

        /// <returns>True where the secondary colour shows.</returns>
        private static bool Sample(DeckPattern p, int x, int y)
        {
            switch (p)
            {
                case DeckPattern.Stripes: return (y / 4) % 2 == 0;
                case DeckPattern.Checker: return ((x / 4) + (y / 4)) % 2 == 0;
                case DeckPattern.Split: return x >= W / 2;
                case DeckPattern.Chevron: return ((y + Mathf.Abs(x - W / 2)) / 5) % 2 == 0;
                case DeckPattern.Dots:
                {
                    int cx = x % 8 - 4, cy = y % 8 - 4;
                    return cx * cx + cy * cy <= 5;
                }
                case DeckPattern.Bands: return y > H * 0.33f && y < H * 0.66f;
                default: return false;
            }
        }
    }
}
