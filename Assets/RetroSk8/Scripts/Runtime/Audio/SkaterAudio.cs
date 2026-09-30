using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Feedback;
using RetroSk8.Player;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Audio
{
    /// <summary>Maps skater events to sounds and haptics. The only place gameplay and feedback meet.</summary>
    public sealed class SkaterAudio : MonoBehaviour
    {
        private PlayerController _player;
        private AudioManager _audio;
        private AudioSource _roll;
        private AudioSource _grind;
        private bool _specialWasReady;
        private ComboManager _combo;

        public void Init(PlayerController player, ComboManager combo, TrickController tricks, GrindController grind, ManualController manual, BailHandler bail)
        {
            _player = player;
            _combo = combo;
            _audio = AudioManager.Ensure();
            _roll = _audio.CreateEffectLoop(SfxId.RollLoop, transform);
            _grind = _audio.CreateEffectLoop(SfxId.GrindLoop, transform);
            _roll.Play();

            player.Popped += charge =>
            {
                _audio.PlaySfx(SfxId.Pop, 0.8f + 0.2f * charge, 1f + 0.1f * charge);
                HapticsManager.Play(HapticKind.Light);
            };
            player.Landed += verdict =>
            {
                bool sketchy = verdict.Quality == LandingQuality.Sketchy;
                _audio.PlaySfx(sketchy ? SfxId.LandSketchy : SfxId.Land, Mathf.Clamp01(0.5f + player.Speed / 20f));
                HapticsManager.Play(sketchy ? HapticKind.Medium : HapticKind.Light);
            };
            tricks.TrickStarted += t =>
            {
                _audio.PlaySfx(SfxId.TrickWhoosh, 0.6f, t.isSpecial ? 0.8f : Random.Range(0.95f, 1.1f));
                if (t.isSpecial) HapticsManager.Play(HapticKind.Medium);
            };
            grind.GrindStarted += _ =>
            {
                _grind.Play();
                HapticsManager.Play(HapticKind.Medium);
            };
            grind.GrindEnded += () => _grind.Stop();
            manual.ManualStarted += () => HapticsManager.Play(HapticKind.Selection);
            bail.BailStarted += _ =>
            {
                _audio.PlaySfx(SfxId.Bail);
                HapticsManager.Play(HapticKind.Heavy);
            };
            combo.Banked += (result, label, quality) =>
            {
                if (result.Points <= 0) return;
                _audio.PlaySfx(SfxId.Bank, 0.7f, 1f + Mathf.Min(0.3f, result.Points / 50000f));
                HapticsManager.Play(HapticKind.Success);
            };
        }

        private void Update()
        {
            if (_player == null) return;
            float fx = _audio.BusVolume(AudioBus.Effects);
            bool rolling = (_player.State == SkaterState.Rolling || _player.State == SkaterState.Manual) && _player.IsGrounded;
            float speedK = Mathf.Clamp01(_player.Speed / 16f);
            _roll.volume = Mathf.MoveTowards(_roll.volume, rolling ? speedK * 0.55f * fx : 0f, Time.deltaTime * 4f);
            _roll.pitch = 0.7f + speedK * 0.6f;
            _grind.volume = 0.5f * fx;
            _grind.pitch = 0.9f + speedK * 0.3f;

            bool ready = _combo.Special.IsReady;
            if (ready && !_specialWasReady)
            {
                _audio.PlaySfx(SfxId.SpecialReady, 0.7f);
                HapticsManager.Play(HapticKind.Success);
            }
            _specialWasReady = ready;
        }
    }
}
