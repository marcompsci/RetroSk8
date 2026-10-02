using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Player;
using RetroSk8.Save;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Turns the save file into <see cref="PlayerProgress"/>, unlocks achievements locally, and mirrors
    /// scores and achievements to Game Center when it is signed in. Local records always work.
    /// </summary>
    public static class ProgressService
    {
        public static PlayerProgress Snapshot(ContentRegistry content)
        {
            var d = SaveManager.Data;
            var p = new PlayerProgress
            {
                CombosBanked = d.stats.combosBanked,
                MaxHalfTurns = d.stats.maxHalfTurns,
                DistinctGaps = d.stats.gapIds.Count,
                DailyClears = d.daily.clears,
                TutorialDone = d.settings.tutorialDone && d.settings.tutorialRewarded,
            };
            foreach (var r in d.locations)
            {
                if (r.bestCombo > p.BestCombo) p.BestCombo = r.bestCombo;
                if (r.bestScore > p.BestRunScore) p.BestRunScore = r.bestScore;
            }
            if (content != null)
            {
                foreach (var c in content.contracts)
                {
                    if (c == null) continue;
                    p.ContractsTotal++;
                    if (d.Contract(c.locationId).allComplete) p.ContractsComplete++;
                }
                foreach (var l in content.locations)
                {
                    if (l == null || !l.isPlayable) continue;
                    p.ParksTotal++;
                    if (d.stats.parksPlayed.Contains(l.id)) p.ParksPlayed++;
                }
            }
            return p;
        }

        /// <summary>Unlocks anything newly earned, saves, reports to Game Center, and returns what was unlocked.</summary>
        public static List<AchievementDefinition> Evaluate(ContentRegistry content)
        {
            var progress = Snapshot(content);
            var unlocked = Achievements.NewlyUnlocked(progress, SaveManager.Data.achievements);
            foreach (var a in unlocked)
            {
                SaveManager.Data.achievements.Add(a.Id);
                GameCenter.ReportAchievement(Achievements.GameCenterId(a), 1f);
            }
            if (unlocked.Count > 0) SaveManager.Save();
            return unlocked;
        }

        /// <summary>Pushes every best score and achievement to Game Center (safe to repeat; Game Center keeps the max).</summary>
        public static void SyncGameCenter(ContentRegistry content)
        {
            if (!GameCenter.IsAuthenticated) return;
            foreach (var r in SaveManager.Data.locations)
                if (r.bestScore > 0) GameCenter.SubmitScore(Achievements.LeaderboardId(r.locationId), r.bestScore);
            var progress = Snapshot(content);
            foreach (var a in Achievements.All)
            {
                float pct = a.Progress(progress);
                if (pct > 0f) GameCenter.ReportAchievement(Achievements.GameCenterId(a), pct);
            }
        }
    }

    /// <summary>Collects this run's achievement stats (spins, gaps, banks) and settles them when the run ends.</summary>
    public sealed class AchievementHook : MonoBehaviour
    {
        private RunController _run;
        private ContentRegistry _content;
        private string _locationId;
        private int _maxHalfTurns;
        private int _banks;
        private readonly HashSet<string> _gaps = new HashSet<string>();

        public void Init(PlayerController player, ComboManager combo, RunController run, ContentRegistry content, string locationId)
        {
            _run = run;
            _content = content;
            _locationId = locationId;
            player.Landed += v => { if (v.Quality != LandingQuality.Bail && v.HalfTurns > _maxHalfTurns) _maxHalfTurns = v.HalfTurns; };
            combo.Banked += (result, _, __) => { if (result.Points > 0) _banks++; };
            var gaps = player.GetComponent<GapTracker>();
            if (gaps != null) gaps.GapCleared += z => { if (z != null && !string.IsNullOrEmpty(z.gapId)) _gaps.Add(z.gapId); };
            run.Finished += OnFinished;
        }

        private void OnFinished(RunResult result)
        {
            var stats = SaveManager.Data.stats;
            stats.combosBanked += _banks;
            if (_maxHalfTurns > stats.maxHalfTurns) stats.maxHalfTurns = _maxHalfTurns;
            foreach (var g in _gaps) if (!stats.gapIds.Contains(g)) stats.gapIds.Add(g);
            if (!stats.parksPlayed.Contains(_locationId)) stats.parksPlayed.Add(_locationId);
            SaveManager.Save();

            foreach (var a in ProgressService.Evaluate(_content)) result?.newAchievements.Add(a.Title);
            if (result != null && result.newBest) GameCenter.SubmitScore(Achievements.LeaderboardId(_locationId), result.score);
        }

        private void OnDestroy()
        {
            if (_run != null) _run.Finished -= OnFinished;
        }
    }
}
