using System;
using RetroSk8.Core;
using RetroSk8.Level;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>
    /// Lip tricks: come up a quarter pipe square to the coping and press GRIND/ACTION near the top to stall on it.
    /// The stick picks the stall. Press ACTION or JUMP to drop back in (it also drops you in automatically after
    /// a couple of seconds). Points accrue while you hold it, like a grind.
    /// </summary>
    public sealed class LipController : MonoBehaviour
    {
        public float reach = 1.4f;

        private PlayerController _player;
        private ComboManager _combo;
        private SkaterVisual _visual;
        private TrickController _tricks;

        private GrindRail _rail;
        private Vector3 _point;
        private Vector3 _inward;   // horizontal direction back down into the ramp
        private float _time;

        public bool IsActive => _rail != null;
        public string CurrentName { get; private set; }

        public event Action<string> LipStarted;
        public event Action LipEnded;

        public void Init(PlayerController player, ComboManager combo, SkaterVisual visual)
        {
            _player = player;
            _combo = combo;
            _visual = visual;
            _tricks = GetComponent<TrickController>();
        }

        public bool TryStart(StickZone zone)
        {
            if (IsActive) return false;
            var state = _player.State;
            if (state != SkaterState.Airborne && state != SkaterState.Rolling) return false;

            Vector3 feet = _player.Body.position;
            if (!NearestCoping(feet + Vector3.up * 0.4f, out var rail, out float along, out Vector3 point)) return false;

            Vector3 tangent = rail.TangentAt(along);
            Vector3 flatV = Vector3.ProjectOnPlane(_player.Body.linearVelocity, Vector3.up);
            Vector3 travel = flatV.sqrMagnitude > 0.5f ? flatV.normalized : Vector3.ProjectOnPlane(_player.Heading, Vector3.up).normalized;
            if (travel.sqrMagnitude < 1e-4f) return false;
            float angle = Vector3.Angle(travel, tangent);
            if (angle > 90f) angle = 180f - angle;
            if (!LipRules.IsLipApproach(angle)) return false;

            // Coming up the ramp means travelling outward over the coping, so "inward" is the opposite way.
            Vector3 across = Vector3.Cross(tangent, Vector3.up).normalized;
            _inward = Vector3.Dot(across, travel) > 0f ? -across : across;

            _tricks.Cancel();
            _rail = rail;
            _point = point;
            _time = 0f;
            CurrentName = LipRules.NameFor(zone);
            _combo.StartContinuous(LipRules.IdFor(zone), CurrentName, TrickCategory.Lip, LipRules.StartPoints);
            _combo.MarkElement(LineElement.Lip);
            _player.EnterHeldTrick(SkaterState.LipStall);
            _player.SetPose(_point + Vector3.up * 0.04f, -_inward, Vector3.up);
            _visual.SetBalancePose(0f, true, zone == StickZone.Up);
            LipStarted?.Invoke(CurrentName);
            return true;
        }

        private void Update()
        {
            if (!IsActive || Time.timeScale <= 0f) return;
            float dt = Time.deltaTime;
            _time += dt;
            _combo.TickContinuous(dt);
            // A small wobble so the stall reads as balanced, not frozen.
            _visual.SetBalancePose(Mathf.Sin(_time * 7f) * 0.15f, true, CurrentName == LipRules.NameFor(StickZone.Up));
            if (_time >= LipRules.MaxHoldSeconds) DropIn();
        }

        /// <summary>Back into the ramp: step off the coping on the ramp side and let gravity carry you down the transition.</summary>
        public void DropIn()
        {
            if (!IsActive) return;
            Vector3 start = _point + _inward * 0.45f + Vector3.up * 0.15f;
            Cleanup();
            _player.SetPose(start, _inward, Vector3.up);
            _player.ExitGrind(_inward * 1.5f + Vector3.down * 0.5f);
        }

        public void Abort()
        {
            if (IsActive) Cleanup();
        }

        private void Cleanup()
        {
            _rail = null;
            _combo.EndContinuous();
            _visual.ClearBalancePose();
            LipEnded?.Invoke();
        }

        private bool NearestCoping(Vector3 pos, out GrindRail best, out float bestAlong, out Vector3 bestPoint)
        {
            best = null;
            bestAlong = 0f;
            bestPoint = Vector3.zero;
            float bestDist = reach;
            foreach (var rail in GrindRail.Active)
            {
                if (rail == null || rail.surface != GrindSurface.Coping) continue;
                float along = rail.ClosestDistance(pos, out Vector3 p);
                float d = Vector3.Distance(pos, p);
                if (d < bestDist && !rail.IsEnd(along, 0.3f))
                {
                    bestDist = d;
                    best = rail;
                    bestAlong = along;
                    bestPoint = p;
                }
            }
            return best != null;
        }
    }
}
