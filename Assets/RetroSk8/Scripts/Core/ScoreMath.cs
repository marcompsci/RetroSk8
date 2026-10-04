using System;

namespace RetroSk8.Core
{
    /// <summary>
    /// Rounding for points that is the same on every runtime (Phase 16). Unity's Mono can evaluate float maths at
    /// higher precision than .NET or IL2CPP, so 900 × 1.3f can come out as 1169.99996 on one and 1170 on another, and
    /// a plain Floor/Ceiling then disagrees by a point. These snap values that are within a hair of a whole number.
    /// </summary>
    public static class ScoreMath
    {
        /// <summary>Relative tolerance: far above float rounding error, far below one point for any real score.</summary>
        private const double Tolerance = 1e-6;

        public static long Floor(double value) => (long)Math.Floor(value + Slack(value));

        public static long Ceiling(double value) => (long)Math.Ceiling(value - Slack(value));

        /// <summary>Exact integer share: the smallest whole number that is at least <paramref name="percent"/>% of <paramref name="points"/>.</summary>
        public static long CeilPercent(long points, int percent) =>
            points <= 0 ? 0 : (points * percent + 99) / 100;

        private static double Slack(double v) => Tolerance * Math.Max(1.0, Math.Abs(v));
    }
}
