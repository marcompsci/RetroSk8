using System;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>
    /// Air tricks. Swipe direction picks the family, the stick zone at swipe time picks the variation,
    /// and a full special meter upgrades the trick to that family's special.
    /// One trick animates at a time; one extra swipe is buffered.
    /// </summary>
    public sealed class TrickController : MonoBehaviour
    {
        private const float BufferWindow = 0.25f;

        private PlayerController _player;
        private ComboManager _combo;
        private TrickLibrary _library;
        private SkaterVisual _visual;

        private TrickDefinition _active;
        private float _elapsed;
        private float _sideSign = 1f;
        private SwipeDirection _buffered;
        private StickZone _bufferedZone;
        private float _bufferTimer;

        public TrickDefinition Active => _active;
        public event Action<TrickDefinition> TrickStarted;

        /// <summary>1 = trick just started, 0 = finished or none. Landing above LandingRules.maxUnfinishedFraction bails.</summary>
        public float UnfinishedFraction => _active == null ? 0f : 1f - Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _active.duration));

        public void Init(PlayerController player, ComboManager combo, TrickLibrary library, SkaterVisual visual)
        {
            _player = player;
            _combo = combo;
            _library = library;
            _visual = visual;
        }

        public void OnSwipe(SwipeDirection dir, StickZone zone)
        {
            if (_player.State != SkaterState.Airborne || dir == SwipeDirection.None) return;
            if (_active != null)
            {
                _buffered = dir;
                _bufferedZone = zone;
                _bufferTimer = BufferWindow;
                return;
            }
            Begin(dir, zone);
        }

        private void Begin(SwipeDirection dir, StickZone zone)
        {
            var family = GestureRules.FamilyFor(dir);
            int variation = GestureRules.VariationFor(zone);
            bool special = _combo.Special.IsReady;
            // A full meter on a flip swipe performs your style's signature special.
            var trick = special && family == TrickFamily.Flip ? StylePack.Signature() : _library.GetAir(family, variation, special);
            if (trick == null) return;
            if (trick.isSpecial) _combo.TryConsumeSpecial();

            _active = trick;
            _elapsed = 0f;
            _sideSign = dir == SwipeDirection.Left ? -1f : 1f;
            _combo.AddTrick(trick);
            TrickStarted?.Invoke(trick);
        }

        private void Update()
        {
            if (_bufferTimer > 0f) _bufferTimer -= Time.deltaTime;
            if (_active == null) return;

            _elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _active.duration));
            _visual.SetTrickPose(_active, p, _sideSign);
            if (p < 1f) return;

            _active = null;
            _visual.ClearTrickPose();
            if (_bufferTimer > 0f && _buffered != SwipeDirection.None && _player.State == SkaterState.Airborne)
            {
                var d = _buffered;
                _buffered = SwipeDirection.None;
                Begin(d, _bufferedZone);
            }
        }

        /// <summary>Starts a specific trick (pop variants from the style pack) as if it had been swiped.</summary>
        public void BeginDefinition(TrickDefinition trick)
        {
            if (trick == null || _player.State != SkaterState.Airborne || _active != null) return;
            _active = trick;
            _elapsed = 0f;
            _sideSign = 1f;
            _combo.AddTrick(trick);
            TrickStarted?.Invoke(trick);
        }

        public void OnLanded() => Cancel();

        public void Cancel()
        {
            _active = null;
            _buffered = SwipeDirection.None;
            _bufferTimer = 0f;
            _visual?.ClearTrickPose();
        }
    }
}
