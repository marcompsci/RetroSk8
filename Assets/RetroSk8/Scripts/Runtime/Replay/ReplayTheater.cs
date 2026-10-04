using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Player;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroSk8.Replay
{
    /// <summary>
    /// The replay editor (RunMode.Replay): plays a saved run in its park with your skater's look, at 0.25x-1x,
    /// with four camera styles (follow, fisheye, tripod filmer, free orbit), scrubbing, in/out marks and clip export
    /// through ReplayKit (iOS). The live skater is switched off; nothing here touches scoring or saves progress.
    /// </summary>
    public sealed class ReplayTheater : MonoBehaviour
    {
        private ReplayTrack _track;
        private SkaterVisual _visual;
        private Camera _cam;
        private Behaviour[] _disabled = new Behaviour[0];
        private Vector3 _travel = Vector3.forward;
        private Vector3? _tripod;
        private float _orbitYaw, _orbitPitch = 18f, _orbitDistance = 6f;
        private Vector2? _lastPointer;

        public ReplayEntry Entry { get; private set; }
        public ReplayClock Clock { get; private set; }
        public ReplayCamera CameraMode { get; private set; } = ReplayCamera.Follow;
        public bool Exporting { get; private set; }
        public bool Loaded => _track != null;
        /// <summary>Screen points over the editor's buttons don't orbit the camera (set by the view).</summary>
        public Func<Vector2, bool> IsOverUi;

        /// <summary>The view hides itself while a clip is exported and comes back after.</summary>
        public event Action<bool> ExportingChanged;

        public void Init(PlayerController player, ContentRegistry content, string replayId)
        {
            _cam = Camera.main;
            Entry = ReplayLibrary.Index.Find(replayId);
            _track = ReplayLibrary.Load(replayId);

            // The live skater sits this one out.
            player.gameObject.SetActive(false);
            var rig = FindAnyObjectByType<CameraRig>();
            var list = new List<Behaviour>();
            if (rig != null) list.Add(rig);
            if (_cam != null)
                foreach (var b in _cam.GetComponents<Behaviour>())
                    if (b != null && b.GetType().Name == "CinemachineBrain") list.Add(b);
            foreach (var b in list) b.enabled = false;
            _disabled = list.ToArray();

            var go = new GameObject("ReplaySkater");
            _visual = go.AddComponent<SkaterVisual>();
            _visual.Build();
            _visual.ApplyLook(SaveManager.Data.look);
            _visual.ApplyLoadout(CosmeticsService.CurrentLoadout(content));

            Clock = new ReplayClock(_track != null ? _track.Duration : 0f);
            Apply(0f, true);
        }

        private void OnDestroy()
        {
            foreach (var b in _disabled) if (b != null) b.enabled = true;
            if (Exporting) ClipRecorder.CancelRun();
        }

        // ---------------------------------------------------------------- controls

        public void TogglePlay() { if (Clock != null) Clock.Playing = !Clock.Playing; }
        public void Skip(float seconds) { Clock?.Skip(seconds); Apply(Clock.Time, true); }
        public void SeekFraction(float f) { Clock?.SeekFraction(f); Apply(Clock.Time, true); }
        public void CycleSpeed() => Clock?.CycleSpeed();
        public void MarkIn() => Clock?.MarkIn();
        public void MarkOut() => Clock?.MarkOut();
        public void ClearMarks() => Clock?.ClearMarks();

        public void CycleCamera()
        {
            CameraMode = (ReplayCamera)(((int)CameraMode + 1) % 4);
            _tripod = null;
            if (CameraMode == ReplayCamera.Orbit && _cam != null && _visual != null)
            {
                var to = _cam.transform.position - _visual.transform.position;
                _orbitYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            }
        }

        public static string CameraName(ReplayCamera c) =>
            c == ReplayCamera.Follow ? "FOLLOW" : c == ReplayCamera.Fisheye ? "FISHEYE" : c == ReplayCamera.Tripod ? "TRIPOD" : "ORBIT";

        public static bool CanExport => ClipRecorder.IsSupported;

        /// <summary>Records the in→out range as a screen clip (UI hidden) and offers the share sheet when it's done.</summary>
        public void Export()
        {
            if (!CanExport || Exporting || Clock == null) return;
            Exporting = true;
            Clock.Loop = false;
            Clock.Seek(Clock.In);
            Clock.Playing = true;
            Apply(Clock.Time, true);
            ExportingChanged?.Invoke(true);
            ClipRecorder.StartExport();
        }

        /// <summary>The banked line being shown right now (for the on-screen label), or null.</summary>
        public ReplayMoment CurrentMoment
        {
            get
            {
                if (Entry == null || Clock == null) return null;
                foreach (var m in Entry.moments)
                    if (m != null && Clock.Time >= m.time - 0.1f && Clock.Time <= m.time + 2f) return m;
                return null;
            }
        }

        // ---------------------------------------------------------------- playback

        private void Update()
        {
            if (Clock == null || _track == null) return;
            bool reachedOut = Clock.Tick(Time.unscaledDeltaTime);
            Apply(Clock.Time, false);
            if (Exporting && reachedOut)
            {
                Exporting = false;
                Clock.Loop = true;
                ClipRecorder.EndRun();
                ExportingChanged?.Invoke(false);
            }
            if (CameraMode == ReplayCamera.Orbit) OrbitInput();
        }

        private void Apply(float time, bool snap)
        {
            if (_track == null || !_track.Sample(time, out var f)) return;
            var prev = _visual.transform.position;
            _visual.transform.SetPositionAndRotation(f.Position.ToUnity(), f.Rotation.ToUnity());
            _visual.ApplyPose(f.Pose.ToUnity(), f.Body.ToUnity(), f.BoardPosition.ToUnity(), f.Board.ToUnity());
            var move = Vector3.ProjectOnPlane(_visual.transform.position - prev, Vector3.up);
            if (!snap && move.sqrMagnitude > 0.0004f) _travel = Vector3.Slerp(_travel, move.normalized, 0.15f);
            else if (snap && _track.Sample(time + 0.2f, out var ahead))
            {
                var d = Vector3.ProjectOnPlane(ahead.Position.ToUnity() - f.Position.ToUnity(), Vector3.up);
                if (d.sqrMagnitude > 0.0004f) _travel = d.normalized;
            }
            if (snap) PlaceCamera(1f);
        }

        private void LateUpdate()
        {
            if (_track == null) return;
            PlaceCamera(1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        }

        private void PlaceCamera(float blend)
        {
            if (_cam == null || _visual == null) return;
            var target = _visual.transform.position;
            var focus = target + Vector3.up * 0.9f;
            var fwd = _travel.sqrMagnitude > 0.01f ? _travel.normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, fwd);
            Vector3 pos;
            float fov;
            switch (CameraMode)
            {
                case ReplayCamera.Fisheye:
                    // Low and close behind the board, very wide: the classic filmer-on-a-board look.
                    pos = target - fwd * 1.7f + side * 0.35f + Vector3.up * 0.35f;
                    focus = target + Vector3.up * 0.55f + fwd * 0.5f;
                    fov = 105f;
                    break;
                case ReplayCamera.Tripod:
                    if (!_tripod.HasValue || Vector3.Distance(_tripod.Value, target) > 24f)
                        _tripod = target + fwd * 9f + side * 5f + Vector3.up * 1.2f; // set up ahead so the line comes past
                    pos = _tripod.Value;
                    fov = 50f;
                    blend = 1f;
                    break;
                case ReplayCamera.Orbit:
                {
                    float y = _orbitYaw * Mathf.Deg2Rad, p = _orbitPitch * Mathf.Deg2Rad;
                    pos = focus + new Vector3(Mathf.Sin(y) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Cos(y) * Mathf.Cos(p)) * _orbitDistance;
                    fov = 55f;
                    blend = 1f;
                    break;
                }
                default:
                    pos = target - fwd * 5.5f + Vector3.up * 2.3f;
                    fov = 60f;
                    break;
            }
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, pos, blend);
            _cam.transform.rotation = Quaternion.LookRotation(focus - _cam.transform.position, Vector3.up);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fov, blend);
        }

        private void OrbitInput()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 pos = pointer.position.ReadValue();
            if (pointer.press.isPressed && (IsOverUi == null || !IsOverUi(pos) || _lastPointer.HasValue))
            {
                if (_lastPointer.HasValue)
                {
                    Vector2 d = pos - _lastPointer.Value;
                    float k = 180f / Mathf.Max(1f, Screen.width);
                    _orbitYaw += d.x * k * 1.6f;
                    _orbitPitch = Mathf.Clamp(_orbitPitch - d.y * k * 1.2f, -5f, 75f);
                }
                _lastPointer = pos;
            }
            else _lastPointer = null;
        }

        public void Zoom(float dir) => _orbitDistance = Mathf.Clamp(_orbitDistance + dir, 2f, 16f);
    }
}
