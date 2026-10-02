using System;
using System.Collections;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Player;
using RetroSk8.Save;
using RetroSk8.Scoring;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RetroSk8.Game
{
    /// <summary>
    /// Run lifecycle: timer, pause, end-of-run (the combo in progress may finish), saving and the hand-off to Results.
    /// </summary>
    public sealed class RunController : MonoBehaviour
    {
        public float maxOvertime = 8f;
        public float resultsDelay = 1.4f;
        public bool loadResultsScene = true;

        private PlayerController _player;
        private ComboManager _combo;
        private ScoreManager _score;
        private LocationDefinition _location;
        private ScoringProfile _profile;
        private float _overtime;
        private bool _finished;

        public RunTimer Timer { get; private set; }
        public RunMode Mode { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsEnding { get; private set; }

        public event Action<bool> PauseChanged;
        public event Action TimeUp;
        public event Action<RunResult> Finished;

        private GoalManager _goals;

        public void Init(PlayerController player, ComboManager combo, ScoreManager score, LocationDefinition location,
            ScoringProfile profile, GoalManager goals = null)
        {
            _goals = goals;
            _player = player;
            _combo = combo;
            _score = score;
            _location = location;
            _profile = profile;
            Mode = GameSession.Mode;
            Timer = new RunTimer(Mode == RunMode.FreeSkate || Mode == RunMode.Tutorial ? 0f : location.runDurationSeconds);
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (Timer == null || IsPaused || _finished) return;

            if (!IsEnding)
            {
                if (!GameSession.DebugInfiniteTime && Timer.Tick(Time.deltaTime)) BeginEnding();
                return;
            }

            // Overtime: let the current combo land or bail, then finish.
            _overtime += Time.deltaTime;
            bool comboOpen = _combo.Tracker.IsActive || _combo.HasPendingBank;
            bool midLine = _player.State == SkaterState.Airborne || _player.State == SkaterState.Grinding || _player.State == SkaterState.Manual;
            if (!comboOpen && !midLine) Finish();
            else if (_overtime >= maxOvertime)
            {
                _combo.BankNow();
                Finish();
            }
        }

        private void BeginEnding()
        {
            IsEnding = true;
            _overtime = 0f;
            TimeUp?.Invoke();
        }

        public void TogglePause() => SetPaused(!IsPaused);

        public void SetPaused(bool paused)
        {
            if (_finished) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            PauseChanged?.Invoke(paused);
        }

        public void Restart()
        {
            SetPaused(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>End immediately from the pause menu; an unfinished combo is discarded.</summary>
        public void EndRunNow()
        {
            SetPaused(false);
            _combo.Discard();
            Finish();
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            _player.SetInputEnabled(false);
            _combo.AcceptingTricks = false;

            var ledger = _score.Ledger;
            bool scored = Mode != RunMode.FreeSkate && Mode != RunMode.Tutorial;
            bool hasGoals = _goals != null && _goals.HasGoals;

            // Goal rewards pay once per goal ever (contracts) or once per day (Daily Line).
            int firstTimeGoals = 0;
            bool allFirstTime = false;
            bool dailyBonus = false;
            if (hasGoals && Mode == RunMode.SpotContract)
                (firstTimeGoals, allFirstTime) = SaveManager.RecordContract(_location.id, _goals.CompletedGoalIds(), _goals.Tracker.Total);
            if (hasGoals && Mode == RunMode.DailyLine)
                dailyBonus = SaveManager.RecordDaily(GameSession.TodayKey, ledger.Total, _goals.Tracker.AllComplete);

            var award = TokenRewards.ForRun(_profile.scoring, ledger.Total, scored, firstTimeGoals, allFirstTime, dailyBonus);
            int tokens = award.Total;
            bool newBest = scored && SaveManager.RecordRun(_location.id, ledger.Total, ledger.BestCombo, ledger.BestComboLabel, tokens);

            var result = new RunResult
            {
                modeLabel = GameSession.ModeLabel(Mode),
                tokensFromScore = award.FromScore,
                tokensFromGoals = award.FromGoals,
                tokensFromDaily = award.FromDailyBonus,
                dailyTargetScore = _goals != null && _goals.Daily != null ? _goals.Daily.TargetScore : 0,
                goalsCompleted = hasGoals ? _goals.Tracker.CompletedCount : 0,
                goalsTotal = hasGoals ? _goals.Tracker.Total : 0,
                locationId = _location.id,
                locationName = _location.displayName,
                mode = Mode,
                score = ledger.Total,
                bestCombo = ledger.BestCombo,
                bestComboLabel = ledger.BestComboLabel,
                combosBanked = ledger.CombosBanked,
                bails = ledger.Bails,
                tapeTokensEarned = tokens,
                newBest = newBest,
            };
            if (hasGoals)
            {
                foreach (var g in _goals.Tracker.Goals)
                {
                    result.goalDescriptions.Add(g.Goal.description);
                    result.goalCompleted.Add(g.Completed);
                }
            }
            GameSession.LastResult = result;
            Finished?.Invoke(result);
            if (loadResultsScene) StartCoroutine(LoadResults());
        }

        private IEnumerator LoadResults()
        {
            yield return new WaitForSecondsRealtime(resultsDelay);
            if (Application.CanStreamedLevelBeLoaded(SceneNames.Results)) SceneManager.LoadScene(SceneNames.Results);
            else Debug.LogWarning("[RetroSk8] ResultsScene is not in Build Settings. Run 'Retro Sk8 > Setup Project'.");
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
