using System;

namespace RetroSk8.Core
{
    [Serializable]
    public class BalanceSettings
    {
        // Lean accelerates away from centre by (tip * lean); tip grows the longer you hold the grind/manual.
        public float baseTip = 1.1f;
        public float tipGrowthPerSecond = 0.35f;
        public float maxTip = 4f;
        public float jitter = 0.6f;       // random push strength (noise supplied by caller)
        public float control = 2.6f;      // how hard player input corrects lean
        public float startOffset = 0.08f; // initial lean so the meter is never perfectly still
        public float repeatPenalty = 0.25f; // extra tip per repeat of the same balance trick in a combo
        /// <summary>Phase 26 balance assist: how hard the meter pulls itself back to centre (0 = off, the default).</summary>
        public float autoCorrect;
    }

    /// <summary>
    /// Deterministic balance simulation used by grinds and manuals. Lean is in [-1, 1]; |lean| >= 1 fails.
    /// Noise is passed in so tests can drive it exactly.
    /// </summary>
    public sealed class BalanceMeter
    {
        private readonly BalanceSettings _settings;
        private float _extraTip;

        public BalanceMeter(BalanceSettings settings)
        {
            _settings = settings ?? new BalanceSettings();
        }

        public float Lean { get; private set; }
        public float Elapsed { get; private set; }
        public bool IsActive { get; private set; }
        public bool HasFailed { get; private set; }

        public float CurrentTip => Math.Min(_settings.maxTip, _settings.baseTip + _extraTip + _settings.tipGrowthPerSecond * Elapsed);

        /// <param name="startSign">-1 or 1: which side the initial lean starts on.</param>
        /// <param name="repeatCount">How many times this balance trick was already used in the combo.</param>
        public void Begin(int startSign, int repeatCount = 0)
        {
            IsActive = true;
            HasFailed = false;
            Elapsed = 0f;
            _extraTip = _settings.repeatPenalty * Math.Max(0, repeatCount);
            Lean = _settings.startOffset * (startSign < 0 ? -1f : 1f);
        }

        public void End()
        {
            IsActive = false;
            Lean = 0f;
        }

        /// <param name="input">Player correction in [-1, 1]; positive pushes lean right.</param>
        /// <param name="noise">Random value in [-1, 1].</param>
        /// <returns>True on the step the meter fails.</returns>
        public bool Step(float dt, float input, float noise)
        {
            if (!IsActive || HasFailed || dt <= 0f) return false;
            Elapsed += dt;
            input = Clamp(input, -1f, 1f);
            noise = Clamp(noise, -1f, 1f);

            float drift = Lean * (CurrentTip - _settings.autoCorrect) + noise * _settings.jitter;
            Lean += (drift + input * _settings.control) * dt;

            if (Math.Abs(Lean) >= 1f)
            {
                Lean = Lean > 0 ? 1f : -1f;
                HasFailed = true;
                return true;
            }
            return false;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
