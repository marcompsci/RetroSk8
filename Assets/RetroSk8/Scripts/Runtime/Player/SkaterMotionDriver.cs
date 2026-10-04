using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>
    /// Feeds the skater's everyday motion layer (Phase 16) from the controller: push kicks while speeding up, a squash
    /// on every landing, the board's nose-up pop on an ollie, a lean into turns and a tuck in the air. Shapes come
    /// from <see cref="SkaterMotion"/>; tricks, grinds, manuals and bails take over the pose when they run.
    /// </summary>
    public sealed class SkaterMotionDriver : MonoBehaviour
    {
        private PlayerController _player;
        private SkaterVisual _visual;
        private float _pushPhase;
        private float _pushWeight;
        private float _sinceLanding = 99f;
        private float _landImpact;
        private float _sincePop = 99f;
        private float _carve;
        private float _tuck;

        public void Init(PlayerController player, SkaterVisual visual)
        {
            _player = player;
            _visual = visual;
            player.Landed += OnLanded;
            player.Popped += OnPopped;
        }

        private void OnDestroy()
        {
            if (_player == null) return;
            _player.Landed -= OnLanded;
            _player.Popped -= OnPopped;
        }

        private void OnLanded(LandingVerdict v)
        {
            _sinceLanding = 0f;
            // Longer airs land heavier.
            _landImpact = Mathf.Clamp01(_player.AirTime / 1.2f);
        }

        private void OnPopped(float charge) => _sincePop = 0f;

        private void LateUpdate()
        {
            if (_player == null || _visual == null) return;
            float dt = Time.deltaTime;
            _sinceLanding += dt;
            _sincePop += dt;

            var state = _player.State;
            bool rolling = state == SkaterState.Rolling;
            bool air = state == SkaterState.Airborne;

            // Push: ramp in while kicking; finish the current kick before standing back on the board.
            bool pushing = rolling && _player.IsPushing && _player.JumpCharge <= 0f;
            if (pushing || (_pushWeight > 0.01f && Frac(_pushPhase) > 0.05f))
            {
                _pushPhase += dt * SkaterMotion.PushRate * (pushing ? 1f : 1.6f);
                _pushWeight = Mathf.MoveTowards(_pushWeight, pushing ? 1f : 0f, dt * 4f);
            }
            else
            {
                _pushWeight = Mathf.MoveTowards(_pushWeight, 0f, dt * 6f);
                _pushPhase = 0f;
            }
            if (!rolling) { _pushWeight = 0f; _pushPhase = 0f; }

            float targetCarve = rolling ? SkaterMotion.CarveLean(_player.CarveRate, _player.Speed) : 0f;
            _carve = Mathf.Lerp(_carve, targetCarve, 1f - Mathf.Exp(-10f * dt));
            _tuck = Mathf.MoveTowards(_tuck, air ? 1f : 0f, dt * (air ? 3f : 8f));

            if (state == SkaterState.Bailed || !_visual.MotionFree) return;
            var (swing, reach, bob) = SkaterMotion.Push(_pushPhase);
            float squash = SkaterMotion.Squash(_sinceLanding, _landImpact) + (rolling && _player.IsBraking ? 0.35f : 0f);
            _visual.SetMotion(swing * _pushWeight, reach * _pushWeight, bob * _pushWeight, squash,
                SkaterMotion.Pop(_sincePop), _carve, _tuck);
        }

        private static float Frac(float v) => v - Mathf.Floor(v);
    }
}
