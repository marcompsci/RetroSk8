using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    public enum GoalType
    {
        TotalScore = 0,       // run score >= target
        ComboScore = 1,       // one banked combo >= target
        ComboTrickCount = 2,  // one banked combo with >= target tricks
        DistinctRails = 3,    // grind >= target different rails (counted when the combo banks)
        ClearGap = 4,         // clear the named gap (param = gap id)
        ManualSeconds = 5,    // one manual held >= target seconds
        SpinHalfTurns = 6,    // land a spin of >= target half turns
        DistinctTricks = 7,   // land >= target different tricks (counted when the combo banks)
        LineFlow = 8,         // bank a combo with Line Flow multiplier >= target
    }

    /// <summary>One objective in a Spot Contract or Daily Line. Plain data so designers edit it inside ScriptableObjects.</summary>
    [Serializable]
    public class GoalDefinition
    {
        public string id = "goal";
        public string description = "Goal";
        public GoalType type;
        public float target = 1f;
        /// <summary>Gap id for ClearGap goals.</summary>
        public string param = "";

        public GoalDefinition() { }

        public GoalDefinition(string id, string description, GoalType type, float target, string param = "")
        {
            this.id = id;
            this.description = description;
            this.type = type;
            this.target = target;
            this.param = param ?? "";
        }
    }

    /// <summary>Tracks progress on a set of goals for one run. Feed it gameplay facts; read completions back.</summary>
    public sealed class GoalTracker
    {
        public sealed class GoalState
        {
            public GoalDefinition Goal;
            public float Progress;   // best value reached so far, in the goal's own units
            public bool Completed;
            public float Fraction => Goal.target <= 0f ? 1f : Math.Min(1f, Progress / Goal.target);
        }

        private readonly List<GoalState> _states = new List<GoalState>();
        private readonly List<GoalState> _justCompleted = new List<GoalState>();
        private readonly HashSet<int> _rails = new HashSet<int>();
        private readonly HashSet<string> _tricks = new HashSet<string>();

        public GoalTracker(IEnumerable<GoalDefinition> goals)
        {
            if (goals == null) return;
            foreach (var g in goals)
                if (g != null) _states.Add(new GoalState { Goal = g });
        }

        public IReadOnlyList<GoalState> Goals => _states;
        public int Total => _states.Count;
        public bool AllComplete => _states.Count > 0 && CompletedCount == _states.Count;

        public int CompletedCount
        {
            get
            {
                int n = 0;
                foreach (var s in _states) if (s.Completed) n++;
                return n;
            }
        }

        /// <summary>Goals completed since the last call (for toasts and sounds).</summary>
        public List<GoalState> ConsumeCompleted()
        {
            var list = new List<GoalState>(_justCompleted);
            _justCompleted.Clear();
            return list;
        }

        public void OnScoreChanged(long total) => Report(GoalType.TotalScore, total);

        public void OnBanked(long points, int trickCount, float flowMultiplier)
        {
            Report(GoalType.ComboScore, points);
            Report(GoalType.ComboTrickCount, trickCount);
            Report(GoalType.LineFlow, flowMultiplier);
        }

        /// <summary>Call when a combo containing these rails banks (bailed grinds don't count).</summary>
        public void OnRailsBanked(IEnumerable<int> railIds)
        {
            foreach (var r in railIds) _rails.Add(r);
            Report(GoalType.DistinctRails, _rails.Count);
        }

        /// <summary>Call when a combo containing these tricks banks.</summary>
        public void OnTricksBanked(IEnumerable<string> trickIds)
        {
            foreach (var t in trickIds) _tricks.Add(t);
            Report(GoalType.DistinctTricks, _tricks.Count);
        }

        public void OnGapCleared(string gapId)
        {
            foreach (var s in _states)
            {
                if (s.Completed || s.Goal.type != GoalType.ClearGap || s.Goal.param != gapId) continue;
                s.Progress = s.Goal.target;
                Complete(s);
            }
        }

        public void OnManualFinished(float seconds) => Report(GoalType.ManualSeconds, seconds);

        public void OnSpinLanded(int halfTurns) => Report(GoalType.SpinHalfTurns, halfTurns);

        private void Report(GoalType type, float value)
        {
            foreach (var s in _states)
            {
                if (s.Completed || s.Goal.type != type) continue;
                if (value > s.Progress) s.Progress = value;
                if (s.Progress >= s.Goal.target) Complete(s);
            }
        }

        private void Complete(GoalState s)
        {
            if (s.Completed) return;
            s.Completed = true;
            _justCompleted.Add(s);
        }
    }

    public sealed class DailyLine
    {
        public int Date;            // yyyymmdd
        public string LocationId;
        public long TargetScore;
        public List<GoalDefinition> Goals = new List<GoalDefinition>();
    }

    /// <summary>
    /// Builds the same three goals for everyone on a given day and park, from original templates.
    /// Uses its own RNG so results match on every platform and .NET runtime.
    /// </summary>
    public static class DailyLineGenerator
    {
        public struct Gap
        {
            public string Id;
            public string Name;
            public Gap(string id, string name) { Id = id; Name = name; }
        }

        public static int DateKey(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;

        /// <summary>
        /// Which of <paramref name="count"/> parks hosts the Daily Line on <paramref name="dateKey"/> (yyyymmdd).
        /// Parks rotate one per calendar day, so consecutive days always differ when there is more than one park.
        /// </summary>
        public static int PickIndex(int dateKey, int count)
        {
            if (count <= 1) return 0;
            int y = dateKey / 10000, m = dateKey / 100 % 100, d = dateKey % 100;
            int days;
            try { days = (int)(new DateTime(y, m, d) - new DateTime(2000, 1, 1)).TotalDays; }
            catch (ArgumentOutOfRangeException) { days = (int)(Hash(dateKey, "") & 0x7fffffff); }
            int i = days % count;
            return i < 0 ? i + count : i;
        }

        public static DailyLine Generate(int dateKey, string locationId, IReadOnlyList<Gap> gaps)
        {
            var rng = new Rng(Hash(dateKey, locationId ?? ""));
            var line = new DailyLine { Date = dateKey, LocationId = locationId, TargetScore = 15000 + 5000 * rng.Next(0, 4) };

            var types = new List<GoalType>
            {
                GoalType.ComboScore, GoalType.DistinctRails, GoalType.ManualSeconds, GoalType.SpinHalfTurns,
                GoalType.DistinctTricks, GoalType.ComboTrickCount, GoalType.LineFlow,
            };
            if (gaps != null && gaps.Count > 0) types.Add(GoalType.ClearGap);

            for (int i = 0; i < 3 && types.Count > 0; i++)
            {
                int pick = rng.Next(0, types.Count);
                var type = types[pick];
                types.RemoveAt(pick);
                line.Goals.Add(MakeGoal(type, rng, gaps, $"daily_{dateKey}_{i}"));
            }
            return line;
        }

        private static GoalDefinition MakeGoal(GoalType type, Rng rng, IReadOnlyList<Gap> gaps, string id)
        {
            switch (type)
            {
                case GoalType.ComboScore:
                {
                    int t = new[] { 4000, 6000, 8000, 12000 }[rng.Next(0, 4)];
                    return new GoalDefinition(id, $"Bank a {t:N0}-point combo", type, t);
                }
                case GoalType.DistinctRails:
                {
                    int t = rng.Next(2, 5);
                    return new GoalDefinition(id, $"Grind {t} different rails", type, t);
                }
                case GoalType.ManualSeconds:
                {
                    int t = rng.Next(2, 5);
                    return new GoalDefinition(id, $"Hold a manual for {t} seconds", type, t);
                }
                case GoalType.SpinHalfTurns:
                {
                    int t = rng.Next(2, 4);
                    return new GoalDefinition(id, $"Land a {t * 180} spin", type, t);
                }
                case GoalType.DistinctTricks:
                {
                    int t = new[] { 5, 7, 9 }[rng.Next(0, 3)];
                    return new GoalDefinition(id, $"Land {t} different tricks", type, t);
                }
                case GoalType.ComboTrickCount:
                {
                    int t = rng.Next(4, 7);
                    return new GoalDefinition(id, $"String {t} tricks into one combo", type, t);
                }
                case GoalType.LineFlow:
                {
                    float t = rng.Next(0, 2) == 0 ? 1.2f : 1.3f;
                    return new GoalDefinition(id, $"Bank a combo with +{(int)Math.Round((t - 1f) * 100f)}% Line Flow", type, t);
                }
                default:
                {
                    var gap = gaps[rng.Next(0, gaps.Count)];
                    return new GoalDefinition(id, $"Clear the {gap.Name}", GoalType.ClearGap, 1f, gap.Id);
                }
            }
        }

        private static uint Hash(int date, string location)
        {
            uint h = 2166136261u;
            unchecked
            {
                h = (h ^ (uint)date) * 16777619u;
                foreach (char c in location) h = (h ^ c) * 16777619u;
            }
            return h == 0 ? 1u : h;
        }

        /// <summary>xorshift32: tiny, deterministic, identical everywhere.</summary>
        private sealed class Rng
        {
            private uint _s;
            public Rng(uint seed) { _s = seed; }

            /// <summary>Uniform integer in [min, max).</summary>
            public int Next(int min, int max)
            {
                _s ^= _s << 13;
                _s ^= _s >> 17;
                _s ^= _s << 5;
                return min + (int)(_s % (uint)Math.Max(1, max - min));
            }
        }
    }

    public struct TokenAward
    {
        public int FromScore;
        public int FromGoals;
        public int FromDailyBonus;
        public int Total => FromScore + FromGoals + FromDailyBonus;
    }

    /// <summary>Tape Token economy. No randomness, no purchases with money: tokens are only earned by skating.</summary>
    public static class TokenRewards
    {
        /// <param name="firstTimeGoals">Goals completed for the first time ever (repeat completions pay nothing).</param>
        /// <param name="allGoalsFirstTime">True the first time every goal of a contract is completed.</param>
        /// <param name="dailyBonusEarned">True when today's Daily Line is completed for the first time today.</param>
        public static TokenAward ForRun(ScoringConfig config, long score, bool scoreCounts, int firstTimeGoals,
            bool allGoalsFirstTime, bool dailyBonusEarned)
        {
            var a = new TokenAward();
            if (scoreCounts && config.pointsPerTapeToken > 0) a.FromScore = (int)(Math.Max(0, score) / config.pointsPerTapeToken);
            a.FromGoals = Math.Max(0, firstTimeGoals) * config.tokensPerGoal + (allGoalsFirstTime ? config.tokensAllGoalsBonus : 0);
            a.FromDailyBonus = dailyBonusEarned ? config.tokensDailyBonus : 0;
            return a;
        }
    }

    public enum PurchaseResult
    {
        Ok = 0,
        AlreadyOwned = 1,
        NotEnoughTokens = 2,
        InvalidPrice = 3,
        /// <summary>Comes in a cosmetic pack (App Store), not for Tape Tokens.</summary>
        PackOnly = 4,
        /// <summary>Phase 23: earned in the story, never sold.</summary>
        StoryReward = 5,
    }

    public static class ShopRules
    {
        public static PurchaseResult Check(int tokens, int price, bool owned)
        {
            if (owned) return PurchaseResult.AlreadyOwned;
            if (price < 0) return PurchaseResult.InvalidPrice;
            return tokens >= price ? PurchaseResult.Ok : PurchaseResult.NotEnoughTokens;
        }
    }
}
