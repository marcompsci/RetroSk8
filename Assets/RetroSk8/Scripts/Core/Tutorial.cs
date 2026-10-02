using System;

namespace RetroSk8.Core
{
    public enum TutorialStep
    {
        Push = 0,
        Ollie = 1,
        Flip = 2,
        Grab = 3,
        Spin = 4,
        Grind = 5,
        Manual = 6,
        Combo = 7,
        Done = 8,
    }

    /// <summary>
    /// The first-run lesson as a pure state machine: one skill per step, advanced by what the skater
    /// actually does (not by tapping "next"). The runtime feeds it events; the UI reads Step and Progress.
    /// A sketchy landing counts (we're teaching, not judging); a bail never does.
    /// </summary>
    public sealed class TutorialFlow
    {
        public const float PushSeconds = 1.5f;
        public const float PushSpeed = 8f;
        public const float GrindSeconds = 0.75f;
        public const float ManualSeconds = 1.5f;
        public const int ComboTricks = 3;
        /// <summary>Tape Tokens paid the first time the lesson is finished (skipping pays nothing).</summary>
        public const int RewardTokens = 50;

        private float _timer;

        public TutorialStep Step { get; private set; } = TutorialStep.Push;
        public bool IsDone => Step == TutorialStep.Done;
        public int StepNumber => Math.Min((int)Step + 1, StepCount);
        public static int StepCount => (int)TutorialStep.Done;

        /// <summary>0..1 for the timed steps (push, grind, manual); 0 otherwise.</summary>
        public float Progress
        {
            get
            {
                switch (Step)
                {
                    case TutorialStep.Push: return Clamp01(_timer / PushSeconds);
                    case TutorialStep.Grind: return Clamp01(_timer / GrindSeconds);
                    case TutorialStep.Manual: return Clamp01(_timer / ManualSeconds);
                    default: return 0f;
                }
            }
        }

        /// <summary>Raised whenever a step completes, with the step that was just finished.</summary>
        public event Action<TutorialStep> StepCompleted;

        /// <summary>Called every frame while rolling on the ground.</summary>
        public bool OnRolling(float dt, float speed)
        {
            if (Step != TutorialStep.Push) return false;
            if (speed >= PushSpeed) _timer += dt;
            return _timer >= PushSeconds && Advance();
        }

        /// <param name="quality">How the landing was judged.</param>
        /// <param name="halfTurns">Spin landed, in half turns (1 = 180).</param>
        /// <param name="flip">A flip-family trick was done during this air.</param>
        /// <param name="grab">A grab-family trick was done during this air.</param>
        public bool OnLanded(LandingQuality quality, int halfTurns, bool flip, bool grab)
        {
            if (quality == LandingQuality.Bail) return false;
            switch (Step)
            {
                case TutorialStep.Ollie: return Advance();
                case TutorialStep.Flip: return flip && Advance();
                case TutorialStep.Grab: return grab && Advance();
                case TutorialStep.Spin: return halfTurns >= 1 && Advance();
                default: return false;
            }
        }

        /// <summary>Called every frame while grinding, with the time spent on this grind so far.</summary>
        public bool OnGrinding(float secondsOnRail)
        {
            if (Step != TutorialStep.Grind) return false;
            _timer = Math.Max(_timer, secondsOnRail);
            return _timer >= GrindSeconds && Advance();
        }

        /// <summary>Called every frame while in a manual, with the time spent in this manual so far.</summary>
        public bool OnManual(float secondsHeld)
        {
            if (Step != TutorialStep.Manual) return false;
            _timer = Math.Max(_timer, secondsHeld);
            return _timer >= ManualSeconds && Advance();
        }

        public bool OnBanked(int trickCount)
        {
            if (Step != TutorialStep.Combo) return false;
            return trickCount >= ComboTricks && Advance();
        }

        /// <summary>A bail resets the timed steps' progress (you have to hold it in one go).</summary>
        public void OnBailed()
        {
            if (Step == TutorialStep.Grind || Step == TutorialStep.Manual) _timer = 0f;
        }

        public void SkipAll()
        {
            Step = TutorialStep.Done;
            _timer = 0f;
        }

        private bool Advance()
        {
            var finished = Step;
            Step = (TutorialStep)((int)Step + 1);
            _timer = 0f;
            StepCompleted?.Invoke(finished);
            return true;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
