using System;
using System.Collections;
using RetroSk8.Core;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>Single entry point for every bail: wipes the combo, plays the tumble, then respawns safely.</summary>
    public sealed class BailHandler : MonoBehaviour
    {
        public float bailDuration = 1.3f;
        public float outOfBoundsDuration = 0.6f;

        private PlayerController _player;
        private ComboManager _combo;
        private GrindController _grind;
        private ManualController _manual;
        private TrickController _tricks;
        private SkaterVisual _visual;
        private RespawnSafety _respawn;
        private Coroutine _routine;

        public bool IsBailing => _routine != null;
        public event Action<BailReason> BailStarted;
        public event Action Respawned;

        public void Init(PlayerController player, ComboManager combo, RespawnSafety respawn)
        {
            _player = player;
            _combo = combo;
            _respawn = respawn;
            _grind = GetComponent<GrindController>();
            _manual = GetComponent<ManualController>();
            _tricks = GetComponent<TrickController>();
            _visual = GetComponentInChildren<SkaterVisual>();
        }

        public void Trigger(BailReason reason)
        {
            if (IsBailing || _player == null) return;
            _grind.Abort();
            _manual.Abort();
            GetComponent<WallController>()?.Abort();
            GetComponent<LipController>()?.Abort();
            _tricks.Cancel();
            _player.EnterBail();
            _combo.Bail(reason);
            if (reason != BailReason.OutOfBounds) _visual.PlayBail();
            BailStarted?.Invoke(reason);
            _routine = StartCoroutine(RespawnAfter(reason == BailReason.OutOfBounds ? outOfBoundsDuration : bailDuration));
        }

        /// <summary>Immediate respawn with no penalty (stuck recovery, debug, pause menu).</summary>
        public void RespawnNow()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            _grind.Abort();
            _manual.Abort();
            GetComponent<WallController>()?.Abort();
            GetComponent<LipController>()?.Abort();
            _combo.Discard();
            DoRespawn();
        }

        private IEnumerator RespawnAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _routine = null;
            DoRespawn();
        }

        private void DoRespawn()
        {
            _respawn.GetRespawnPose(out Vector3 pos, out Vector3 heading);
            _player.Teleport(pos, heading);
            _visual.ResetPose();
            Respawned?.Invoke();
        }
    }
}
