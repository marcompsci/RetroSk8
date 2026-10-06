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
        public float RenderScale => _baseScale * _governor.Scale;
        public bool Adaptive { get; private set; }

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
            if (!Adaptive) return;

            float shortSide = Mathf.Min(Screen.width, Screen.height);
            _baseScale = shortSide > TargetShortSide ? TargetShortSide / shortSide : 1f;
#if RETROSK8_URP
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = Mathf.Min(urp.shadowDistance, MobileShadowDistance);
                urp.renderScale = RenderScale;
            }
#endif
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fps = Mathf.Lerp(_fps, 1f / dt, 0.1f);

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
            string text = $"{_fps:0} FPS  CPU {CpuMs:0.0}ms  GPU {GpuMs:0.0}ms  SCALE {RenderScale:0.00}{gc}{hitch}";
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
