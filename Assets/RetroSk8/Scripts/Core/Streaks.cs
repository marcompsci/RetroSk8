using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>Your daily skate streak (JsonUtility-friendly). Days are local calendar days counted from 2000-01-01.</summary>
    [Serializable]
    public sealed class StreakState
    {
        /// <summary>The last day the streak was checked in (0 = never).</summary>
        public int lastDay;
        public int current;
        public int best;
        /// <summary>Tape Savers: each covers one missed day so the streak survives. Earned every 7 days, up to 2.</summary>
        public int savers;

        public void Sanitize()
        {
            current = Math.Max(0, current);
            best = Math.Max(best, current);
            savers = Math.Max(0, Math.Min(Streaks.MaxSavers, savers));
        }
    }

    /// <summary>What happened when you opened the game today.</summary>
    public sealed class StreakResult
    {
        /// <summary>False when you already checked in today (nothing to show or pay).</summary>
        public bool NewDay;
        public int Day;
        public int Tokens;
        public bool Reset;
        public int SaversUsed;
        public bool EarnedSaver;
        public bool Milestone;
    }

    /// <summary>
    /// Daily streak rules (Phase 15). Opening the game on a new day checks in: the next day in a row adds one; missing
    /// days resets to day 1 unless Tape Savers cover every missed day. Rewards loop over a 7-day card (day 7 is big)
    /// with a bonus every 30 days. Engine-free and unit-tested; the clock is passed in.
    /// </summary>
    public static class Streaks
    {
        public const int MaxSavers = 2;
        public const int SaverEvery = 7;
        public const int MilestoneEvery = 30;
        public const int MilestoneBonus = 100;

        /// <summary>The 7-day reward card.</summary>
        public static readonly int[] Card = { 10, 10, 15, 15, 20, 25, 50 };

        private static readonly DateTime Epoch = new DateTime(2000, 1, 1);

        public static int DayNumber(DateTime localDate) => (int)(localDate.Date - Epoch).TotalDays;

        public static int RewardFor(int day)
        {
            if (day <= 0) return 0;
            int tokens = Card[(day - 1) % Card.Length];
            if (day % MilestoneEvery == 0) tokens += MilestoneBonus;
            return tokens;
        }

        public static StreakResult CheckIn(StreakState s, int today)
        {
            var r = new StreakResult();
            if (s == null) return r;
            if (s.lastDay == today || (s.lastDay > today && s.lastDay != 0))
            {
                // Same day (or the clock went backwards): nothing new. Never punish a clock change.
                r.Day = s.current;
                return r;
            }
            int gap = s.lastDay == 0 ? int.MaxValue : today - s.lastDay;
            int missed = gap == int.MaxValue ? int.MaxValue : gap - 1;
            if (s.lastDay != 0 && missed == 0) s.current++;
            else if (s.lastDay != 0 && missed <= s.savers && s.current > 0)
            {
                s.savers -= missed;
                r.SaversUsed = missed;
                s.current++;
            }
            else
            {
                r.Reset = s.lastDay != 0 && s.current > 0;
                s.current = 1;
            }
            s.lastDay = today;
            s.best = Math.Max(s.best, s.current);
            if (s.current % SaverEvery == 0 && s.savers < MaxSavers)
            {
                s.savers++;
                r.EarnedSaver = true;
            }
            r.NewDay = true;
            r.Day = s.current;
            r.Tokens = RewardFor(s.current);
            r.Milestone = s.current % MilestoneEvery == 0;
            return r;
        }

        /// <summary>Which slot (0-6) of the 7-day card a streak day lands on.</summary>
        public static int CardSlot(int day) => day <= 0 ? 0 : (day - 1) % Card.Length;
    }

    /// <summary>One local notification to schedule.</summary>
    public sealed class PlannedNotification
    {
        public string Id;
        public string Title;
        public string Body;
        public DateTime FireAt;
    }

    /// <summary>
    /// Plans the local reminders (Phase 15), all opt-in: keep your streak alive, the new Daily Line, and the weekly
    /// event closing with goals left. Nothing fires during quiet hours, and at most one per day. The game reschedules
    /// the whole plan every time it goes to the background, so reminders always reflect the latest save.
    /// </summary>
    public static class NotificationPlan
    {
        public const int QuietStart = 21; // 9 pm
        public const int QuietEnd = 9;    // 9 am
        public const int StreakHour = 18;
        public const int DailyHour = 10;

        public const string StreakId = "retrosk8.streak";
        public const string DailyId = "retrosk8.daily";
        public const string WeeklyId = "retrosk8.weekly";

        /// <param name="now">Local time now.</param>
        /// <param name="streak">Current streak (after today's check-in).</param>
        /// <param name="weeklyGoalsLeft">Weekly goals not yet done.</param>
        /// <param name="weeklyEventName">This week's event name.</param>
        public static List<PlannedNotification> Build(DateTime now, StreakState streak, int weeklyGoalsLeft, string weeklyEventName)
        {
            var list = new List<PlannedNotification>();
            var tomorrow = now.Date.AddDays(1);
            int today = Streaks.DayNumber(now);
            bool checkedInToday = streak != null && streak.lastDay == today;

            // 1. Streak: tomorrow evening, only when there's a streak worth keeping.
            if (streak != null && checkedInToday && streak.current >= 2)
            {
                int next = streak.current + 1;
                list.Add(new PlannedNotification
                {
                    Id = StreakId,
                    Title = $"DAY {next} IS WAITING",
                    Body = $"Keep your {streak.current}-day streak rolling. Tomorrow's check-in pays +{Streaks.RewardFor(next)} Tape Tokens.",
                    FireAt = tomorrow.AddHours(StreakHour),
                });
            }
            else
            {
                // 2. Otherwise a gentle "new Daily Line" nudge tomorrow morning.
                list.Add(new PlannedNotification
                {
                    Id = DailyId,
                    Title = "NEW DAILY LINE",
                    Body = "A fresh line just dropped. Two minutes, one shot at the bonus.",
                    FireAt = tomorrow.AddHours(DailyHour),
                });
            }

            // 3. Weekly event: the evening before it ends, if goals are left (and it's not already that evening).
            if (weeklyGoalsLeft > 0)
            {
                int dow = ((int)now.DayOfWeek + 6) % 7;            // Monday = 0
                var nextMonday = now.Date.AddDays(7 - dow);
                var fire = nextMonday.AddDays(-1).AddHours(StreakHour); // Sunday 6 pm
                if (fire > now)
                    list.Add(new PlannedNotification
                    {
                        Id = WeeklyId,
                        Title = "LAST DAY OF THE WEEK",
                        Body = $"{(string.IsNullOrEmpty(weeklyEventName) ? "This week's event" : weeklyEventName)} ends tonight. {weeklyGoalsLeft} goal{(weeklyGoalsLeft == 1 ? "" : "s")} left.",
                        FireAt = fire,
                    });
            }

            // Quiet hours, then at most one per calendar day (earliest wins; weekly beats the others on its day).
            foreach (var n in list) n.FireAt = OutOfQuietHours(n.FireAt);
            list.RemoveAll(n => n.FireAt <= now);
            list.Sort((a, b) => a.Id == WeeklyId && a.FireAt.Date == b.FireAt.Date ? -1
                              : b.Id == WeeklyId && a.FireAt.Date == b.FireAt.Date ? 1
                              : a.FireAt.CompareTo(b.FireAt));
            var days = new HashSet<DateTime>();
            list.RemoveAll(n => !days.Add(n.FireAt.Date));
            list.Sort((a, b) => a.FireAt.CompareTo(b.FireAt));
            return list;
        }

        public static bool InQuietHours(DateTime t) => t.Hour >= QuietStart || t.Hour < QuietEnd;

        public static DateTime OutOfQuietHours(DateTime t)
        {
            if (!InQuietHours(t)) return t;
            var morning = t.Date.AddHours(QuietEnd);
            return t.Hour < QuietEnd ? morning : morning.AddDays(1);
        }
    }
}
