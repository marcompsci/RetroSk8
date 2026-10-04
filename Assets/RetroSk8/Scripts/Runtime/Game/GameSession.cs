using System.Collections.Generic;

namespace RetroSk8.Game
{
    public enum RunMode
    {
        TwoMinuteRun = 0,
        FreeSkate = 1,
        SpotContract = 2,
        DailyLine = 3,
        /// <summary>First-run lesson: no timer, no score tokens.</summary>
        Tutorial = 4,
        /// <summary>Local pass-and-play for 2-4 players on one phone (PartyController runs the turns).</summary>
        Party = 5,
        /// <summary>Game of S.K.A.T.E. against another phone (Game Center) or the CPU (DuelController runs it).</summary>
        Duel = 6,
        /// <summary>Watching a saved replay (ReplayTheater runs the scene).</summary>
        Replay = 7,
    }

    public static class SceneNames
    {
        public const string Boot = "BootScene";
        public const string MainMenu = "MainMenuScene";
        public const string HarborPlaza = "SkateScene_HarborPlaza";
        public const string NeonWarehouse = "SkateScene_NeonWarehouse";
        public const string RooftopRun = "SkateScene_RooftopRun";
        public const string RetroCity = "SkateScene_RetroCity";
        public const string SunsetBowls = "SkateScene_SunsetBowls";
        public const string FloodgateDitch = "SkateScene_FloodgateDitch";
        public const string MoonlightPier = "SkateScene_MoonlightPier";
        public const string Results = "ResultsScene";
        public const string Customization = "CustomizationScene";
    }

    public sealed class RunResult
    {
        public string modeLabel;
        public List<string> goalDescriptions = new List<string>();
        /// <summary>Titles of achievements unlocked by this run (shown on Results).</summary>
        public List<string> newAchievements = new List<string>();
        public List<bool> goalCompleted = new List<bool>();
        public int tokensFromScore;
        public int tokensFromGoals;
        public int tokensFromDaily;
        public long dailyTargetScore;
        public string locationId;
        public string locationName;
        public RunMode mode;
        public long score;
        public long bestCombo;
        public string bestComboLabel;
        public int combosBanked;
        public int bails;
        public int tapeTokensEarned;
        public bool newBest;
        public int goalsCompleted;
        public int goalsTotal;
    }

    /// <summary>Cross-scene handoff (which park, which mode, last result). Deliberately tiny.</summary>
    public static class GameSession
    {
        public static string ModeLabel(RunMode mode)
        {
            switch (mode)
            {
                case RunMode.SpotContract: return "SPOT CONTRACT";
                case RunMode.FreeSkate: return "FREE SKATE";
                case RunMode.DailyLine: return "DAILY LINE";
                case RunMode.Tutorial: return "HOW TO SKATE";
                case RunMode.Party: return "PASS & PLAY";
                case RunMode.Duel: return "S.K.A.T.E.";
                case RunMode.Replay: return "REPLAY";
                default: return "TWO-MINUTE RUN";
            }
        }

        /// <summary>Today's key for the Daily Line (player's local calendar day).</summary>
        public static int TodayKey => RetroSk8.Core.DailyLineGenerator.DateKey(System.DateTime.Now);

        public static string LocationId = "harbor_plaza";
        /// <summary>The Trick Book lesson being played (Phase 17), or null.</summary>
        public static string LessonId;
        /// <summary>Open the Trick Book when the main menu loads (coming back from a lesson).</summary>
        public static bool OpenTricksOnMenu;
        public static RunMode Mode = RunMode.TwoMinuteRun;
        public static RunResult LastResult;
        public static bool DebugInfiniteTime;
        /// <summary>Set by SceneRouter when a park is loaded through another park's scene.</summary>
        public static bool ParkOverride;
        public static RetroSk8.Core.PartyGame PartyGame = RetroSk8.Core.PartyGame.Letters;
        public static int PartyPlayers = 2;
        /// <summary>Open the Create-a-Park editor when this (custom) park loads.</summary>
        public static bool EditPark;
        /// <summary>The saved replay to open in RunMode.Replay.</summary>
        public static string ReplayId;
        /// <summary>The crew member you're trying to recruit with this run or S.K.A.T.E. game (null when none).</summary>
        public static string CrewRecruitId;
        /// <summary>A friend's score to beat on this park (from a challenge code), shown in the HUD and on Results.</summary>
        public static RetroSk8.Core.ScoreChallenge Challenge;
        /// <summary>The last finished Two-Minute Run's full recording and banked lines (for SEND GHOST on Results).</summary>
        public static RetroSk8.Core.ReplayTrack LastRunTrack;
        public static System.Collections.Generic.List<RetroSk8.Core.GhostBank> LastRunBanks;
        /// <summary>The story step being played (null when not in story mode).</summary>
        public static string StoryStepId;
        /// <summary>A story step just cleared: the menu plays its closing panels.</summary>
        public static string StoryOutroPending;

        /// <summary>The challenge, when it is for the park being played in a Two-Minute Run.</summary>
        public static RetroSk8.Core.ScoreChallenge ActiveChallengeFor(string locationId)
        {
            var c = Challenge;
            if (c == null || Mode != RunMode.TwoMinuteRun) return null;
            string id = c.Park != null ? c.Park.id : c.LocationId;
            return id == locationId ? c : null;
        }
    }
}
