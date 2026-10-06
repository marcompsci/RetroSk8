using RetroSk8.Audio;
using RetroSk8.Core;
using RetroSk8.Player;
using RetroSk8.Save;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Big-moment feedback during a run: fireworks over lines worth showing off and a short slow-motion beat on the
    /// biggest (see <see cref="Juice"/>). Slow-mo scales the physics step with time so it stays smooth, never runs
    /// while paused, and is skipped with Reduced Motion on or in turn-based modes.
    /// </summary>
    public sealed class JuiceController : MonoBehaviour
    {
        private ComboManager _combo;
        private PlayerController _player;
        private RunController _run;
        private VisualFx _fx;
        private float _slowT = -1f;
        private bool _slowAllowed;

        public bool InSlowMo => _slowT >= 0f;

        public void Init(ComboManager combo, PlayerController player, RunController run, VisualFx fx, bool slowMoAllowed)
        {
            _combo = combo;
            _player = player;
            _run = run;
            _fx = fx;
            _slowAllowed = slowMoAllowed;
            combo.Banked += OnBanked;
        }

        private void OnDestroy()
        {
            if (_combo != null) _combo.Banked -= OnBanked;
            if (InSlowMo) Restore();
        }

        private void OnBanked(ComboResult result, string label, LandingQuality quality)
        {
            int sparks = Juice.Fireworks(result.Points);
            if (sparks > 0 && _fx != null && _player != null)
            {
                _fx.Fireworks(_player.transform.position + Vector3.up * 3.2f, sparks);
                AudioManager.Instance?.PlaySfx(SfxId.Firework, 0.6f, Random.Range(0.95f, 1.08f));
            }
            if (Juice.SlowMo(result.Points) && _slowAllowed && !SaveManager.Data.settings.reducedMotion && !InSlowMo
                && (_run == null || !_run.IsPaused))
            {
                _slowT = 0f;
                AudioManager.Instance?.PlaySfx(SfxId.Whoosh, 0.7f);
            }
        }

        private void Update()
        {
            if (!InSlowMo) return;
            if (_run != null && (_run.IsPaused || _run.IsEnding)) { Restore(); return; } // pause owns the time scale
            _slowT += Time.unscaledDeltaTime;
            float scale = Juice.TimeScaleAt(_slowT);
            ActiveAssists.ApplyTimeScale(scale); // on top of the game-speed assist
            if (_slowT >= Juice.SlowMoLength) Restore();
        }

        private void Restore()
        {
            _slowT = -1f;
            if (_run == null || !_run.IsPaused) ActiveAssists.ApplyTimeScale(); // paused: unpausing restores it
        }
    }
}
