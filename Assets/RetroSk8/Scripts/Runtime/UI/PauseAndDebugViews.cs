using System.Collections.Generic;
using System.Text;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Player;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    public sealed class PauseMenuView : MonoBehaviour
    {
        private Text _hapticsLabel;
        private Text _musicLabel;
        private GameObject _skip;
        private Button _map;
        private System.Action _openMap;

        public void Build(RectTransform root, RunController run, BailHandler bail, System.Action toggleDebug, System.Action openPhoto = null, System.Func<string> saveReplay = null)
        {
            var dim = UIFactory.Panel("Dim", root, Theme.InkSoft, true);
            UIFactory.Stretch(dim.rectTransform);

            var title = UIFactory.TapeLabel("Title", root, "PAUSED", 90, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 0.84f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 140f));

            var col = UIFactory.Rect("Buttons", root);
            UIFactory.Place(col, new Vector2(0.5f, 0.4f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 760f));
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = layout.childControlWidth = false;

            UIFactory.MakeButton("Resume", col, "RESUME", new Vector2(560f, 84f), Theme.Tape, () => run.SetPaused(false));

            // City map (Retro City only, enabled by EnableMap) and photo mode share a row.
            var row = UIFactory.Rect("MapPhoto", col);
            row.sizeDelta = new Vector2(560f, 80f);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 20f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlHeight = rowLayout.childControlWidth = false;
            _map = UIFactory.MakeButton("Map", row, "MAP", new Vector2(270f, 80f), Theme.Teal, () => _openMap?.Invoke(), 38);
            _map.gameObject.SetActive(false);
            bool replayButton = saveReplay != null && (run.Mode == RunMode.FreeSkate || run.Mode == RunMode.Party);
            float w = replayButton ? 175f : 270f;
            _map.GetComponent<RectTransform>().sizeDelta = new Vector2(w, 80f);
            if (openPhoto != null) UIFactory.MakeButton("Photo", row, "PHOTO", new Vector2(w, 80f), Theme.Teal, openPhoto, 34);
            if (replayButton)
            {
                Text label = null;
                var save = UIFactory.MakeButton("SaveReplay", row, "SAVE REPLAY", new Vector2(w, 80f), Theme.Teal, () =>
                {
                    string id = saveReplay();
                    label.text = id != null ? "SAVED!" : "NOTHING YET";
                }, 26);
                label = save.GetComponentInChildren<Text>();
            }

            UIFactory.MakeButton("Respawn", col, "RESPAWN", new Vector2(560f, 84f), Theme.Cream, () => { run.SetPaused(false); bail.RespawnNow(); });
            if (run.Mode != RunMode.Duel) // a S.K.A.T.E. match only ends by winning, losing or quitting
            {
                UIFactory.MakeButton("Restart", col, "RESTART RUN", new Vector2(560f, 84f), Theme.Cream, run.Restart);
                UIFactory.MakeButton("EndRun", col, "END RUN", new Vector2(560f, 84f), Theme.Coral, run.EndRunNow);
            }
            if (Application.CanStreamedLevelBeLoaded(SceneNames.MainMenu))
                UIFactory.MakeButton("Quit", col, "QUIT TO MENU", new Vector2(560f, 84f), Theme.Cream, () =>
                {
                    run.SetPaused(false);
                    RetroSk8.Duel.DuelSession.End();
                    SceneManager.LoadScene(SceneNames.MainMenu);
                }, 40);
            // Haptics, the music button (park themes → radio stations → off) and NEXT SONG while the radio is on.
            var settingsRow = UIFactory.Rect("HapticsMusic", col);
            settingsRow.sizeDelta = new Vector2(560f, 72f);
            var srLayout = settingsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            srLayout.spacing = 10f;
            srLayout.childAlignment = TextAnchor.MiddleCenter;
            srLayout.childControlHeight = srLayout.childControlWidth = false;
            var haptics = UIFactory.MakeButton("Haptics", settingsRow, "", new Vector2(170f, 72f), Theme.Teal, ToggleHaptics, 26);
            _hapticsLabel = haptics.GetComponentInChildren<Text>();
            var music = UIFactory.MakeButton("Music", settingsRow, "", new Vector2(240f, 72f), Theme.Teal, () =>
            {
                RetroSk8.Audio.AudioManager.Ensure().CycleMusicMode();
                SaveManager.Save();
                RefreshHaptics();
            }, 20);
            _musicLabel = music.GetComponentInChildren<Text>();
            _skip = UIFactory.MakeButton("Skip", settingsRow, "NEXT >>", new Vector2(130f, 72f), Theme.Teal, () => RetroSk8.Audio.AudioManager.Instance?.SkipSong(), 24).gameObject;
            UIFactory.MakeButton("Debug", col, "DEBUG", new Vector2(300f, 64f), Theme.Cream, toggleDebug, 30);

            // Controller layout, shown while a controller is connected.
            _padHelp = UIFactory.Label("PadHelp", root, PadHelpText, 24, Theme.Cream, TextAnchor.LowerCenter, false);
            _padHelp.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_padHelp.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(2000f, 70f));
            RefreshHaptics();
        }

        private Text _padHelp;

        public const string PadHelpText =
            "CONTROLLER  ·  LEFT STICK STEER  ·  A / CROSS JUMP (HOLD TO CROUCH)  ·  RIGHT STICK, D-PAD, Y / TRIANGLE, B / CIRCLE TRICKS  ·  X / SQUARE OR R1 ACTION  ·  MENU PAUSE";

        private void OnEnable()
        {
            if (_padHelp != null) _padHelp.gameObject.SetActive(RetroSk8.Input.InputDeviceTracker.PadConnected);
        }

        /// <summary>Shows the first button of the MAP/PHOTO row: MAP in Retro City, EDIT PARK in a Create-a-Park park.</summary>
        public void EnableMap(System.Action openMap, string label = "MAP")
        {
            _openMap = openMap;
            _map.gameObject.SetActive(openMap != null);
            _map.GetComponentInChildren<Text>().text = label;
        }

        private void ToggleHaptics()
        {
            SaveManager.Data.settings.hapticsEnabled = !SaveManager.Data.settings.hapticsEnabled;
            SaveManager.Save();
            RefreshHaptics();
        }

        private void RefreshHaptics()
        {
            var settings = SaveManager.Data.settings;
            _hapticsLabel.text = settings.hapticsEnabled ? "HAPTICS: ON" : "HAPTICS: OFF";
            _musicLabel.text = RetroSk8.Core.Radio.ModeName((RetroSk8.Core.MusicMode)settings.musicMode, settings.radioStation);
            _skip.SetActive(settings.musicMode == (int)RetroSk8.Core.MusicMode.Radio);
        }
    }

    /// <summary>Developer overlay: live skater state plus save/progression and park-switching shortcuts.</summary>
    public sealed class DebugMenuView : MonoBehaviour
    {
        private PlayerController _player;
        private Text _stats;
        private Text _infiniteLabel;
        private Text _touchLabel;
        private Text _perfLabel;
        private GameObject _touchRoot;
        private float _fps;

        public void Build(RectTransform root, PlayerController player, BailHandler bail, ContentRegistry content, GameObject touchRoot, System.Action toggleTuning)
        {
            _player = player;
            _touchRoot = touchRoot;

            var panel = UIFactory.Panel("DebugPanel", root, new Color(0f, 0f, 0f, 0.78f), true);
            UIFactory.Place(panel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(760f, 960f));

            _stats = UIFactory.Label("Stats", panel.transform, "", 28, Theme.Cream, TextAnchor.UpperLeft, false);
            UIFactory.Place(_stats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(700f, 250f));

            var col = UIFactory.Rect("Buttons", panel.transform);
            UIFactory.Place(col, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(700f, 700f));
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlHeight = layout.childControlWidth = false;

            UIFactory.MakeButton("Tuning", col, "FEEL TUNING", new Vector2(660f, 52f), Theme.Teal, toggleTuning, 30);
            var inf = UIFactory.MakeButton("Infinite", col, "", new Vector2(660f, 52f), Theme.Tape, ToggleInfinite, 30);
            _infiniteLabel = inf.GetComponentInChildren<Text>();
            UIFactory.MakeButton("Respawn", col, "RESPAWN", new Vector2(660f, 52f), Theme.Cream, bail.RespawnNow, 30);
            UIFactory.MakeButton("Tokens", col, "+500 TAPE TOKENS", new Vector2(660f, 52f), Theme.Cream, () => SaveManager.AddTokens(500), 30);
            UIFactory.MakeButton("ResetSave", col, "RESET SAVE DATA", new Vector2(660f, 52f), Theme.Coral, SaveManager.ResetAll, 30);
            var touch = UIFactory.MakeButton("Touch", col, "", new Vector2(660f, 52f), Theme.Cream, ToggleTouch, 30);
            _touchLabel = touch.GetComponentInChildren<Text>();
            var perf = UIFactory.MakeButton("PerfHud", col, "", new Vector2(660f, 52f), Theme.Cream, TogglePerfHud, 30);
            _perfLabel = perf.GetComponentInChildren<Text>();

            foreach (var loc in content.locations)
            {
                if (loc == null) continue;
                var l = loc;
                var b = UIFactory.MakeButton("Park_" + l.id, col, "PARK: " + l.displayName.ToUpperInvariant() + (l.isPlayable ? "" : " (LOCKED)"),
                    new Vector2(660f, 44f), Theme.Teal, () => SwitchPark(l), 24);
                b.interactable = l.isPlayable;
            }
            Refresh();
        }

        private void ToggleInfinite()
        {
            GameSession.DebugInfiniteTime = !GameSession.DebugInfiniteTime;
            Refresh();
        }

        private void ToggleTouch()
        {
            SaveManager.Data.settings.showTouchControlsInEditor = !SaveManager.Data.settings.showTouchControlsInEditor;
            SaveManager.Save();
            if (_touchRoot != null) _touchRoot.SetActive(SaveManager.Data.settings.showTouchControlsInEditor || UIManager.IsTouchDevice);
            Refresh();
        }

        private void TogglePerfHud()
        {
            DevicePerformance.HudVisible = !DevicePerformance.HudVisible;
            Refresh();
        }

        private static void SwitchPark(LocationDefinition loc)
        {
            Time.timeScale = 1f;
            SceneRouter.LoadPark(loc.id, loc.sceneName);
        }

        private void Refresh()
        {
            _infiniteLabel.text = GameSession.DebugInfiniteTime ? "INFINITE TIME: ON" : "INFINITE TIME: OFF";
            _touchLabel.text = SaveManager.Data.settings.showTouchControlsInEditor ? "TOUCH UI IN EDITOR: ON" : "TOUCH UI IN EDITOR: OFF";
            _perfLabel.text = DevicePerformance.HudVisible ? "PERFORMANCE HUD: ON" : "PERFORMANCE HUD: OFF";
        }

        private void Update()
        {
            if (_player == null) return;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fps = Mathf.Lerp(_fps, 1f / dt, 0.1f);
            var sb = new StringBuilder();
            var perf = DevicePerformance.Instance;
            sb.AppendLine(perf != null
                ? $"FPS {_fps:0}   CPU {perf.CpuMs:0.0}ms  GPU {perf.GpuMs:0.0}ms  SCALE {perf.RenderScale:0.00}"
                : $"FPS {_fps:0}");
            sb.AppendLine($"TOKENS {SaveManager.Data.tapeTokens}");
            sb.AppendLine($"STATE {_player.State}   GROUNDED {_player.IsGrounded}");
            sb.AppendLine($"SPEED {_player.Speed:0.0} m/s   SIGN {_player.MovementSign:+0;-0}");
            sb.AppendLine($"AIR {_player.AirTime:0.00}s   YAW {_player.AirYaw:0}°");
            sb.AppendLine($"SLOPE {Vector3.Angle(_player.GroundNormal, Vector3.up):0}°   CHARGE {_player.JumpCharge:0.00}");
            sb.Append($"POS {_player.transform.position.x:0.0}, {_player.transform.position.y:0.0}, {_player.transform.position.z:0.0}");
            _stats.text = sb.ToString();
        }
    }
}
