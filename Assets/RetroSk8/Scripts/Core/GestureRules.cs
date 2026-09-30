using System;

namespace RetroSk8.Core
{
    public enum StickZone
    {
        Neutral = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
    }

    /// <summary>Pure rules for turning raw touch/stick data into trick choices.</summary>
    public static class GestureRules
    {
        /// <summary>Classifies a swipe delta (in reference pixels). Returns None if shorter than minDistance.</summary>
        public static SwipeDirection ClassifySwipe(float dx, float dy, float minDistance)
        {
            if (dx * dx + dy * dy < minDistance * minDistance) return SwipeDirection.None;
            if (Math.Abs(dx) > Math.Abs(dy)) return dx > 0 ? SwipeDirection.Right : SwipeDirection.Left;
            return dy > 0 ? SwipeDirection.Up : SwipeDirection.Down;
        }

        public static StickZone ClassifyStick(float x, float y, float deadZone)
        {
            if (x * x + y * y < deadZone * deadZone) return StickZone.Neutral;
            if (Math.Abs(x) > Math.Abs(y)) return x > 0 ? StickZone.Right : StickZone.Left;
            return y > 0 ? StickZone.Up : StickZone.Down;
        }

        public static TrickFamily FamilyFor(SwipeDirection swipe)
        {
            switch (swipe)
            {
                case SwipeDirection.Down: return TrickFamily.Grab;
                case SwipeDirection.Left:
                case SwipeDirection.Right: return TrickFamily.Shove;
                default: return TrickFamily.Flip;
            }
        }

        /// <summary>
        /// Variation index (0-3) inside a family from the stick zone held at swipe time.
        /// Neutral = 0, Left = 1, Right = 2, Up or Down = 3.
        /// </summary>
        public static int VariationFor(StickZone zone)
        {
            switch (zone)
            {
                case StickZone.Left: return 1;
                case StickZone.Right: return 2;
                case StickZone.Up:
                case StickZone.Down: return 3;
                default: return 0;
            }
        }
    }
}
