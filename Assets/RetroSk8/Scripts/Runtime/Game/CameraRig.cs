using RetroSk8.Core;
using RetroSk8.Player;
using UnityEngine;
#if RETROSK8_CINEMACHINE
using Unity.Cinemachine;
#endif

namespace RetroSk8.Game
{
    /// <summary>
    /// Chase camera. A yaw-only proxy follows the skater's travel direction (not air spins), and either
    /// Cinemachine 3 (when installed) or a small built-in follower tracks that proxy.
    /// Phase 2 adds speed-based FOV, an airborne pull-back, look-ahead and a landing shake.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [Header("Framing")]
        public float distance = 6.3f;
        public float height = 2.5f;
        public float lookHeight = 1.1f;
        public float yawSmoothTime = 0.28f;
        public float fieldOfView = 62f;

        [Header("Juice")]
        [Tooltip("Extra FOV per m/s above cruise speed.")]
        public float fovPerSpeed = 0.6f;
        public float maxFovBoost = 10f;
        [Tooltip("Distance multiplier while airborne, so big airs read clearly.")]
        public float airPullBack = 1.18f;
        [Tooltip("Metres the look target leads the skater per m/s.")]
        public float lookAheadPerSpeed = 0.06f;
        [Tooltip("Shake amplitude per m/s of landing impact.")]
        public float landingShakePerSpeed = 0.012f;
        public float shakeDecay = 9f;

        private PlayerController _player;
        private Transform _proxy;
        private Camera _camera;
        private float _yaw;
        private float _yawVelocity;
        private float _airBlend;
        private float _fov;
        private float _shake;
        private Vector3 _lookAhead;
        private bool _useCinemachine;
#if RETROSK8_CINEMACHINE
        private CinemachineCamera _vcam;
        private CinemachineFollow _follow;
#endif

        public static CameraRig Create(PlayerController player)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;

            var rig = new GameObject("CameraRig").AddComponent<CameraRig>();
            rig.Setup(player, cam);
            return rig;
        }

        private void Setup(PlayerController player, Camera cam)
        {
            _player = player;
            _camera = cam;
            _fov = fieldOfView;
            _proxy = new GameObject("CameraTarget").transform;
            _proxy.SetParent(transform, false);
            _yaw = Quaternion.LookRotation(player.Heading).eulerAngles.y;
            UpdateProxy(true, 0f);
            player.Landed += OnLanded;

#if RETROSK8_CINEMACHINE
            if (cam.GetComponent<CinemachineBrain>() == null) cam.gameObject.AddComponent<CinemachineBrain>();
            var vcamGo = new GameObject("SkateCam");
            vcamGo.transform.SetParent(transform, false);
            _vcam = vcamGo.AddComponent<CinemachineCamera>();
            _vcam.Follow = _proxy;
            _vcam.LookAt = _proxy;
            _follow = vcamGo.AddComponent<CinemachineFollow>();
            vcamGo.AddComponent<CinemachineRotationComposer>();
            ApplyLens(fieldOfView);
            _follow.FollowOffset = CurrentOffset();
            _useCinemachine = true;
#endif
            cam.fieldOfView = fieldOfView;
            if (!_useCinemachine) SnapCamera();
        }

        private void OnDestroy()
        {
            if (_player != null) _player.Landed -= OnLanded;
        }

        private void OnLanded(LandingVerdict verdict)
        {
            if (RetroSk8.Save.SaveManager.Data.settings.reducedMotion) return; // accessibility: no shake
            _shake = Mathf.Min(0.35f, _shake + _player.LastImpactSpeed * landingShakePerSpeed);
        }

        private void LateUpdate()
        {
            if (_player == null) return;
            float dt = Time.deltaTime;

            bool air = _player.State == SkaterState.Airborne;
            _airBlend = Mathf.MoveTowards(_airBlend, air ? 1f : 0f, dt * (air ? 1.5f : 3f));

            float overCruise = Mathf.Max(0f, _player.Speed - _player.motor.cruiseSpeed);
            bool calm = RetroSk8.Save.SaveManager.Data.settings.reducedMotion;
            float targetFov = fieldOfView + (calm ? 0f : Mathf.Min(maxFovBoost, overCruise * fovPerSpeed));
            _fov = Mathf.Lerp(_fov, targetFov, 1f - Mathf.Exp(-4f * dt));

            _shake = Mathf.MoveTowards(_shake, 0f, _shake * shakeDecay * dt + 0.01f * dt);
            UpdateProxy(false, dt);

#if RETROSK8_CINEMACHINE
            if (_useCinemachine)
            {
                ApplyLens(_fov);
                _follow.FollowOffset = CurrentOffset();
                return;
            }
#endif
            _camera.fieldOfView = _fov;
            FollowCamera(dt);
        }

        private Vector3 CurrentOffset()
        {
            float k = Mathf.Lerp(1f, airPullBack, _airBlend);
            return new Vector3(0f, height * k, -distance * k);
        }

