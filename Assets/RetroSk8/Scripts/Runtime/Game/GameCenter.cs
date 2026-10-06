using System.Runtime.InteropServices;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Apple Game Center: sign-in, per-park best-score leaderboards and achievements.
    /// Only active in iOS builds made with "Enable Game Center" switched on (the post-build step adds the
    /// capability and an Info.plist flag the native side checks). Everywhere else every call is a no-op and
    /// the game uses its local records screen instead.
    /// </summary>
    public static class GameCenter
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int RetroSk8_GCAvailable();
        [DllImport("__Internal")] private static extern void RetroSk8_GCAuthenticate();
        [DllImport("__Internal")] private static extern int RetroSk8_GCIsAuthenticated();
        [DllImport("__Internal")] private static extern void RetroSk8_GCSubmitScore(string leaderboardId, long score);
        [DllImport("__Internal")] private static extern void RetroSk8_GCReportAchievement(string achievementId, double percent);
        [DllImport("__Internal")] private static extern void RetroSk8_GCShowDashboard();
        [DllImport("__Internal")] private static extern void RetroSk8_GCLoadFriendScores(string leaderboardId);
        [DllImport("__Internal")] private static extern void RetroSk8_GCLoadScores(string leaderboardId, int friendsOnly, int pageSize);
        [DllImport("__Internal")] private static extern int RetroSk8_GCFriendScoresState();
        [DllImport("__Internal")] private static extern string RetroSk8_GCFriendScores();

        public static bool IsAvailable => RetroSk8_GCAvailable() != 0;
        public static bool IsAuthenticated => IsAvailable && RetroSk8_GCIsAuthenticated() != 0;
        public static void Authenticate() { if (IsAvailable) RetroSk8_GCAuthenticate(); }
        public static void SubmitScore(string leaderboardId, long score)
        {
            if (!IsAuthenticated || score <= 0 || !Allowed(leaderboardId, score)) return;
            RetroSk8_GCSubmitScore(leaderboardId, score);
        }
        public static void ReportAchievement(string id, float percent01) { if (IsAuthenticated) RetroSk8_GCReportAchievement(id, Mathf.Clamp01(percent01) * 100.0); }
        public static void ShowDashboard() { if (IsAvailable) RetroSk8_GCShowDashboard(); }

        /// <summary>Starts loading friends' scores on a leaderboard; poll <see cref="FriendScoresState"/>.</summary>
        public static void LoadFriendScores(string leaderboardId) { if (IsAuthenticated) RetroSk8_GCLoadFriendScores(leaderboardId); }
        /// <summary>0 idle, 1 loading, 2 ready, 3 failed.</summary>
        /// <summary>Starts loading a leaderboard page (Leaderboards hub); shares <see cref="FriendScoresState"/> with friends loads.</summary>
        public static void LoadScores(string leaderboardId, bool friendsOnly, int pageSize) { if (IsAuthenticated) RetroSk8_GCLoadScores(leaderboardId, friendsOnly ? 1 : 0, pageSize); }
        public static RetroSk8.Core.BoardPage ScoresPage() => RetroSk8.Core.BoardPage.Parse(IsAvailable ? RetroSk8_GCFriendScores() : "");
        public static int FriendScoresState => IsAvailable ? RetroSk8_GCFriendScoresState() : 0;
        public static System.Collections.Generic.List<RetroSk8.Core.FriendScore> FriendScores() =>
            RetroSk8.Core.FriendScore.Parse(IsAvailable ? RetroSk8_GCFriendScores() : "");
#else
        public static bool IsAvailable => false;
        public static bool IsAuthenticated => false;
        public static void Authenticate() { }
        public static void SubmitScore(string leaderboardId, long score) { Allowed(leaderboardId, score); }
        public static void ReportAchievement(string id, float percent01) { }
        public static void ShowDashboard() { }
        public static void LoadFriendScores(string leaderboardId) { }
        public static void LoadScores(string leaderboardId, bool friendsOnly, int pageSize) { }
        public static RetroSk8.Core.BoardPage ScoresPage() => new RetroSk8.Core.BoardPage();
        public static int FriendScoresState => 0;
        public static System.Collections.Generic.List<RetroSk8.Core.FriendScore> FriendScores() => new System.Collections.Generic.List<RetroSk8.Core.FriendScore>();
#endif

        /// <summary>Scores dropped by the anti-cheat check this session (Phase 19; shown in the debug menu).</summary>
        public static int Rejected { get; private set; }

        /// <summary>
        /// Phase 19 anti-cheat: only believable scores leave the device (ScoreLimits), and never stored bests from a
        /// save file that failed its tamper check.
        /// </summary>
        public static bool Allowed(string leaderboardId, long score)
        {
            if (score <= 0) return false;
            if (RetroSk8.Core.ScoreLimits.IsPlausible(leaderboardId, score)) return true;
            Rejected++;
            Debug.LogWarning($"[RetroSk8] Not sending {score} to {leaderboardId}: outside its believable range.");
            return false;
        }
    }
}
