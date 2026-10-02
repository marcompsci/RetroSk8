using System;
using System.Text;
using RetroSk8.Core;
using RetroSk8.Data;
using UnityEngine;

namespace RetroSk8.Scoring
{
    /// <summary>
    /// Scene-facing wrapper around ComboTracker. Owns the post-landing manual window, which decides
    /// whether a landing banks the combo or a manual keeps it alive.
    /// </summary>
    public sealed class ComboManager : MonoBehaviour
    {
        private ScoringProfile _profile;
        private ScoreManager _score;
        private float _windowTimer;
        private LandingQuality _pendingQuality;

        public ComboTracker Tracker { get; private set; }
        public SpecialMeter Special { get; private set; }
        public bool HasPendingBank { get; private set; }
        public bool InManualWindow => HasPendingBank && _windowTimer > 0f;
        public bool AcceptingTricks { get; set; } = true;

        public event Action ComboChanged;
        public event Action<string, float> TrickAdded;                 // display name, base points after decay
        public event Action<ComboResult, string, LandingQuality> Banked;
        public event Action<long, BailReason> Bailed;
        public event Action Discarded;

        public void Init(ScoringProfile profile, ScoreManager score)
        {
            _profile = profile;
            _score = score;
            Tracker = new ComboTracker(profile.scoring);
            Special = new SpecialMeter(profile.specialFillPoints);
        }

        public void AddTrick(TrickDefinition trick) => AddTrick(trick.id, trick.displayName, trick.category, trick.baseValue);

        public void AddTrick(string id, string displayName, TrickCategory category, int baseValue)
        {
            if (!AcceptingTricks) return;
            if (HasPendingBank) BankNow();
            float pts = Tracker.AddTrick(id, displayName, category, baseValue);
            TrickAdded?.Invoke(displayName, pts);
            ComboChanged?.Invoke();
        }

        /// <summary>Starts a grind or manual. Starting one inside the landing window keeps the combo alive.</summary>
        public void StartContinuous(TrickDefinition trick)
        {
            if (!AcceptingTricks) return;
            HasPendingBank = false;
            float factor = Tracker.PeekRepeatFactor(trick.id);
            Tracker.StartContinuous(trick.id, trick.displayName, trick.category, trick.baseValue);
            TrickAdded?.Invoke(trick.displayName, trick.baseValue * factor);
            ComboChanged?.Invoke();
        }

        /// <summary>Starts a continuous trick that has no TrickDefinition asset (lip stalls, wallrides).</summary>
        public void StartContinuous(string id, string displayName, TrickCategory category, int baseValue)
        {
            if (!AcceptingTricks) return;
            HasPendingBank = false;
            float factor = Tracker.PeekRepeatFactor(id);
            Tracker.StartContinuous(id, displayName, category, baseValue);
            TrickAdded?.Invoke(displayName, baseValue * factor);
            ComboChanged?.Invoke();
        }

        /// <summary>
        /// Adds a trick done right after a landing (a revert) without banking: the combo stays open and the
        /// manual window restarts, so revert → manual keeps the line going.
        /// </summary>
        public void AddLinkTrick(string id, string displayName, TrickCategory category, int baseValue)
        {
            if (!AcceptingTricks) return;
            bool wasPending = HasPendingBank;
            var quality = _pendingQuality;
            HasPendingBank = false;
            float pts = Tracker.AddTrick(id, displayName, category, baseValue);
            TrickAdded?.Invoke(displayName, pts);
            if (wasPending)
            {
                HasPendingBank = true;
                _pendingQuality = quality;
                _windowTimer = _profile.manualWindow;
            }
            ComboChanged?.Invoke();
        }

        public void TickContinuous(float dt)
        {
            if (!Tracker.HasActiveContinuous) return;
            Tracker.TickContinuous(dt);
            ComboChanged?.Invoke();
        }

        public void EndContinuous() => Tracker.EndContinuous();

        public void MarkElement(LineElement element) => Tracker.MarkElement(element);

        public int RepeatCount(string trickId) => Tracker.RepeatCount(trickId);

        /// <summary>Called on every clean/sketchy touchdown. Opens the manual window; the combo banks when it closes.</summary>
        public void OnLanded(LandingQuality quality)
        {
            if (!Tracker.IsActive) return;
            HasPendingBank = true;
            _pendingQuality = quality;
            _windowTimer = _profile.manualWindow;
        }

        /// <summary>Called when the skater pops again. A pending bank must resolve first so the new air starts a new combo.</summary>
        public void OnPop()
        {
            if (HasPendingBank) BankNow();
        }

        /// <summary>Extra multiplier on banked combos (Retro City block parties). 1 = none.</summary>
        public float BonusFactor { get; set; } = 1f;

        public void BankNow(LandingQuality? qualityOverride = null)
        {
            var quality = qualityOverride ?? (HasPendingBank ? _pendingQuality : LandingQuality.Clean);
            HasPendingBank = false;
            if (!Tracker.IsActive) return;

            float factor = (quality == LandingQuality.Sketchy ? _profile.scoring.sketchyBankFactor : 1f) * Mathf.Max(0f, BonusFactor);
            string label = BuildLabel();
            var result = Tracker.Bank(factor);
            _score.Bank(result, label);
            Special.Add(result.Points);
            Banked?.Invoke(result, label, quality);
            ComboChanged?.Invoke();
        }

        public void Bail(BailReason reason)
        {
            HasPendingBank = false;
            long lost = Tracker.Bail();
            _score.RecordBail(lost);
            Special.Reset();
            Bailed?.Invoke(lost, reason);
            ComboChanged?.Invoke();
        }

        /// <summary>Drops the current combo without counting a bail (stuck recovery, debug respawn, run end).</summary>
        public void Discard()
        {
            HasPendingBank = false;
            Tracker.Reset();
            Discarded?.Invoke();
            ComboChanged?.Invoke();
        }

        public bool TryConsumeSpecial() => Special.TryConsume();

        private void Update()
        {
            if (!HasPendingBank) return;
            _windowTimer -= Time.deltaTime;
            if (_windowTimer <= 0f) BankNow();
        }

        public string BuildLabel(int maxNames = 4)
        {
            var entries = Tracker.Entries;
            if (entries.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            int shown = Mathf.Min(maxNames, entries.Count);
            for (int i = entries.Count - shown; i < entries.Count; i++)
            {
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append(entries[i].DisplayName);
            }
            if (entries.Count > shown) sb.Insert(0, "… + ");
            return sb.ToString();
        }
    }
}
