using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Level;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>Remembers gaps crossed during the current air and pays them out on a successful touchdown or grind.</summary>
    public sealed class GapTracker : MonoBehaviour
    {
        private PlayerController _player;
        private ComboManager _combo;
        private readonly List<GapZone> _pending = new List<GapZone>();

        public event Action<GapZone> GapCleared;

        public void Init(PlayerController player, ComboManager combo)
        {
            _player = player;
            _combo = combo;
            player.StateChanged += (prev, next) =>
            {
                // A new air starts clean; a bail forfeits whatever was crossed.
                if ((next == SkaterState.Airborne && prev != SkaterState.Airborne) || next == SkaterState.Bailed) _pending.Clear();
            };
        }

        public void OnEnterGap(GapZone zone)
        {
            if (_player == null || _player.State != SkaterState.Airborne || _pending.Contains(zone)) return;
            _pending.Add(zone);
        }

        /// <summary>Called by PlayerController on a non-bail landing and by GrindController when a grind locks in.</summary>
        public void OnTouchdown()
        {
            if (_pending.Count == 0) return;
            foreach (var zone in _pending)
            {
                _combo.AddTrick("gap_" + zone.gapId, zone.displayName, TrickCategory.Gap, Mathf.RoundToInt(zone.points * RetroSk8.Game.WeeklyService.GapFactor));
                GapCleared?.Invoke(zone);
            }
            _pending.Clear();
        }
    }
}
