using RetroSk8.Core;
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

        public void Init(SkaterVisual visual, RunController run, string locationId)
        {
            _visual = visual;
            _run = run;
            Track = new ReplayTrack { LocationId = locationId };
            run.Finished += OnFinished;
        }

        private void LateUpdate()
        {
            if (_stopped || _visual == null || _run == null || _run.IsPaused) return;
            _time += Time.deltaTime;
            if (!Track.IsDue(_time)) return;

            _visual.CapturePose(out var pose, out var body, out var boardPos, out var board);
            var t = _visual.transform;
            Track.Add(new ReplayFrame
            {
                Time = _time,
                Position = t.position.ToR(),
                Rotation = t.rotation.ToR(),
                Pose = pose.ToR(),
                Body = body.ToR(),
                BoardPosition = boardPos.ToR(),
                Board = board.ToR(),
            });
        }

        private void OnFinished(RunResult result)
        {
            _stopped = true;
            if (result == null || !result.newBest || result.score <= 0) return;
            Track.Score = result.score;
            if (GhostStore.Save(Track)) GhostSaved?.Invoke(Track);
        }

        private void OnDestroy()
        {
            if (_run != null) _run.Finished -= OnFinished;
        }
    }
}
