using System;

namespace RetroSk8.Core
{
    public enum WallMove
    {
        None = 0,
        /// <summary>Glancing contact: roll along the wall.</summary>
        Wallride = 1,
        /// <summary>Head-on contact: plant the board and spring back off.</summary>
        Wallplant = 2,
    }

    /// <summary>
    /// Rules for the Phase 7 wall tricks. The approach angle is between the skater's horizontal travel and the
    /// wall surface: 0° = skimming along it, 90° = straight into it.
    /// </summary>
    public static class WallRules
    {
        public const float MinSpeed = 5f;
        public const float WallrideMaxAngle = 50f;
        public const float WallplantMinAngle = 55f;
        /// <summary>Walls must be close to vertical (degrees between the wall normal and horizontal).</summary>
        public const float MaxWallTilt = 25f;
        public const float WallrideMaxSeconds = 1.4f;
        public const float WallrideGravityScale = 0.3f;
        public const int WallrideStartPoints = 250;
        public const int WallplantPoints = 400;
        public const int WalliePoints = 300;

        public static WallMove Classify(float approachAngleDegrees, float speed, float wallTiltDegrees)
        {
            if (speed < MinSpeed || wallTiltDegrees > MaxWallTilt) return WallMove.None;
            float a = Math.Abs(approachAngleDegrees);
            if (a <= WallrideMaxAngle) return WallMove.Wallride;
            if (a >= WallplantMinAngle) return WallMove.Wallplant;
            return WallMove.None; // the awkward in-between: just bounce off
        }
    }

    /// <summary>Rules for lip tricks: stalls on quarter-pipe coping when you come up the ramp square to it.</summary>
    public static class LipRules
    {
        /// <summary>Minimum angle between travel and the coping line (90° = square to the coping).</summary>
        public const float MinApproachAngle = 55f;
        public const float MaxHoldSeconds = 2.5f;
        public const int StartPoints = 300;

        public static bool IsLipApproach(float angleToCopingDegrees) => Math.Abs(angleToCopingDegrees) >= MinApproachAngle;

        /// <summary>Stick direction picks the stall, like grinds. All names are original.</summary>
        public static string NameFor(StickZone zone)
        {
            switch (zone)
            {
                case StickZone.Up: return "Nose Hang";
                case StickZone.Down: return "Tail Perch";
                case StickZone.Left: return "Lean Plant";
                case StickZone.Right: return "Hand Hold";
                default: return "Coping Stall";
            }
        }

        public static string IdFor(StickZone zone) => "lip_" + ((int)zone);
    }

    /// <summary>Reverts: a quick swipe right after landing a ramp air spins back to fakie and keeps the combo open.</summary>
    public static class RevertRules
    {
        public const float Window = 0.3f;
        public const int Points = 150;
        public const string Id = "revert";
        public const string Name = "Revert";

        public static bool CanRevert(float timeSinceLanding, bool landedFromRampAir, bool alreadyReverted) =>
            landedFromRampAir && !alreadyReverted && timeSinceLanding >= 0f && timeSinceLanding <= Window;
    }
}
