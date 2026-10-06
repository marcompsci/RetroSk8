using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RetroSk8.Save
{
    /// <summary>
    /// Where the save seal's key lives (Phase 19): the iOS Keychain on device (RetroSk8Keychain.mm), PlayerPrefs in
    /// the editor and elsewhere. Created once per install. <see cref="Get"/> reports whether it was just created,
    /// because a brand-new key can't judge a seal written with an older one.
    /// </summary>
    public static class SealKeys
    {
        private const string Account = "save-seal-v1";

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern string RetroSk8_KeychainGet(string account);
        [DllImport("__Internal")] private static extern int RetroSk8_KeychainSet(string account, string value);
        private static string Read() => RetroSk8_KeychainGet(Account) ?? "";
        private static bool Write(string value) => RetroSk8_KeychainSet(Account, value) != 0;
#else
        private const string PrefsKey = "RetroSk8.SealKey";
        private static string Read() => PlayerPrefs.GetString(PrefsKey, "");
        private static bool Write(string value)
        {
            PlayerPrefs.SetString(PrefsKey, value);
            PlayerPrefs.Save();
            return true;
        }
#endif

        private static byte[] s_key;
        private static bool s_isNew;

        /// <summary>The install's key, creating it the first time.</summary>
        public static byte[] Get(out bool isNew)
        {
            if (s_key == null)
            {
                s_isNew = false;
                try
                {
                    string stored = Read();
                    if (!string.IsNullOrEmpty(stored)) s_key = Convert.FromBase64String(stored);
                }
                catch (FormatException) { s_key = null; }
                if (s_key == null || s_key.Length < 16)
                {
                    s_key = RetroSk8.Core.SaveSeal.NewKey();
                    s_isNew = true;
                    if (!Write(Convert.ToBase64String(s_key))) Debug.LogWarning("[RetroSk8] Couldn't store the save seal key.");
                }
            }
            isNew = s_isNew;
            return s_key;
        }

        /// <summary>A seal was just written with this key, so seals from now on can be judged by it.</summary>
        public static void MarkUsed() => s_isNew = false;
    }
}
