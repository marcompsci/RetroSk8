#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using RetroSk8.Core;
using RetroSk8.Input;
using RetroSk8.Player;
using RetroSk8.Save;
using RetroSk8.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RetroSk8.Game
{
    /// <summary>
    /// Phase 28: takes the App Store screenshots by itself (editor only). The remote bridge's "storeshots" command
    /// writes <see cref="RequestPath"/> and enters Play mode; this then plays a short scripted tour on a separate
    /// showcase save (your progress is never touched), freezes each moment, and renders it in every language at both
    /// sizes App Store Connect asks for, UI included:
    ///   StoreScreenshots/en|es|ja/iPhone69/NN_name.png (2868 x 1320) and .../iPad13/NN_name.png (2752 x 2064).
    /// The canvases are switched to camera space for the render, so the UI is laid out for each size rather than
    /// cropped from the Game view.
    /// </summary>
    public sealed class StoreShotDirector : MonoBehaviour
    {
        public const string RequestPath = "Temp/RetroSk8StoreShots.request";
        public const string DonePath = "Temp/RetroSk8StoreShots.done";
        public const string Folder = "StoreScreenshots";
        private const string ShowcaseSave = "retrosk8_storeshots.json";

        public static readonly (string set, int w, int h)[] Sizes = { ("iPhone69", 2868, 1320), ("iPad13", 2752, 2064) };

        private readonly List<string> _written = new List<string>();
        private readonly List<string> _problems = new List<string>();
        private int _shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void MaybeStart()
        {
            if (!File.Exists(RequestPath)) return;
            try { File.Delete(RequestPath); } catch (IOException) { }
            var go = new GameObject("StoreShotDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<StoreShotDirector>();
        }

        private IEnumerator Start()
        {
            try { if (File.Exists(DonePath)) File.Delete(DonePath); } catch (IOException) { }
            // Keep playing while Unity isn't the front app (the project's Run In Background is off for the phone).
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            // Always put the editor back (time scale, layout size, save file, language) and leave Play mode,
            // even if a step of the tour fails.
            try
            {
                PrepareShowcaseSave();
                yield return Tour();
            }
            finally { Finish(); }
        }

        private IEnumerator Tour()
        {
            // 1. The park picker at night, Subway Depot selected.
            yield return LoadScene(SceneNames.MainMenu, 2.5f);
            var menu = FindAnyObjectByType<MainMenuView>();
            if (menu != null) { GameSession.ParkTime = ParkTime.Night; SaveManager.Data.settings.parkTime = (int)ParkTime.Night; menu.ShowcasePlayPanel(Level.ParkCatalog.SubwayDepot); }
            yield return Settle(0.6f);
            yield return ShootAll("park_picker");

            // 2. Subway Depot at night, mid-air off the kicker area.
            yield return Skate(Level.ParkCatalog.SubwayDepot, ParkTime.Night, RunMode.TwoMinuteRun, SwipeDirection.Up, "subway_night");
            // 3. Harbor Plaza at dusk with a grab.
            yield return Skate(Level.ParkCatalog.HarborPlaza, ParkTime.Dusk, RunMode.TwoMinuteRun, SwipeDirection.Down, "harbor_dusk");
            // 4. Shipyard in daylight with a spin.
            yield return Skate(Level.ParkCatalog.Shipyard, ParkTime.Day, RunMode.FreeSkate, SwipeDirection.Left, "shipyard_day");
            // 5. Retro City (explore).
            yield return Skate(Level.ParkCatalog.RetroCity, ParkTime.Day, RunMode.FreeSkate, SwipeDirection.Right, "retro_city");

            // 6. The deck designer.
            yield return LoadScene(SceneNames.Customization, 2f);
            var custom = FindAnyObjectByType<CustomizationView>();
            if (custom != null) custom.ShowcaseDeckDesigner();
            else _problems.Add("deck designer: CustomizationView not found");
            yield return Settle(0.6f);
            yield return ShootAll("deck_designer");
        }

        private IEnumerator Skate(string parkId, ParkTime time, RunMode mode, SwipeDirection trick, string name)
        {
            GameSession.ParkTime = time;
            GameSession.Mode = mode;
            GameSession.LessonId = null;
            var content = ContentRegistry();
            var loc = content != null ? content.FindLocation(parkId) : null;
            SceneRouter.LoadPark(parkId, loc != null ? loc.sceneName : null);
            yield return null;
            yield return Settle(2.2f);
            var installer = FindAnyObjectByType<SkateSceneInstaller>();
            if (installer == null || installer.Player == null) { _problems.Add(name + ": the park didn't load"); yield break; }
            var input = new ScriptedInputSource();
            installer.InputRouter.AddSource(input);
            installer.InputRouter.IgnoreDevices();
            var player = installer.Player;
            var spawn = installer.Level != null ? installer.Level.spawnPoint : null;
            // Roll, pop, start a trick, and freeze near the top of the ollie. A bail (a bench in the way) tries again
            // from the spawn, up to three times.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (attempt > 0 && spawn != null) player.Teleport(spawn.position, spawn.forward);
                float end = Time.time + 4f;
                while (player.State != SkaterState.Rolling && Time.time < end) yield return null;
                yield return Settle(1.4f);
                input.JumpHeld = true;
                yield return Settle(0.28f);
                input.JumpHeld = false;
                end = Time.time + 1f;
                while (player.State != SkaterState.Airborne && Time.time < end) yield return null;
                input.Swipe(trick);
                yield return Settle(0.3f);
                if (player.State == SkaterState.Airborne) break;
                yield return Settle(2.5f); // let a bail and respawn play out
            }
            yield return ShootAll(name);
        }

        // ---------------------------------------------------------------- shooting

        private IEnumerator ShootAll(string name)
        {
            _shot++;
            float scale = Time.timeScale;
            Time.timeScale = 0f; // the same moment in every language
            for (int l = 0; l < Loc.LangCount; l++)
            {
                LocSetup.Use((Lang)l);
                yield return null;
                LocText.Pump();
                foreach (var (set, w, h) in Sizes)
                {
                    string dir = Path.Combine(Folder, Loc.LangCodes[l], set);
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, $"{_shot:00}_{name}.png");
                    yield return Capture(w, h, path);
                }
            }
            LocSetup.Use(Lang.English);
            Time.timeScale = scale;
        }

        private IEnumerator Capture(int w, int h, string path)
        {
            var cam = Camera.main;
            if (cam == null) { _problems.Add(path + ": no camera"); yield break; }
            var canvases = new List<(Canvas c, RenderMode mode, Camera cam, float plane)>();
            foreach (var c in FindObjectsByType<Canvas>())
            {
                if (!c.isRootCanvas || !c.isActiveAndEnabled) continue;
                canvases.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = cam.nearClipPlane + 0.05f;
                }
            }
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var oldTarget = cam.targetTexture;
            try
            {
                AdaptiveCanvasScaler.OverrideSize = new Vector2Int(w, h);
                cam.targetTexture = rt;
                cam.ResetAspect();
                // Two frames: canvas scalers pick up the new size, then layouts settle.
                yield return new WaitForEndOfFrame();
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false); // no alpha: App Store Connect rejects it
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                try
                {
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    _written.Add(path);
                }
                catch (Exception e) { _problems.Add(path + ": " + e.Message); }
                Destroy(tex);
            }
            finally
            {
                if (cam != null)
                {
                    cam.targetTexture = oldTarget;
                    cam.ResetAspect();
                }
                RenderTexture.ReleaseTemporary(rt);
                AdaptiveCanvasScaler.OverrideSize = null;
                foreach (var (c, mode, worldCam, plane) in canvases)
                {
                    if (c == null) continue;
                    c.renderMode = mode;
                    c.worldCamera = worldCam;
                    c.planeDistance = plane;
                }
            }
            yield return null;
        }

        // ---------------------------------------------------------------- setup

        private void PrepareShowcaseSave()
        {
            SaveManager.UseFile(ShowcaseSave);
            try { if (File.Exists(SaveManager.FilePath)) File.Delete(SaveManager.FilePath); } catch (IOException) { }
            SaveManager.UseFile(ShowcaseSave);
            var d = SaveManager.Data;
            d.settings.tutorialDone = true;
            d.settings.tutorialRewarded = true;
            d.onboardingDone = true;
            foreach (TipId tip in Enum.GetValues(typeof(TipId))) if (!d.tips.seen.Contains((int)tip)) d.tips.seen.Add((int)tip);
            d.settings.playerName = "OMARI";
            d.settings.musicMode = 0;
            d.tapeTokens = 4820;
            // Already checked in today (no streak card over the menu), with a streak worth showing.
            d.streak.lastDay = Streaks.DayNumber(DateTime.Now);
            d.streak.current = 12;
            d.streak.best = 12;
            // A designed board for the designer shot.
            var look = d.look;
            look.customBoard = true;
            look.customGrip = true;
            look.board = new BoardArt { pattern = (int)BoardPattern.Sunrise, primary = 6, secondary = 9, sticker = 0 };
            look.board.layers.Add(new StickerLayer { sticker = 13, color = 2, x = 4, y = 46 });
            look.board.layers.Add(new StickerLayer { sticker = 19, color = 1, x = 0, y = 4, big = true });
            look.board.layers.Add(new StickerLayer { sticker = 1, color = 3, x = 9, y = 30, rot = 1 });
            for (int y = 22; y < 26; y++) for (int x = 0; x < DeckDesign.Width; x++) DeckDesign.Paint(look.board, x, y, (x + y) % 2 == 0 ? 1 : 2);
            look.grip = new GripArt { pattern = (int)GripPattern.Bolt, color = 0, accent = 4 };
            d.nightLines.Add(Level.ParkCatalog.HarborPlaza);
            SaveManager.Save();
        }

        private static RetroSk8.Data.ContentRegistry ContentRegistry()
        {
            var menu = FindAnyObjectByType<MainMenuView>();
            if (menu != null && menu.content != null) return RetroSk8.Data.ContentRegistry.WithDefaults(menu.content);
            var installer = FindAnyObjectByType<SkateSceneInstaller>();
            if (installer != null && installer.content != null) return installer.content;
            return RetroSk8.Data.DefaultContent.CreateRegistry();
        }

        private static IEnumerator LoadScene(string scene, float settle)
        {
            SceneRouter.Load(scene);
            yield return null;
            yield return Settle(settle);
        }

        private static IEnumerator Settle(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }

        private bool _finished;
        private bool _runInBackground;

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            Time.timeScale = 1f;
            AdaptiveCanvasScaler.OverrideSize = null;
            SaveManager.UseFile(null);
            Application.runInBackground = _runInBackground;
            LocSetup.Apply();
            string report = $"STORE SHOTS {DateTime.Now:yyyy-MM-dd HH:mm}  {_written.Count} written, {_problems.Count} problems\n"
                + string.Join("\n", _problems) + (_problems.Count > 0 ? "\n" : "") + string.Join("\n", _written) + "\n";
            try { File.WriteAllText(DonePath, report); } catch (IOException) { }
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }
}
#endif
