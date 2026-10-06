using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Save;

namespace RetroSk8.Game
{
    /// <summary>
    /// Daily challenges 2.0 (Phase 24): today's Daily Trick from <see cref="DailyTricks"/>, fed by every banked line
    /// (MetaHook), paid in Tape Tokens with a streak bonus.
    /// </summary>
    public static class DailyTrickService
    {
        private static DailyTrickChallenge s_today;

        public static DailyTrickState State
        {
            get
            {
                var d = SaveManager.Data;
                if (d.dailyTricks == null) d.dailyTricks = new DailyTrickState();
                return d.dailyTricks;
            }
        }

        public static int TodayNumber => Streaks.DayNumber(System.DateTime.Now);

        /// <summary>Today's challenge (built from the Trick Book's flips, grabs, grinds, lips and the like).</summary>
        public static DailyTrickChallenge Today
        {
            get
            {
                int key = GameSession.TodayKey;
                if (s_today != null && s_today.DateKey == key) return s_today;
                var tricks = new List<(string, string)>();
                var catalog = TrickBookService.Catalog;
                if (catalog != null)
                    foreach (var t in catalog)
                    {
                        if (t == null || t.IsSpecial || string.IsNullOrEmpty(t.Id)) continue;
                        if (t.Category == TrickCategory.Spin || t.Category == TrickCategory.Gap || t.Category == TrickCategory.Special
                            || t.Category == TrickCategory.Bonk || t.Category == TrickCategory.Revert) continue;
                        tricks.Add((t.Id, t.Name));
                    }
                tricks.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1)); // same pick for everyone, whatever the load order
                s_today = DailyTricks.For(key, tricks);
                return s_today;
            }
        }

        public static int Progress => State.date == Today.DateKey ? State.count : 0;
        public static bool DoneToday => State.date == Today.DateKey && State.paid;

        /// <summary>Called for every banked line.</summary>
        public static void Record(IReadOnlyList<string> ids, long points)
        {
            if (ids == null || points <= 0) return;
            var list = new List<string>(ids);
            var u = DailyTricks.Record(State, Today, TodayNumber, list, points);
            if (!u.Completed) return;
            SaveManager.AddTokens(u.Tokens);
            CareerService.Pending.Add(u.Streak > 1
                ? $"DAILY TRICK DONE, {u.Streak} DAYS IN A ROW!  +{u.Tokens}"
                : $"DAILY TRICK DONE!  +{u.Tokens}");
        }
    }
}
