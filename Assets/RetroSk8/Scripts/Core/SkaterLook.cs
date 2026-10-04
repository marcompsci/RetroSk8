using System;

namespace RetroSk8.Core
{
    /// <summary>An engine-free colour (0-1 RGB) so Core data never depends on UnityEngine.</summary>
    public readonly struct Rgb
    {
        public readonly float R, G, B;
        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }
        public static Rgb Hex(int hex) => new Rgb(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);
    }

    public enum HairStyle { None = 0, Short = 1, Afro = 2, Bun = 3, Long = 4, Mohawk = 5, Twists = 6 }
    public enum BodyBuild { Slim = 0, Regular = 1, Broad = 2 }
    public enum Eyewear { None = 0, Shades = 1, Round = 2 }

    /// <summary>
    /// Deck graphic patterns for the board maker. The first seven match the shop's DeckPattern values;
    /// the rest are maker-only. Saved by number, so never renumber.
    /// </summary>
    public enum BoardPattern { Solid = 0, Stripes = 1, Checker = 2, Split = 3, Chevron = 4, Dots = 5, Bands = 6, Waves = 7, Grid = 8, Diamonds = 9, Bolt = 10, Sunrise = 11 }

    /// <summary>Create-a-Skater choices (body and face; gear stays with the cosmetics shop) plus the custom board graphic.</summary>
    [Serializable]
    public sealed class SkaterLook
    {
        public int skinTone = 3;
        public int hairStyle = (int)HairStyle.Short;
        public int hairColor = 0;
        public int build = (int)BodyBuild.Regular;
        public int eyewear = (int)Eyewear.None;
        public int shoeColor = 0;
        /// <summary><see cref="SkaterStyle"/>: picks the signature special.</summary>
        public int style = (int)SkaterStyle.Street;
        /// <summary>When on, the board maker's graphic replaces the shop deck.</summary>
        public bool customBoard;
        public BoardArt board = new BoardArt();

        public SkaterLook Clone()
        {
            var c = (SkaterLook)MemberwiseClone();
            c.board = (board ?? new BoardArt()).Clone();
            return c;
        }

        public void Sanitize()
        {
            skinTone = Wrap(skinTone, LookPalette.SkinTones.Length);
            hairStyle = Wrap(hairStyle, 7);
            hairColor = Wrap(hairColor, LookPalette.HairColors.Length);
            build = Wrap(build, 3);
            eyewear = Wrap(eyewear, 3);
            shoeColor = Wrap(shoeColor, LookPalette.Colors.Length);
            style = Wrap(style, 4);
            if (board == null) board = new BoardArt();
            board.Sanitize();
        }

        internal static int Wrap(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;
    }

    [Serializable]
    public sealed class BoardArt
    {
        public int pattern = (int)BoardPattern.Waves;
        public int primary = 2;
        public int secondary = 0;
        /// <summary>Sticker id from <see cref="Stickers.All"/> (0 = none).</summary>
        public int sticker = 1;
        public int stickerColor = 1;
        /// <summary>0 = nose, 1 = middle, 2 = tail.</summary>
        public int stickerSpot = 1;

        public BoardArt Clone() => (BoardArt)MemberwiseClone();

        public void Sanitize()
        {
            pattern = SkaterLook.Wrap(pattern, 12);
            primary = SkaterLook.Wrap(primary, LookPalette.Colors.Length);
            secondary = SkaterLook.Wrap(secondary, LookPalette.Colors.Length);
            sticker = SkaterLook.Wrap(sticker, Stickers.All.Length);
            stickerColor = SkaterLook.Wrap(stickerColor, LookPalette.Colors.Length);
            stickerSpot = SkaterLook.Wrap(stickerSpot, 3);
        }

        /// <summary>A short key for caching generated textures.</summary>
        public string Key => $"{pattern}_{primary}_{secondary}_{sticker}_{stickerColor}_{stickerSpot}";
    }

    /// <summary>Original colour sets for Create-a-Skater and the board maker.</summary>
    public static class LookPalette
    {
        public static readonly Rgb[] SkinTones =
        {
            Rgb.Hex(0xF6D7C3), Rgb.Hex(0xEBC2A0), Rgb.Hex(0xD9A47A), Rgb.Hex(0xB97C55),
            Rgb.Hex(0x8D5A3B), Rgb.Hex(0x6E4029), Rgb.Hex(0x4E2D1D), Rgb.Hex(0x3A2216),
        };

        public static readonly Rgb[] HairColors =
        {
            Rgb.Hex(0x1A1412), Rgb.Hex(0x4A2E1E), Rgb.Hex(0x8A5A32), Rgb.Hex(0xD8B46A),
            Rgb.Hex(0xB2442A), Rgb.Hex(0xBFBFBF), Rgb.Hex(0x2BC4B6), Rgb.Hex(0xFF4F9E),
        };

        /// <summary>Gear and board colours (the game's tape/coral/teal/ink palette plus brights).</summary>
        public static readonly Rgb[] Colors =
        {
            Rgb.Hex(0x121317), Rgb.Hex(0xF3E9D2), Rgb.Hex(0xF2C230), Rgb.Hex(0xFF5A4E),
            Rgb.Hex(0x1FC7B5), Rgb.Hex(0x3B6FE0), Rgb.Hex(0x9E52FF), Rgb.Hex(0x7BE03B),
            Rgb.Hex(0xFF8A1F), Rgb.Hex(0xFF4F9E), Rgb.Hex(0x7A7F8C), Rgb.Hex(0x6B3E26),
        };

        public static readonly string[] HairNames = { "CAP ONLY", "SHORT", "AFRO", "BUN", "LONG", "MOHAWK", "TWISTS" };
        public static readonly string[] BuildNames = { "SLIM", "REGULAR", "BROAD" };
        public static readonly string[] EyewearNames = { "NONE", "SHADES", "ROUND" };
        public static readonly string[] PatternNames = { "SOLID", "STRIPES", "CHECKER", "SPLIT", "CHEVRON", "DOTS", "BANDS", "WAVES", "GRID", "DIAMONDS", "BOLT", "SUNRISE" };
        public static readonly string[] SpotNames = { "NOSE", "MIDDLE", "TAIL" };
    }

    /// <summary>One 8x8 pixel sticker. Rows top to bottom; '#' is ink, anything else is clear.</summary>
    public sealed class Sticker
    {
        public int Id;
        public string Name;
        public string[] Rows;
        /// <summary>Career chapter that unlocks it (0 = available from the start).</summary>
        public int UnlockChapter;
        /// <summary>Crew level that unlocks it (0 = no crew requirement).</summary>
        public int UnlockCrewLevel;

        /// <summary>Unlocked given how far your career and crew have come.</summary>
        public bool UnlockedFor(int chaptersCompleted, int crewLevel) =>
            (UnlockChapter <= 0 || chaptersCompleted >= UnlockChapter) && (UnlockCrewLevel <= 0 || crewLevel >= UnlockCrewLevel);

        public bool Pixel(int x, int y) => y >= 0 && y < Rows.Length && x >= 0 && x < Rows[y].Length && Rows[y][x] == '#';
    }

    /// <summary>Original pixel stickers. Ids are saved, so append only.</summary>
    public static class Stickers
    {
        public static readonly Sticker[] All =
        {
            new Sticker { Id = 0, Name = "NONE", Rows = new string[8] { "........", "........", "........", "........", "........", "........", "........", "........" } },
            new Sticker { Id = 1, Name = "STAR", Rows = new[] { "...##...", "...##...", "########", ".######.", "..####..", ".##..##.", ".#....#.", "........" } },
            new Sticker { Id = 2, Name = "HEART", Rows = new[] { ".##..##.", "########", "########", "########", ".######.", "..####..", "...##...", "........" } },
            new Sticker { Id = 3, Name = "BOLT", Rows = new[] { "....###.", "...###..", "..###...", ".######.", "...###..", "..###...", ".##.....", "#......." } },
            new Sticker { Id = 4, Name = "SMILE", Rows = new[] { ".######.", "#......#", "#.#..#.#", "#......#", "#.#..#.#", "#..##..#", "#......#", ".######." } },
            new Sticker { Id = 5, Name = "CASSETTE", Rows = new[] { "########", "#......#", "#.#..#.#", "#.####.#", "#......#", "#.####.#", "##....##", "########" }, UnlockChapter = 1 },
            new Sticker { Id = 6, Name = "WAVE", Rows = new[] { "........", "..###...", ".#...#..", "#..#..#.", "..#.#..#", ".#...###", "#.......", "########" }, UnlockChapter = 2 },
            new Sticker { Id = 7, Name = "CROWN", Rows = new[] { "........", "#..##..#", "##.##.##", "########", "########", "#.#..#.#", "########", "........" }, UnlockChapter = 4 },
            new Sticker { Id = 8, Name = "FLAME", Rows = new[] { "...#....", "...##...", "..###.#.", ".#####.#", ".######.", "########", ".######.", "..####.." }, UnlockChapter = 6 },
            new Sticker { Id = 9, Name = "SKULL", Rows = new[] { ".######.", "########", "##.##.##", "########", ".######.", "..#..#..", "..####..", "........" }, UnlockChapter = 8 },
            new Sticker { Id = 10, Name = "CREW HAND", Rows = new[] { "..#.#...", "..#.#.#.", "#.#.#.#.", "#######.", "########", ".######.", "..####..", "..####.." }, UnlockCrewLevel = 3 },
            new Sticker { Id = 11, Name = "CREW KEY", Rows = new[] { ".###....", "#...#...", "#...####", "#...#.##", ".###..#.", "........", "........", "........" }, UnlockCrewLevel = 6 },
            new Sticker { Id = 12, Name = "CREW CROWN", Rows = new[] { "#.#..#.#", "########", "#......#", "#.#..#.#", "#......#", "########", ".#.##.#.", "..####.." }, UnlockCrewLevel = 9 },
        };

        public static Sticker Find(int id)
        {
            foreach (var s in All) if (s.Id == id) return s;
            return All[0];
        }
    }
}

