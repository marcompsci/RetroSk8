using System;

namespace RetroSk8.Core
{
    /// <summary>
    /// Where the on-screen controls sit, as fractions of the safe area (0,0 = bottom-left, 1,1 = top-right),
    /// so a layout works on every screen shape. Plain data: saved in settings and edited in the layout editor.
    /// </summary>
    [Serializable]
    public sealed class TouchLayout
    {
        public float stickX = 0.111f, stickY = 0.24f;
        public float jumpX = 0.902f, jumpY = 0.204f;
        public float actionX = 0.778f, actionY = 0.139f;
        /// <summary>Button and stick size multiplier.</summary>
        public float scale = 1f;
        /// <summary>Stick sensitivity: higher means less thumb travel for full steer.</summary>
        public float sensitivity = 1f;

        public const float MinScale = 0.75f, MaxScale = 1.4f;
        public const float MinSensitivity = 0.6f, MaxSensitivity = 1.8f;
        /// <summary>Controls stay this far inside the edges so they can always be reached and dragged back.</summary>
        public const float EdgeMargin = 0.06f;

        public static TouchLayout Default() => new TouchLayout();

        /// <summary>The stick sits on the right and the buttons on the left.</summary>
        public static TouchLayout LeftHanded()
        {
            var l = new TouchLayout();
            l.Mirror();
            return l;
        }

        /// <summary>True when the stick is on the right half (the steer zone and swipe pad swap sides).</summary>
        public bool StickOnRight => stickX > 0.5f;

        public void Mirror()
        {
            stickX = 1f - stickX;
            jumpX = 1f - jumpX;
            actionX = 1f - actionX;
        }

        public TouchLayout Clone() => (TouchLayout)MemberwiseClone();

        /// <summary>Keeps everything on screen and inside the allowed ranges (old or hand-edited saves).</summary>
        public void Clamp()
        {
            stickX = C(stickX); stickY = C(stickY);
            jumpX = C(jumpX); jumpY = C(jumpY);
            actionX = C(actionX); actionY = C(actionY);
            scale = Math.Max(MinScale, Math.Min(MaxScale, IsBad(scale) ? 1f : scale));
            sensitivity = Math.Max(MinSensitivity, Math.Min(MaxSensitivity, IsBad(sensitivity) ? 1f : sensitivity));
        }

        /// <summary>Stick travel (reference pixels) for full steer.</summary>
        public float StickRadius(float baseRadius) => baseRadius / Math.Max(MinSensitivity, sensitivity);

        private static bool IsBad(float v) => float.IsNaN(v) || float.IsInfinity(v) || v <= 0f;
        private static float C(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0.5f : Math.Max(EdgeMargin, Math.Min(1f - EdgeMargin, v));
    }
}
