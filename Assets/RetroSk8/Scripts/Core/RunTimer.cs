using System;

namespace RetroSk8.Core
{
    /// <summary>Countdown for timed modes. A duration &lt;= 0 means untimed (Free Skate).</summary>
    public sealed class RunTimer
    {
        public RunTimer(float durationSeconds)
        {
            Duration = durationSeconds;
            Remaining = durationSeconds;
        }

        public float Duration { get; }
        public float Remaining { get; private set; }
        public bool IsTimed => Duration > 0f;
        public bool IsPaused { get; set; }
        public bool IsExpired => IsTimed && Remaining <= 0f;

        /// <returns>True only on the tick the timer runs out.</returns>
        public bool Tick(float dt)
        {
            if (!IsTimed || IsPaused || IsExpired || dt <= 0f) return false;
            Remaining = Math.Max(0f, Remaining - dt);
            return Remaining <= 0f;
        }

        public void Reset() => Remaining = Duration;

        /// <summary>Formats as M:SS, rounding up so "0:00" only shows at expiry.</summary>
        public static string Format(float seconds)
        {
            int total = (int)Math.Ceiling(Math.Max(0f, seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
