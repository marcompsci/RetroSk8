using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>
    /// Feedback rules for big moments: fireworks over lines worth showing off, and a short slow-motion beat on the
    /// biggest ones. Engine-free so the thresholds and the time curve are unit-tested.
    /// </summary>
    public static class Juice
    {
        public const long FireworkPoints = 8000;
        public const long SlowMoPoints = 15000;
        /// <summary>How slow the slow-mo gets (fraction of normal speed).</summary>
        public const float SlowScale = 0.35f;
        public const float SlowHold = 0.45f;   // real seconds at full slow
        public const float SlowEase = 0.35f;   // real seconds back to normal

        /// <summary>Fireworks particles for a banked line (0 = none). Bigger lines get bigger bursts.</summary>
        public static int Fireworks(long points) =>
            points < FireworkPoints ? 0 : (int)Math.Min(120, 30 + (points - FireworkPoints) / 400);

        public static bool SlowMo(long points) => points >= SlowMoPoints;

        /// <summary>Time scale <paramref name="t"/> real seconds into a slow-mo beat (1 once it's over).</summary>
        public static float TimeScaleAt(float t)
        {
            if (t < 0f) return 1f;
            if (t <= SlowHold) return SlowScale;
            float k = (t - SlowHold) / SlowEase;
            if (k >= 1f) return 1f;
            float smooth = k * k * (3f - 2f * k);
            return SlowScale + (1f - SlowScale) * smooth;
        }

        public static float SlowMoLength => SlowHold + SlowEase;

        /// <summary>Title-screen "ease out back" pop for the logo (overshoots a little, settles at 1).</summary>
        public static float EaseOutBack(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }

    /// <summary>A one-time hint shown during your first runs.</summary>
    public enum TipId
    {
        Ollie = 0,
        AirTrick = 1,
        CleanLanding = 2,
        Grind = 3,
        Special = 4,
        Bail = 5,
        Gap = 6,
    }

    /// <summary>The hint texts and which have been seen (each shows once, ever, and never twice within a few seconds).</summary>
    [Serializable]
    public sealed class TipState
    {
        public const float MinGap = 12f;
        public List<int> seen = new List<int>();

        public bool Seen(TipId id) => seen.Contains((int)id);

        /// <summary>True if the tip should show now (marks it seen). <paramref name="sinceLastTip"/> is seconds since the previous one.</summary>
        public bool TryShow(TipId id, float sinceLastTip)
        {
            if (Seen(id) || sinceLastTip < MinGap) return false;
            seen.Add((int)id);
            return true;
        }

        public void Sanitize()
        {
            if (seen == null) seen = new List<int>();
            seen.RemoveAll(i => !Enum.IsDefined(typeof(TipId), i));
        }

        public static string Text(TipId id, bool touch) => id switch
        {
            TipId.Ollie => "HOLD JUMP TO CROUCH, LET GO TO OLLIE. LONGER HOLD = HIGHER POP.",
            TipId.AirTrick => touch ? "IN THE AIR, SWIPE ON THE RIGHT FOR FLIPS AND GRABS." : "IN THE AIR, USE THE TRICK BUTTONS OR RIGHT STICK FOR FLIPS AND GRABS.",
            TipId.CleanLanding => "LAND WITH YOUR BOARD STRAIGHT FOR A CLEAN LANDING AND A BIGGER MULTIPLIER.",
            TipId.Grind => "JUMP ONTO A RAIL, LEDGE OR COPING TO GRIND. TILT TO KEEP YOUR BALANCE.",
            TipId.Special => "SPECIAL READY! YOUR NEXT AIR TRICK IS A SPECIAL. A FLIP SWIPE DOES YOUR SIGNATURE MOVE.",
            TipId.Bail => "A COMBO ONLY COUNTS ONCE YOU LAND IT. BAIL AND YOU LOSE IT.",
            TipId.Gap => "CLEAR A NAMED GAP FOR BONUS POINTS. EVERY PARK HAS A FEW TO FIND.",
            _ => "",
        };
    }
}
