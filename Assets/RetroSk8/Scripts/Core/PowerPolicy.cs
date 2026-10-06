namespace RetroSk8.Core
{
    /// <summary>Settings > FRAME RATE (Phase 23). Saved by number: append only.</summary>
    public enum FrameRateMode
    {
        /// <summary>60 FPS, dropping to 30 in Low Power Mode, when the phone runs hot, or on a low battery.</summary>
        Auto = 0,
        /// <summary>Always aim for 60 FPS.</summary>
        Smooth = 1,
        /// <summary>30 FPS, a lower render scale and no shadows: much longer battery life.</summary>
        BatterySaver = 2,
    }

    /// <summary>What the phone reports right now (from iOS and Unity's SystemInfo).</summary>
    public struct PowerState
    {
        public bool LowPowerMode;
        /// <summary>0 nominal, 1 fair, 2 serious, 3 critical (iOS thermal state).</summary>
        public int Thermal;
        /// <summary>0..1, or a negative value when unknown (the editor, desktops).</summary>
        public float Battery;
        public bool Charging;
    }

    /// <summary>The frame rate and quality to use.</summary>
    public struct PowerPlan
    {
        public int TargetFps;
        /// <summary>Upper limit for the adaptive render scale.</summary>
        public float MaxRenderScale;
        public bool Shadows;
        /// <summary>A short reason for the debug overlay and the settings label ("LOW POWER MODE").</summary>
        public string Reason;
    }

    /// <summary>
    /// Phase 23 battery and heat policy. Engine-free and unit-tested; DevicePerformance asks it every few seconds and
    /// applies the plan (Application.targetFrameRate, the adaptive render scale's cap, shadows).
    /// </summary>
    public static class PowerPolicy
    {
        public const float LowBattery = 0.2f;
        public const float SaverRenderScale = 0.8f;

        public static PowerPlan Plan(FrameRateMode mode, PowerState s)
        {
            if (mode == FrameRateMode.BatterySaver) return Saver("BATTERY SAVER");
            // Critical heat always wins: iOS will throttle hard anyway, so get ahead of it.
            if (s.Thermal >= 3) return Saver("PHONE IS HOT");
            if (mode == FrameRateMode.Smooth) return Full(s.Thermal >= 2 ? "WARM" : "");
            if (s.LowPowerMode) return Saver("LOW POWER MODE");
            if (s.Thermal >= 2) return new PowerPlan { TargetFps = 30, MaxRenderScale = 1f, Shadows = true, Reason = "PHONE IS WARM" };
            if (s.Battery >= 0f && s.Battery < LowBattery && !s.Charging) return Saver("LOW BATTERY");
            return Full("");
        }

        public static string ModeName(FrameRateMode mode) =>
            mode == FrameRateMode.Smooth ? "FRAME RATE: 60 FPS" : mode == FrameRateMode.BatterySaver ? "FRAME RATE: BATTERY SAVER (30)" : "FRAME RATE: AUTO";

        public static FrameRateMode Next(FrameRateMode mode) =>
            mode == FrameRateMode.Auto ? FrameRateMode.Smooth : mode == FrameRateMode.Smooth ? FrameRateMode.BatterySaver : FrameRateMode.Auto;

        private static PowerPlan Full(string reason) => new PowerPlan { TargetFps = 60, MaxRenderScale = 1f, Shadows = true, Reason = reason };
        private static PowerPlan Saver(string reason) => new PowerPlan { TargetFps = 30, MaxRenderScale = SaverRenderScale, Shadows = false, Reason = reason };
    }
}
