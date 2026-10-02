using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Save;

namespace RetroSk8.Game
{
    /// <summary>Reads career facts from the save file and pays chapter rewards (see <see cref="Career"/>).</summary>
    public sealed class SaveCareerFacts : ICareerFacts
    {
        private readonly SaveData _d;
        public SaveCareerFacts(SaveData data) { _d = data; }

        private LocationRecord Rec(string id)
        {
            foreach (var r in _d.locations) if (r.locationId == id) return r;
            return null;
        }

        public long BestScore(string id) => Rec(id)?.bestScore ?? 0;
        public long BestCombo(string id) => Rec(id)?.bestCombo ?? 0;
        public bool HasGap(string id) => _d.stats.gapIds.Contains(id);

        public int ContractStars(string id)
        {
            foreach (var c in _d.contracts) if (c.locationId == id) return c.completedGoalIds.Count;
            return 0;
        }

        public CityProgress City => _d.city;

        public int MaxCustomPieces
        {
            get
            {
                int n = 0;
                foreach (var p in _d.customParks) if (p.pieces.Count > n) n = p.pieces.Count;
                return n;
            }
        }

        public long BestCustomScore
        {
            get
            {
                long best = 0;
                foreach (var r in _d.locations) if (CustomParkIds.IsCustom(r.locationId) && r.bestScore > best) best = r.bestScore;
                return best;
            }
        }

        public int DailyClears => _d.daily.clears;

        public long BestScoreAnywhere
        {
            get
            {
                long best = 0;
                foreach (var r in _d.locations) if (r.bestScore > best) best = r.bestScore;
                return best;
            }
        }
    }

    public static class CareerService
    {
        public static ICareerFacts Facts => new SaveCareerFacts(SaveManager.Data);
        public static CareerState State => SaveManager.Data.career;

        /// <summary>Messages from the last check that a screen hasn't shown yet (results, menu).</summary>
        public static readonly List<string> Pending = new List<string>();

        /// <summary>Announces newly finished goals and pays newly finished chapters. Safe to call often.</summary>
        public static CareerState.Update Check()
        {
            var u = State.Refresh(Facts);
            if (!u.Any) return u;
            foreach (var g in u.NewGoals) Pending.Add("CAREER GOAL: " + g.Text.ToUpperInvariant());
            foreach (var c in u.NewChapters) Pending.Add($"CHAPTER {c.Number} COMPLETE: {c.Title.ToUpperInvariant()}  +{c.RewardTokens}  ·  YOU'RE A {c.RewardTitle}");
            if (u.Tokens > 0) SaveManager.AddTokens(u.Tokens); else SaveManager.Save();
            return u;
        }

        public static List<string> TakePending()
        {
            var copy = new List<string>(Pending);
            Pending.Clear();
            return copy;
        }
    }
}
