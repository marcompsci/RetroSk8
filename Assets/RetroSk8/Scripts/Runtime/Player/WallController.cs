using System;
using RetroSk8.Core;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>
    /// Wallrides, wallplants and wallies. Press GRIND/ACTION in the air just before (or just after) touching an
    /// upright wall: skim along it and you wallride; hit it square and you wallplant and spring back off.
    /// Jumping during a wallride is a wallie. The body is held kinematically while on the wall, like a grind.
    /// </summary>
    public sealed class WallController : MonoBehaviour
    {
        private const float ArmWindow = 0.25f;
        private const float ContactMemory = 0.2f;
        private const float WallOffset = 0.32f;
        private const float PlantStall = 0.22f;

        private PlayerController _player;
        private ComboManager _combo;
        private SkaterVisual _visual;
        private TrickController _tricks;

        private float _armTimer;
        private float _contactAge = 999f;
        private Vector3 _contactNormal;
        private Vector3 _contactPoint;
        private Vector3 _contactVelocity;

        private WallMove _move;
        private Vector3 _normal;
        private Vector3 _along;
        private Vector3 _position;
        private float _speed;
        private float _vy;
        private float _time;

        public bool IsActive => _move != WallMove.None;
        public WallMove CurrentMove => _move;

        public event Action<WallMove> WallStarted;
        public event Action WallEnded;

        public void Init(PlayerController player, ComboManager combo, SkaterVisual visual)
        {
            _player = player;
            _combo = combo;
            _visual = visual;
            _tricks = GetComponent<TrickController>();
        }

        /// <summary>Action pressed in the air: start now if a wall was just touched, otherwise wait briefly for one.</summary>
        public bool Arm()
        {
            _armTimer = ArmWindow;
            return _contactAge <= ContactMemory && TryStart();
        }

        /// <summary>From PlayerController.OnCollisionEnter while airborne. Returns true if a wall trick began.</summary>
        public bool OnWallContact(Vector3 normal, Vector3 point, Vector3 velocityBefore)
        {
            _contactNormal = normal;
            _contactPoint = point;
            _contactVelocity = velocityBefore;
            _contactAge = 0f;
            return _armTimer > 0f && TryStart();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _armTimer -= dt;
            _contactAge += dt;
            if (_move == WallMove.Wallride && Time.timeScale > 0f) _combo.TickContinuous(dt);
        }

        private bool TryStart()
        {
            if (IsActive || _player.State != SkaterState.Airborne) return false;
            Vector3 n = Vector3.ProjectOnPlane(_contactNormal, Vector3.up);
            if (n.sqrMagnitude < 1e-4f) return false;
            n.Normalize();
            float tilt = Mathf.Abs(90f - Vector3.Angle(_contactNormal, Vector3.up));
            Vector3 flat = Vector3.ProjectOnPlane(_contactVelocity, Vector3.up);
            float speed = flat.magnitude;
            float approach = speed > 0.01f ? Mathf.Asin(Mathf.Clamp01(Mathf.Abs(Vector3.Dot(flat / speed, n)))) * Mathf.Rad2Deg : 90f;

            var move = WallRules.Classify(approach, speed, tilt);
            if (move == WallMove.None) return false;

            _armTimer = 0f;
            _tricks.Cancel();
            _move = move;
            _normal = n;
            _time = 0f;
            _position = _player.Body.position + n * 0.05f;

            if (move == WallMove.Wallride)
            {
                _along = Vector3.ProjectOnPlane(flat, n).normalized;
                _speed = Mathf.Max(speed, WallRules.MinSpeed);
                _vy = Mathf.Max(_contactVelocity.y, 2.5f); // a little lift so the ride arcs up the wall
                _combo.StartContinuous("wallride", "Wallride", TrickCategory.Wall, WallRules.WallrideStartPoints);
            }
            else
            {
                _along = -n;
                _speed = speed;
                _vy = 0f;
                if (StyleTricks.IsFootplant(_player.CurrentInput.SteerZone))
                    _combo.AddTrick(StyleTricks.FootplantId, StyleTricks.FootplantName, TrickCategory.Wall, Mathf.RoundToInt(WallRules.WallplantPoints * StyleTricks.FootplantBonus));
                else
                    _combo.AddTrick("wallplant", "Wallplant", TrickCategory.Wall, WallRules.WallplantPoints);
            }
            _combo.MarkElement(LineElement.Wall);
            _player.EnterHeldTrick(SkaterState.Wallride);
            ApplyPose();
            WallStarted?.Invoke(move);
            return true;
        }

        /// <summary>Physics step while on the wall (called from PlayerController.FixedUpdate).</summary>
        public void Step(float dt)
        {
            if (!IsActive) return;
            _time += dt;

            if (_move == WallMove.Wallplant)
            {
                ApplyPose();
                if (_time >= PlantStall)
                    Exit(_normal * (_speed * 0.45f + 2.5f) + Vector3.up * 7f); // spring back off the wall
                return;
            }

            _vy -= _player.motor.gravity * WallRules.WallrideGravityScale * dt;
            _speed = Mathf.Max(0f, _speed - 1.5f * dt);
            _position += (_along * _speed + Vector3.up * _vy) * dt;

            bool wallStillThere = Physics.Raycast(_position + Vector3.up * 0.9f, -_normal, out var hit, WallOffset + 0.6f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                                  && Vector3.Dot(hit.normal, _normal) > 0.8f;
            bool nearGround = _vy < 0f && Physics.Raycast(_position + Vector3.up * 0.3f, Vector3.down, 0.45f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            if (!wallStillThere || nearGround || _time >= WallRules.WallrideMaxSeconds || _speed < 3f)
            {
                Exit(_along * _speed + _normal * 1.5f + Vector3.up * Mathf.Max(_vy, 0f));
                return;
            }
            _position = hit.point + _normal * WallOffset - Vector3.up * 0.9f; // hold a steady distance from the wall
            ApplyPose();
        }

        /// <summary>Jump during a wallride (or a plant): pop off the wall.</summary>
        public void Wallie(float popStrength)
        {
            if (!IsActive) return;
            if (_move == WallMove.Wallride)
            {
                _combo.EndContinuous();
                _combo.AddTrick("wallie", "Wallie", TrickCategory.Wall, WallRules.WalliePoints);
            }
            Exit(_along * _speed + _normal * 3.5f + Vector3.up * popStrength);
        }

        private void ApplyPose()
        {
            // Lean into the wall a little; the visual reads as riding on it.
            Vector3 up = Vector3.Slerp(Vector3.up, _normal, _move == WallMove.Wallride ? 0.55f : 0.2f).normalized;
            Vector3 fwd = _move == WallMove.Wallride ? _along : -_normal;
            _player.SetPose(_position, fwd, up);
        }

        private void Exit(Vector3 velocity)
        {
            Cleanup();
            _player.ExitGrind(velocity);
        }

        /// <summary>End without touching the player state (bails, respawns).</summary>
        public void Abort()
        {
            if (IsActive) Cleanup();
        }

        private void Cleanup()
        {
            if (_move == WallMove.Wallride) _combo.EndContinuous();
            _move = WallMove.None;
            _armTimer = 0f;
            WallEnded?.Invoke();
        }
    }
}
