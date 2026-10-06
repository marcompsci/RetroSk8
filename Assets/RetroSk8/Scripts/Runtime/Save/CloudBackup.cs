using System;
using System.Runtime.InteropServices;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Save
{
    /// <summary>
    /// Phase 22: backs the save up to iCloud (key-value storage, one key) and restores it on a new phone. It also
    /// writes and reads SAVE CODES (the same packed text, copied to the clipboard) for moving progress without iCloud.
    /// Automatic backups run when the app goes to the background, at most every 10 minutes. Ghosts aren't included.
    /// </summary>
    public static class CloudBackup
    {
        public const string Key = "retrosk8.save.v1";
        private const string LastBackupPref = "retrosk8.lastBackupUnix";

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int RetroSk8_CloudSaveAvailable();
        [DllImport("__Internal")] private static extern int RetroSk8_CloudSaveSet(string key, string value);
        [DllImport("__Internal")] private static extern string RetroSk8_CloudSaveGet(string key);
        [DllImport("__Internal")] private static extern void RetroSk8_CloudSaveSync();
#endif

        /// <summary>True on an iPhone build with iCloud on and a signed-in iCloud account.</summary>
        public static bool Available
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                try { return RetroSk8_CloudSaveAvailable() == 1; } catch (Exception) { return false; }
#else
                return false;
#endif
            }
        }

        public static long NowUnix => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>When this phone last backed up (0 = never).</summary>
        public static long LastBackupUnix
        {
            get { return long.TryParse(PlayerPrefs.GetString(LastBackupPref, "0"), out long v) ? v : 0L; }
            private set { PlayerPrefs.SetString(LastBackupPref, value.ToString()); PlayerPrefs.Save(); }
        }

        /// <summary>The current save as a SAVE CODE (also what goes to iCloud).</summary>
        public static string MakeCode() => SaveBackup.Pack(SaveManager.CurrentJson(), NowUnix);

        public static bool BackupNow(out string message)
        {
            if (!Available) { message = "ICLOUD BACKUP ISN'T AVAILABLE (SIGN IN TO ICLOUD)"; return false; }
            string code = MakeCode();
            if (!SaveBackup.Fits(code)) { message = "YOUR SAVE IS TOO BIG FOR ICLOUD BACKUP"; return false; }
#if UNITY_IOS && !UNITY_EDITOR
            bool ok = RetroSk8_CloudSaveSet(Key, code) == 1;
#else
            bool ok = false;
#endif
            if (ok) LastBackupUnix = NowUnix;
            message = ok ? "BACKED UP TO ICLOUD" : "BACKUP FAILED. TRY AGAIN LATER";
            return ok;
        }

        /// <summary>
        /// Quietly backs up when one is due (called when the app goes to the background). Only after this phone has
        /// backed up by hand or restored once: a fresh install must never overwrite the backup from your old phone.
        /// </summary>
        public static void AutoBackup()
        {
            if (LastBackupUnix <= 0) return;
            if (!Available || !SaveBackup.AutoBackupDue(LastBackupUnix, NowUnix)) return;
            BackupNow(out _);
        }

        /// <summary>Reads the iCloud backup without applying it: what it holds and when it was made.</summary>
        public static bool PeekCloud(out SaveData data, out long madeUnix, out string error)
        {
            data = null;
            madeUnix = 0;
            if (!Available) { error = "ICLOUD BACKUP ISN'T AVAILABLE"; return false; }
#if UNITY_IOS && !UNITY_EDITOR
            string code = RetroSk8_CloudSaveGet(Key);
#else
            string code = null;
#endif
            if (string.IsNullOrEmpty(code)) { error = "NO ICLOUD BACKUP FOUND YET. ON A NEW PHONE ICLOUD CAN TAKE A MINUTE: TRY AGAIN SOON"; return false; }
            return Peek(code, out data, out madeUnix, out error);
        }

        /// <summary>Checks a SAVE CODE (pasted or from iCloud) and reads it without applying it.</summary>
        public static bool Peek(string code, out SaveData data, out long madeUnix, out string error)
        {
            data = null;
            if (!SaveBackup.TryUnpack(code, out string json, out madeUnix, out error)) return false;
            if (!SaveManager.TryParse(json, out data)) { error = "THAT SAVE CODE COULDN'T BE READ"; return false; }
            return true;
        }

        /// <summary>One line describing a backup for the confirm button: age, tokens and story progress.</summary>
        public static string Describe(SaveData d, long madeUnix)
        {
            if (d == null) return "";
            int steps = 0, cleared = 0;
            foreach (var s in Story.AllSteps()) { steps++; if (d.story != null && d.story.IsCleared(s.Id)) cleared++; }
            return $"{SaveBackup.Ago(madeUnix, NowUnix)} · {d.tapeTokens:N0} TOKENS · STORY {cleared}/{steps}";
        }

        /// <summary>Applies a restored save. <paramref name="fromICloud"/>: only an iCloud restore turns on automatic backups
        /// (a pasted older code must never overwrite a newer iCloud backup).</summary>
        public static void Apply(SaveData d, bool fromICloud)
        {
            SaveManager.ReplaceWith(d);
            if (fromICloud) LastBackupUnix = NowUnix;
        }

        /// <summary>Starts the background-backup runner (once per app run).</summary>
        public static void Ensure()
        {
            if (CloudBackupRunner.Instance != null) return;
#if UNITY_IOS && !UNITY_EDITOR
            try { RetroSk8_CloudSaveSync(); } catch (Exception) { } // start fetching the iCloud copy early (a restore needs it)
#endif
            var go = new GameObject("CloudBackupRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<CloudBackupRunner>();
        }
    }

    /// <summary>Backs up when the app is sent to the background (if one is due).</summary>
    public sealed class CloudBackupRunner : MonoBehaviour
    {
        public static CloudBackupRunner Instance { get; private set; }
        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void OnApplicationPause(bool paused) { if (paused) CloudBackup.AutoBackup(); }
    }
}
