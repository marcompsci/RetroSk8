using System;

namespace RetroSk8.Core
{
    /// <summary>The assist levels a session plays with (Phase 26). 0 is off for each.</summary>
    public struct AssistLevels
    {
        /// <summary>0 = 100%, 1 = 90%, 2 = 80%, 3 = 70% game speed.</summary>
        public int Speed;
        /// <summary>0 off, 1 steady (calmer meter), 2 auto (the meter rights itself).</summary>
        public int Balance;
        /// <summary>0 normal, 1 wide, 2 widest landing windows.</summary>
        public int Landing;

        public bool Any => Speed > 0 || Balance > 0 || Landing > 0;
    }

    /// <summary>
    /// Difficulty assists (Phase 26): slower game speed, calmer or self-righting grind and manual balance, and more
    /// forgiving landings. They change the rules the skater is judged by, never the scoring, and they're off by default.
    /// Runs with any assist on still earn tokens, story steps and achievements, but aren't sent to Game Center
    /// leaderboards. Engine-free and unit-tested.
    /// </summary>
    public static class Assists
    {
        public const int MaxSpeed = 3;
        public const int MaxBalance = 2;
        public const int MaxLanding = 2;

        private static readonly float[] SpeedScales = { 1f, 0.9f, 0.8f, 0.7f };

        public static AssistLevels Clamp(AssistLevels a) => new AssistLevels
        {
            Speed = Math.Max(0, Math.Min(MaxSpeed, a.Speed)),
            Balance = Math.Max(0, Math.Min(MaxBalance, a.Balance)),
            Landing = Math.Max(0, Math.Min(MaxLanding, a.Landing)),
        };

        /// <summary>Time scale for a game speed level (out-of-range levels clamp).</summary>
        public static float SpeedScale(int level) => SpeedScales[Math.Max(0, Math.Min(MaxSpeed, level))];

        public static string SpeedName(int level) => $"GAME SPEED: {Math.Round(SpeedScale(level) * 100f)}%";

        public static string BalanceName(int level) =>
            level >= 2 ? "BALANCE ASSIST: AUTO" : level == 1 ? "BALANCE ASSIST: STEADY" : "BALANCE ASSIST: OFF";

        public static string LandingName(int level) =>
            level >= 2 ? "LANDING WINDOW: WIDEST" : level == 1 ? "LANDING WINDOW: WIDE" : "LANDING WINDOW: NORMAL";

        public static int Next(int level, int max) => level >= max || level < 0 ? 0 : level + 1;

        /// <summary>
        /// Grind or manual balance with the assist applied. Steady softens the meter's lean and halves the random push
        /// and how fast it gets twitchy; auto also makes the meter pull itself back to centre, so you only fall by steering off on purpose.
        /// Returns a new object; <paramref name="baseSettings"/> is never changed.
        /// </summary>
        public static BalanceSettings Balance(BalanceSettings baseSettings, int level)
        {
            var b = baseSettings ?? new BalanceSettings();
            var s = new BalanceSettings
            {
                baseTip = b.baseTip, tipGrowthPerSecond = b.tipGrowthPerSecond, maxTip = b.maxTip, jitter = b.jitter,
                control = b.control, startOffset = b.startOffset, repeatPenalty = b.repeatPenalty, autoCorrect = b.autoCorrect,
            };
            if (level <= 0) return s;
            s.baseTip *= 0.75f;
            s.jitter *= 0.5f;
            s.tipGrowthPerSecond *= 0.5f;
            s.repeatPenalty *= 0.5f;
            s.control *= 1.25f;
            if (level >= 2)
            {
                s.jitter = b.jitter * 0.25f;
                s.tipGrowthPerSecond = b.tipGrowthPerSecond * 0.25f;
                s.maxTip = Math.Max(b.baseTip, b.maxTip * 0.6f);
                // Stronger than the largest possible drift (lean * maxTip + jitter), so an untouched meter never falls.
                s.autoCorrect = s.maxTip + s.jitter + 0.5f;
            }
            return s;
        }

        /// <summary>Landing rules with the assist applied (new object; the yaw windows stay under 90° so spins still count).</summary>
        public static LandingRules Landing(LandingRules baseRules, int level)
        {
            var b = baseRules ?? new LandingRules();
            var r = new LandingRules
            {
                cleanYawError = b.cleanYawError, sketchyYawError = b.sketchyYawError, cleanSurfaceAngle = b.cleanSurfaceAngle,
                sketchySurfaceAngle = b.sketchySurfaceAngle, maxUnfinishedFraction = b.maxUnfinishedFraction,
            };
            if (level <= 0) return r;
            float yaw = level >= 2 ? 1.6f : 1.3f;
            r.cleanYawError = Math.Min(60f, b.cleanYawError * yaw);
            r.sketchyYawError = Math.Min(80f, b.sketchyYawError * yaw);
            r.cleanSurfaceAngle = Math.Min(60f, b.cleanSurfaceAngle + (level >= 2 ? 14f : 7f));
            r.sketchySurfaceAngle = Math.Min(80f, b.sketchySurfaceAngle + (level >= 2 ? 18f : 9f));
            r.maxUnfinishedFraction = Math.Min(0.6f, b.maxUnfinishedFraction + (level >= 2 ? 0.3f : 0.15f));
            return r;
        }
    }

    /// <summary>Which colour-vision palette the HUD uses (Phase 26; saved by number, append only).</summary>
    public enum ColorVision
    {
        Standard = 0,
        /// <summary>Protanopia / deuteranopia (the Phase 20 "colour-safe HUD").</summary>
        RedGreen = 1,
        /// <summary>Tritanopia.</summary>
        BlueYellow = 2,
        /// <summary>Brighter, more saturated marks on darker panels.</summary>
        HighContrast = 3,
    }

