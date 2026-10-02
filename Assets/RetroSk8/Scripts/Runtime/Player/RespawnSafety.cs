using RetroSk8.Core;
using RetroSk8.Level;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>
    /// Guarantees the skater can never be permanently stuck:
    /// records recent safe spots, and forces a respawn when out of bounds, airborne too long, or wedged.
    /// </summary>
    public sealed class RespawnSafety : MonoBehaviour
    {
        private const int HistorySize = 8;

        public float sampleInterval = 0.5f;
        [Tooltip("Respawn uses a safe spot at least this old, so you don't reappear at the edge you just fell from.")]
        public float minSampleAge = 1.5f;
        public float maxAirTime = 6f;
        public float stuckSpeed = 0.35f;
        public float stuckTime = 2.5f;

        private PlayerController _player;
        private BailHandler _bail;
        private LevelInfo _level;

        private readonly Vector3[] _positions = new Vector3[HistorySize];
        private readonly Vector3[] _headings = new Vector3[HistorySize];
        private readonly float[] _times = new float[HistorySize];
        private int _count;
        private int _next;
        private float _sampleTimer;
        private float _stuckTimer;

        public void Init(PlayerController player, BailHandler bail, LevelInfo level)
        {
            _player = player;
            _bail = bail;
            _level = level;
        }

        private void FixedUpdate()
        {
            if (_player == null || _bail.IsBailing) return;
            Vector3 pos = _player.Body.position;

            if (_level != null && _level.IsOutOfBounds(pos))
            {
                _bail.Trigger(BailReason.OutOfBounds);
                return;
            }

            if (_player.State == SkaterState.Airborne && _player.AirTime > maxAirTime)
            {
                _bail.RespawnNow();
                return;
            }

            // Rolling auto-cruises, so standing still while not braking means something is blocking the skater.
            bool braking = _player.CurrentInput.Steer.y < -0.5f;
            bool slow = _player.Speed < stuckSpeed;
            bool held = _player.State == SkaterState.Grinding || _player.State == SkaterState.LipStall || _player.State == SkaterState.Wallride;
            bool wedged = slow && !braking && !held;
            _stuckTimer = wedged ? _stuckTimer + Time.fixedDeltaTime : 0f;
            if (_stuckTimer > stuckTime)
            {
                _stuckTimer = 0f;
                _bail.RespawnNow();
                return;
            }

            _sampleTimer -= Time.fixedDeltaTime;
            if (_sampleTimer <= 0f && IsSafeToRecord())
            {
                _sampleTimer = sampleInterval;
                _positions[_next] = pos;
                _headings[_next] = _player.Heading;
                _times[_next] = Time.time;
                _next = (_next + 1) % HistorySize;
                _count = Mathf.Min(_count + 1, HistorySize);
            }
        }

        private bool IsSafeToRecord()
        {
            return _player.State == SkaterState.Rolling
                   && _player.IsGrounded
                   && Vector3.Angle(_player.GroundNormal, Vector3.up) < 8f
                   && _player.Speed > 1f
                   && (_level == null || !_level.IsOutOfBounds(_player.Body.position + Vector3.down));
        }

        public void GetRespawnPose(out Vector3 position, out Vector3 heading)
        {
            float now = Time.time;
            for (int i = 1; i <= _count; i++)
            {
                int idx = (_next - i + HistorySize) % HistorySize;
                if (now - _times[idx] < minSampleAge) continue;
                position = _positions[idx] + Vector3.up * 0.1f;
                heading = _headings[idx];
                return;
            }
            if (_level != null && _level.spawnPoint != null)
            {
                position = _level.spawnPoint.position;
                heading = _level.spawnPoint.forward;
                return;
            }
            position = Vector3.up;
            heading = Vector3.forward;
        }

        public void ClearHistory()
        {
            _count = 0;
            _next = 0;
        }
    }
}
