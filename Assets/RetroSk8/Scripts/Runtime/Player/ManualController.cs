using System;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>
    /// Manuals: started with ACTION inside the post-landing window, balanced with left/right,
    /// ended by ACTION (banks), ollie or rolling off an edge (combo continues), or losing balance (bail).
    /// </summary>
    public sealed class ManualController : MonoBehaviour
    {
        public float minSpeed = 1.2f;
        public float noiseFrequency = 1.1f;

        private PlayerController _player;
        private ComboManager _combo;
        private TrickLibrary _library;
        private SkaterVisual _visual;
        private BailHandler _bail;
        private ScoringProfile _profile;
        private BalanceMeter _balance;
        private bool _nose;
        private float _noiseSeed;

        public bool IsActive => _player != null && _player.State == SkaterState.Manual;
        public BalanceMeter Balance => _balance;
        public event Action ManualStarted;
        public event Action ManualEnded;

        public void Init(PlayerController player, ComboManager combo, TrickLibrary library, SkaterVisual visual, BailHandler bail, ScoringProfile profile)
        {
            _player = player;
            _combo = combo;
            _library = library;
            _visual = visual;
            _bail = bail;
            _profile = profile;
            _balance = new BalanceMeter(profile.manualBalance);
        }

        /// <summary>Rebuilds the balance meter after live tuning changes its settings.</summary>
        public void ApplyBalanceSettings(BalanceSettings settings)
        {
            if (!IsActive) _balance = new BalanceMeter(settings);
        }

        public bool TryStartManual(StickZone zone)
        {
            if (_player.State != SkaterState.Rolling) return false;
            if (_player.TimeSinceLanding > _profile.manualWindow) return false;
            if (_player.Speed < minSpeed) return false;

            _nose = zone == StickZone.Up;
            var trick = _library.GetManual(_nose);
            if (trick == null) return false;

            _noiseSeed = UnityEngine.Random.value * 100f;
            _balance.Begin(UnityEngine.Random.value < 0.5f ? -1 : 1, _combo.RepeatCount(trick.id));
            _combo.StartContinuous(trick);
            _player.SetState(SkaterState.Manual);
            ManualStarted?.Invoke();
            return true;
        }

        private void Update()
        {
            if (!IsActive || Time.timeScale <= 0f) return;
            float dt = Time.deltaTime;
            float noise = Mathf.PerlinNoise(_noiseSeed, Time.time * noiseFrequency) * 2f - 1f;
            if (_balance.Step(dt, _player.CurrentInput.Steer.x, noise))
            {
                _bail.Trigger(BailReason.LostBalance);
                return;
            }
            _combo.TickContinuous(dt);
            _visual.SetBalancePose(_balance.Lean, true, _nose);

            if (_player.Speed < minSpeed) EndAndBank();
        }

        /// <summary>Stop manualling on the ground: the combo banks.</summary>
        public void EndAndBank()
        {
            if (!IsActive) return;
            Stop();
            _player.SetState(SkaterState.Rolling);
            _combo.BankNow(LandingQuality.Clean);
        }

        /// <summary>Leave the manual into the air (ollie or rolling off an edge): the combo continues.</summary>
        public void EndContinue()
        {
            if (!IsActive) return;
            Stop();
        }

        /// <summary>Cleanup without scoring, used by bails.</summary>
        public void Abort()
        {
            if (!_balance.IsActive) return;
            Stop();
        }

        private void Stop()
        {
            _balance.End();
            _combo.EndContinuous();
            _visual.ClearBalancePose();
            ManualEnded?.Invoke();
        }
    }
}
