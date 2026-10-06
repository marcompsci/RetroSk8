using RetroSk8.Audio;
using RetroSk8.Data;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RetroSk8.Game
{
    /// <summary>BootScene entry: applies platform settings, loads the save, starts persistent services, then routes onward.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public ContentRegistry content;

        private static bool s_applied;

        public static void ApplyRuntimeSettings()
        {
            if (s_applied) return;
            s_applied = true;
            AccessibilitySupport.ApplyFirstLaunchDefaults(); // Phase 20: start with the phone's Reduce Motion / text size
            RetroSk8.UI.Theme.ApplySettings(SaveManager.Data.settings);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            DevicePerformance.Ensure();
            RetroSk8.UI.ScreenReaderBridge.Ensure(); // Phase 20: VoiceOver reads the menus
        }

        private void Start()
        {
            ApplyRuntimeSettings();
            SaveManager.Load();
            AudioManager.Ensure();

            // Phase 1: no main menu yet, so boot straight into the vertical slice.
            string target = Application.CanStreamedLevelBeLoaded(SceneNames.MainMenu) ? SceneNames.MainMenu : SceneNames.HarborPlaza;
            if (target == SceneNames.HarborPlaza)
            {
                GameSession.LocationId = "harbor_plaza";
                GameSession.Mode = RunMode.TwoMinuteRun;
            }
            SceneManager.LoadScene(target);
        }
    }
}
