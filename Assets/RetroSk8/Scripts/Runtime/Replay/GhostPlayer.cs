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
        /// <summary>
        /// Phase 26: when set, the ghost follows this clock (a city race's own timer, which stops during bails) instead
        /// of its own.
        /// </summary>
        public System.Func<float> Clock;
        public bool Finished { get; private set; }

        /// <summary>The friend's ghost in a ghost race (null otherwise), for the HUD's live rival score.</summary>
        public static GhostPlayer Rival { get; private set; }

        public static GhostPlayer Create(ReplayTrack track, RunController run) => Create(track, run, Palette.NeonCyan, false);

        /// <summary>A ghost in <paramref name="color"/>; <paramref name="rival"/> marks a friend's ghost from a ghost code.</summary>
        public static GhostPlayer Create(ReplayTrack track, RunController run, Color color, bool rival)
        {
            var go = new GameObject(rival ? "RivalGhost" : "Ghost");
            var player = go.AddComponent<GhostPlayer>();
            var visualGo = new GameObject("GhostVisual");
            visualGo.transform.SetParent(go.transform, false);
            player._visual = visualGo.AddComponent<SkaterVisual>();
            player._visual.Build();
            player._visual.MakeGhost(color);
            if (rival) Rival = player;
            player._track = track;
            player._run = run;
            player.Apply(0f);
            return player;
        }

        private void OnDestroy()
        {
            if (Rival == this) Rival = null;
        }

        private void LateUpdate()
        {
            if (Finished || _run == null || _run.IsPaused) return;
            _time = Clock != null ? Clock() : _time + Time.deltaTime;
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
