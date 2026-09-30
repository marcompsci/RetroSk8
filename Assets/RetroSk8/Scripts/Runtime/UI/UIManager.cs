using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Input;
using RetroSk8.Player;
using RetroSk8.Save;
using RetroSk8.Scoring;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroSk8.UI
{
    /// <summary>Builds the in-run canvases and switches between HUD, pause and debug layers.</summary>
    public sealed class UIManager : MonoBehaviour
    {
        private PlayerInputRouter _input;
        private RunController _run;
        private GameObject _pauseRoot;
        private GameObject _debugRoot;
        private GameObject _touchRoot;
        private GameObject _tuningRoot;

        public static bool IsTouchDevice => Application.isMobilePlatform || (Touchscreen.current != null && !Application.isEditor);

        public static UIManager Create(PlayerInputRouter input, PlayerController player, ComboManager combo,
            ScoreManager score, RunController run, ContentRegistry content, TuningSession tuning, GoalManager goals = null)
        {
            UIFactory.EnsureEventSystem();
            var ui = new GameObject("UI").AddComponent<UIManager>();
            ui.Build(input, player, combo, score, run, content, tuning, goals);
            return ui;
        }

        private void Build(PlayerInputRouter input, PlayerController player, ComboManager combo, ScoreManager score,
            RunController run, ContentRegistry content, TuningSession tuning, GoalManager goals)
        {
            _input = input;
            _run = run;
            var bail = player.GetComponent<BailHandler>();

            var hudCanvas = UIFactory.CreateCanvas("HUD", 0, transform);
            var hudSafe = UIFactory.SafeArea(hudCanvas.transform);
            var hud = hudSafe.gameObject.AddComponent<HudView>();
            hud.Build(hudSafe, player, combo, score, run);
            if (goals != null && goals.HasGoals)
            {
                string title = run.Mode == RunMode.DailyLine ? "DAILY LINE" : "SPOT CONTRACT";
                hudSafe.gameObject.AddComponent<GoalsHudView>().Build(hudSafe, goals, hud, title);
            }

            var pauseButton = UIFactory.MakeButton("PauseButton", hudSafe, "II", new Vector2(120f, 120f), Theme.Cream, run.TogglePause, 56);
            UIFactory.Place((RectTransform)pauseButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -28f), new Vector2(120f, 120f));

            var touchCanvas = UIFactory.CreateCanvas("TouchControls", 1, transform);
            var touchSafe = UIFactory.SafeArea(touchCanvas.transform);
            touchSafe.gameObject.AddComponent<TouchControlsView>().Build(touchSafe, input.Touch, touchCanvas);
            _touchRoot = touchCanvas.gameObject;
            _touchRoot.SetActive(IsTouchDevice || (Application.isEditor && SaveManager.Data.settings.showTouchControlsInEditor));
            // Pause button must sit above the touch layer to stay tappable.
            hudCanvas.sortingOrder = 2;

            var overlay = UIFactory.CreateCanvas("Overlay", 10, transform);
            var overlaySafe = UIFactory.SafeArea(overlay.transform);

            var pause = UIFactory.Rect("Pause", overlaySafe);
            UIFactory.Stretch(pause);
            pause.gameObject.AddComponent<PauseMenuView>().Build(pause, run, bail, ToggleDebug);
            _pauseRoot = pause.gameObject;
            _pauseRoot.SetActive(false);

            var debug = UIFactory.Rect("Debug", overlaySafe);
            UIFactory.Stretch(debug);
            var tuningRect = UIFactory.Rect("Tuning", overlaySafe);
            UIFactory.Stretch(tuningRect);
            tuningRect.gameObject.AddComponent<TuningPanelView>().Build(tuningRect, tuning);
            _tuningRoot = tuningRect.gameObject;
            _tuningRoot.SetActive(false);

            debug.gameObject.AddComponent<DebugMenuView>().Build(debug, player, bail, content, _touchRoot, ToggleTuning);
            _debugRoot = debug.gameObject;
            _debugRoot.SetActive(false);

            run.PauseChanged += paused =>
            {
                _pauseRoot.SetActive(paused);
                if (paused) input.Touch.ReleaseAll();
            };
            run.Finished += _ =>
            {
                _touchRoot.SetActive(false);
                _pauseRoot.SetActive(false);
            };
        }

        private void ToggleDebug() => _debugRoot.SetActive(!_debugRoot.activeSelf);
        private void ToggleTuning() => _tuningRoot.SetActive(!_tuningRoot.activeSelf);

        private void Update()
        {
            var f = _input.Frame;
            if (f.PausePressed) _run.TogglePause();
            if (f.DebugPressed) ToggleDebug();
        }
    }
}
