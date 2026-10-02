using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Feedback;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Audio
{
    /// <summary>
    /// Maps skater events to sounds and haptics. The only place gameplay and feedback meet.
    /// Rolling and grinding are surface-aware: each surface has its own loop and they crossfade as the
    /// wheels move from concrete to plywood, metal or a conveyor belt, or from a metal rail to a ledge.
    /// </summary>
    public sealed class SkaterAudio : MonoBehaviour
    {
        private const float FadeSpeed = 6f;

        private PlayerController _player;
        private AudioManager _audio;
        private ComboManager _combo;
        private bool _specialWasReady;

        // Indexed by SurfaceKind.
        private readonly AudioSource[] _rolls = new AudioSource[4];
        private AudioSource _grindMetal;
        private AudioSource _grindLedge;
        private bool _grinding;
        private bool _grindOnLedge;
        private SurfaceKind _surface;

        public SurfaceKind CurrentSurface => _surface;

        public void Init(PlayerController player, ComboManager combo, TrickController tricks, GrindController grind, ManualController manual, BailHandler bail)
        {
            _player = player;
            _combo = combo;
            _audio = AudioManager.Ensure();
            _rolls[(int)SurfaceKind.Concrete] = _audio.CreateEffectLoop(SfxId.RollLoop, transform);
            _rolls[(int)SurfaceKind.Wood] = _audio.CreateEffectLoop(SfxId.RollWood, transform);
            _rolls[(int)SurfaceKind.Metal] = _audio.CreateEffectLoop(SfxId.RollMetal, transform);
            _rolls[(int)SurfaceKind.Rubber] = _audio.CreateEffectLoop(SfxId.RollRubber, transform);
            foreach (var r in _rolls) r.Play(); // silent until their surface is under the wheels
            _grindMetal = _audio.CreateEffectLoop(SfxId.GrindLoop, transform);
            _grindLedge = _audio.CreateEffectLoop(SfxId.GrindLedge, transform);

            player.Popped += charge =>
            {
                _audio.PlaySfx(SfxId.Pop, 0.8f + 0.2f * charge, 1f + 0.1f * charge);
                HapticsManager.Play(HapticKind.Light);
            };
            player.Landed += verdict =>
            {
                bool sketchy = verdict.Quality == LandingQuality.Sketchy;
                var surface = SurfaceLookup.FromCollider(player.GroundCollider);
                // Plywood lands low and boomy, metal a little brighter.
                float pitch = surface == SurfaceKind.Wood ? 0.8f : surface == SurfaceKind.Metal ? 1.15f : 1f;
                _audio.PlaySfx(sketchy ? SfxId.LandSketchy : SfxId.Land, Mathf.Clamp01(0.5f + player.Speed / 20f), pitch);
                HapticsManager.Play(sketchy ? HapticKind.Medium : HapticKind.Light);
            };
            tricks.TrickStarted += t =>
            {
                _audio.PlaySfx(SfxId.TrickWhoosh, 0.6f, t.isSpecial ? 0.8f : Random.Range(0.95f, 1.1f));
                if (t.isSpecial) HapticsManager.Play(HapticKind.Medium);
            };
            grind.GrindStarted += rail =>
            {
                _grinding = true;
                _grindOnLedge = rail != null && rail.surface == GrindSurface.Ledge;
                var src = _grindOnLedge ? _grindLedge : _grindMetal;
                if (!src.isPlaying) src.Play();
                HapticsManager.Play(HapticKind.Medium);
            };
            grind.GrindEnded += () => _grinding = false;
            manual.ManualStarted += () => HapticsManager.Play(HapticKind.Selection);
            var wall = player.GetComponent<WallController>();
            if (wall != null)
            {
                wall.WallStarted += move =>
                {
                    if (move == WallMove.Wallride) { _grinding = true; _grindOnLedge = true; if (!_grindLedge.isPlaying) _grindLedge.Play(); }
                    else _audio.PlaySfx(SfxId.Land, 0.8f, 0.7f); // board slapping the wall
                    HapticsManager.Play(HapticKind.Medium);
                };
                wall.WallEnded += () => _grinding = false;
            }
            var lip = player.GetComponent<LipController>();
            if (lip != null) lip.LipStarted += _ => { _audio.PlaySfx(SfxId.Land, 0.6f, 1.3f); HapticsManager.Play(HapticKind.Medium); };
            player.Reverted += () => { _audio.PlaySfx(SfxId.TrickWhoosh, 0.5f, 1.25f); HapticsManager.Play(HapticKind.Light); };
            bail.BailStarted += _ =>
            {
                _audio.PlaySfx(SfxId.Bail);
                _audio.Duck(0.35f, 1.4f); // let the slam land before the music comes back
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
            float dt = Time.deltaTime;
            float fx = _audio.BusVolume(AudioBus.Effects);
            bool rolling = (_player.State == SkaterState.Rolling || _player.State == SkaterState.Manual) && _player.IsGrounded;
            if (rolling) _surface = SurfaceLookup.FromCollider(_player.GroundCollider);
            float speedK = Mathf.Clamp01(_player.Speed / 16f);
            float rollTarget = rolling ? speedK * 0.55f * fx : 0f;

            for (int i = 0; i < _rolls.Length; i++)
            {
                var src = _rolls[i];
                float target = i == (int)_surface ? rollTarget : 0f;
                src.volume = Mathf.MoveTowards(src.volume, target, dt * FadeSpeed * 0.7f);
                src.pitch = 0.7f + speedK * 0.6f;
            }

            float grindTarget = _grinding ? 0.5f * fx : 0f;
            Fade(_grindMetal, _grindOnLedge ? 0f : grindTarget, speedK, dt);
            Fade(_grindLedge, _grindOnLedge ? grindTarget : 0f, speedK, dt);

            bool ready = _combo.Special.IsReady;
            if (ready && !_specialWasReady)
            {
                _audio.PlaySfx(SfxId.SpecialReady, 0.7f);
                HapticsManager.Play(HapticKind.Success);
            }
            _specialWasReady = ready;
        }

        private static void Fade(AudioSource src, float target, float speedK, float dt)
        {
            src.volume = Mathf.MoveTowards(src.volume, target, dt * FadeSpeed);
            src.pitch = 0.9f + speedK * 0.3f;
            if (src.volume <= 0f && target <= 0f && src.isPlaying) src.Stop();
        }
    }
}
