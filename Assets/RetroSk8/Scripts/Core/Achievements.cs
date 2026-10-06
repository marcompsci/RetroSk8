using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>Lifetime numbers the achievements are judged on. Filled from the save file after each run.</summary>
    public struct PlayerProgress
    {
        public long BestCombo;
        public long BestRunScore;
        public int CombosBanked;
        public int MaxHalfTurns;
        public int DistinctGaps;
        public int ContractsComplete;
        public int ContractsTotal;
        public int DailyClears;
        public int ParksPlayed;
        public int ParksTotal;
        public bool TutorialDone;
        /// <summary>Phase 20: the story's Double Feature chapter is finished.</summary>
        public bool DoubleFeature;
        /// <summary>Phase 22: the Off-Season story chapter is finished.</summary>
        public bool OffSeason;
        /// <summary>Phase 25.</summary>
        public bool DryDock;
        public int DailyTrickBestStreak;
        public int TrickBattlesFinished;
        public int TotalBonks;
        public bool ClearedContainerCanyon;
    }

    public sealed class AchievementDefinition
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Description;
        private readonly Func<PlayerProgress, float> _progress;

        public AchievementDefinition(string id, string title, string description, Func<PlayerProgress, float> progress)
        {
            Id = id;
            Title = title;
            Description = description;
            _progress = progress;
        }

        /// <summary>0..1 toward unlocking (Game Center shows partial progress too).</summary>
        public float Progress(PlayerProgress p)
        {
            float v = _progress(p);
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }

    /// <summary>
    /// The original achievement set and the Game Center ids that go with it.
    /// Ids are stable: Game Center and the save file both key on them, so never rename one.
    /// </summary>
    public static class Achievements
    {
        /// <summary>Prefix shared by every Game Center leaderboard and achievement id. Must match App Store Connect.</summary>
        public const string GameCenterPrefix = "retrosk8.";

        private static float Ratio(long value, long target) => target <= 0 ? 0f : (float)value / target;

        public static readonly IReadOnlyList<AchievementDefinition> All = new List<AchievementDefinition>
        {
            new AchievementDefinition("class_dismissed", "Class Dismissed", "Finish the How to Skate lesson.", p => p.TutorialDone ? 1f : 0f),
            new AchievementDefinition("first_bank", "Money in the Bank", "Bank your first combo.", p => Ratio(p.CombosBanked, 1)),
            new AchievementDefinition("line_10k", "Ten Grand Line", "Bank a 10,000-point combo.", p => Ratio(p.BestCombo, 10000)),
            new AchievementDefinition("line_50k", "Fifty Grand Line", "Bank a 50,000-point combo.", p => Ratio(p.BestCombo, 50000)),
            new AchievementDefinition("run_100k", "Six Figures", "Score 100,000 in one run.", p => Ratio(p.BestRunScore, 100000)),
            new AchievementDefinition("spin_540", "Washing Machine", "Land a 540.", p => Ratio(p.MaxHalfTurns, 3)),
            new AchievementDefinition("gap_hunter", "Gap Hunter", "Clear five different named gaps.", p => Ratio(p.DistinctGaps, 5)),
            new AchievementDefinition("contractor", "On Contract", "Finish every goal of a Spot Contract.", p => Ratio(p.ContractsComplete, 1)),
            new AchievementDefinition("all_contracts", "Closed Book", "Finish every Spot Contract.", p => p.ContractsTotal <= 0 ? 0f : Ratio(p.ContractsComplete, p.ContractsTotal)),
            new AchievementDefinition("daily_regular", "Regular", "Clear the Daily Line on three days.", p => Ratio(p.DailyClears, 3)),
            new AchievementDefinition("tourist", "Tourist", "Skate every park.", p => p.ParksTotal <= 0 ? 0f : Ratio(p.ParksPlayed, p.ParksTotal)),
            new AchievementDefinition("double_feature", "Double Feature", "Finish the Double Feature story chapter.", p => p.DoubleFeature ? 1f : 0f),
            new AchievementDefinition("rink_rats", "Rink Rats", "Finish the Off-Season story chapter.", p => p.OffSeason ? 1f : 0f),
            // Phase 25
            new AchievementDefinition("dry_dock", "Dry Dock", "Finish the Dry Dock story chapter.", p => p.DryDock ? 1f : 0f),
            new AchievementDefinition("daily_driver", "Daily Driver", "Finish the Daily Trick seven days in a row.", p => Ratio(p.DailyTrickBestStreak, 7)),
            new AchievementDefinition("called_it", "Called It", "Finish a game of Trick Battle.", p => Ratio(p.TrickBattlesFinished, 1)),
            new AchievementDefinition("bonk_collector", "Bonk Collector", "Bank 100 bonks or pole jams.", p => Ratio(p.TotalBonks, 100)),
            new AchievementDefinition("mind_the_gap", "Mind the Gap", "Clear Container Canyon at the Shipyard.", p => p.ClearedContainerCanyon ? 1f : 0f),
        };

        public static AchievementDefinition Find(string id)
        {
            foreach (var a in All) if (a.Id == id) return a;
            return null;
        }

        /// <summary>Achievements complete for <paramref name="progress"/> that aren't in <paramref name="alreadyUnlocked"/>.</summary>
        public static List<AchievementDefinition> NewlyUnlocked(PlayerProgress progress, ICollection<string> alreadyUnlocked)
        {
            var list = new List<AchievementDefinition>();
            foreach (var a in All)
                if (a.Progress(progress) >= 1f && (alreadyUnlocked == null || !alreadyUnlocked.Contains(a.Id)))
                    list.Add(a);
            return list;
        }

        public static string GameCenterId(AchievementDefinition a) => GameCenterPrefix + "ach." + a.Id;

        /// <summary>One "best score" leaderboard per park.</summary>
        public static string LeaderboardId(string locationId) => GameCenterPrefix + "score." + locationId;
    }
}
