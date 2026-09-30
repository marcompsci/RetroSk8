using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Player;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Feeds gameplay events into the engine-free GoalTracker for Spot Contracts and the Daily Line.
    /// Rails and tricks only count once their combo banks, so bailed lines never tick a goal.
    /// </summary>
    public sealed class GoalManager : MonoBehaviour
    {
        private readonly List<int> _pendingRails = new List<int>();
        private readonly List<string> _pendingTricks = new List<string>();
        private float _manualStart = -1f;

        public GoalTracker Tracker { get; private set; } = new GoalTracker(null);
        public bool HasGoals => Tracker.Total > 0;
        public DailyLine Daily { get; private set; }

        public event Action<GoalTracker.GoalState> GoalCompleted;

        public void Init(IEnumerable<GoalDefinition> goals, DailyLine daily, PlayerController player, ComboManager combo, ScoreManager score)
        {
            Tracker = new GoalTracker(goals);
            Daily = daily;
            if (!HasGoals) return;

            combo.TrickAdded += (name, _) => _pendingTricks.Add(name);
            combo.Banked += (result, label, quality) =>
            {
                Tracker.OnBanked(result.Points, result.TrickCount, result.FlowMultiplier);
                Tracker.OnRailsBanked(_pendingRails);
                Tracker.OnTricksBanked(_pendingTricks);
                ClearPending();
                Flush();
            };
            combo.Bailed += (_, __) => ClearPending();
            combo.Discarded += ClearPending;
            score.TotalChanged += total =>
            {
                Tracker.OnScoreChanged(total);
                Flush();
            };

            player.GetComponent<GrindController>().GrindStarted += rail => _pendingRails.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(rail)); // identity hash: distinct per rail
            var gaps = player.GetComponent<GapTracker>();
            if (gaps != null) gaps.GapCleared += zone =>
            {
                Tracker.OnGapCleared(zone.gapId);
                Flush();
            };
            player.Landed += verdict =>
            {
                if (verdict.HalfTurns <= 0) return;
                Tracker.OnSpinLanded(verdict.HalfTurns);
                Flush();
            };
            player.StateChanged += (prev, next) =>
            {
                if (next == SkaterState.Manual) _manualStart = Time.time;
                else if (prev == SkaterState.Manual && next != SkaterState.Bailed && _manualStart >= 0f)
                {
                    Tracker.OnManualFinished(Time.time - _manualStart);
                    Flush();
                }
            };
        }

        public List<string> CompletedGoalIds()
        {
            var ids = new List<string>();
            foreach (var s in Tracker.Goals) if (s.Completed) ids.Add(s.Goal.id);
            return ids;
        }

        private void ClearPending()
        {
            _pendingRails.Clear();
            _pendingTricks.Clear();
        }

        private void Flush()
        {
            foreach (var s in Tracker.ConsumeCompleted()) GoalCompleted?.Invoke(s);
        }
    }
}
