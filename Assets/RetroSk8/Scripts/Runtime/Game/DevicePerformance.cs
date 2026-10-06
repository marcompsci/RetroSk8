using System.Runtime.InteropServices;
using RetroSk8.Core;
using UnityEngine;
using UnityEngine.Rendering;
#if RETROSK8_URP
using UnityEngine.Rendering.Universal;
#endif

namespace RetroSk8.Game
{
    /// <summary>
    /// Device performance settings and the adaptive-resolution loop that holds 60 FPS on phones.
    /// On device: caps the internal resolution near 1080p, trims shadow distance, and lowers the render
    /// scale when frames run long (raising it again when there is headroom). In the editor nothing changes,
    /// so the URP asset on disk is never modified.
    /// </summary>
    public sealed class DevicePerformance : MonoBehaviour
    {
        /// <summary>Internal-resolution cap (short side, in pixels). Phones above this render slightly scaled.</summary>
        public const float TargetShortSide = 1080f;
        public const float MobileShadowDistance = 45f;

        private static DevicePerformance s_instance;
        private static bool s_hudVisible;

        private readonly FrameGovernor _governor = new FrameGovernor();
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private float _baseScale = 1f;
        private float _fps;
        private GUIStyle _hudStyle;

        public static DevicePerformance Instance => s_instance;
        public static bool HudVisible
        {
            get => s_hudVisible;
            set => s_hudVisible = value;
        }

        /// <summary>Latest CPU main-thread and GPU frame times in ms (0 when the platform doesn't report them).</summary>
        public float CpuMs { get; private set; }
        public float GpuMs { get; private set; }
        public float Fps => _fps;
        public float RenderScale => _baseScale * _governor.Scale * Plan.MaxRenderScale;
        public bool Adaptive { get; private set; }
        /// <summary>Phase 23: the frame rate / quality plan in force (from <see cref="PowerPolicy"/>).</summary>
        public PowerPlan Plan { get; private set; } = new PowerPlan { TargetFps = 60, MaxRenderScale = 1f, Shadows = true, Reason = "" };
        public int MemoryWarnings { get; private set; }
        private const float PolicySeconds = 5f;
        private float _nextPolicy;
        private float _shadowDistance = MobileShadowDistance;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int RetroSk8_LowPowerMode();
        [DllImport("__Internal")] private static extern int RetroSk8_ThermalState();
#endif

        /// <summary>What the phone reports now: Low Power Mode, heat, battery.</summary>
        public static PowerState ReadPowerState()
        {
            var s = new PowerState { Battery = SystemInfo.batteryLevel };
            var status = SystemInfo.batteryStatus;
            s.Charging = status == BatteryStatus.Charging || status == BatteryStatus.Full;
#if UNITY_IOS && !UNITY_EDITOR
            try { s.LowPowerMode = RetroSk8_LowPowerMode() == 1; s.Thermal = RetroSk8_ThermalState(); } catch (System.Exception) { }
#endif
            return s;
        }

        /// <summary>Re-reads the phone and the FRAME RATE setting now (Settings calls this after a change).</summary>
        public void RefreshPlan()
        {
            var mode = (FrameRateMode)Mathf.Clamp(RetroSk8.Save.SaveManager.Data.settings.frameRateMode, 0, 2);
            var plan = PowerPolicy.Plan(mode, ReadPowerState());
            bool changed = plan.TargetFps != Plan.TargetFps || plan.MaxRenderScale != Plan.MaxRenderScale || plan.Shadows != Plan.Shadows;
            Plan = plan;
            _nextPolicy = Time.unscaledTime + PolicySeconds;
            if (!changed) return;
            Application.targetFrameRate = plan.TargetFps;
            _governor.TargetFps = plan.TargetFps;
            if (!Adaptive) return;
#if RETROSK8_URP
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.shadowDistance = plan.Shadows ? _shadowDistance : 0f;
#endif
            ApplyScale();
        }

