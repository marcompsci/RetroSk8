using System;

namespace RetroSk8.Core
{
    public enum LandingQuality
    {
        Clean = 0,
        Sketchy = 1,
        Bail = 2,
    }

    public enum BailReason
    {
        None = 0,
        TrickUnfinished,
        OverRotated,
        BadAngle,
        Collision,
        LostBalance,
        OutOfBounds,
    }

    [Serializable]
    public class LandingRules
    {
        public float cleanYawError = 22f;
        public float sketchyYawError = 50f;
        public float cleanSurfaceAngle = 28f;
        public float sketchySurfaceAngle = 55f;
        /// <summary>Tricks with more than this fraction still to animate cause a bail on touchdown.</summary>
        public float maxUnfinishedFraction = 0.2f;
    }

    public struct LandingInput
    {
        /// <summary>Accumulated air yaw in degrees (signed, unbounded).</summary>
        public float AirYawDegrees;
        /// <summary>Angle between the board's up vector and the ground normal at touchdown.</summary>
        public float SurfaceAngleDegrees;
        /// <summary>0 = no trick running, 1 = trick just started.</summary>
        public float UnfinishedTrickFraction;
    }

    public struct LandingVerdict
    {
        public LandingQuality Quality;
        public BailReason Reason;
        public int HalfTurns;
        public bool Switch; // landed an odd number of half turns, i.e. rolling the other way
    }

    public static class LandingJudge
    {
        public static LandingVerdict Evaluate(LandingInput input, LandingRules rules)
        {
            rules = rules ?? new LandingRules();
            int halfTurns = SpinRules.HalfTurns(input.AirYawDegrees);
            float yawError = SpinRules.YawErrorDegrees(input.AirYawDegrees);

            var verdict = new LandingVerdict
            {
                HalfTurns = halfTurns,
                Switch = (SpinRules.NearestHalfTurns(input.AirYawDegrees) & 1) == 1,
            };

            if (input.UnfinishedTrickFraction > rules.maxUnfinishedFraction)
                return Fail(verdict, BailReason.TrickUnfinished);
            if (yawError > rules.sketchyYawError)
                return Fail(verdict, BailReason.OverRotated);
            if (input.SurfaceAngleDegrees > rules.sketchySurfaceAngle)
                return Fail(verdict, BailReason.BadAngle);

            bool sketchy = yawError > rules.cleanYawError || input.SurfaceAngleDegrees > rules.cleanSurfaceAngle;
            verdict.Quality = sketchy ? LandingQuality.Sketchy : LandingQuality.Clean;
            verdict.Reason = BailReason.None;
            return verdict;
        }

        private static LandingVerdict Fail(LandingVerdict v, BailReason reason)
        {
            v.Quality = LandingQuality.Bail;
            v.Reason = reason;
            return v;
        }
    }

    public static class SpinRules
    {
        /// <summary>Nearest whole number of half turns (180°) to the given yaw.</summary>
        public static int NearestHalfTurns(float yawDegrees) => (int)Math.Round(Math.Abs(yawDegrees) / 180f, MidpointRounding.AwayFromZero);

        /// <summary>Half turns that count for scoring: only awarded once you're within the sketchy window of the target.</summary>
        public static int HalfTurns(float yawDegrees) => NearestHalfTurns(yawDegrees);

        public static float YawErrorDegrees(float yawDegrees)
        {
            float abs = Math.Abs(yawDegrees);
            float nearest = NearestHalfTurns(yawDegrees) * 180f;
            return Math.Abs(abs - nearest);
        }

        public static int SpinPoints(int halfTurns, ScoringConfig config)
        {
            if (halfTurns <= 0) return 0;
            float chain = 1f + config.spinChainBonusPerHalfTurn * (halfTurns - 1);
            return (int)Math.Round(config.spinPointsPerHalfTurn * halfTurns * chain);
        }
    }
}
