using System;

namespace RetroSk8.Core
{
    public enum TruckFeel { Loose = 0, Medium = 1, Tight = 2 }
    public enum WheelFeel { Soft = 0, Medium = 1, Hard = 2 }

    /// <summary>Multipliers a board setup applies to the skater's handling. All 1 for MEDIUM / MEDIUM (the game's tuning).</summary>
    public struct SetupFactors
    {
        public float TurnSlow, TurnFast, LandingAlign, Cruise, Push, MaxSpeed, RollingDrag, GrindDecel, BalanceNoise;

        public static SetupFactors Neutral => new SetupFactors
        {
            TurnSlow = 1f, TurnFast = 1f, LandingAlign = 1f, Cruise = 1f, Push = 1f, MaxSpeed = 1f, RollingDrag = 1f, GrindDecel = 1f, BalanceNoise = 1f,
        };
    }

    /// <summary>
    /// Phase 30: board setup, the tuning skate-game players ask for most. Trucks trade turning for stability, wheels trade
    /// grip for speed and slide. Every choice is a trade-off (nothing is strictly better), so setups are allowed on the
    /// leaderboards; they're switched off in online S.K.A.T.E. so both phones play by the same rules. Engine-free.
    /// </summary>
    public static class BoardSetup
    {
        public static readonly string[] TruckNames = { "LOOSE", "MEDIUM", "TIGHT" };
        public static readonly string[] WheelNames = { "SOFT 78A", "MEDIUM 92A", "HARD 101A" };

        public static readonly string[] TruckBlurbs =
        {
            "TURNS SHARPER, CARVES DEEPER · LESS STABLE LANDINGS AND GRINDS",
            "THE GAME'S STANDARD FEEL",
            "STEADY LANDINGS AND GRINDS · WIDER TURNS",
        };
        public static readonly string[] WheelBlurbs =
        {
            "GRIPPY AND SMOOTH: TIGHTER TURNS AT SPEED · SLOWER TOP SPEED, SHORTER GRINDS",
            "THE GAME'S STANDARD FEEL",
            "FAST AND SLIDEY: HIGHER TOP SPEED, LONGER GRINDS · LESS GRIP IN TURNS",
        };

        public static int ClampTruck(int v) => v < 0 || v > 2 ? (int)TruckFeel.Medium : v;
        public static int ClampWheel(int v) => v < 0 || v > 2 ? (int)WheelFeel.Medium : v;

        public static SetupFactors Factors(int trucks, int wheels)
        {
            var f = SetupFactors.Neutral;
            switch ((TruckFeel)ClampTruck(trucks))
            {
                case TruckFeel.Loose:
                    f.TurnSlow = 1.15f; f.TurnFast = 1.2f; f.LandingAlign = 0.85f; f.BalanceNoise = 1.12f;
                    break;
                case TruckFeel.Tight:
                    f.TurnSlow = 0.88f; f.TurnFast = 0.85f; f.LandingAlign = 1f; f.BalanceNoise = 0.88f;
                    break;
            }
            switch ((WheelFeel)ClampWheel(wheels))
            {
                case WheelFeel.Soft:
                    f.Cruise *= 0.97f; f.Push *= 0.97f; f.MaxSpeed *= 0.95f; f.RollingDrag *= 1.3f; f.GrindDecel *= 1.15f;
                    f.TurnFast *= 1.06f;
                    break;
                case WheelFeel.Hard:
                    f.Cruise *= 1.03f; f.Push *= 1.03f; f.MaxSpeed *= 1.05f; f.RollingDrag *= 0.7f; f.GrindDecel *= 0.85f;
                    f.TurnFast *= 0.94f;
                    break;
            }
            return f;
        }

        public static string Summary(int trucks, int wheels) =>
            $"{TruckNames[ClampTruck(trucks)]} TRUCKS · {WheelNames[ClampWheel(wheels)]} WHEELS";
    }

    /// <summary>Board wear for the current deck: grows as you grind and land, resets with a new deck. JsonUtility-friendly.</summary>
    [Serializable]
    public sealed class BoardWearState
    {
        /// <summary>The deck this wear belongs to (a new deck starts fresh).</summary>
        public string deckKey = "";
        public float grindSeconds;
        public int landings;
        public bool off;

        public float Level => off ? 0f : BoardWear.Level(grindSeconds, landings);

        /// <summary>Switching decks starts a fresh board. Returns true when it reset.</summary>
        public bool UseDeck(string key)
        {
            key = key ?? "";
            if (key == deckKey) return false;
            deckKey = key;
            grindSeconds = 0f;
            landings = 0;
            return true;
        }

        public void Add(float grind, int lands)
        {
            if (grind > 0f && !float.IsNaN(grind)) grindSeconds = Math.Min(1e6f, grindSeconds + grind);
            if (lands > 0) landings = (int)Math.Min(1000000L, (long)landings + lands);
        }
    }

    /// <summary>
    /// Phase 30: the deck graphic wears through where a real board does: the tail and nose chip first, then the middle
    /// where grinds scrape it. Purely cosmetic; switch it off in Create-a-Skater, or get a fresh deck by changing it.
    /// </summary>
    public static class BoardWear
    {
        /// <summary>Grinding seconds and landings that wear a board about two-thirds of the way.</summary>
        public const float GrindScale = 600f;
        public const float LandingScale = 1500f;
        public const int Steps = 10;

        public static float Level(float grindSeconds, int landings)
        {
            double x = Math.Max(0, grindSeconds) / GrindScale + Math.Max(0, landings) / (double)LandingScale;
            return (float)(1.0 - Math.Exp(-x));
        }

        /// <summary>The wear rounded to one of <see cref="Steps"/> looks (0 = new), so textures can be cached.</summary>
        public static int Step(float level) => level <= 0f ? 0 : Math.Min(Steps, (int)Math.Round(level * Steps));

        public static string Label(float level) =>
            level < 0.05f ? "FRESH" : level < 0.3f ? "BROKEN IN" : level < 0.6f ? "WORN" : level < 0.85f ? "THRASHED" : "RETIRE IT?";

        /// <summary>
        /// True when pixel (x, y) of the 16 x 64 deck graphic has worn through at <paramref name="step"/>. Ends wear
        /// first, the grind zone in the middle next, and the rest last. Deterministic, and more wear only ever adds chips.
        /// </summary>
        public static bool Chipped(int x, int y, int step, int width = 16, int height = 64)
        {
            if (step <= 0 || x < 0 || y < 0 || x >= width || y >= height) return false;
            float t = (float)step / Steps;
            float fromEnd = Math.Min(y, height - 1 - y) / (height * 0.5f);   // 0 at the nose/tail, 1 in the middle
            float fromEdge = Math.Min(x, width - 1 - x) / (width * 0.5f);   // 0 at the rails
            float middle = Math.Abs(y - height * 0.5f) < height * 0.18f ? 0.35f : 0f; // truck-to-truck grind scrape
            float exposure = 1f - Math.Min(fromEnd, 0.85f) + (1f - fromEdge) * 0.35f + middle; // ~0.15..1.7
            float threshold = t * exposure * 0.75f;
            return Hash(x, y) < threshold;
        }

        /// <summary>A fixed 0..1 value per pixel.</summary>
        public static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
