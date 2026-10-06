using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>What kind of task today's Daily Trick is. Saved by number: append only.</summary>
    public enum DailyTrickKind
    {
        /// <summary>Land a named trick in a banked line, Count times (one per line).</summary>
        LandTrick = 0,
        /// <summary>Bank one line worth at least MinPoints.</summary>
        BigLine = 1,
        /// <summary>Bank a line with at least Count grinds in it.</summary>
        GrindChain = 2,
        /// <summary>Land a spin of at least HalfTurns half-turns in a banked line.</summary>
        BigSpin = 3,
        /// <summary>Bank Count lines that each include a bonk or pole jam.</summary>
        BonkLines = 4,
    }

    public sealed class DailyTrickChallenge
    {
        public int DateKey;
        public DailyTrickKind Kind;
        public string TrickId;
        public string TrickName;
        public int Count = 1;
        public long MinPoints;
        public int HalfTurns;
        public string Text;
    }

    /// <summary>Saved progress (JsonUtility-friendly): today's count and the days you finished.</summary>
    [Serializable]
    public sealed class DailyTrickState
    {
        public int date;
        public int count;
        public bool paid;
        /// <summary>Day numbers (<see cref="Streaks.DayNumber"/>) with the challenge finished, newest last, capped.</summary>
        public List<int> doneDays = new List<int>();

        public void Sanitize()
        {
            if (doneDays == null) doneDays = new List<int>();
            doneDays.Sort();
            while (doneDays.Count > DailyTricks.KeepDays) doneDays.RemoveAt(0);
            if (count < 0) count = 0;
        }
    }

    public sealed class DailyTrickUpdate
    {
        public bool Progressed;
        public bool Completed;
        public int Tokens;
        public int Streak;
    }

    /// <summary>
    /// Daily challenges 2.0 (Phase 24): one trick task a day, the same for everyone, picked from the date.
    /// - Tasks: land a named trick a few times, bank a big line, chain grinds, land a big spin, or bonk your way through
    ///   some lines.
    /// - Rewards: finishing pays Tape Tokens, plus a bonus that grows with the run of days in a row (capped).
    /// - A 14-day calendar shows which days you finished.
    /// Engine-free and unit-tested.
    /// </summary>
    public static class DailyTricks
    {
        public const int BaseTokens = 30;
        public const int StreakBonusPerDay = 5;
        public const int MaxStreakBonusDays = 6;
        public const int CalendarDays = 14;
        public const int KeepDays = 60;

        /// <summary>Today's challenge. <paramref name="tricks"/> are the nameable tricks to pick from (id, name).</summary>
        public static DailyTrickChallenge For(int dateKey, IList<(string id, string name)> tricks)
        {
            uint h = Hash((uint)dateKey);
            var kind = (DailyTrickKind)(h % 5);
            if (kind == DailyTrickKind.LandTrick && (tricks == null || tricks.Count == 0)) kind = DailyTrickKind.BigLine;
            var c = new DailyTrickChallenge { DateKey = dateKey, Kind = kind };
            uint p = Hash(h);
            switch (kind)
            {
                case DailyTrickKind.LandTrick:
                    var t = tricks[(int)(p % (uint)tricks.Count)];
                    c.TrickId = t.id;
                    c.TrickName = (t.name ?? t.id).ToUpperInvariant();
                    c.Count = 3;
                    c.Text = $"LAND A {c.TrickName} IN 3 BANKED LINES";
                    break;
                case DailyTrickKind.BigLine:
                    c.MinPoints = 8000 + (p % 7) * 2000; // 8,000 to 20,000
                    c.Text = $"BANK ONE LINE WORTH {c.MinPoints:N0}";
                    break;
                case DailyTrickKind.GrindChain:
                    c.Count = 2 + (int)(p % 2);
                    c.Text = $"BANK A LINE WITH {c.Count} GRINDS IN IT";
                    break;
                case DailyTrickKind.BigSpin:
                    c.HalfTurns = 3 + (int)(p % 2); // a 540 or a 720
                    c.Text = $"LAND A {c.HalfTurns * 180} IN A BANKED LINE";
                    break;
                default:
                    c.Count = 3;
                    c.Text = "BANK 3 LINES THAT EACH HAVE A BONK OR POLE JAM";
                    break;
            }
            return c;
        }

        /// <summary>How much one banked line moves the challenge on (0 or 1; BigLine and BigSpin finish at once).</summary>
        public static int Matches(DailyTrickChallenge c, IList<string> lineIds, long points)
        {
            if (c == null || lineIds == null || points <= 0) return 0;
            switch (c.Kind)
            {
                case DailyTrickKind.LandTrick:
                    foreach (var id in lineIds) if (id == c.TrickId) return 1;
                    return 0;
                case DailyTrickKind.BigLine:
                    return points >= c.MinPoints ? 1 : 0;
                case DailyTrickKind.GrindChain:
                    int grinds = 0;
                    foreach (var id in lineIds) if (WeeklyCounters.IsGrind(id)) grinds++;
                    return grinds >= c.Count ? 1 : 0;
                case DailyTrickKind.BigSpin:
                    foreach (var id in lineIds) if (SpinHalfTurns(id) >= c.HalfTurns) return 1;
                    return 0;
                case DailyTrickKind.BonkLines:
                    foreach (var id in lineIds) if (BonkRules.IsBonk(id)) return 1;
                    return 0;
            }
            return 0;
        }

        /// <summary>Lines (or single events) needed to finish.</summary>
        public static int Needed(DailyTrickChallenge c) =>
            c == null ? 1 : c.Kind == DailyTrickKind.LandTrick || c.Kind == DailyTrickKind.BonkLines ? c.Count : 1;

        /// <summary>"spin_540" → 3 half-turns; anything else → 0.</summary>
        public static int SpinHalfTurns(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("spin_", StringComparison.Ordinal)) return 0;
            return int.TryParse(id.Substring(5), out int deg) && deg > 0 ? deg / 180 : 0;
        }

        /// <summary>Records a banked line. <paramref name="day"/> is today's day number.</summary>
        public static DailyTrickUpdate Record(DailyTrickState s, DailyTrickChallenge c, int day, IList<string> lineIds, long points)
        {
            var u = new DailyTrickUpdate();
            if (s == null || c == null) return u;
            if (s.date != c.DateKey) { s.date = c.DateKey; s.count = 0; s.paid = false; }
            if (s.paid) return u;
            int add = Matches(c, lineIds, points);
            if (add <= 0) return u;
            s.count += add;
            u.Progressed = true;
            if (s.count < Needed(c)) return u;
            s.paid = true;
            if (!s.doneDays.Contains(day)) s.doneDays.Add(day);
            s.Sanitize();
            u.Completed = true;
            u.Streak = StreakEnding(s, day);
            u.Tokens = TokensFor(u.Streak);
            return u;
        }

        public static int TokensFor(int streak) => BaseTokens + StreakBonusPerDay * Math.Min(Math.Max(0, streak - 1), MaxStreakBonusDays);

        /// <summary>Days in a row finished, ending at <paramref name="day"/> (or yesterday, when today isn't done yet).</summary>
        public static int StreakEnding(DailyTrickState s, int day)
        {
            if (s == null || s.doneDays == null) return 0;
            var set = new HashSet<int>(s.doneDays);
            int d = set.Contains(day) ? day : day - 1;
            int n = 0;
            while (set.Contains(d)) { n++; d--; }
            return n;
        }

        /// <summary>The longest run of days in a row in the saved history (Daily Driver achievement).</summary>
        public static int LongestStreak(DailyTrickState s)
        {
            if (s == null || s.doneDays == null || s.doneDays.Count == 0) return 0;
            var days = new List<int>(new HashSet<int>(s.doneDays));
            days.Sort();
            int best = 1, run = 1;
            for (int i = 1; i < days.Count; i++)
            {
                run = days[i] == days[i - 1] + 1 ? run + 1 : 1;
                if (run > best) best = run;
            }
            return best;
        }

        /// <summary>The last <see cref="CalendarDays"/> days, oldest first: (day number, finished).</summary>
        public static List<(int day, bool done)> Calendar(DailyTrickState s, int today)
        {
            var set = new HashSet<int>(s != null && s.doneDays != null ? s.doneDays : new List<int>());
            var list = new List<(int, bool)>();
            for (int d = today - CalendarDays + 1; d <= today; d++) list.Add((d, set.Contains(d)));
            return list;
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
            return x;
        }
    }
}
