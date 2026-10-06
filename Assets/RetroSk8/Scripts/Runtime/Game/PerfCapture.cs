using System.Collections.Generic;
using System.IO;
using RetroSk8.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RetroSk8.Game
{
    /// <summary>
    /// On-device profiling (Phase 18): records every scene's frame times, hitches and (in Development builds)
    /// garbage per frame, plus how long the scene took to load, into perf_log.txt in the app's save folder. Each
    /// finished scene also prints one "[RetroSk8 PERF]" line to Xcode's console. The debug menu's COPY PERF LOG puts
    /// the log on the clipboard so it can be pasted anywhere. Lives on the DevicePerformance object.
    /// </summary>
    public sealed class PerfCapture : MonoBehaviour
    {
        public const string FileName = "perf_log.txt";
        public const int KeepLines = 40;

        private readonly PerfStats _stats = new PerfStats();
        private ProfilerRecorder _gc;
        private string _scene = "";
        private float _loadStartedAt = -1f;
        private float _loadSeconds;
        private bool _waitingFirstFrame;
        private readonly List<string> _lines = new List<string>();

        public static PerfCapture Instance { get; private set; }
        public PerfStats Current => _stats;
        public static string LogPath => Path.Combine(Application.persistentDataPath, FileName);

        private void OnEnable()
        {
            Instance = this;
            // "GC Allocated In Frame" is only counted in Development builds; Valid is false otherwise.
            _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            SceneManager.sceneUnloaded += OnUnloaded;
            SceneManager.sceneLoaded += OnLoaded;
            _scene = SceneManager.GetActiveScene().name;
            LoadLog();
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnUnloaded;
            SceneManager.sceneLoaded -= OnLoaded;
            Flush();
            _gc.Dispose();
            if (Instance == this) Instance = null;
        }

        private void OnUnloaded(Scene s)
        {
            if (s.name != _scene) return;
            Flush();
            _loadStartedAt = Time.realtimeSinceStartup;
        }

        private void OnLoaded(Scene s, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            _scene = s.name;
            _stats.Reset();
            _waitingFirstFrame = true;
        }

        private void Update()
        {
            if (_waitingFirstFrame)
            {
                // The first frame after a load is the load itself: time it, then start counting from the next one.
                _waitingFirstFrame = false;
                _loadSeconds = _loadStartedAt > 0f ? Time.realtimeSinceStartup - _loadStartedAt : 0f;
                _loadStartedAt = -1f;
                return;
            }
            if (Time.timeScale <= 0f) return; // paused menus don't count
            long gc = _gc.Valid ? _gc.LastValue : -1;
            _stats.Add(Time.unscaledDeltaTime * 1000f, gc);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        /// <summary>Writes the current scene's stats as one line (skipped when it's too short to mean anything).</summary>
        public void Flush()
        {
            if (_stats.Frames < 60) return;
            string line = System.DateTime.Now.ToString("MM-dd HH:mm") + "  " + _stats.Summary(_scene, _loadSeconds) + "  → " + _stats.Verdict();
            _stats.Reset();
            _loadSeconds = 0f;
            Debug.Log("[RetroSk8 PERF] " + line);
            _lines.Add(line);
            while (_lines.Count > KeepLines) _lines.RemoveAt(0);
            try { File.WriteAllLines(LogPath, _lines); }
            catch (IOException e) { Debug.LogWarning("[RetroSk8] perf log: " + e.Message); }
        }

        /// <summary>The whole log plus the scene being played right now, for the clipboard.</summary>
        public string Report()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"RETRO SK8 PERF  {Application.version}  {SystemInfo.deviceModel}  iOS {SystemInfo.operatingSystem}  {Screen.width}x{Screen.height}");
            foreach (var l in _lines) sb.AppendLine(l);
            if (_stats.Frames > 0) sb.AppendLine("now  " + _stats.Summary(_scene, 0f) + "  → " + _stats.Verdict());
            return sb.ToString();
        }

        private void LoadLog()
        {
            _lines.Clear();
            try
            {
                if (File.Exists(LogPath)) _lines.AddRange(File.ReadAllLines(LogPath));
            }
            catch (IOException) { }
            while (_lines.Count > KeepLines) _lines.RemoveAt(0);
        }
    }
}
