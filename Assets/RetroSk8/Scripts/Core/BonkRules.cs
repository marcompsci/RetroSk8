namespace RetroSk8.Core
{
    public enum BonkMove { None = 0, Bonk = 1, PoleJam = 2 }

    /// <summary>
    /// Bonks and pole jams (Phase 18). No new button: in the air, hit a bonkable object (a cone, a hydrant, a
    /// car's bumper, a speaker post) and your wheels tap it for a <b>Bonk</b>, which lifts you a little and keeps the
    /// line going instead of bailing. Roll into a slanted post fast enough and you ride up it for a <b>Pole Jam</b>,
    /// which launches you like an ollie. Bonkable objects are marked in the world (BonkTarget). Engine-free and
    /// unit-tested; PlayerController applies the result.
    /// </summary>
    public static class BonkRules
    {
        public const string BonkId = "bonk";
        public const string BonkName = "Bonk";
        public const string PoleJamId = "pole_jam";
        public const string PoleJamName = "Pole Jam";

        public const int BonkPoints = 250;
        public const int PoleJamPoints = 350;

        /// <summary>Slowest you can be moving across the ground and still bonk (m/s).</summary>
        public const float MinBonkSpeed = 1.5f;
        /// <summary>Slowest roll that rides up a post instead of stopping against it (m/s).</summary>
        public const float MinPoleJamSpeed = 4f;
        /// <summary>The same object can't be bonked again this soon (seconds).</summary>
        public const float Cooldown = 0.8f;

        /// <summary>Upward speed a bonk gives you, at least (m/s).</summary>
        public const float BonkLift = 4.2f;
        /// <summary>Upward speed out of a pole jam (m/s): about an ollie.</summary>
        public const float PoleJamLift = 6.2f;
        /// <summary>Share of your speed across the ground you keep through a bonk or pole jam.</summary>
        public const float SpeedKeep = 0.85f;
        /// <summary>How long the skater passes through the object after hitting it (seconds), so you clear it.</summary>
        public const float PassThroughSeconds = 0.6f;
        /// <summary>A contact counts only when it's on the object's side, not its top: |normal · up| below this.</summary>
        public const float MaxContactUpDot = 0.6f;

        /// <param name="airborne">The skater is in the air.</param>
        /// <param name="rolling">The skater is rolling or manualling on the ground.</param>
        /// <param name="flatSpeed">Speed across the ground at the moment of contact (m/s).</param>
        /// <param name="pole">The object is a post you can ride up (speaker posts, signposts).</param>
        /// <param name="sinceLastHit">Seconds since this object was last bonked.</param>
        /// <param name="contactUpDot">|contact normal · up| (0 = side of the object, 1 = its top).</param>
        public static BonkMove Classify(bool airborne, bool rolling, float flatSpeed, bool pole, float sinceLastHit, float contactUpDot)
        {
            if (sinceLastHit < Cooldown) return BonkMove.None;
            if (contactUpDot >= MaxContactUpDot) return BonkMove.None; // landing on top is just landing
            if (airborne) return flatSpeed >= MinBonkSpeed ? BonkMove.Bonk : BonkMove.None;
            if (rolling && pole && flatSpeed >= MinPoleJamSpeed) return BonkMove.PoleJam;
            return BonkMove.None;
        }

        /// <summary>Upward speed after the move (never slows a skater who is already rising faster).</summary>
        public static float LiftAfter(BonkMove move, float verticalSpeed)
        {
            if (move == BonkMove.Bonk) return verticalSpeed > BonkLift ? verticalSpeed : BonkLift;
            if (move == BonkMove.PoleJam) return PoleJamLift;
            return verticalSpeed;
        }

        public static string IdFor(BonkMove move) => move == BonkMove.PoleJam ? PoleJamId : move == BonkMove.Bonk ? BonkId : "";
        public static string NameFor(BonkMove move) => move == BonkMove.PoleJam ? PoleJamName : move == BonkMove.Bonk ? BonkName : "";
        public static int PointsFor(BonkMove move) => move == BonkMove.PoleJam ? PoleJamPoints : move == BonkMove.Bonk ? BonkPoints : 0;

        /// <summary>True for a bonk or pole jam trick id (used by the Bonk Hunt City Jam).</summary>
        public static bool IsBonk(string trickId) => trickId == BonkId || trickId == PoleJamId;
    }
}
