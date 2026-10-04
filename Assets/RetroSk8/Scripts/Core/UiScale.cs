using System;

namespace RetroSk8.Core
{
    /// <summary>
    /// Fits the UI to any screen shape. Retro Sk8's screens are laid out on a 2340×1080 landscape-phone canvas
    /// that scales with screen height. On squarer screens (iPads at 4:3, older 16:9 iPhones) that would leave the
    /// canvas too narrow for the menus, so the scaler leans toward matching width until the canvas is at least
    /// <see cref="MinLogicalWidth"/> units wide. Mirrors CanvasScaler's log-space blend; engine-free so it is unit-tested.
    /// </summary>
    public static class UiScale
    {
        public const float ReferenceWidth = 2340f;
        public const float ReferenceHeight = 1080f;
        /// <summary>Widest menu panels are ~2000 units; this leaves a margin.</summary>
        public const float MinLogicalWidth = 2120f;

        /// <summary>CanvasScaler.matchWidthOrHeight (0 = width, 1 = height) for a screen.</summary>
        public static float MatchFor(float screenWidth, float screenHeight, float minLogicalWidth = MinLogicalWidth,
            float refWidth = ReferenceWidth, float refHeight = ReferenceHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f || refWidth <= 0f || refHeight <= 0f) return 1f;
            double a = Math.Log(screenWidth / refWidth, 2.0);
            double b = Math.Log(screenHeight / refHeight, 2.0);
            if (screenWidth / Math.Pow(2.0, b) >= minLogicalWidth) return 1f; // wide enough when matching height
            if (Math.Abs(b - a) < 1e-9) return 1f;
            double c = Math.Log(screenWidth / minLogicalWidth, 2.0);
            double m = (c - a) / (b - a);
            return (float)Math.Max(0.0, Math.Min(1.0, m));
        }

        /// <summary>The canvas size in UI units for a screen and match value (what CanvasScaler produces).</summary>
        public static void LogicalSize(float screenWidth, float screenHeight, float match, out float width, out float height,
            float refWidth = ReferenceWidth, float refHeight = ReferenceHeight)
        {
            double a = Math.Log(screenWidth / refWidth, 2.0);
            double b = Math.Log(screenHeight / refHeight, 2.0);
            double scale = Math.Pow(2.0, a + (b - a) * match);
            width = (float)(screenWidth / scale);
            height = (float)(screenHeight / scale);
        }

        /// <summary>True on squarer, tablet-shaped screens (iPad), where layouts can use the extra height.</summary>
        public static bool IsTabletShape(float screenWidth, float screenHeight)
        {
            float w = Math.Max(screenWidth, screenHeight), h = Math.Min(screenWidth, screenHeight);
            return h > 0f && w / h < 1.6f;
        }
    }
}
