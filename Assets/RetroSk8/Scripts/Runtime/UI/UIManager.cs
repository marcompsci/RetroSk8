using RetroSk8.Core;
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
        private ScoreManager _score;
        private GameObject _pauseRoot;
        private GameObject _debugRoot;
        private GameObject _touchRoot;
        private GameObject _tuningRoot;
        private RectTransform _hudSafe;
        private RectTransform _overlaySafe;
        private PauseMenuView _pauseView;
        private PhotoModeView _photo;
        private GameObject _mapRoot;
        private GameObject _hudRoot;
        private readonly System.Collections.Generic.List<GameObject> _photoHides = new System.Collections.Generic.List<GameObject>();

        public HudView Hud { get; private set; }
        public bool PhotoActive => _photo != null && _photo.Active;
        public PhotoModeView Photo => _photo;

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
            _score = score;
            var bail = player.GetComponent<BailHandler>();

            var hudCanvas = UIFactory.CreateCanvas("HUD", 0, transform);
            var hudSafe = UIFactory.SafeArea(hudCanvas.transform);
            hudSafe.gameObject.AddComponent<SpeedLinesView>().Build(hudSafe, player);
            var hud = hudSafe.gameObject.AddComponent<HudView>();
            hud.Build(hudSafe, player, combo, score, run);
            Hud = hud;
            _hudSafe = hudSafe;
            if (goals != null && goals.HasGoals)
            {
                string title = run.Mode == RunMode.DailyLine ? "DAILY LINE" : "SPOT CONTRACT";
                hudSafe.gameObject.AddComponent<GoalsHudView>().Build(hudSafe, goals, hud, title);
            }
            hudSafe.gameObject.AddComponent<NowPlayingView>().Build(hudSafe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -226f));
            var challenge = GameSession.ActiveChallengeFor(GameSession.LocationId);
            if (challenge != null) hudSafe.gameObject.AddComponent<ChallengeHudView>().Build(hudSafe, score, challenge, run);
            var lesson = run.Mode == RunMode.FreeSkate ? TrickLessons.Find(GameSession.LessonId) : null;
            if (run.Mode == RunMode.Tutorial)
                hudSafe.gameObject.AddComponent<TutorialCoach>().Build(hudSafe, player, combo, run, hud);
            else if (lesson != null)
                hudSafe.gameObject.AddComponent<LessonCoach>().Build(hudSafe, lesson, player, combo); // Phase 17 trick lessons
            else if (run.Mode != RunMode.Replay && run.Mode != RunMode.Duel)
                hudSafe.gameObject.AddComponent<TipCoachView>().Build(hudSafe, player, combo, run); // one-time hints

            var pauseButton = UIFactory.MakeButton("PauseButton", hudSafe, "II", new Vector2(120f, 120f), Theme.Cream, run.TogglePause, 56);
            UIFactory.Place((RectTransform)pauseButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -28f), new Vector2(120f, 120f));

            _hudRoot = hudCanvas.gameObject;
            var touchCanvas = UIFactory.CreateCanvas("TouchControls", 1, transform);
            var touchSafe = UIFactory.SafeArea(touchCanvas.transform);
            touchSafe.gameObject.AddComponent<TouchControlsView>().Build(touchSafe, input.Touch, touchCanvas);
            touchSafe.gameObject.AddComponent<RetroSk8.Input.TouchAutoHide>().Init(input.Touch); // hidden while a controller is in use
            _touchRoot = touchCanvas.gameObject;
            _touchRoot.SetActive(IsTouchDevice || (Application.isEditor && SaveManager.Data.settings.showTouchControlsInEditor));
            // Pause button must sit above the touch layer to stay tappable.
            hudCanvas.sortingOrder = 2;

            var overlay = UIFactory.CreateCanvas("Overlay", 10, transform);
            var overlaySafe = UIFactory.SafeArea(overlay.transform);
            _overlaySafe = overlaySafe;

            var pause = UIFactory.Rect("Pause", overlaySafe);
            UIFactory.Stretch(pause);
            _pauseView = pause.gameObject.AddComponent<PauseMenuView>();
            _pauseView.Build(pause, run, bail, ToggleDebug, () => _photo.Enter(), SaveReplay);
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

            // Photo mode: its own top canvas; hides every other in-run canvas while it is open.
            var photoCanvas = UIFactory.CreateCanvas("PhotoMode", 20, transform);
            var photoSafe = UIFactory.SafeArea(photoCanvas.transform);
            _photoHides.Add(hudCanvas.gameObject);
            _photoHides.Add(_touchRoot);
            _photoHides.Add(overlay.gameObject);
            _photo = photoSafe.gameObject.AddComponent<PhotoModeView>();
            _photo.Build(photoSafe, player, _photoHides, null);

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

        /// <summary>Pass-and-play banner, standings and hand-off screens (RunMode.Party).</summary>
        public void AddPartyView(PartyController party)
        {
            // Its own canvas above the touch controls (so READY can be tapped) but below pause/debug.
            var canvas = UIFactory.CreateCanvas("Party", 5, transform);
            var safe = UIFactory.SafeArea(canvas.transform);
            safe.gameObject.AddComponent<PartyView>().Build(safe, party);
            _photoHides.Add(canvas.gameObject);
        }

        /// <summary>S.K.A.T.E. letters, turns and result screen (RunMode.Duel), above the touch controls.</summary>
        public void AddDuelView(RetroSk8.Duel.DuelController duel)
        {
            var canvas = UIFactory.CreateCanvas("Duel", 5, transform);
            var safe = UIFactory.SafeArea(canvas.transform);
            safe.gameObject.AddComponent<DuelView>().Build(safe, duel);
            _photoHides.Add(canvas.gameObject);
        }

        /// <summary>Retro City: district/toast/race HUD plus the pause-menu MAP.</summary>
        public void AddCity(CityController city)
        {
            _hudSafe.gameObject.AddComponent<CityHudView>().Build(_hudSafe, city);

            var map = UIFactory.Rect("CityMap", _overlaySafe);
            UIFactory.Stretch(map);
            map.gameObject.AddComponent<CityMapView>().Build(map, city, CloseMap, () =>
            {
                CloseMap();
                _run.SetPaused(false);
            });
            _mapRoot = map.gameObject;
            _mapRoot.SetActive(false);
            _pauseView.EnableMap(OpenMap);
            _run.PauseChanged += paused => { if (!paused) _mapRoot.SetActive(false); };
        }

        /// <summary>Create-a-Park editing: the editor screen replaces the HUD and touch controls.</summary>
        public void AddParkEditor(ParkEditorController editor)
        {
            var canvas = UIFactory.CreateCanvas("ParkEditor", 15, transform);
            var safe = UIFactory.SafeArea(canvas.transform);
            safe.gameObject.AddComponent<ParkEditorView>().Build(safe, editor);
            _hudRoot.SetActive(false);
            _touchRoot.SetActive(false);
            _editing = true;
        }

        private bool _editing;

        /// <summary>Replay editor: its own screen replaces the HUD, touch controls and pause.</summary>
        public void AddReplayTheater(RetroSk8.Replay.ReplayTheater theater)
        {
            var canvas = UIFactory.CreateCanvas("ReplayTheater", 15, transform);
            var safe = UIFactory.SafeArea(canvas.transform);
            safe.gameObject.AddComponent<ReplayTheaterView>().Build(safe, theater);
            _hudRoot.SetActive(false);
            _touchRoot.SetActive(false);
            _editing = true;
        }

        /// <summary>Pause-menu EDIT PARK in a custom park (reloads it in the editor).</summary>
        public void EnableEditPark(System.Action edit) => _pauseView.EnableMap(edit, "EDIT PARK");

        /// <summary>Pause → SAVE REPLAY (sessions that don't save automatically): keeps the last two minutes.</summary>
        private string SaveReplay()
        {
            var recorder = FindAnyObjectByType<RetroSk8.Replay.ReplayRecorder>();
            return recorder != null ? recorder.SaveToLibrary(GameSession.ModeLabel(_run.Mode), _score != null ? _score.Ledger.Total : 0) : null;
        }

        private void OpenMap()
        {
            _pauseRoot.SetActive(false);
            _mapRoot.SetActive(true);
        }

        private void CloseMap()
        {
            _mapRoot.SetActive(false);
            _pauseRoot.SetActive(_run.IsPaused);
        }

        private void ToggleDebug() => _debugRoot.SetActive(!_debugRoot.activeSelf);
        private void ToggleTuning() => _tuningRoot.SetActive(!_tuningRoot.activeSelf);

        private void Update()
        {
            var f = _input.Frame;
            if (_editing) return; // the editor has its own buttons; Esc/arrows belong to it
            if (PhotoActive)
            {
                if (f.PausePressed) _photo.Exit(); // Esc / pause leaves photo mode first
                return;
            }
            if (f.PausePressed)
            {
                if (_mapRoot != null && _mapRoot.activeSelf) { CloseMap(); return; }
                _run.TogglePause();
            }
            if (f.DebugPressed) ToggleDebug();
        }
    }
}