        private void OnLowMemory()
        {
            // iOS memory warning: drop cached songs and unused assets before the system closes the app.
            MemoryWarnings++;
            int songs = RetroSk8.Audio.AudioManager.Instance != null ? RetroSk8.Audio.AudioManager.Instance.ReleaseCachedMusic() : 0;
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
            Debug.LogWarning($"[RetroSk8] Memory warning: released {songs} cached songs and unused assets.");
        }

        private void OnDestroy() => Application.lowMemory -= OnLowMemory;

        public static void Ensure()
        {
            if (s_instance != null) return;
            var go = new GameObject("DevicePerformance");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<DevicePerformance>();
            go.AddComponent<PerfCapture>(); // Phase 18: per-scene frame stats in perf_log.txt
        }

        private void Awake()
        {
            // Phones and tablets only: the editor and desktop keep the project's quality settings untouched.
            Adaptive = Application.isMobilePlatform && !Application.isEditor;
            Application.lowMemory += OnLowMemory; // Phase 23
            if (!Adaptive) return;

            float shortSide = Mathf.Min(Screen.width, Screen.height);
            _baseScale = shortSide > TargetShortSide ? TargetShortSide / shortSide : 1f;
#if RETROSK8_URP
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = Mathf.Min(urp.shadowDistance, MobileShadowDistance);
                _shadowDistance = urp.shadowDistance;
                urp.renderScale = RenderScale;
            }
#endif
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fps = Mathf.Lerp(_fps, 1f / dt, 0.1f);
            if (Time.unscaledTime >= _nextPolicy) RefreshPlan(); // Phase 23: battery and heat

            // Frame timing stats report the real CPU/GPU work, which matters because a capped 60 FPS
            // frame always *looks* like 16.7 ms. Without them, use the frame time and only ever scale down.
            FrameTimingManager.CaptureFrameTimings();
            bool haveTimings = FrameTimingManager.GetLatestTimings(1, _timings) > 0;
            CpuMs = haveTimings ? (float)_timings[0].cpuMainThreadFrameTime : 0f;
            GpuMs = haveTimings ? (float)_timings[0].gpuFrameTime : 0f;
            if (!Adaptive) return;

            float work = haveTimings && (CpuMs > 0f || GpuMs > 0f) ? Mathf.Max(CpuMs, GpuMs) / 1000f : dt;
            _governor.AllowStepUp = haveTimings;
            if (_governor.Tick(work)) ApplyScale();
        }

        private void ApplyScale()
        {
#if RETROSK8_URP
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = RenderScale;
#endif
        }

        private void OnGUI()
        {
            if (!s_hudVisible) return;
            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 40), fontStyle = FontStyle.Bold };
                _hudStyle.normal.textColor = Color.white;
            }
            var cap = PerfCapture.Instance;
            string gc = cap != null && cap.Current.GcMeasured ? $"  GC {cap.Current.GcBytesPerFrame:0}B/f" : "";
            string hitch = cap != null ? $"  HITCHES {cap.Current.Hitches}" : "";
            string plan = string.IsNullOrEmpty(Plan.Reason) ? "" : $"  {Plan.TargetFps} FPS: {Plan.Reason}";
            string text = $"{_fps:0} FPS{plan}  CPU {CpuMs:0.0}ms  GPU {GpuMs:0.0}ms  SCALE {RenderScale:0.00}{gc}{hitch}{(MemoryWarnings > 0 ? $"  MEM WARN {MemoryWarnings}" : "")}";
            var rect = new Rect(Screen.safeArea.x + 12f, Screen.height - Screen.safeArea.yMax + 8f, Screen.width, _hudStyle.fontSize * 1.6f);
            var shadow = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);
            var c = _hudStyle.normal.textColor;
            _hudStyle.normal.textColor = Color.black;
            GUI.Label(shadow, text, _hudStyle);
            _hudStyle.normal.textColor = c;
            GUI.Label(rect, text, _hudStyle);
        }
    }
}
