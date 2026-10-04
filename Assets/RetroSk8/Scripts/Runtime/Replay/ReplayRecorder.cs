using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Scoring;
using RetroSk8.Game;
using RetroSk8.Player;
using UnityEngine;

namespace RetroSk8.Replay
{
    /// <summary>
    /// Samples the skater's pose at 20 Hz of run time (pauses excluded) for the whole run.
    /// When the run finishes with a new best score, the recording becomes that park's ghost.
    /// </summary>
    public sealed class ReplayRecorder : MonoBehaviour
    {
        private SkaterVisual _visual;
        private RunController _run;
        private float _time;
        private bool _stopped;

        public ReplayTrack Track { get; private set; }
        /// <summary>Raised after a new ghost is written (tests and UI hooks).</summary>
        public event System.Action<ReplayTrack> GhostSaved;

        private ComboManager _combo;
        private string _parkName;
        private readonly List<ReplayMoment> _moments = new List<ReplayMoment>();

        public void Init(SkaterVisual visual, RunController run, string locationId, ComboManager combo = null, string parkName = null)
        {
            _visual = visual;
            _run = run;
            _combo = combo;
            _parkName = parkName ?? locationId;
            Track = new ReplayTrack { LocationId = locationId };
            run.Finished += OnFinished;
            if (combo != null) combo.Banked += OnBanked;
        }

        private void OnBanked(ComboResult result, string label, LandingQuality quality)
        {
            if (result.Points > 0) _moments.Add(new ReplayMoment { time = _time, points = result.Points, label = label });
        }

        /// <summary>
        /// Saves what's been recorded so far (the last <see cref="ReplayIndex.MaxSavedSeconds"/>) to the replay
        /// library. Returns the replay id, or null when there's nothing to save.
        /// </summary>
        public string SaveToLibrary(string modeLabel, long score)
        {
            if (Track == null || Track.Count < 2) return null;
            var frames = ReplayIndex.LastSeconds(Track.Frames, ReplayIndex.MaxSavedSeconds, out float cut);
            var copy = new ReplayTrack { LocationId = Track.LocationId, Score = score };
            foreach (var f in frames) copy.Add(f);
            var entry = new ReplayEntry { locationId = Track.LocationId, parkName = _parkName, mode = modeLabel, score = score };
            foreach (var m in _moments)
                if (m.time >= cut) entry.moments.Add(new ReplayMoment { time = m.time - cut, points = m.points, label = m.label });
            return ReplayLibrary.Save(copy, entry);
        }

        private void LateUpdate()
        {
            if (_stopped || _visual == null || _run == null || _run.IsPaused) return;
            _time += Time.deltaTime;
            if (!Track.IsDue(_time)) return;

            _visual.CapturePose(out var pose, out var body, out var boardPos, out var board);
            var t = _visual.transform;
            var frame = new ReplayFrame
            {
                Time = _time,
                Position = t.position.ToR(),
                Rotation = t.rotation.ToR(),
                Pose = pose.ToR(),
                Body = body.ToR(),
                BoardPosition = boardPos.ToR(),
                Board = board.ToR(),
            };
            if (Track.Add(frame) || Track.Count < ReplayTrack.MaxFrames) return;
            // Long Free Skate sessions: keep a rolling window so SAVE REPLAY always has the latest stretch.
            var keep = new ReplayTrack { LocationId = Track.LocationId };
            foreach (var f in Track.Frames) if (f.Time >= _time - ReplayIndex.MaxSavedSeconds - 10f) keep.Add(f);
            keep.Add(frame);
            Track = keep;
        }

        private void OnFinished(RunResult result)
        {
            _stopped = true;
            // Every scored run lands in the replay library (newest few are kept, favorites forever).
            if (result != null && result.score > 0) SaveToLibrary(result.modeLabel, result.score);
            if (result == null || !result.newBest || result.score <= 0) return;
            Track.Score = result.score;
            if (GhostStore.Save(Track)) GhostSaved?.Invoke(Track);
        }

        private void OnDestroy()
        {
            if (_run != null) _run.Finished -= OnFinished;
            if (_combo != null) _combo.Banked -= OnBanked;
        }
    }
}
