using System;

namespace RetroSk8.Core
{
    /// <summary>
    /// Pure maths for the skater's procedural animation (Phase 16): the push cycle, landing squash, ollie pop, carve
    /// lean and the slam. The runtime (SkaterVisual / SkaterMotionDriver) only turns these numbers into joint angles,
    /// so the shapes are unit-tested and identical in replays.
    /// </summary>
    public static class SkaterMotion
    {
        /// <summary>Push kicks per second at full push.</summary>
        public const float PushRate = 1.35f;
        public const float SquashSeconds = 0.28f;
        public const float PopSeconds = 0.32f;
        /// <summary>Nose-up board pitch at the peak of an ollie pop (degrees).</summary>
        public const float PopPitch = 24f;
        public const float MaxCarveLean = 16f;

        /// <summary>
        /// The back foot through one push (phase 0..1): it steps down beside the front truck, sweeps back along the
        /// ground, then lifts and returns to the tail. Returns the sideways swing of the back thigh (degrees, + = toward
        /// the tail), how much the leg straightens to reach the ground (0..1) and a small hip bob (0..1).
        /// </summary>
        public static (float swing, float reach, float bob) Push(float phase)
        {
            float p = phase - (float)Math.Floor(phase);
            // 0-0.15 step down forward, 0.15-0.6 sweep back on the ground, 0.6-1 lift and return to the board.
            float swing, reach;
            if (p < 0.15f)
            {
                float k = p / 0.15f;
                swing = Lerp(0f, -14f, Smooth(k));
                reach = Smooth(k);
            }
            else if (p < 0.6f)
            {
                float k = (p - 0.15f) / 0.45f;
                swing = Lerp(-14f, 30f, k);
                reach = 1f;
            }
            else
            {
                float k = (p - 0.6f) / 0.4f;
                swing = Lerp(30f, 0f, Smooth(k));
                reach = 1f - Smooth(Math.Min(1f, k * 1.6f));
            }
            float bob = 0.5f - 0.5f * (float)Math.Cos(p * 2.0 * Math.PI);
            return (swing, reach, bob);
        }

        /// <summary>Landing squash: 1 at touchdown easing back to 0 (heavier for harder landings).</summary>
        public static float Squash(float sinceLanding, float impact01)
        {
            if (sinceLanding < 0f || sinceLanding >= SquashSeconds) return 0f;
            float k = sinceLanding / SquashSeconds;
            float strength = 0.45f + 0.55f * Clamp01(impact01);
            // Quick dip in the first fifth, then a soft recovery.
            float shape = k < 0.2f ? Smooth(k / 0.2f) : 1f - Smooth((k - 0.2f) / 0.8f);
            return strength * shape;
        }

        /// <summary>Board pitch through an ollie (degrees, + = nose up): the tail snaps, the nose rises, then it levels.</summary>
        public static float Pop(float sincePop)
        {
            if (sincePop < 0f || sincePop >= PopSeconds) return 0f;
            float k = sincePop / PopSeconds;
            return k < 0.3f ? PopPitch * Smooth(k / 0.3f) : PopPitch * (1f - Smooth((k - 0.3f) / 0.7f));
        }

        /// <summary>Lean into a turn from the ground turn rate (deg/s) and speed.</summary>
        public static float CarveLean(float turnRateDegPerSec, float speed) =>
            Clamp(turnRateDegPerSec * 0.09f * Clamp01(speed / 10f), -MaxCarveLean, MaxCarveLean);

        /// <summary>
        /// Where the board is during a slam: thrown with <paramref name="vx"/>,<paramref name="vy"/>,<paramref name="vz"/>
        /// (m/s), under gravity, bouncing off the ground (y = 0) with damping. Returns the offset from the start.
        /// </summary>
        public static (float x, float y, float z) BoardArc(float t, float vx, float vy, float vz, float gravity = 20f)
        {
            float x = 0f, y = 0f, z = 0f, dx = vx, dy = vy, dz = vz;
            const float step = 1f / 120f;
            for (float s = 0f; s < t; s += step)
            {
                float h = Math.Min(step, t - s);
                dy -= gravity * h;
                x += dx * h; y += dy * h; z += dz * h;
                if (y < 0f)
                {
                    y = 0f;
                    dy = -dy * 0.35f;
                    dx *= 0.6f; dz *= 0.6f;
                    if (dy < 0.4f) dy = 0f;
                }
            }
            return (x, y, z);
        }

        public static float Smooth(float k) { k = Clamp01(k); return k * k * (3f - 2f * k); }
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
