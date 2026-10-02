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

        public static bool IsAvailable => RetroSk8_GCAvailable() != 0;
        public static bool IsAuthenticated => IsAvailable && RetroSk8_GCIsAuthenticated() != 0;
        public static void Authenticate() { if (IsAvailable) RetroSk8_GCAuthenticate(); }
        public static void SubmitScore(string leaderboardId, long score) { if (IsAuthenticated && score > 0) RetroSk8_GCSubmitScore(leaderboardId, score); }
        public static void ReportAchievement(string id, float percent01) { if (IsAuthenticated) RetroSk8_GCReportAchievement(id, Mathf.Clamp01(percent01) * 100.0); }
        public static void ShowDashboard() { if (IsAvailable) RetroSk8_GCShowDashboard(); }
#else
        public static bool IsAvailable => false;
        public static bool IsAuthenticated => false;
        public static void Authenticate() { }
        public static void SubmitScore(string leaderboardId, long score) { }
        public static void ReportAchievement(string id, float percent01) { }
        public static void ShowDashboard() { }
#endif
    }
}
