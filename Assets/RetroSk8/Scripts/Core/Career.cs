using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    public enum CareerGoalKind
    {
        /// <summary>Best scored-run total at a park (Two-Minute Run, contract or daily).</summary>
        ParkScore = 0,
        /// <summary>Best banked combo at a park (from scored runs).</summary>
        ParkCombo = 1,
        /// <summary>A named gap cleared at least once.</summary>
        Gap = 2,
        ContractStars = 3,
        CitySpots = 4,
        CityTapes = 5,
        /// <summary>Spot challenges at or above a medal (count).</summary>
        CityChallengeMedals = 6,
        /// <summary>One race at or above a medal.</summary>
        RaceMedal = 7,
        /// <summary>A Create-a-Park park with at least N pieces.</summary>
        BuildPieces = 8,
        /// <summary>Best scored run in any of your own parks.</summary>
        CustomParkScore = 9,
        DailyClears = 10,
        /// <summary>Best scored run anywhere.</summary>
        AnyScore = 11,
    }

    public sealed class CareerGoal
    {
        public string Id;
        public CareerGoalKind Kind;
        /// <summary>Park, gap, race or spot id (when the kind needs one).</summary>
        public string Target;
        public long Amount;
        public Medal Medal;
        public string Text;
    }

    public enum CareerAction
    {
        /// <summary>Two-Minute Run at the chapter's park.</summary>
        TimedRun = 0,
        /// <summary>Explore Retro City.</summary>
        ExploreCity = 1,
        /// <summary>Open Create-a-Park.</summary>
        CreatePark = 2,
        /// <summary>Daily Line.</summary>
        Daily = 3,
    }

    public sealed class CareerChapter
    {
        public int Number;
        public string Id;
        public string Title;
        public string LocationId;
        public string Crew;
        public string Sponsor;
        public string Story;
        public CareerAction Action;
        public int RewardTokens;
        public string RewardTitle;
        public CareerGoal[] Goals;
    }

    /// <summary>What the career reads from your progress. The game implements it from the save; tests fake it.</summary>
    public interface ICareerFacts
    {
        long BestScore(string locationId);
        long BestCombo(string locationId);
        bool HasGap(string gapId);
        int ContractStars(string locationId);
        CityProgress City { get; }
        int MaxCustomPieces { get; }
        long BestCustomScore { get; }
        int DailyClears { get; }
        long BestScoreAnywhere { get; }
    }

    /// <summary>
    /// Career mode: eight original chapters that walk through every park, the city and Create-a-Park. Each
    /// chapter has a crew or sponsor (all fictional), a short story and three goals measured from normal
    /// progress, so any way you play counts. Finishing a chapter pays Tape Tokens, gives a title and opens the
    /// next one; some chapters also unlock board-maker stickers.
    /// </summary>
    public static class Career
    {
        public static readonly CareerChapter[] Chapters =
        {
            new CareerChapter
            {
                Number = 1, Id = "new_in_town", Title = "New in Town", LocationId = "harbor_plaza", Crew = "The Dock Rats", Sponsor = "",
                Story = "You just rolled into the harbor with one board and no crew. The Dock Rats skate the plaza every evening. Show them what you've got.",
                Action = CareerAction.TimedRun, RewardTokens = 60, RewardTitle = "DOCK RAT",
                Goals = new[]
                {
                    G("c1_score", CareerGoalKind.ParkScore, "harbor_plaza", 12000, "Score 12,000 in a Harbor Plaza run"),
                    G("c1_combo", CareerGoalKind.ParkCombo, "harbor_plaza", 2500, "Bank a 2,500 combo at Harbor Plaza"),
                    G("c1_gap", CareerGoalKind.Gap, "fountain_gap", 0, "Clear the Fountain Gap"),
                },
            },
            new CareerChapter
            {
                Number = 2, Id = "warehouse_rules", Title = "Warehouse Rules", LocationId = "neon_warehouse", Crew = "Night Shift", Sponsor = "Lowtide Bearings",
                Story = "Word got around. The Night Shift crew lets you into their warehouse after hours, and a small bearing company is watching the footage.",
                Action = CareerAction.TimedRun, RewardTokens = 80, RewardTitle = "NIGHT SHIFT",
                Goals = new[]
                {
                    G("c2_score", CareerGoalKind.ParkScore, "neon_warehouse", 16000, "Score 16,000 in a Neon Warehouse run"),
                    G("c2_gap", CareerGoalKind.Gap, "conveyor_gap", 0, "Clear the Conveyor Gap"),
                    G("c2_stars", CareerGoalKind.ContractStars, "neon_warehouse", 2, "Earn 2 Spot Contract stars at Neon Warehouse"),
                },
            },
            new CareerChapter
            {
                Number = 3, Id = "above_the_streets", Title = "Above the Streets", LocationId = "rooftop_run", Crew = "Skyline Club", Sponsor = "Lowtide Bearings",
                Story = "Lowtide sends you up to the rooftops for a photo shoot. The Skyline Club says nobody has cleared their gap on the first day.",
                Action = CareerAction.TimedRun, RewardTokens = 100, RewardTitle = "SKYLINER",
                Goals = new[]
                {
                    G("c3_score", CareerGoalKind.ParkScore, "rooftop_run", 18000, "Score 18,000 in a Rooftop Run run"),
                    G("c3_gap", CareerGoalKind.Gap, "rooftop_gap", 0, "Clear the Rooftop Gap"),
                    G("c3_combo", CareerGoalKind.ParkCombo, "rooftop_run", 5000, "Bank a 5,000 combo on the rooftops"),
                },
            },
            new CareerChapter
            {
                Number = 4, Id = "golden_hour", Title = "Golden Hour", LocationId = "sunset_bowls", Crew = "Bowl Hounds", Sponsor = "Sundown Wheel Co.",
                Story = "Transition time. The Bowl Hounds run the Sunset Bowls, and Sundown Wheel Co. wants a rider who can carve.",
                Action = CareerAction.TimedRun, RewardTokens = 120, RewardTitle = "BOWL HOUND",
                Goals = new[]
                {
                    G("c4_score", CareerGoalKind.ParkScore, "sunset_bowls", 20000, "Score 20,000 in a Sunset Bowls run"),
                    G("c4_gap", CareerGoalKind.Gap, "deep_end_air", 0, "Boost the Deep End Air"),
                    G("c4_combo", CareerGoalKind.ParkCombo, "sunset_bowls", 7000, "Bank a 7,000 combo in the bowls"),
                },
            },
            new CareerChapter
            {
                Number = 5, Id = "city_limits", Title = "City Limits", LocationId = "retro_city", Crew = "Gridline Crew", Sponsor = "Sundown Wheel Co.",
                Story = "The whole city is a skatepark if you look right. The Gridline Crew hid tapes all over town. Find the spots, find the tapes.",
                Action = CareerAction.ExploreCity, RewardTokens = 140, RewardTitle = "STREET SCOUT",
                Goals = new[]
                {
                    G("c5_spots", CareerGoalKind.CitySpots, null, 6, "Find 6 spots in Retro City"),
                    G("c5_tapes", CareerGoalKind.CityTapes, null, 8, "Collect 8 hidden tapes"),
                    M("c5_medals", CareerGoalKind.CityChallengeMedals, null, 2, Medal.Bronze, "Earn bronze or better in 2 spot challenges"),
                },
            },
            new CareerChapter
            {
                Number = 6, Id = "race_the_grid", Title = "Race the Grid", LocationId = "retro_city", Crew = "Gridline Crew", Sponsor = "Fastline Shoes",
                Story = "Fastline Shoes runs a street race series. Three routes, gates all over the city, one clock.",
                Action = CareerAction.ExploreCity, RewardTokens = 160, RewardTitle = "GRID RACER",
                Goals = new[]
                {
                    M("c6_dash", CareerGoalKind.RaceMedal, "downtown_dash", 1, Medal.Silver, "Silver or better in Downtown Dash"),
                    M("c6_canal", CareerGoalKind.RaceMedal, "canal_cut", 1, Medal.Bronze, "Bronze or better in Canal Cut"),
                    M("c6_ring", CareerGoalKind.RaceMedal, "ring_road", 1, Medal.Bronze, "Bronze or better in Ring Road"),
                },
            },
            new CareerChapter
            {
                Number = 7, Id = "build_it", Title = "Build It Yourself", LocationId = null, Crew = "DIY Society", Sponsor = "Fastline Shoes",
                Story = "Real skaters build their own spots. The DIY Society hands you a lot and a pile of ramps. Make it yours, then shred it.",
                Action = CareerAction.CreatePark, RewardTokens = 180, RewardTitle = "BUILDER",
                Goals = new[]
                {
                    G("c7_build", CareerGoalKind.BuildPieces, null, 15, "Build a park with 15 or more pieces"),
                    G("c7_score", CareerGoalKind.CustomParkScore, null, 10000, "Score 10,000 in a 2-minute run in your own park"),
                    G("c7_daily", CareerGoalKind.DailyClears, null, 1, "Clear a Daily Line"),
                },
            },
            new CareerChapter
            {
                Number = 8, Id = "retro_legend", Title = "Retro Legend", LocationId = "retro_city", Crew = "Every Crew", Sponsor = "All Sponsors",
                Story = "Every crew in town knows your name now. One last push to become a Retro City legend.",
                Action = CareerAction.ExploreCity, RewardTokens = 300, RewardTitle = "RETRO LEGEND",
                Goals = new[]
                {
                    M("c8_golds", CareerGoalKind.CityChallengeMedals, null, 3, Medal.Gold, "Gold in 3 spot challenges"),
                    G("c8_score", CareerGoalKind.AnyScore, null, 35000, "Score 35,000 in any run"),
                    G("c8_tapes", CareerGoalKind.CityTapes, null, 20, "Collect all 20 tapes"),
                },
            },
        };

        private static CareerGoal G(string id, CareerGoalKind kind, string target, long amount, string text) =>
            new CareerGoal { Id = id, Kind = kind, Target = target, Amount = amount, Text = text };

        private static CareerGoal M(string id, CareerGoalKind kind, string target, long amount, Medal medal, string text) =>
            new CareerGoal { Id = id, Kind = kind, Target = target, Amount = amount, Medal = medal, Text = text };

        public static CareerChapter Find(int number) => number >= 1 && number <= Chapters.Length ? Chapters[number - 1] : null;

        public static bool IsDone(CareerGoal goal, ICareerFacts f) => Progress(goal, f) >= 1f;

        /// <summary>0..1 progress toward a goal (for the checklist bars).</summary>
        public static float Progress(CareerGoal goal, ICareerFacts f)
        {
            switch (goal.Kind)
            {
                case CareerGoalKind.ParkScore: return Ratio(f.BestScore(goal.Target), goal.Amount);
                case CareerGoalKind.ParkCombo: return Ratio(f.BestCombo(goal.Target), goal.Amount);
                case CareerGoalKind.Gap: return f.HasGap(goal.Target) ? 1f : 0f;
                case CareerGoalKind.ContractStars: return Ratio(f.ContractStars(goal.Target), goal.Amount);
                case CareerGoalKind.CitySpots: return Ratio(f.City?.spots.Count ?? 0, goal.Amount);
                case CareerGoalKind.CityTapes: return Ratio(f.City?.tapes.Count ?? 0, goal.Amount);
                case CareerGoalKind.CityChallengeMedals:
                {
                    int n = 0;
                    if (f.City != null) foreach (var e in f.City.challenges) if (e.medal >= (int)goal.Medal) n++;
                    return Ratio(n, goal.Amount);
                }
                case CareerGoalKind.RaceMedal: return f.City != null && (int)f.City.RaceMedal(goal.Target) >= (int)goal.Medal ? 1f : 0f;
                case CareerGoalKind.BuildPieces: return Ratio(f.MaxCustomPieces, goal.Amount);
                case CareerGoalKind.CustomParkScore: return Ratio(f.BestCustomScore, goal.Amount);
                case CareerGoalKind.DailyClears: return Ratio(f.DailyClears, goal.Amount);
                case CareerGoalKind.AnyScore: return Ratio(f.BestScoreAnywhere, goal.Amount);
                default: return 0f;
            }
        }

        public static int GoalsDone(CareerChapter c, ICareerFacts f)
        {
            int n = 0;
            foreach (var g in c.Goals) if (IsDone(g, f)) n++;
            return n;
        }

        public static bool IsComplete(CareerChapter c, ICareerFacts f) => GoalsDone(c, f) == c.Goals.Length;

        /// <summary>Chapter 1 is always open; each later chapter opens when the one before it is complete.</summary>
        public static bool IsUnlocked(CareerChapter c, ICareerFacts f) => c.Number == 1 || IsComplete(Find(c.Number - 1), f);

        /// <summary>The first unlocked chapter that isn't complete (or the last chapter when all are done).</summary>
        public static CareerChapter Current(ICareerFacts f)
        {
            foreach (var c in Chapters) if (!IsComplete(c, f)) return c;
            return Chapters[Chapters.Length - 1];
        }

        private static float Ratio(long have, long need) => need <= 0 ? (have > 0 ? 1f : 0f) : Math.Min(1f, have / (float)need);
    }

    /// <summary>Saved career state: which chapter rewards were paid and which goals were announced.</summary>
    [Serializable]
    public sealed class CareerState
    {
        public List<string> paidChapters = new List<string>();
        public List<string> announcedGoals = new List<string>();
        public string title = "";

        /// <summary>Highest chapter whose reward has been paid (stickers unlock from this).</summary>
        public int ChaptersCompleted()
        {
            int n = 0;
            foreach (var c in Career.Chapters) if (paidChapters.Contains(c.Id)) n = Math.Max(n, c.Number);
            return n;
        }

        public bool StickerUnlocked(Sticker s) => s.UnlockChapter <= 0 || ChaptersCompleted() >= s.UnlockChapter;

        public sealed class Update
        {
            public readonly List<CareerGoal> NewGoals = new List<CareerGoal>();
            public readonly List<CareerChapter> NewChapters = new List<CareerChapter>();
            public int Tokens;
            public bool Any => NewGoals.Count > 0 || NewChapters.Count > 0;
        }

        /// <summary>
        /// Compares progress against what was already announced/paid. Pays each completed (and unlocked) chapter
        /// once, in order. Goals completed in chapters that aren't open yet are remembered but not announced.
        /// </summary>
        public Update Refresh(ICareerFacts f)
        {
            var u = new Update();
            foreach (var c in Career.Chapters)
            {
                if (!Career.IsUnlocked(c, f)) break;
                foreach (var g in c.Goals)
                {
                    if (announcedGoals.Contains(g.Id) || !Career.IsDone(g, f)) continue;
                    announcedGoals.Add(g.Id);
                    u.NewGoals.Add(g);
                }
                if (!paidChapters.Contains(c.Id) && Career.IsComplete(c, f))
                {
                    paidChapters.Add(c.Id);
                    u.NewChapters.Add(c);
                    u.Tokens += c.RewardTokens;
                    title = c.RewardTitle;
                }
            }
            return u;
        }
    }
}
