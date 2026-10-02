using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Level;
using RetroSk8.Player;
using UnityEngine;

namespace RetroSk8.Replay
{
    /// <summary>
    /// Plays a saved best run as a glowing ghost skater alongside the live run, in step with run time.
    /// Purely visual: no colliders, no scoring, no audio.
    /// </summary>
    public sealed class GhostPlayer : MonoBehaviour
    {
        private ReplayTrack _track;
        private RunController _run;
        private SkaterVisual _visual;
        private float _time;

        public ReplayTrack Track => _track;
        public float PlaybackTime => _time;
        public bool Finished { get; private set; }

        public static GhostPlayer Create(ReplayTrack track, RunController run)
        {
            var go = new GameObject("Ghost");
            var player = go.AddComponent<GhostPlayer>();
            var visualGo = new GameObject("GhostVisual");
            visualGo.transform.SetParent(go.transform, false);
            player._visual = visualGo.AddComponent<SkaterVisual>();
            player._visual.Build();
            player._visual.MakeGhost(Palette.NeonCyan);
            player._track = track;
            player._run = run;
            player.Apply(0f);
            return player;
        }

        private void LateUpdate()
        {
            if (Finished || _run == null || _run.IsPaused) return;
            _time += Time.deltaTime;
            Apply(_time);
            if (_time > _track.Duration + 0.5f)
            {
                Finished = true;
                _visual.gameObject.SetActive(false); // the best run is over: get out of the way
            }
        }

        private void Apply(float time)
        {
            if (!_track.Sample(time, out var f)) return;
            _visual.transform.SetPositionAndRotation(f.Position.ToUnity(), f.Rotation.ToUnity());
            _visual.ApplyPose(f.Pose.ToUnity(), f.Body.ToUnity(), f.BoardPosition.ToUnity(), f.Board.ToUnity());
        }
    }
}
