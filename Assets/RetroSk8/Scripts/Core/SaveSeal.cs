using System;
using System.Security.Cryptography;
using System.Text;

namespace RetroSk8.Core
{
    /// <summary>What the save file's tamper check found when it was loaded (Phase 19).</summary>
    public enum SaveIntegrity
    {
        /// <summary>No save yet, or nothing checked.</summary>
        Unknown = 0,
        /// <summary>The seal matches: the file is exactly what the game wrote.</summary>
        Sealed = 1,
        /// <summary>No seal to check (a save from before Phase 19, or this install's key is new). Sealed on the next save.</summary>
        Unsealed = 2,
        /// <summary>The file was changed outside the game. Stored bests aren't sent to Game Center from it.</summary>
        Edited = 3,
    }

    /// <summary>
    /// Tamper seal for the save file (Phase 19): an HMAC-SHA256 of the exact JSON the game wrote, keyed with a random
    /// per-install secret kept outside the save (the iOS Keychain on device). Editing the JSON by hand breaks the seal.
    /// This can't stop someone who can also read the Keychain (a jailbroken phone), so it only gates what leaves the
    /// device (Game Center bests); see SECURITY.md. Engine-free and unit-tested.
    /// </summary>
    public static class SaveSeal
    {
        public const int KeyBytes = 32;

        /// <summary>A fresh random key.</summary>
        public static byte[] NewKey()
        {
            var key = new byte[KeyBytes];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(key);
            return key;
        }

        /// <summary>Lower-case hex HMAC-SHA256 of the save text.</summary>
        public static string Seal(byte[] key, string json)
        {
            if (key == null || key.Length == 0) throw new ArgumentException("No key");
            using (var h = new HMACSHA256(key))
                return ToHex(h.ComputeHash(Encoding.UTF8.GetBytes(json ?? "")));
        }

        /// <summary>
        /// Checks a loaded save. <paramref name="keyIsNew"/> means this install had no key until now (fresh install or a
        /// restore to a new phone): a seal can't be judged then, so the save counts as unsealed, never as edited.
        /// </summary>
        public static SaveIntegrity Check(byte[] key, bool keyIsNew, string json, string seal)
        {
            if (json == null) return SaveIntegrity.Unknown;
            if (string.IsNullOrEmpty(seal) || keyIsNew || key == null || key.Length == 0) return SaveIntegrity.Unsealed;
            return FixedTimeEquals(Seal(key, json), seal.Trim().ToLowerInvariant()) ? SaveIntegrity.Sealed : SaveIntegrity.Edited;
        }

        public static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static byte[] FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0) return null;
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int hi = HexValue(hex[i * 2]), lo = HexValue(hex[i * 2 + 1]);
                if (hi < 0 || lo < 0) return null;
                bytes[i] = (byte)(hi * 16 + lo);
            }
            return bytes;
        }

        private static int HexValue(char c) =>
            c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;

        /// <summary>Compares without stopping at the first difference (no timing hint about the seal).</summary>
        public static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