    /// <summary>The HUD's signal colours for one colour-vision mode.</summary>
    public struct VisionColors
    {
        /// <summary>Fail / warning (coral by default).</summary>
        public Rgb Bad;
        /// <summary>Success (teal by default).</summary>
        public Rgb Good;
        /// <summary>Highlight (tape yellow by default).</summary>
        public Rgb Accent;
        /// <summary>Opacity of the dark HUD panels (0-1).</summary>
        public float PanelAlpha;
    }

    /// <summary>
    /// Colour-vision palettes (Phase 26). Every mode keeps "bad", "good" and the accent apart for the colour vision it
    /// targets; the tests check that with a standard simulation of each type. Engine-free.
    /// </summary>
    public static class VisionPalette
    {
        public const int Count = 4;

        public static string Name(ColorVision v) =>
            v == ColorVision.RedGreen ? "COLOUR VISION: RED-GREEN SAFE"
            : v == ColorVision.BlueYellow ? "COLOUR VISION: BLUE-YELLOW SAFE"
            : v == ColorVision.HighContrast ? "COLOUR VISION: HIGH CONTRAST"
            : "COLOUR VISION: STANDARD";

        public static ColorVision Next(ColorVision v) => (ColorVision)(((int)v + 1) % Count);

        public static ColorVision FromSaved(int value) => value < 0 || value >= Count ? ColorVision.Standard : (ColorVision)value;

        public static VisionColors For(ColorVision v)
        {
            switch (v)
            {
                case ColorVision.RedGreen:
                    // Okabe-Ito vermillion and blue (unchanged from Phase 20), standard tape yellow.
                    return new VisionColors { Bad = Rgb.Hex(0xD55E00), Good = Rgb.Hex(0x0072B2), Accent = Rgb.Hex(0xF2C230), PanelAlpha = 0.72f };
                case ColorVision.BlueYellow:
                    // Tritan-safe: red vs. teal-green differ in the red-green channel tritans keep, and a pink accent
                    // replaces tape yellow, which nearly vanishes against the cream text for tritan vision (ΔE 28 → 61).
                    return new VisionColors { Bad = Rgb.Hex(0xE8303A), Good = Rgb.Hex(0x00A88F), Accent = Rgb.Hex(0xFF6FB5), PanelAlpha = 0.72f };
                case ColorVision.HighContrast:
                    return new VisionColors { Bad = Rgb.Hex(0xFF7A00), Good = Rgb.Hex(0x00E5FF), Accent = Rgb.Hex(0xFFE000), PanelAlpha = 0.92f };
                default:
                    return new VisionColors { Bad = Rgb.Hex(0xFF5A4E), Good = Rgb.Hex(0x1FC7B6), Accent = Rgb.Hex(0xF2C230), PanelAlpha = 0.72f };
            }
        }

        /// <summary>
        /// How a colour looks with a colour-vision type (Machado, Oliveira and Fernandes 2009, full severity, applied
        /// to linear RGB). <paramref name="type"/>: 0 protan, 1 deutan, 2 tritan. Returns 0-1 linear RGB.
        /// </summary>
        public static void Simulate(Rgb c, int type, out double r, out double g, out double b)
        {
            double lr = Lin(c.R), lg = Lin(c.G), lb = Lin(c.B);
            double[] m = type == 0 ? Protan : type == 1 ? Deutan : Tritan;
            r = Clamp01(m[0] * lr + m[1] * lg + m[2] * lb);
            g = Clamp01(m[3] * lr + m[4] * lg + m[5] * lb);
            b = Clamp01(m[6] * lr + m[7] * lg + m[8] * lb);
        }

        /// <summary>
        /// How different two colours look with a colour-vision type: CIE 1976 ΔE in CIELAB (D65) of the simulated
        /// colours. Around 2 is just noticeable; 40+ is easy to tell apart at a glance. <paramref name="type"/>: 0 protan,
        /// 1 deutan, 2 tritan, 3 typical colour vision (no simulation).
        /// </summary>
        public static double SeenDistance(Rgb a, Rgb b, int type)
        {
            Lab(a, type, out var l1, out var a1, out var b1);
            Lab(b, type, out var l2, out var a2, out var b2);
            return Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
        }

        private static void Lab(Rgb c, int type, out double l, out double a, out double b)
        {
            double r, g, bl;
            if (type >= 0 && type <= 2) Simulate(c, type, out r, out g, out bl);
            else { r = Lin(c.R); g = Lin(c.G); bl = Lin(c.B); }
            double x = (0.4124 * r + 0.3576 * g + 0.1805 * bl) / 0.95047;
            double y = 0.2126 * r + 0.7152 * g + 0.0722 * bl;
            double z = (0.0193 * r + 0.1192 * g + 0.9505 * bl) / 1.08883;
            double fx = F(x), fy = F(y), fz = F(z);
            l = 116 * fy - 16;
            a = 500 * (fx - fy);
            b = 200 * (fy - fz);
        }

        private static double F(double t) => t > 0.008856 ? Math.Pow(t, 1.0 / 3.0) : 7.787 * t + 16.0 / 116.0;

        private static readonly double[] Protan = { 0.152286, 1.052583, -0.204868, 0.114503, 0.786281, 0.099216, -0.003882, -0.048116, 1.051998 };
        private static readonly double[] Deutan = { 0.367322, 0.860646, -0.227968, 0.280085, 0.672501, 0.047413, -0.011820, 0.042940, 0.968881 };
        private static readonly double[] Tritan = { 1.255528, -0.076749, -0.178779, -0.078411, 0.930809, 0.147602, 0.004733, 0.691367, 0.303900 };

        private static double Lin(float v)
        {
            double s = Math.Max(0f, Math.Min(1f, v));
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