#if RETROSK8_CINEMACHINE
        private void ApplyLens(float fov)
        {
            var lens = _vcam.Lens;
            lens.FieldOfView = fov;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 400f;
            _vcam.Lens = lens;
        }
#endif

        /// <summary>When set, the camera follows this instead of the player (watching an opponent in S.K.A.T.E.).</summary>
        public Transform SpectateTarget { get; set; }
        private Vector3 _lastSpectatePos;

        private void UpdateProxy(bool snap, float dt)
        {
            if (SpectateTarget != null)
            {
                Vector3 pos = SpectateTarget.position;
                Vector3 v = dt > 0f ? (pos - _lastSpectatePos) / dt : Vector3.zero;
                _lastSpectatePos = pos;
                Vector3 flat = Vector3.ProjectOnPlane(v, Vector3.up);
                if (flat.sqrMagnitude > 4f && flat.sqrMagnitude < 2500f)
                    _yaw = Mathf.SmoothDampAngle(_yaw, Quaternion.LookRotation(flat).eulerAngles.y, ref _yawVelocity, yawSmoothTime);
                _proxy.position = pos + Vector3.up * lookHeight;
                _proxy.rotation = Quaternion.Euler(0f, _yaw, 0f);
                return;
            }

            Vector3 travel = TravelDirection();
            if (travel.sqrMagnitude > 0.01f)
            {
                float target = Quaternion.LookRotation(travel).eulerAngles.y;
                _yaw = snap ? target : Mathf.SmoothDampAngle(_yaw, target, ref _yawVelocity, yawSmoothTime);
            }

            Vector3 flatVel = Vector3.ProjectOnPlane(_player.Body.linearVelocity, Vector3.up);
            Vector3 desiredLead = _player.State == SkaterState.Bailed ? Vector3.zero : flatVel * lookAheadPerSpeed;
            _lookAhead = snap ? desiredLead : Vector3.Lerp(_lookAhead, desiredLead, 1f - Mathf.Exp(-3f * dt));

            Vector3 shake = _shake > 0.001f
                ? new Vector3(Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f, 0f) * (2f * _shake)
                : Vector3.zero;

            _proxy.position = _player.transform.position + Vector3.up * lookHeight + _lookAhead + shake;
            _proxy.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private Vector3 TravelDirection()
        {
            switch (_player.State)
            {
                case SkaterState.Airborne:
                case SkaterState.Bailed:
                {
                    Vector3 v = Vector3.ProjectOnPlane(_player.Body.linearVelocity, Vector3.up);
                    return v.sqrMagnitude > 4f ? v : Vector3.zero; // hold yaw during vert airs and slow tumbles
                }
                default:
                    return Vector3.ProjectOnPlane(_player.Heading * _player.MovementSign, Vector3.up);
            }
        }

        private void FollowCamera(float dt)
        {
            Vector3 desired = _proxy.TransformPoint(CurrentOffset());
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired, 1f - Mathf.Exp(-10f * dt));
            _camera.transform.rotation = Quaternion.LookRotation(_proxy.position - _camera.transform.position, Vector3.up);
        }

        private void SnapCamera()
        {
            _camera.transform.position = _proxy.TransformPoint(CurrentOffset());
            _camera.transform.rotation = Quaternion.LookRotation(_proxy.position - _camera.transform.position, Vector3.up);
        }
    }
}
