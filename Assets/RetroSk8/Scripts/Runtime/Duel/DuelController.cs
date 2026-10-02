using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Replay;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Duel
{
    /// <summary>
    /// Runs S.K.A.T.E. turns in the park (RunMode.Duel) on top of <see cref="DuelSession"/>.
    /// Your turn: a 3-second countdown at the spawn, then one attempt (up to 25 s) that ends when you bank a
    /// combo or bail; your pose streams to the other phone. Their turn: you wait at the spawn and the camera
    /// follows their live ghost (online) while the CPU's turns show a short "skating" status instead.
    /// </summary>
    public sealed class DuelController : MonoBehaviour
    {
        public const float CountdownSeconds = 3f;
        public const float SettleDelay = 1.2f;
        public const float FrameInterval = 1f / 12f;

        public enum LocalState { Waiting = 0, Countdown = 1, Attempting = 2, Watching = 3, Over = 4 }

        private PlayerController _player;
        private ComboManager _combo;
        private BailHandler _bail;
        private LevelInfo _level;
        private CameraRig _rig;
        private SkaterVisual _visual;
        private LiveGhost _ghost;

        private float _timer;
        private float _endAt = -1f;
        private float _frameTimer;
        private float _attemptClock;
        private int _turnStarted = -1;
        private long _best;
        private string _bestLabel = "";
        private float _lastFrameAt = -10f;

        public DuelSession Session => DuelSession.Current;
        public LocalState State { get; private set; } = LocalState.Waiting;
        public float Countdown => Mathf.Max(0f, _timer);
        public float AttemptLeft => State == LocalState.Attempting ? Mathf.Max(0f, _timer) : 0f;
        public long AttemptBest => _best;
        public bool WatchingLive => State == LocalState.Watching && Time.time - _lastFrameAt < 1.5f;

        /// <summary>Raised when the view should redraw (state, letters, messages).</summary>
        public event Action Changed;

        public void Init(PlayerController player, ComboManager combo, LevelInfo level)
        {
            _player = player;
            _combo = combo;
            _level = level;
            _bail = player.GetComponent<BailHandler>();
            _rig = FindFirstObjectByType<CameraRig>();
            _visual = player.GetComponentInChildren<SkaterVisual>();
            _ghost = LiveGhost.Create();

            combo.Banked += (result, label, _) =>
            {
                if (State != LocalState.Attempting || _endAt >= 0f) return;
                if (result.Points > _best) { _best = result.Points; _bestLabel = label; }
                if (result.Points > 0) _endAt = Time.time + SettleDelay;
            };
            combo.Bailed += (_, __) =>
            {
                if (State == LocalState.Attempting && _endAt < 0f) _endAt = Time.time + SettleDelay;
            };

            if (Session != null)
            {
                Session.OpponentFrame += OnOpponentFrame;
                Session.OpponentAttemptStarted += _ => { _ghost.Clear(); Changed?.Invoke(); };
                Session.Changed += OnSessionChanged;
            }
            Park();
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (DuelSession.Current != null)
            {
                DuelSession.Current.OpponentFrame -= OnOpponentFrame;
                DuelSession.Current.Changed -= OnSessionChanged;
            }
            if (_ghost != null) Destroy(_ghost.gameObject);
        }

        private void OnSessionChanged() => Changed?.Invoke();

        private void OnOpponentFrame(ReplayFrame f)
        {
            _lastFrameAt = Time.time;
            _ghost.Push(f);
        }

        private void Update()
        {
            var s = Session;
            if (s == null || s.Duel == null)
            {
                if (State != LocalState.Over) { State = LocalState.Over; Spectate(false); Changed?.Invoke(); }
                return;
            }
            if (s.State == DuelSession.Stage.Ended || s.Duel.Phase == DuelPhase.Finished)
            {
                if (State != LocalState.Over)
                {
                    State = LocalState.Over;
                    Park();
                    Spectate(false);
                    Changed?.Invoke();
                }
                return;
            }
            if (State == LocalState.Over) { State = LocalState.Waiting; _turnStarted = -1; } // rematch began

            if (s.MyTurn) MyTurn(s);
            else TheirTurn();
        }

        // ---------------------------------------------------------------- my turn

        private void MyTurn(DuelSession s)
        {
            if (_turnStarted != s.Duel.Turn)
            {
                _turnStarted = s.Duel.Turn;
                State = LocalState.Countdown;
                _timer = CountdownSeconds;
                Spectate(false);
                Park();
                Changed?.Invoke();
                return;
            }

            if (State == LocalState.Countdown)
            {
                _timer -= Time.deltaTime;
                if (_timer > 0f) return;
                State = LocalState.Attempting;
                _timer = SkateDuel.AttemptSeconds;
                _best = 0;
                _bestLabel = "";
                _endAt = -1f;
                _attemptClock = 0f;
                _player.SetInputEnabled(true);
                s.SendAttemptBegin();
                Changed?.Invoke();
                return;
            }

            if (State != LocalState.Attempting) return;
            _attemptClock += Time.deltaTime;
            _frameTimer -= Time.deltaTime;
            if (_frameTimer <= 0f)
            {
                _frameTimer = FrameInterval;
                s.SendFrame(Capture());
            }

            if (_endAt >= 0f)
            {
                if (Time.time >= _endAt) Finish(s);
                return;
            }
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                if (_combo.HasPendingBank) _combo.BankNow(); // a landed line counts on the buzzer
                else _combo.Discard();
                Finish(s);
            }
        }

        private void Finish(DuelSession s)
        {
            _endAt = -1f;
            _player.SetInputEnabled(false);
            State = LocalState.Waiting;
            s.ReportMyResult(_best, _bestLabel);
            Changed?.Invoke();
        }

        private ReplayFrame Capture()
        {
            var t = _visual != null ? _visual.transform : _player.transform;
            var f = new ReplayFrame { Time = _attemptClock, Position = t.position.ToR(), Rotation = t.rotation.ToR() };
            if (_visual != null)
            {
                _visual.CapturePose(out var pose, out var body, out var boardPos, out var board);
                f.Pose = pose.ToR();
                f.Body = body.ToR();
                f.BoardPosition = boardPos.ToR();
                f.Board = board.ToR();
            }
            return f;
        }

        // ---------------------------------------------------------------- their turn

        private void TheirTurn()
        {
            if (State != LocalState.Watching)
            {
                State = LocalState.Watching;
                Park();
                Changed?.Invoke();
            }
            Spectate(WatchingLive);
        }

        private void Spectate(bool on)
        {
            if (_rig != null) _rig.SpectateTarget = on && _ghost != null ? _ghost.Root : null;
            if (_ghost != null) _ghost.SetVisible(on);
        }

        /// <summary>Back to the spawn, combo cleared, input off.</summary>
        private void Park()
        {
            _player.SetInputEnabled(false);
            _combo.Discard();
            if (_bail != null && _bail.IsBailing) _bail.RespawnNow();
            if (_level != null && _level.spawnPoint != null) _player.Teleport(_level.spawnPoint.position, _level.spawnPoint.forward);
        }
    }

    /// <summary>The opponent's skater, drawn from streamed poses a fraction of a second behind (smooth over network jitter).</summary>
    public sealed class LiveGhost : MonoBehaviour
    {
        private const float Delay = 0.2f;
        private readonly List<ReplayFrame> _frames = new List<ReplayFrame>();
        private SkaterVisual _visual;
        private float _clock;

        public Transform Root => _visual != null ? _visual.transform : transform;

        public static LiveGhost Create()
        {
            var go = new GameObject("OpponentGhost");
            var g = go.AddComponent<LiveGhost>();
            var v = new GameObject("GhostVisual");
            v.transform.SetParent(go.transform, false);
            g._visual = v.AddComponent<SkaterVisual>();
            g._visual.Build();
            g._visual.MakeGhost(Palette.NeonPink);
            g.SetVisible(false);
            return g;
        }

        public void SetVisible(bool on)
        {
            if (_visual != null && _visual.gameObject.activeSelf != on) _visual.gameObject.SetActive(on);
        }

        public void Clear()
        {
            _frames.Clear();
            _clock = 0f;
        }

        public void Push(ReplayFrame f)
        {
            if (_frames.Count > 0 && f.Time < _frames[_frames.Count - 1].Time) Clear(); // a new attempt restarted the clock
            _frames.Add(f);
            if (_frames.Count > 64) _frames.RemoveAt(0);
            if (_frames.Count == 1) _clock = f.Time - Delay;
        }

        private void LateUpdate()
        {
            if (_frames.Count == 0 || _visual == null) return;
            _clock += Time.deltaTime;
            float latest = _frames[_frames.Count - 1].Time;
            if (_clock > latest) _clock = latest;          // starved: hold the last pose
            if (latest - _clock > 0.6f) _clock = latest - Delay; // fell behind: catch up

            ReplayFrame f = _frames[0];
            for (int i = 0; i < _frames.Count - 1; i++)
            {
                var a = _frames[i];
                var b = _frames[i + 1];
                if (_clock < a.Time || _clock > b.Time) continue;
                float k = b.Time > a.Time ? (_clock - a.Time) / (b.Time - a.Time) : 0f;
                f = ReplayFrame.Lerp(a, b, k);
                break;
            }
            if (_clock >= latest) f = _frames[_frames.Count - 1];
            _visual.transform.SetPositionAndRotation(f.Position.ToUnity(), f.Rotation.ToUnity());
            _visual.ApplyPose(f.Pose.ToUnity(), f.Body.ToUnity(), f.BoardPosition.ToUnity(), f.Board.ToUnity());
        }
    }
}
