using System;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Level;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>Locks the skater to a GrindRail, runs the balance meter and feeds grind points into the combo.</summary>
    public sealed class GrindController : MonoBehaviour
    {
        [Serializable]
        public class GrindSettings
        {
            public float snapRadius = 1.5f;
            [Tooltip("Extra snap radius per m/s of speed, so fast approaches are as forgiving as slow ones.")]
            public float snapRadiusPerSpeed = 0.04f;
            public float maxSnapRadius = 2.2f;
            public float maxRailAboveFeetGrounded = 0.95f;
            public float maxRailAboveFeetAir = 1.9f;
            public float maxRailBelowFeet = 1.2f;
            public float minSpeed = 5f;
            public float deceleration = 0.6f;
            public float exitUpSpeed = 1.5f;
            public float noiseFrequency = 0.9f;
        }

        public GrindSettings settings = new GrindSettings();

        private PlayerController _player;
        private ComboManager _combo;
        private TrickLibrary _library;
        private SkaterVisual _visual;
        private BailHandler _bail;
        private BalanceMeter _balance;
        private TrickController _tricks;
        private ManualController _manual;

        private GrindRail _rail;
        private float _distance;
        private float _dir;
        private float _speed;
        private float _noiseSeed;

        public bool IsGrinding => _rail != null;
        public BalanceMeter Balance => _balance;
        public GrindRail CurrentRail => _rail;
        public TrickDefinition CurrentTrick { get; private set; }
        public Vector3 CurrentVelocity => _rail == null ? Vector3.zero : _rail.TangentAt(_distance) * (_dir * _speed);

        public event Action<GrindRail> GrindStarted;
        public event Action GrindEnded;

        public void Init(PlayerController player, ComboManager combo, TrickLibrary library, SkaterVisual visual, BailHandler bail, ScoringProfile profile)
        {
            _player = player;
            _combo = combo;
            _library = library;
            _visual = visual;
            _bail = bail;
            _balance = new BalanceMeter(profile.grindBalance);
            _tricks = GetComponent<TrickController>();
            _manual = GetComponent<ManualController>();
        }

        /// <summary>Rebuilds the balance meter after live tuning changes its settings.</summary>
        public void ApplyBalanceSettings(BalanceSettings settings)
        {
            if (!IsGrinding) _balance = new BalanceMeter(settings);
        }

        public bool TryStartGrind(StickZone zone)
        {
            if (IsGrinding) return false;
            var state = _player.State;
            if (state != SkaterState.Rolling && state != SkaterState.Airborne && state != SkaterState.Manual) return false;
            if (_tricks.UnfinishedFraction > 0.2f) return false;

            Vector3 feet = _player.Body.position;
            float radius = Mathf.Min(settings.maxSnapRadius, settings.snapRadius + _player.Speed * settings.snapRadiusPerSpeed);
            if (!GrindRail.FindNearest(feet + Vector3.up * 0.4f, radius, out var rail, out float along, out Vector3 point))
                return false;

            float above = point.y - feet.y;
            float maxAbove = state == SkaterState.Airborne ? settings.maxRailAboveFeetAir : settings.maxRailAboveFeetGrounded;
            if (above > maxAbove || above < -settings.maxRailBelowFeet) return false;

            Vector3 v = _player.Body.linearVelocity;
            Vector3 tangent = rail.TangentAt(along);
            Vector3 flatV = Vector3.ProjectOnPlane(v, Vector3.up);
            Vector3 reference = flatV.sqrMagnitude > 0.25f ? flatV : _player.Heading;
            _dir = Vector3.Dot(reference, tangent) >= 0f ? 1f : -1f;
            _speed = Mathf.Max(settings.minSpeed, Mathf.Abs(Vector3.Dot(v, tangent)), flatV.magnitude * 0.6f);

            if (rail.IsEnd(along, 0.25f) && ((_dir > 0f && along > rail.Length * 0.5f) || (_dir < 0f && along < rail.Length * 0.5f)))
                return false; // would pop straight off the end

            if (_player.State == SkaterState.Manual) _manual.EndContinue();
            _tricks.Cancel();

            _rail = rail;
            _distance = along;
            _noiseSeed = UnityEngine.Random.value * 100f;
            CurrentTrick = StyleTricks.IsBluntslide(zone, rail.surface == GrindSurface.Rail)
                ? StylePack.Get(StyleTricks.Bluntslide)
                : _library.GetGrind(GestureRules.VariationFor(zone));
            int repeats = CurrentTrick != null ? _combo.RepeatCount(CurrentTrick.id) : 0;
            _balance.Begin(UnityEngine.Random.value < 0.5f ? -1 : 1, repeats);

            GetComponent<GapTracker>()?.OnTouchdown(); // gap-to-grind counts
            _player.EnterGrind();
            if (CurrentTrick != null) _combo.StartContinuous(CurrentTrick);
            ApplyPose();
            GrindStarted?.Invoke(rail);
            return true;
        }

        private void FixedUpdate()
        {
            if (!IsGrinding) return;
            float dt = Time.fixedDeltaTime;

            _speed = Mathf.Max(settings.minSpeed, _speed - settings.deceleration * dt);
            _distance += _dir * _speed * dt;

            if (!_rail.loop && (_distance <= 0f || _distance >= _rail.Length))
            {
                _distance = _rail.Normalize(_distance);
                ExitWithVelocity(CurrentVelocity + Vector3.up * settings.exitUpSpeed);
                return;
            }
            _distance = _rail.Normalize(_distance);
            ApplyPose();
        }

        private void Update()
        {
            if (!IsGrinding || Time.timeScale <= 0f) return;
            float dt = Time.deltaTime;
            float noise = Mathf.PerlinNoise(_noiseSeed, Time.time * settings.noiseFrequency) * 2f - 1f;
            float input = _player.CurrentInput.Steer.x;
            if (_balance.Step(dt, input, noise))
            {
                _bail.Trigger(BailReason.LostBalance);
                return;
            }
            _combo.TickContinuous(dt);
            _visual.SetBalancePose(_balance.Lean, false, false);
        }

        private void ApplyPose()
        {
            Vector3 pos = _rail.PointAt(_distance);
            Vector3 fwd = _rail.TangentAt(_distance) * _dir;
            Vector3 flat = Vector3.ProjectOnPlane(fwd, Vector3.up);
            _player.SetPose(pos, flat.sqrMagnitude > 1e-4f ? flat.normalized : _player.Heading, Vector3.up);
        }

        public void ExitWithVelocity(Vector3 velocity)
        {
            if (!IsGrinding) return;
            Cleanup();
            _player.ExitGrind(velocity);
        }

        /// <summary>Ends the grind without touching the player state (used by bails and respawns).</summary>
        public void Abort()
        {
            if (!IsGrinding) return;
            Cleanup();
        }

        private void Cleanup()
        {
            _rail = null;
            _balance.End();
            _combo.EndContinuous();
            _visual.ClearBalancePose();
            GrindEnded?.Invoke();
        }
    }
}
