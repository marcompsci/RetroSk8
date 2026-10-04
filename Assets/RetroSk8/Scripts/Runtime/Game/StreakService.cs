using System;
using System.Runtime.InteropServices;
using RetroSk8.Core;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>Daily streak check-in (Phase 15): once per local day, from the main menu.</summary>
    public static class StreakService
    {
        public static StreakState State => SaveManager.Data.streak;

        /// <summary>Checks in for today and pays the day's tokens. Returns null when today was already counted.</summary>
        public static StreakResult CheckIn()
        {
            var r = Streaks.CheckIn(State, Streaks.DayNumber(DateTime.Now));
            if (!r.NewDay) return null;
            if (r.Tokens > 0) SaveManager.AddTokens(r.Tokens); else SaveManager.Save();
            return r;
        }
    }

    /// <summary>
    /// Opt-in local reminders (Phase 15). Off until the player turns REMINDERS on in Settings and allows them in the
    /// iOS prompt. Every time the game goes to the background the whole plan is rebuilt from the save, so the
    /// reminders always match what's actually left to do. Nothing leaves the phone.
    /// </summary>
    public static class NotificationService
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroSk8_NotifyRefreshAuth();
        [DllImport("__Internal")] private static extern void RetroSk8_NotifyRequest();
        [DllImport("__Internal")] private static extern int RetroSk8_NotifyAuthState();
        [DllImport("__Internal")] private static extern void RetroSk8_NotifyCancelAll();
        [DllImport("__Internal")] private static extern void RetroSk8_NotifySchedule(string id, string title, string body, double secondsFromNow);

        public static bool IsSupported => true;
        public static void RefreshAuth() => RetroSk8_NotifyRefreshAuth();
        /// <summary>0 not asked, 1 asking, 2 allowed, 3 denied.</summary>
        public static int AuthState => RetroSk8_NotifyAuthState();
        private static void Request() => RetroSk8_NotifyRequest();
        private static void CancelAll() => RetroSk8_NotifyCancelAll();
        private static void Schedule(PlannedNotification n, double seconds) => RetroSk8_NotifySchedule(n.Id, n.Title, n.Body, seconds);
#else
        public static bool IsSupported => false;
        public static void RefreshAuth() { }
        public static int AuthState => 0;
        private static void Request() { }
        private static void CancelAll() { }
        private static void Schedule(PlannedNotification n, double seconds) { }
#endif

        public static bool Enabled => SaveManager.Data.settings.reminders;

        /// <summary>Settings toggle. Turning it on asks iOS for permission the first time.</summary>
        public static void SetEnabled(bool on)
        {
            SaveManager.Data.settings.reminders = on;
            SaveManager.Save();
            if (on) { Request(); NotificationRunner.Ensure(); }
            else CancelAll();
        }

        /// <summary>Replaces every pending reminder with a fresh plan (or clears them when reminders are off).</summary>
        public static void Reschedule()
        {
            if (!IsSupported) return;
            CancelAll();
            if (!Enabled || AuthState == 3) return;
            var now = DateTime.Now;
            var weekly = WeeklyService.State; // rolls over to this week if needed
            var ev = WeeklyService.Current;
            int left = 0;
            if (ev != null && ev.Goals != null)
                for (int i = 0; i < ev.Goals.Length; i++)
                    if (!weekly.paidGoals.Contains(i)) left++;
            foreach (var n in NotificationPlan.Build(now, StreakService.State, left, ev?.Name))
            {
                double seconds = (n.FireAt - now).TotalSeconds;
                if (seconds >= 60) Schedule(n, seconds);
            }
        }
    }

    /// <summary>Lives across scenes and reschedules reminders whenever the game is backgrounded or closed.</summary>
    public sealed class NotificationRunner : MonoBehaviour
    {
        private static NotificationRunner s_instance;

        public static void Ensure()
        {
            if (s_instance != null || !NotificationService.IsSupported) return;
            var go = new GameObject("NotificationRunner");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<NotificationRunner>();
            NotificationService.RefreshAuth();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) NotificationService.Reschedule();
            else NotificationService.RefreshAuth();
        }

        private void OnApplicationQuit() => NotificationService.Reschedule();
    }
}
