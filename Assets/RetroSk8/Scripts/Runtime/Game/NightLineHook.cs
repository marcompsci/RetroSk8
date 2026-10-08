using RetroSk8.Core;
using RetroSk8.Feedback;
using RetroSk8.Save;
using RetroSk8.Scoring;
using RetroSk8.UI;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Night sessions (Phase 28): watches banked combos in a night session and pays the park's NIGHT LINE the first
    /// time one reaches its target. Night line stickers unlock at 1, 5 and every park.
    /// </summary>
    public sealed class NightLineHook : MonoBehaviour
    {
        private ComboManager _combo;
        private HudView _hud;
        private string _parkId;

        public void Init(ComboManager combo, string parkId, HudView hud)
        {
            _combo = combo;
            _parkId = parkId;
            _hud = hud;
            combo.Banked += OnBanked;
        }

        private void OnDestroy()
        {
            if (_combo != null) _combo.Banked -= OnBanked;
        }

        private void OnBanked(ComboResult result, string label, LandingQuality quality) => Check(result.Points);

        /// <summary>Pays the night line when <paramref name="points"/> reaches it (public for tests).</summary>
        public bool Check(long points)
        {
            var done = SaveManager.Data.nightLines;
            if (!NightSessions.Clears(_parkId, ParkTime.Night, points, done)) return false;
            done.Add(_parkId);
            int before = done.Count - 1;
            SaveManager.AddTokens(NightSessions.LineTokens);
            _hud?.ShowToast($"NIGHT LINE!  +{NightSessions.LineTokens}", Theme.Tape, 2.6f);
            foreach (var s in Stickers.All)
                if (s.UnlockNightLines > before && s.UnlockNightLines <= done.Count)
                    CareerService.Pending.Add($"NEW STICKER: {s.Name}  ·  NIGHT LINES {done.Count}/{NightSessions.ParkCount}");
            HapticsManager.Play(HapticKind.Success);
            return true;
        }
    }
}
