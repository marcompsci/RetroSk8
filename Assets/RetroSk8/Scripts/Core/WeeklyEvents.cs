using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>How a weekly event changes play.</summary>
    public enum WeeklyModifier
    {
        None = 0,
        /// <summary>Retro City stays at night all week.</summary>
        CityNight = 1,
        /// <summary>Retro City rains all week.</summary>
        CityRain = 2,
        /// <summary>City race medals pay double tokens.</summary>
        RaceTokens = 3,
        /// <summary>Crew XP is doubled.</summary>
        CrewXp = 4,
        /// <summary>Gaps score double.</summary>
        GapPoints = 5,
    }

    /// <summary>Things the game counts during the week (saved by name, so keep them stable).</summary>
    public static class WeeklyCounters
    {
        public const string Combos = "combos";
        public const string BestScore = "best_score";
        public const string Tapes = "tapes";
        public const string SkateWins = "skate_wins";
        public const string CityMedals = "city_medals";
        public const string Races = "races";
        public const string RaceGold = "race_gold";
        public const string CustomRuns = "custom_runs";
        public const string CodesShared = "codes_shared";
        public const string Gaps = "gaps";
        public const string Daily = "daily";
        public const string CrewXp = "crew_xp";

        /// <summary>Counters that keep the best value instead of adding up.</summary>
        public static bool IsMax(string counter) => counter == BestScore;
    }

    public sealed class WeeklyGoal
    {
        public string Counter;
        public long Target;
        public string Text;
    }

    public sealed class WeeklyEvent
    {
        public string Id;
        public string Name;
        public string Description;
        public WeeklyModifier Modifier;
        public WeeklyGoal[] Goals;
    }

    /// <summary>
    /// A new event every week (Monday to Sunday, ISO weeks), the same for everyone because it is picked from the
    /// week number. Each event has a twist and three goals; goals pay <see cref="GoalTokens"/>, all three pay a bonus.
    /// The weekly leaderboard is your best single-run score that week.
    /// </summary>
    public static class WeeklyEvents
    {
        public const int GoalTokens = 40;
        public const int AllGoalsBonus = 60;
        public const string LeaderboardId = "retrosk8.weekly";

        public static readonly WeeklyEvent[] Rotation =
        {
            new WeeklyEvent
            {
                Id = "neon_nights", Name = "NEON NIGHTS", Modifier = WeeklyModifier.CityNight,
                Description = "Retro City stays dark all week and the neon is at full blast.",
                Goals = new[]
                {
                    G(WeeklyCounters.BestScore, 25000, "Score 25,000 in one run"),
                    G(WeeklyCounters.Tapes, 4, "Collect 4 city tapes"),
                    G(WeeklyCounters.Combos, 30, "Bank 30 combos"),
                },
            },
            new WeeklyEvent
            {
                Id = "rain_season", Name = "RAIN SEASON", Modifier = WeeklyModifier.CityRain,
                Description = "It's pouring in Retro City. Skate it anyway.",
                Goals = new[]
                {
                    G(WeeklyCounters.SkateWins, 2, "Win 2 games of S.K.A.T.E."),
                    G(WeeklyCounters.CityMedals, 2, "Earn 2 city medals (challenges or races)"),
                    G(WeeklyCounters.Combos, 40, "Bank 40 combos"),
                },
            },
            new WeeklyEvent
            {
                Id = "race_week", Name = "RACE WEEK", Modifier = WeeklyModifier.RaceTokens,
                Description = "City race medals pay double Tape Tokens.",
                Goals = new[]
                {
                    G(WeeklyCounters.Races, 5, "Finish 5 city races"),
                    G(WeeklyCounters.RaceGold, 1, "Win a gold race medal"),
                    G(WeeklyCounters.Combos, 30, "Bank 30 combos"),
                },
            },
            new WeeklyEvent
            {
                Id = "builders_week", Name = "BUILDERS' WEEK", Modifier = WeeklyModifier.None,
                Description = "Build it, skate it, share it.",
                Goals = new[]
                {
                    G(WeeklyCounters.CustomRuns, 3, "Finish 3 two-minute runs in your own parks"),
                    G(WeeklyCounters.CodesShared, 1, "Share a park or challenge code"),
                    G(WeeklyCounters.BestScore, 20000, "Score 20,000 in one run"),
                },
            },
            new WeeklyEvent
            {
                Id = "gap_hunt", Name = "GAP HUNT", Modifier = WeeklyModifier.GapPoints,
                Description = "Every named gap scores double.",
                Goals = new[]
                {
                    G(WeeklyCounters.Gaps, 10, "Clear 10 gaps"),
                    G(WeeklyCounters.BestScore, 30000, "Score 30,000 in one run"),
                    G(WeeklyCounters.Daily, 1, "Clear a Daily Line"),
                },
            },
            new WeeklyEvent
            {
                Id = "crew_week", Name = "CREW WEEK", Modifier = WeeklyModifier.CrewXp,
                Description = "Crew XP is doubled. Grow the crew.",
                Goals = new[]
                {
                    G(WeeklyCounters.CrewXp, 1500, "Earn 1,500 crew XP"),
                    G(WeeklyCounters.SkateWins, 1, "Win a game of S.K.A.T.E."),
                    G(WeeklyCounters.Combos, 40, "Bank 40 combos"),
                },
            },
        };

        private static WeeklyGoal G(string counter, long target, string text) => new WeeklyGoal { Counter = counter, Target = target, Text = text };

        /// <summary>ISO year*100 + ISO week, e.g. 202640.</summary>
        public static int WeekKey(DateTime date)
        {
            // ISO 8601: weeks start Monday; week 1 holds the year's first Thursday. (Done by hand so it works on
            // every .NET profile Unity uses.)
            date = date.Date;
            int dow = ((int)date.DayOfWeek + 6) % 7;     // Monday = 0
            var thursday = date.AddDays(3 - dow);        // the Thursday of this week decides the ISO year
            int year = thursday.Year;
            int week = (thursday.DayOfYear - 1) / 7 + 1;
            return year * 100 + week;
        }

        public static WeeklyEvent For(int weekKey)
        {
            int weeks = (weekKey / 100) * 53 + weekKey % 100;
            return Rotation[((weeks % Rotation.Length) + Rotation.Length) % Rotation.Length];
        }

        /// <summary>Days (rounded up) until the next Monday.</summary>
        public static int DaysLeft(DateTime date)
        {
            int dow = ((int)date.DayOfWeek + 6) % 7; // Monday = 0
            return 7 - dow;
        }
    }

    [Serializable]
    public sealed class WeeklyCounter
    {
        public string name;
        public long value;
    }

    /// <summary>This week's counters and paid goals. Resets itself when the week changes. JsonUtility-friendly.</summary>
    [Serializable]
    public sealed class WeeklyState
    {
        public int week;
        public List<WeeklyCounter> counters = new List<WeeklyCounter>();
        public List<int> paidGoals = new List<int>();
        public bool bonusPaid;

        public long Get(string counter)
        {
            foreach (var c in counters) if (c.name == counter) return c.value;
            return 0;
        }

        /// <summary>Starts a fresh week when <paramref name="weekKey"/> differs from the saved one.</summary>
        public bool EnsureWeek(int weekKey)
        {
            if (week == weekKey) return false;
            week = weekKey;
            counters.Clear();
            paidGoals.Clear();
            bonusPaid = false;
            return true;
        }

        public sealed class Update
        {
            public readonly List<WeeklyGoal> NewGoals = new List<WeeklyGoal>();
            public bool AllDone;
            public int Tokens;
            public bool Any => NewGoals.Count > 0 || AllDone;
        }

        /// <summary>Adds to a counter (or raises a best value) and pays any goals that are now complete.</summary>
        public Update Count(WeeklyEvent ev, string counter, long amount)
        {
            WeeklyCounter c = null;
            foreach (var x in counters) if (x.name == counter) c = x;
            if (c == null) { c = new WeeklyCounter { name = counter }; counters.Add(c); }
            if (WeeklyCounters.IsMax(counter)) c.value = Math.Max(c.value, amount);
            else c.value += Math.Max(0, amount);

            var u = new Update();
            for (int i = 0; i < ev.Goals.Length; i++)
            {
                if (paidGoals.Contains(i) || Get(ev.Goals[i].Counter) < ev.Goals[i].Target) continue;
                paidGoals.Add(i);
                u.NewGoals.Add(ev.Goals[i]);
                u.Tokens += WeeklyEvents.GoalTokens;
            }
            if (!bonusPaid && paidGoals.Count >= ev.Goals.Length)
            {
                bonusPaid = true;
                u.AllDone = true;
                u.Tokens += WeeklyEvents.AllGoalsBonus;
            }
            return u;
        }

        public float Progress(WeeklyGoal g) => g.Target <= 0 ? 1f : Math.Min(1f, Get(g.Counter) / (float)g.Target);

        public void Sanitize()
        {
            if (counters == null) counters = new List<WeeklyCounter>();
            if (paidGoals == null) paidGoals = new List<int>();
            counters.RemoveAll(c => c == null || string.IsNullOrEmpty(c.name));
        }
    }

    /// <summary>Game Center leaderboard ids (create these in App Store Connect; see README).</summary>
    public static class Leaderboards
    {
        public const string SkateWins = "retrosk8.skate.wins";
        public static string Race(string raceId) => "retrosk8.race." + raceId;
        public static string Challenge(string spotId) => "retrosk8.challenge." + spotId;

        /// <summary>Race times are submitted in hundredths of a second (set the board to "low to high").</summary>
        public static long RaceScore(float seconds) => (long)Math.Round(seconds * 100.0);
    }
}