namespace RetroSk8.Core
{
    /// <summary>Paints the board maker's deck graphic on a 16 x 64 pixel grid (x across, y from tail to nose).</summary>
    public static class BoardPainter
    {
        public const int Width = 16, Height = 64;
        public const int Primary = 0, Secondary = 1, StickerInk = 2;

        /// <summary>Bottom row of the 8x8 sticker for each spot (nose, middle, tail).</summary>
        public static readonly int[] StickerBase = { 48, 28, 8 };

        public static int Pixel(BoardArt art, int x, int y)
        {
            if (art.sticker != 0)
            {
                int y0 = StickerBase[SkaterLook.Wrap(art.stickerSpot, 3)];
                int sx = x - 4, sy = 7 - (y - y0);
                if (sx >= 0 && sx < 8 && sy >= 0 && sy < 8 && Stickers.Find(art.sticker).Pixel(sx, sy)) return StickerInk;
            }
            return Pattern((BoardPattern)art.pattern, x, y) ? Secondary : Primary;
        }

        /// <returns>True where the secondary colour shows.</returns>
        public static bool Pattern(BoardPattern p, int x, int y)
        {
            switch (p)
            {
                case BoardPattern.Stripes: return (y / 4) % 2 == 0;
                case BoardPattern.Checker: return ((x / 4) + (y / 4)) % 2 == 0;
                case BoardPattern.Split: return x >= Width / 2;
                case BoardPattern.Chevron: return ((y + Math.Abs(x - Width / 2)) / 5) % 2 == 0;
                case BoardPattern.Dots:
                {
                    int cx = x % 8 - 4, cy = y % 8 - 4;
                    return cx * cx + cy * cy <= 5;
                }
                case BoardPattern.Bands: return y > Height * 0.33f && y < Height * 0.66f;
                case BoardPattern.Waves: return ((y + (int)Math.Round(2.0 * Math.Sin(x * 0.8))) / 4 + 64) % 2 == 0;
                case BoardPattern.Grid: return x % 4 == 0 || y % 4 == 0;
                case BoardPattern.Diamonds: return Math.Abs(x - 8) + Math.Abs(y % 16 - 8) < 5;
                case BoardPattern.Bolt:
                {
                    int phase = y % 12;
                    int offset = phase < 6 ? phase : 12 - phase;
                    return Math.Abs(x - (5 + offset)) <= 1;
                }
                case BoardPattern.Sunrise:
                {
                    int dx = x - 8, dy = y - 22;
                    if (dx * dx + dy * dy <= 30 && dy >= 0) return true;
                    return y < 22 && (y / 3) % 2 == 0;
                }
                default: return false;
            }
        }
    }
}
