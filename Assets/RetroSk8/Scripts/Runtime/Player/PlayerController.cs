using System;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Input;
using RetroSk8.Level;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Player
{
    public enum SkaterState
    {
        Rolling = 0,
        Airborne = 1,
        Grinding = 2,
        Manual = 3,
        Bailed = 4,
        /// <summary>Riding along a wall (or the brief stall of a wallplant).</summary>
        Wallride = 5,
        /// <summary>Stalled on quarter-pipe coping.</summary>
        LipStall = 6,
    }

    /// <summary>
    /// Arcade skate motor. Owns the rigidbody, ground detection, steering, ollie and landing evaluation.
    /// Tricks, grinds, manuals and bails live in sibling components; this class routes input to them by state.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerController : MonoBehaviour
    {
        [Serializable]
        public class MotorSettings
        {
            [Header("Speed")]
            public float cruiseSpeed = 9f;
            public float pushSpeed = 14f;
            public float maxSpeed = 24f;
            public float acceleration = 9f;
            public float brakeDeceleration = 16f;
            public float rollingDrag = 0.03f;

            [Header("Steering")]
            public float turnRateSlow = 170f;
            public float turnRateFast = 95f;
            public float airSpinRate = 560f;

            [Header("Ollie")]
            public float gravity = 22f;
            public float minPop = 5.5f;
            public float maxPop = 9.5f;
            public float chargeTime = 0.45f;
            public float coyoteTime = 0.1f;
            public float groundIgnoreAfterPop = 0.12f;

            [Header("Ground")]
            public float probeRadius = 0.25f;
            public float probeStart = 0.6f;
            public float groundedTolerance = 0.3f;
            public float maxSurfaceChange = 50f;   // degrees between frames while grounded
            public float maxLandingSlope = 80f;    // degrees from world up when landing from air
            public float vertAssistSlope = 65f;    // takeoff steeper than this launches straight up

            [Header("Collisions")]
            public float bailImpactSpeed = 11.5f;
            public float deflectImpactSpeed = 1.5f;

            [Header("Feel assists (Phase 2)")]
            [Tooltip("Jump released this long before touchdown still pops on landing.")]
            public float jumpBuffer = 0.12f;
            [Tooltip("0..1: how strongly a non-bail landing snaps the board to the line of travel.")]
            public float landingAlignAssist = 1f;
            [Tooltip(">1 softens small stick movements for fine steering on the ground.")]
            public float steerExponent = 1.6f;
        }

        public MotorSettings motor = new MotorSettings();

        private const int IgnoreRaycastLayer = 2;

        private Rigidbody _rb;
        private PlayerInputRouter _input;
        private ComboManager _combo;
        private TrickController _tricks;
        private GrindController _grind;
        private ManualController _manual;
        private BailHandler _bail;
        private GapTracker _gaps;
        private WallController _wall;
        private LipController _lip;
        private bool _landedFromRampAir;
        private bool _revertedThisLanding;
        private SkaterVisual _visual;
        private LandingRules _landingRules;
        private ScoringConfig _scoring;
        private TrickLibrary _library;

        private Vector3 _heading = Vector3.forward;
        private Vector3 _up = Vector3.up;
        private float _groundIgnoreTimer;
        private float _timeSinceGrounded;
        private bool _jumpQueued;
        private float _queuedCharge;
        private Vector3 _lastVelocity;
        private float _lastTakeoffSlope;
        private float _actionBufferTimer;
        private float _swipeBufferTimer;
        private SwipeDirection _bufferedSwipe;
        private StickZone _bufferedZone;
        private bool _acceptInput = true;
        private bool _poppedThisAir;
        private float _jumpBufferTimer;
        private float _bufferedJumpCharge;
        private readonly RaycastHit[] _probeHits = new RaycastHit[8];

        public SkaterState State { get; private set; } = SkaterState.Rolling;
        public Rigidbody Body => _rb;
        public Vector3 Heading => _heading;
        public Vector3 Up => _up;
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public bool IsGrounded { get; private set; }
        public float Speed => _rb != null ? _rb.linearVelocity.magnitude : 0f;
        public float AirTime { get; private set; }
        public float AirYaw { get; private set; }
        public float JumpCharge { get; private set; }
        /// <summary>True while the skater is kicking up to speed on flat ground (drives the push animation, Phase 16).</summary>
        public bool IsPushing { get; private set; }
        /// <summary>Turning rate on the ground in degrees per second (signed; drives the carve lean).</summary>
        public float CarveRate { get; private set; }
        public bool IsBraking { get; private set; }
        public InputFrame CurrentInput => _input != null ? _input.Frame : default;
        public float MovementSign { get; private set; } = 1f;
        public float TimeSinceLanding { get; private set; } = 999f;
        /// <summary>Downward speed at the last touchdown (drives camera shake and landing sound).</summary>
        public float LastImpactSpeed { get; private set; }
        /// <summary>Belt velocity of the conveyor currently under the skater (zero elsewhere).</summary>
        public Vector3 GroundConveyorVelocity { get; private set; }
        /// <summary>The collider under the wheels while grounded (null in the air). Used for surface sounds.</summary>
        public Collider GroundCollider { get; private set; }

        public event Action<SkaterState, SkaterState> StateChanged;
        public event Action<float> Popped;                 // charge 0..1
        public event Action<LandingVerdict> Landed;
        /// <summary>A bonk or pole jam (Phase 18).</summary>
        public event Action<BonkMove> Bonked;

        public void Init(PlayerInputRouter input, ComboManager combo, ScoringProfile profile, TrickLibrary library)
        {
            _input = input;
            _combo = combo;
            _landingRules = RetroSk8.Game.ActiveAssists.Landing(profile.landing); // Phase 26 landing assist
            _scoring = profile.scoring;
            _library = library;
            // Sibling lookups happen here (not Awake) because the factory adds components after PlayerController.
            _tricks = GetComponent<TrickController>();
            _grind = GetComponent<GrindController>();
            _manual = GetComponent<ManualController>();
            _bail = GetComponent<BailHandler>();
            _visual = GetComponentInChildren<SkaterVisual>();
            _gaps = GetComponent<GapTracker>();
            _wall = GetComponent<WallController>();
            _lip = GetComponent<LipController>();
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // also valid while kinematic during grinds
            _rb.mass = 70f;

            var col = GetComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.85f, 0f);
            col.height = 1.6f;
            col.radius = 0.3f;
            col.sharedMaterial = new PhysicsMaterial("SkaterFrictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };

            gameObject.layer = IgnoreRaycastLayer;
            _heading = transform.forward;
        }

        public void SetInputEnabled(bool enabled) => _acceptInput = enabled;

        // ------------------------------------------------------------------ frame input

        private void Update()
        {
            if (_input == null || Time.timeScale <= 0f) return;
            var f = _acceptInput ? _input.Frame : default;
            float dt = Time.deltaTime;

            _actionBufferTimer -= dt;
            TimeSinceLanding += dt;
            _swipeBufferTimer -= dt;

            bool canPop = State == SkaterState.Rolling || State == SkaterState.Manual || State == SkaterState.Grinding
                          || State == SkaterState.Wallride || State == SkaterState.LipStall
                          || (State == SkaterState.Airborne && !_poppedThisAir && _timeSinceGrounded <= motor.coyoteTime);

            _jumpBufferTimer -= dt;
            // Charging also works in the air so a jump held through the landing can be buffered.
            if (f.JumpHeld && State != SkaterState.Bailed)
                JumpCharge = Mathf.Min(1f, JumpCharge + dt / motor.chargeTime);
            if (f.JumpReleased)
            {
                if (canPop) { _jumpQueued = true; _queuedCharge = JumpCharge; }
                else if (State == SkaterState.Airborne) { _jumpBufferTimer = motor.jumpBuffer; _bufferedJumpCharge = JumpCharge; }
                JumpCharge = 0f;
            }
            if (State != SkaterState.Airborne && State != SkaterState.Bailed) _visual?.SetCrouch(JumpCharge);

            if (f.Swipe != SwipeDirection.None)
            {
                if (State == SkaterState.Airborne) _tricks.OnSwipe(f.Swipe, f.SteerZone);
                else if ((f.Swipe == SwipeDirection.Left || f.Swipe == SwipeDirection.Right)
                         && State == SkaterState.Rolling
                         && RevertRules.CanRevert(TimeSinceLanding, _landedFromRampAir, _revertedThisLanding))
                    Revert();
                else
                {
                    // Swipes just before takeoff are buffered so "release + swipe" in one motion works.
                    _bufferedSwipe = f.Swipe;
                    _bufferedZone = f.SteerZone;
                    _swipeBufferTimer = 0.2f;
                }
            }

            if (f.ActionPressed) HandleAction(f.SteerZone);
        }

        private void HandleAction(StickZone zone)
        {
            switch (State)
            {
                case SkaterState.Rolling:
                case SkaterState.Airborne:
                    // Square to quarter-pipe coping: a lip trick. Parallel to it: a grind.
                    if (_lip != null && _lip.TryStart(zone)) return;
                    if (_grind.TryStartGrind(zone)) return;
                    if (State == SkaterState.Airborne && _wall != null && _wall.Arm()) return;
                    if (State == SkaterState.Rolling && _manual.TryStartManual(zone)) return;
                    if (State == SkaterState.Airborne) _actionBufferTimer = 0.15f; // pre-landing manual buffer
                    break;
                case SkaterState.Manual:
                    if (!_grind.TryStartGrind(zone)) _manual.EndAndBank();
                    break;
                case SkaterState.LipStall:
                    _lip.DropIn();
                    break;
            }
        }

        // ------------------------------------------------------------------ physics

        private void FixedUpdate()
        {
            if (_input == null) return;
            float dt = Time.fixedDeltaTime;
            _groundIgnoreTimer -= dt;

            switch (State)
            {
                case SkaterState.Rolling:
                case SkaterState.Manual:
                    GroundStep(dt);
                    break;
                case SkaterState.Airborne:
                    AirStep(dt);
                    break;
                case SkaterState.Bailed:
                    BailStep(dt);
                    break;
                case SkaterState.Grinding:
                case SkaterState.LipStall:
                    break; // GrindController / LipController drive the body kinematically
                case SkaterState.Wallride:
                    _wall.Step(dt);
                    break;
            }

            if (_jumpQueued)
            {
                _jumpQueued = false;
                if (State != SkaterState.Bailed) Pop(_queuedCharge);
            }
            _lastVelocity = _rb.linearVelocity;
        }

        private void GroundStep(float dt)
        {
            if (!ProbeGround(true, out RaycastHit hit))
            {
                _timeSinceGrounded += dt;
                if (_timeSinceGrounded > 0.05f) LeaveGround(false);
                return;
            }
            _timeSinceGrounded = 0f;

            AlignTo(hit.normal);
            SnapToGround(hit);

            // Conveyor belts carry the skater without changing their own speed.
            GroundCollider = hit.collider;
            ConveyorSurface belt = null;
            if (hit.collider != null) hit.collider.TryGetComponent(out belt); // TryGetComponent: no editor-only garbage on a miss
            GroundConveyorVelocity = belt != null ? belt.Velocity : Vector3.zero;
            if (belt != null) _rb.position += belt.Velocity * dt;

            Vector3 v = _rb.linearVelocity;
            // Follow curved surfaces: redirect velocity along the new tangent plane, keeping speed.
            Vector3 planar = Vector3.ProjectOnPlane(v, _up);
            if (planar.sqrMagnitude > 1e-4f) v = planar.normalized * v.magnitude;

            float s = Vector3.Dot(v, _heading);
            if (Mathf.Abs(s) > 0.05f) MovementSign = Mathf.Sign(s);

            var f = _acceptInput ? _input.Frame : default;
            bool manual = State == SkaterState.Manual;

            float speedT = Mathf.InverseLerp(0f, motor.pushSpeed, Mathf.Abs(s));
            float turnRate = Mathf.Lerp(motor.turnRateSlow, motor.turnRateFast, speedT);
            float steerX = Mathf.Sign(f.Steer.x) * Mathf.Pow(Mathf.Abs(f.Steer.x), motor.steerExponent);
            float yaw = manual ? 0f : steerX * turnRate * dt * MovementSign;
            CarveRate = dt > 0f ? yaw / dt : 0f;
            IsPushing = false;
            IsBraking = false;
            _heading = Quaternion.AngleAxis(yaw, _up) * _heading;

            // Slope gravity along the heading only (arcade grip ignores sideways slide).
            Vector3 gPlane = Vector3.ProjectOnPlane(Vector3.down * motor.gravity, _up);
            s += Vector3.Dot(gPlane, _heading) * dt;

            float slope = Vector3.Angle(_up, Vector3.up);
            if (!manual && slope < 25f)
            {
                if (f.Steer.y < -0.5f)
                {
                    s = Mathf.MoveTowards(s, 0f, motor.brakeDeceleration * dt);
                    IsBraking = Mathf.Abs(s) > 0.5f;
                }
                else
                {
                    float target = motor.cruiseSpeed + Mathf.Max(0f, f.Steer.y) * (motor.pushSpeed - motor.cruiseSpeed);
                    float dir = Mathf.Abs(s) < 0.05f ? 1f : Mathf.Sign(s);
                    if (Mathf.Abs(s) < target)
                    {
                        s = Mathf.MoveTowards(s, target * dir, motor.acceleration * dt);
                        IsPushing = Mathf.Abs(s) < target - 0.4f; // the last little bit is rolling, not kicking
                    }
                }
            }
            s *= 1f - motor.rollingDrag * dt;
            s = Mathf.Clamp(s, -motor.maxSpeed, motor.maxSpeed);

            _rb.linearVelocity = _heading * s;
            _rb.MoveRotation(Quaternion.LookRotation(_heading, _up));
            _lastTakeoffSlope = slope;
        }

        private void AirStep(float dt)
        {
            AirTime += dt;
            _timeSinceGrounded += dt;
            Vector3 v = _rb.linearVelocity + Vector3.down * motor.gravity * dt;
            _rb.linearVelocity = v;

            // Air spin from steering; the body levels out toward world up.
            var f = _acceptInput ? _input.Frame : default;
            float yaw = f.Steer.x * motor.airSpinRate * dt;
            AirYaw += yaw;
            _up = Vector3.Slerp(_up, Vector3.up, 1f - Mathf.Exp(-6f * dt));
            _heading = Quaternion.AngleAxis(yaw, Vector3.up) * _heading;
            _heading = Vector3.ProjectOnPlane(_heading, _up).normalized;
            _rb.MoveRotation(Quaternion.LookRotation(_heading, _up));

            if (_groundIgnoreTimer > 0f || Vector3.Dot(v, Vector3.up) > 0.5f) return;
            if (ProbeGround(false, out RaycastHit hit)) Land(hit);
        }

        private void BailStep(float dt)
        {
            Vector3 v = _rb.linearVelocity + Vector3.down * motor.gravity * dt;
            Vector3 horiz = Vector3.ProjectOnPlane(v, Vector3.up);
            horiz = Vector3.MoveTowards(horiz, Vector3.zero, 12f * dt);
            _rb.linearVelocity = horiz + Vector3.up * v.y;
        }

        private bool ProbeGround(bool grounded, out RaycastHit hit)
        {
            Vector3 castUp = grounded ? _up : Vector3.up;
            Vector3 origin = _rb.position + castUp * motor.probeStart;
            float restDistance = motor.probeStart - motor.probeRadius;
            float reach = restDistance + (grounded ? motor.groundedTolerance : 0.08f);
            int mask = ~(1 << IgnoreRaycastLayer);

            int count = Physics.SphereCastNonAlloc(origin, motor.probeRadius, -castUp, _probeHits, reach, mask, QueryTriggerInteraction.Ignore);
            hit = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var h = _probeHits[i];
                // Hits reported at distance 0 started inside the collider and carry no usable normal.
                if (h.distance <= 0f || h.collider.TryGetComponent<NonGroundSurface>(out _)) continue;
                if (!found || h.distance < hit.distance) { hit = h; found = true; }
            }
            if (!found)
            {
                IsGrounded = false;
                return false;
            }
            float change = Vector3.Angle(hit.normal, castUp);
            float fromWorld = Vector3.Angle(hit.normal, Vector3.up);
            bool ok = grounded ? change <= motor.maxSurfaceChange : fromWorld <= motor.maxLandingSlope;
            IsGrounded = ok;
            if (ok) GroundNormal = hit.normal;
            return ok;
        }

        private void AlignTo(Vector3 normal)
        {
            _up = normal;
            Vector3 h = Vector3.ProjectOnPlane(_heading, _up);
            if (h.sqrMagnitude > 1e-4f) _heading = h.normalized;
        }

        private void SnapToGround(RaycastHit hit)
        {
            float restDistance = motor.probeStart - motor.probeRadius;
            float gap = hit.distance - restDistance;
            if (gap > 0.01f) _rb.position -= _up * Mathf.Min(gap, 0.3f);
            else if (gap < -0.01f) _rb.position += _up * Mathf.Min(-gap, 0.3f);
        }

        // ------------------------------------------------------------------ transitions

        private void Pop(float charge)
        {
            bool fromFlat = (State == SkaterState.Rolling || State == SkaterState.Manual) && _lastTakeoffSlope < 15f;
            var popVariant = StyleTricks.PopVariant(_acceptInput ? _input.Frame.SteerZone : StickZone.Neutral, charge, fromFlat);
            Vector3 popDir = _lastTakeoffSlope > 45f ? Vector3.Slerp(_up, Vector3.up, 0.5f).normalized : _up;
            float strength = Mathf.Lerp(motor.minPop, motor.maxPop, charge);

            if (State == SkaterState.Grinding) _grind.ExitWithVelocity(_grind.CurrentVelocity + Vector3.up * strength);
            else if (State == SkaterState.Wallride) _wall.Wallie(strength);
            else if (State == SkaterState.LipStall) _lip.DropIn();
            else
            {
                if (State == SkaterState.Manual) _manual.EndContinue();
                _rb.linearVelocity += popDir * strength;
            }
            _combo.OnPop();
            if (_lastTakeoffSlope > 15f && State != SkaterState.Grinding) _combo.MarkElement(LineElement.RampAir);
            EnterAir();
            _poppedThisAir = true;
            _jumpBufferTimer = 0f;
            _visual?.SetCrouch(0f);
            Popped?.Invoke(charge);
            if (popVariant != null) _tricks?.BeginDefinition(StylePack.Get(popVariant));
            TryConsumeBufferedSwipe();
        }

        private void LeaveGround(bool popped)
        {
            if (State == SkaterState.Manual) _manual.EndContinue();

            // Vert assist: off a near-vertical lip, go straight up so you come back down onto the ramp.
            if (!popped && _lastTakeoffSlope > motor.vertAssistSlope)
            {
                Vector3 v = _rb.linearVelocity;
                Vector3 outward = Vector3.ProjectOnPlane(_up, Vector3.up).normalized;
                float along = Vector3.Dot(v, outward);
                v -= outward * along;
                v += outward * 0.35f;
                _rb.linearVelocity = v;
            }
            if (_lastTakeoffSlope > 15f) _combo.MarkElement(LineElement.RampAir);
            EnterAir();
            _poppedThisAir = false; // rolled off an edge: a late (coyote) ollie is still allowed
        }

        private void EnterAir()
        {
            // Leaving a belt keeps its push, so ollies off the conveyor carry over the conveyor gap.
            if (GroundConveyorVelocity != Vector3.zero) _rb.linearVelocity += GroundConveyorVelocity;
            GroundConveyorVelocity = Vector3.zero;
            GroundCollider = null;
            AirTime = 0f;
            AirYaw = 0f;
            _groundIgnoreTimer = motor.groundIgnoreAfterPop;
            IsGrounded = false;
            SetState(SkaterState.Airborne);
        }

        private void Land(RaycastHit hit)
        {
            if (AirTime < 0.08f)
            {
                // Tiny hop (curb, seam): treat as still rolling.
                AlignTo(hit.normal);
                SetState(SkaterState.Rolling);
                return;
            }

            var verdict = LandingJudge.Evaluate(new LandingInput
            {
                AirYawDegrees = AirYaw,
                SurfaceAngleDegrees = CrossSlopeAngle(hit.normal),
                UnfinishedTrickFraction = _tricks.UnfinishedFraction,
            }, _landingRules);

            if (verdict.Quality == LandingQuality.Bail)
            {
                _bail.Trigger(verdict.Reason);
                return;
            }

            LastImpactSpeed = Mathf.Max(0f, -_rb.linearVelocity.y);
            AlignTo(hit.normal);
            SnapToGround(hit);
            _rb.linearVelocity = Vector3.ProjectOnPlane(_rb.linearVelocity, _up);
            AssistLandingAlignment();
            float s = Vector3.Dot(_rb.linearVelocity, _heading);
            if (Mathf.Abs(s) > 0.05f) MovementSign = Mathf.Sign(s);
            _timeSinceGrounded = 0f;
            TimeSinceLanding = 0f;
            _tricks.OnLanded();

            if (verdict.HalfTurns > 0)
            {
                _combo.AddTrick(TrickLibrary.SpinId(verdict.HalfTurns), _library.SpinName(verdict.HalfTurns),
                    TrickCategory.Spin, SpinRules.SpinPoints(verdict.HalfTurns, _scoring));
            }

            _gaps?.OnTouchdown();
            // Landing back on a ramp (or after launching off one) allows a revert.
            _landedFromRampAir = _lastTakeoffSlope > 15f || Vector3.Angle(hit.normal, Vector3.up) > 15f;
            _revertedThisLanding = false;
            SetState(SkaterState.Rolling);
            _poppedThisAir = false;
            _combo.OnLanded(verdict.Quality);
            Landed?.Invoke(verdict);

            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer = 0f;
                _jumpQueued = true;
                _queuedCharge = _bufferedJumpCharge;
            }

            if (_actionBufferTimer > 0f)
            {
                _actionBufferTimer = 0f;
                _manual.TryStartManual(_input.Frame.SteerZone);
            }
        }

        /// <summary>Turns the board onto the line of travel after a non-bail landing so rolling away feels clean.</summary>
        private void AssistLandingAlignment()
        {
            Vector3 travel = Vector3.ProjectOnPlane(_rb.linearVelocity, _up);
            if (travel.sqrMagnitude < 1f || motor.landingAlignAssist <= 0f) return;
            Vector3 dir = travel.normalized;
            if (Vector3.Dot(dir, _heading) < 0f) dir = -dir; // keep fakie/switch landings as they are
            _heading = Vector3.Slerp(_heading, dir, Mathf.Clamp01(motor.landingAlignAssist)).normalized;
        }

        /// <summary>
        /// Arcade landing metric: slope steepness scaled by how sideways the board is to the fall line.
        /// Landing up/down a steep ramp is fine; landing across it is not.
        /// </summary>
        private float CrossSlopeAngle(Vector3 normal)
        {
            float steep = Vector3.Angle(normal, Vector3.up);
            if (steep < 1f) return 0f;
            Vector3 fallLine = Vector3.ProjectOnPlane(Vector3.down, normal).normalized;
            Vector3 h = Vector3.ProjectOnPlane(_heading, normal).normalized;
            float sin = Vector3.Cross(fallLine, h).magnitude;
            return steep * sin;
        }

        /// <summary>Spin the board back around to fakie right after a ramp landing; the combo stays open.</summary>
        private void Revert()
        {
            _revertedThisLanding = true;
            _heading = -_heading;
            MovementSign = -MovementSign;
            _rb.MoveRotation(Quaternion.LookRotation(_heading, _up));
            _combo.AddLinkTrick(RevertRules.Id, RevertRules.Name, TrickCategory.Revert, RevertRules.Points);
            Reverted?.Invoke();
        }

        /// <summary>Raised when a revert lands (sound, tutorial hooks).</summary>
        public event Action Reverted;

        private void TryConsumeBufferedSwipe()
        {
            if (_swipeBufferTimer <= 0f || _bufferedSwipe == SwipeDirection.None) return;
            _tricks.OnSwipe(_bufferedSwipe, _bufferedZone);
            _bufferedSwipe = SwipeDirection.None;
            _swipeBufferTimer = 0f;
        }

        // ------------------------------------------------------------------ API for sibling controllers

        public void SetState(SkaterState next)
        {
            if (next == State) return;
            var prev = State;
            State = next;
            StateChanged?.Invoke(prev, next);
        }

        /// <summary>Called by GrindController when a grind begins; the body becomes kinematic.</summary>
        public void EnterGrind()
        {
            _rb.isKinematic = true;
            _jumpQueued = false;
            SetState(SkaterState.Grinding);
        }

        /// <summary>Wall and lip tricks hold the body kinematically, like grinds.</summary>
        public void EnterHeldTrick(SkaterState state)
        {
            _rb.isKinematic = true;
            _jumpQueued = false;
            SetState(state);
        }

        /// <summary>Called by GrindController on exit with the launch velocity.</summary>
        public void ExitGrind(Vector3 velocity)
        {
            _rb.isKinematic = false;
            _rb.linearVelocity = velocity;
            Vector3 h = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (h.sqrMagnitude > 0.01f) _heading = h.normalized;
            _up = Vector3.up;
            _lastTakeoffSlope = 0f;
            EnterAir();
            _poppedThisAir = false;
        }

        public void SetPose(Vector3 position, Vector3 heading, Vector3 up)
        {
            _up = up;
            _heading = Vector3.ProjectOnPlane(heading, up).normalized;
            _rb.MovePosition(position);
            _rb.MoveRotation(Quaternion.LookRotation(_heading, _up));
        }

        public void EnterBail()
        {
            _jumpQueued = false;
            JumpCharge = 0f;
            if (_rb.isKinematic) _rb.isKinematic = false;
            _rb.linearVelocity *= 0.35f;
            SetState(SkaterState.Bailed);
        }

        /// <summary>Hard reset used by respawn: clears motion and every sub-state.</summary>
        public void Teleport(Vector3 position, Vector3 heading)
        {
            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _up = Vector3.up;
            _heading = Vector3.ProjectOnPlane(heading, Vector3.up).sqrMagnitude > 1e-4f
                ? Vector3.ProjectOnPlane(heading, Vector3.up).normalized
                : Vector3.forward;
            _rb.position = position;
            _rb.rotation = Quaternion.LookRotation(_heading, _up);
            transform.SetPositionAndRotation(position, _rb.rotation);
            AirTime = 0f;
            AirYaw = 0f;
            JumpCharge = 0f;
            _jumpQueued = false;
            _jumpBufferTimer = 0f;
            _poppedThisAir = false;
            _timeSinceGrounded = 0f;
            MovementSign = 1f;
            _groundIgnoreTimer = 0f;
            SetState(SkaterState.Airborne); // lands on the next probe
            _tricks.Cancel();
            _visual?.ResetPose();
        }

        // ------------------------------------------------------------------ collisions

        private void OnCollisionEnter(Collision collision)
        {
            if (State == SkaterState.Bailed || State == SkaterState.Grinding || State == SkaterState.Wallride || State == SkaterState.LipStall) return;
            if (TryBonk(collision)) return;
            if (State == SkaterState.Airborne && _wall != null)
            {
                for (int i = 0; i < collision.contactCount; i++)
                {
                    var c = collision.GetContact(i);
                    // Upright surfaces only; a pressed (armed) grind/action turns the contact into a wall trick.
                    if (Mathf.Abs(Vector3.Dot(c.normal, Vector3.up)) < 0.45f && _wall.OnWallContact(c.normal, c.point, _lastVelocity))
                        return;
                }
            }
            for (int i = 0; i < collision.contactCount; i++)
            {
                var c = collision.GetContact(i);
                // Only wall-like contacts relative to the current board orientation count as impacts.
                if (Mathf.Abs(Vector3.Dot(c.normal, _up)) > 0.5f) continue;
                float impact = -Vector3.Dot(_lastVelocity, c.normal);
                bool headOn = Vector3.Dot(_lastVelocity.normalized, -c.normal) > 0.7f;
                if (impact >= motor.bailImpactSpeed && headOn)
                {
                    _bail.Trigger(BailReason.Collision);
                    return;
                }
                if (impact >= motor.deflectImpactSpeed && Vector3.Dot(_heading, c.normal) < -0.5f)
                {
                    // Glance off walls instead of grinding to a halt against them.
                    Vector3 reflected = Vector3.Reflect(_heading, c.normal);
                    reflected = Vector3.ProjectOnPlane(reflected, _up);
                    if (reflected.sqrMagnitude > 1e-4f) _heading = reflected.normalized;
                    _rb.linearVelocity = _heading * (_lastVelocity.magnitude * 0.4f);
                    return;
                }
            }
        }

        /// <summary>Bonks and pole jams (Phase 18, BonkRules): tap a marked object in the air, or roll up a post.</summary>
        private bool TryBonk(Collision collision)
        {
            var target = collision.collider != null ? collision.collider.GetComponentInParent<RetroSk8.Level.BonkTarget>() : null;
            if (target == null || _combo == null) return false;
            float upDot = 1f;
            for (int i = 0; i < collision.contactCount; i++)
                upDot = Mathf.Min(upDot, Mathf.Abs(Vector3.Dot(collision.GetContact(i).normal, Vector3.up)));
            Vector3 flat = Vector3.ProjectOnPlane(_lastVelocity, Vector3.up);
            bool rolling = State == SkaterState.Rolling || State == SkaterState.Manual;
            var move = BonkRules.Classify(State == SkaterState.Airborne, rolling, flat.magnitude, target.pole, Time.time - target.LastHit, upDot);
            if (move == BonkMove.None) return false;

            target.MarkHit(GetComponent<Collider>(), BonkRules.PassThroughSeconds);
            if (move == BonkMove.PoleJam)
            {
                if (State == SkaterState.Manual) _manual.EndContinue();
                EnterAir();
                _poppedThisAir = true;
                _combo.OnPop();
            }
            Vector3 keep = flat.sqrMagnitude > 1e-4f ? flat * BonkRules.SpeedKeep : _heading * BonkRules.MinBonkSpeed;
            _rb.linearVelocity = keep + Vector3.up * BonkRules.LiftAfter(move, _lastVelocity.y);
            _combo.AddTrick(BonkRules.IdFor(move), BonkRules.NameFor(move), TrickCategory.Bonk,
                Mathf.RoundToInt(BonkRules.PointsFor(move) * RetroSk8.Game.WeeklyService.BonkFactor)); // doubled in Bonk Week
            _combo.MarkElement(LineElement.Air);
            Bonked?.Invoke(move);
            return true;
        }
    }
}
