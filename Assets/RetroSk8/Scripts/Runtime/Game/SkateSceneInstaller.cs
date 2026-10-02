using System.Collections.Generic;
using RetroSk8.Audio;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Input;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Replay;
using RetroSk8.Save;
using RetroSk8.Scoring;
using RetroSk8.UI;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Composition root for a skate scene. The scene itself only holds this component, a light and a camera;
    /// everything else is created and wired here in a fixed order so dependencies are explicit.
    /// Works when played directly from the editor (no Boot scene required).
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class SkateSceneInstaller : MonoBehaviour
    {
        public ContentRegistry content;
        public LocationDefinition location;

        public PlayerController Player { get; private set; }
        public RunController Run { get; private set; }
        public PlayerInputRouter InputRouter { get; private set; }
        public ComboManager Combo { get; private set; }
        public ScoreManager Score { get; private set; }
        public LevelInfo Level { get; private set; }
        public TuningSession Tuning { get; private set; }
        /// <summary>Runtime copy of the scoring profile, so live tuning never edits the asset on disk.</summary>
        public ScoringProfile Profile { get; private set; }
        public GoalManager Goals { get; private set; }
        [Tooltip("Tests turn this off to stay in the skate scene after the run ends.")]
        public bool loadResultsScene = true;
        [Tooltip("Apply the feel-tuning preset saved from the in-game tuning panel. Tests turn this off for determinism.")]
        public bool applySavedTuning = true;
        [Tooltip("Record the run for the best-run ghost and show the saved ghost in Two-Minute Runs.")]
        public bool enableReplays = true;

        public ReplayRecorder Recorder { get; private set; }
        public GhostPlayer Ghost { get; private set; }

        private void Awake()
        {
            GameBootstrap.ApplyRuntimeSettings();
            if (content == null)
            {
                Debug.LogWarning("[RetroSk8] No ContentRegistry assigned; using in-memory defaults. Run 'Retro Sk8 > Setup Project'.");
                content = DefaultContent.CreateRegistry();
            }
            if (location == null) location = content.FindLocation(GameSession.LocationId);
            GameSession.LocationId = location.id;
            PlaceholderMaterials.SetBase(content.baseLitMaterial);
            ApplyLook(location);

            Profile = content.scoringProfile != null ? Instantiate(content.scoringProfile) : DefaultContent.CreateScoringProfile();
            LevelInfo level = BuildLevel();
            Level = level;

            var systems = new GameObject("Systems");
            var input = systems.AddComponent<PlayerInputRouter>();
            var score = systems.AddComponent<ScoreManager>();
            var combo = systems.AddComponent<ComboManager>();
            combo.Init(Profile, score);
            InputRouter = input;
            Combo = combo;
            Score = score;

            Player = SkaterFactory.Create(level.spawnPoint, input, combo, content, Profile, level);

            Goals = systems.AddComponent<GoalManager>();
            DailyLine daily = null;
            List<GoalDefinition> goals = null;
            if (GameSession.Mode == RunMode.SpotContract)
            {
                var contract = content.FindContract(location.id);
                if (contract != null) goals = contract.goals;
            }
            else if (GameSession.Mode == RunMode.DailyLine)
            {
                // Same gap list the menu previews (ParkCatalog), so the menu and the run always show identical goals.
                daily = DailyLineGenerator.Generate(GameSession.TodayKey, location.id, ParkCatalog.GapsFor(location.id));
                goals = daily.Goals;
            }
            Goals.Init(goals, daily, Player, combo, score);

            Run = systems.AddComponent<RunController>();
            Run.loadResultsScene = loadResultsScene;
            Run.Init(Player, combo, score, location, Profile, Goals);

            systems.AddComponent<AchievementHook>().Init(Player, combo, Run, content, location.id);
            if (enableReplays) SetUpReplays(systems);

            var cameraRig = CameraRig.Create(Player);
            Tuning = new TuningSession(Player, Profile, cameraRig);
            int applied = applySavedTuning ? Tuning.LoadSaved() : 0;
            if (applied > 0) Debug.Log($"[RetroSk8] Applied {applied} saved tuning values from {TuningSession.PresetPath}");

            UIManager.Create(input, Player, combo, score, Run, content, Tuning, Goals);

            var audio = AudioManager.Ensure();
            audio.PlayMusic(AudioManager.TrackFor(location.ambience));
            audio.PlayAmbience(location.ambience);
        }

        private void SetUpReplays(GameObject systems)
        {
            var visual = Player.GetComponentInChildren<SkaterVisual>();
            Recorder = systems.AddComponent<ReplayRecorder>();
            Recorder.Init(visual, Run, location.id);

            // The ghost races you in the mode it was set in spirit for: the timed run.
            if (GameSession.Mode == RunMode.TwoMinuteRun && !SaveManager.Data.settings.ghostHidden)
            {
                var track = GhostStore.Load(location.id);
                if (track != null) Ghost = GhostPlayer.Create(track, Run);
            }

            systems.AddComponent<ClipRunHook>().Init(Run);
        }

        public static List<DailyLineGenerator.Gap> GapList(LevelInfo level)
        {
            var list = new List<DailyLineGenerator.Gap>();
            foreach (var g in level.gaps)
                if (g != null) list.Add(new DailyLineGenerator.Gap(g.gapId, g.displayName));
            return list;
        }

        private LevelInfo BuildLevel()
        {
            // A park scene may already hold its builder; otherwise add the one that matches the location.
            var builder = FindFirstObjectByType<ParkBuilder>();
            if (builder == null)
                builder = ParkCatalog.AddBuilder(new GameObject(location.displayName), location.id);
            return builder.Build();
        }

        private static void ApplyLook(LocationDefinition loc)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = loc.ambientColor;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = loc.fogColor;
            RenderSettings.fogDensity = loc.fogDensity;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = loc.skyColor;
            }

            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.color = loc.sunColor;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(loc.sunEuler);
        }
    }
}
