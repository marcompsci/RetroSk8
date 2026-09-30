using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    public struct ComboEntry
    {
        public string TrickId;
        public string DisplayName;
        public TrickCategory Category;
        public float RepeatFactor;
        public float Points;       // unmultiplied points earned by this entry (grows for continuous entries)
        public bool IsContinuous;
    }

    public struct ComboResult
    {
        public long Points;
        public int TrickCount;
        public float Multiplier;
        public float FlowMultiplier;

        public static readonly ComboResult Empty = new ComboResult { Points = 0, TrickCount = 0, Multiplier = 0f, FlowMultiplier = 1f };
    }

    /// <summary>
    /// Tracks one unbanked combo: base points, multiplier, repeat decay and the Line Flow bonus.
    /// Pure logic; the ComboManager MonoBehaviour forwards gameplay events here.
    /// </summary>
    public sealed class ComboTracker
    {
        private readonly ScoringConfig _config;
        private readonly List<ComboEntry> _entries = new List<ComboEntry>();
        private readonly Dictionary<string, int> _repeatCounts = new Dictionary<string, int>();
        private readonly HashSet<TrickCategory> _categories = new HashSet<TrickCategory>();

        private float _basePoints;
        private float _multiplier;
        private int _links;
        private LineElement _lastElement = LineElement.None;
        private int _activeContinuousIndex = -1;

        public ComboTracker(ScoringConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public IReadOnlyList<ComboEntry> Entries => _entries;
        public bool IsActive => _entries.Count > 0;
        public bool HasActiveContinuous => _activeContinuousIndex >= 0;
        public float BasePoints => _basePoints;
        public float Multiplier => _entries.Count == 0 ? 0f : _multiplier;
        public int Links => _links;
        public int DistinctCategories => _categories.Count;

        public float FlowMultiplier
        {
            get
            {
                if (_entries.Count == 0) return 1f;
                float bonus = _config.flowPerExtraCategory * Math.Max(0, _categories.Count - 1)
                            + _config.flowPerLink * _links;
                return 1f + Math.Min(_config.maxFlowBonus, bonus);
            }
        }

        public long CurrentValue => (long)Math.Floor(_basePoints * Multiplier * FlowMultiplier);

        /// <summary>Adds a discrete trick. Returns the base points it contributed after repeat decay.</summary>
        public float AddTrick(string trickId, string displayName, TrickCategory category, int baseValue)
        {
            EndContinuous();
            float factor = ConsumeRepeatFactor(trickId);
            float points = baseValue * factor;
            PushEntry(new ComboEntry
            {
                TrickId = trickId,
                DisplayName = displayName,
                Category = category,
                RepeatFactor = factor,
                Points = points,
            });
            _basePoints += points;
            return points;
        }

        /// <summary>Starts a grind or manual. Points then accrue through <see cref="TickContinuous"/>.</summary>
        public void StartContinuous(string trickId, string displayName, TrickCategory category, int startValue)
        {
            EndContinuous();
            float factor = ConsumeRepeatFactor(trickId);
            float points = startValue * factor;
            PushEntry(new ComboEntry
            {
                TrickId = trickId,
                DisplayName = displayName,
                Category = category,
                RepeatFactor = factor,
                Points = points,
                IsContinuous = true,
            });
            _basePoints += points;
            _activeContinuousIndex = _entries.Count - 1;
        }

        public void TickContinuous(float seconds)
        {
            if (_activeContinuousIndex < 0 || seconds <= 0f) return;
            var entry = _entries[_activeContinuousIndex];
            float gained = _config.RatePerSecond(entry.Category) * entry.RepeatFactor * seconds;
            entry.Points += gained;
            _entries[_activeContinuousIndex] = entry;
            _basePoints += gained;
        }

        public void EndContinuous() => _activeContinuousIndex = -1;

        /// <summary>
        /// Registers a line element that isn't a trick on its own (e.g. launching off a ramp),
        /// so linking ramp → rail → manual is rewarded even between tricks.
        /// </summary>
        public void MarkElement(LineElement element)
        {
            if (_entries.Count == 0) { _lastElement = element; return; }
            RegisterElement(element);
        }

        public ComboResult Bank(float factor = 1f)
        {
            if (_entries.Count == 0) { Reset(); return ComboResult.Empty; }
            var result = new ComboResult
            {
                Points = (long)Math.Floor(CurrentValue * Clamp01(factor)),
                TrickCount = _entries.Count,
                Multiplier = Multiplier,
                FlowMultiplier = FlowMultiplier,
            };
            Reset();
            return result;
        }

        /// <summary>Discards the combo. Returns what was lost so the UI can show it.</summary>
        public long Bail()
        {
            long lost = CurrentValue;
            Reset();
            return lost;
        }

        public void Reset()
        {
            _entries.Clear();
            _repeatCounts.Clear();
            _categories.Clear();
            _basePoints = 0f;
            _multiplier = 0f;
            _links = 0;
            _lastElement = LineElement.None;
            _activeContinuousIndex = -1;
        }

        /// <summary>How many times this trick id has been used in the current combo.</summary>
        public int RepeatCount(string trickId)
        {
            _repeatCounts.TryGetValue(trickId, out int count);
            return count;
        }

        public float PeekRepeatFactor(string trickId)
        {
            _repeatCounts.TryGetValue(trickId, out int count);
            return RepeatFactorFor(count);
        }

        private float ConsumeRepeatFactor(string trickId)
        {
            _repeatCounts.TryGetValue(trickId, out int count);
            _repeatCounts[trickId] = count + 1;
            return RepeatFactorFor(count);
        }

        private float RepeatFactorFor(int priorCount)
        {
            float f = (float)Math.Pow(_config.repeatDecay, priorCount);
            return Math.Max(_config.minRepeatFactor, f);
        }

        private void PushEntry(ComboEntry entry)
        {
            _entries.Add(entry);
            _categories.Add(entry.Category);
            _multiplier = Math.Min(_config.maxMultiplier, _multiplier + _config.multiplierPerTrick);
            RegisterElement(entry.Category.ToLineElement());
        }

        private void RegisterElement(LineElement element)
        {
            if (element == LineElement.None) return;
            if (_lastElement != LineElement.None && element != _lastElement) _links++;
            _lastElement = element;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
