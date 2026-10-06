using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace RetroSk8.Core
{
    /// <summary>
    /// Phase 22: packs the save file into one line of text for the iCloud backup and the SAVE CODE copy/paste.
    /// The format is "RS8B1.&lt;unix seconds&gt;.&lt;sha256 of the json, hex&gt;.&lt;base64 of the gzipped json&gt;".
    /// Everything coming back in is treated as untrusted: the text is size-checked before decoding, decompression
    /// stops at a hard cap (no zip bombs), and the checksum must match. Engine-free and unit-tested.
    /// </summary>
    public static class SaveBackup
    {
        public const string Prefix = "RS8B1";
        /// <summary>iCloud key-value storage allows 1 MB per key; stay well under it.</summary>
        public const int MaxPackedChars = 900 * 1024;
        /// <summary>Largest save JSON accepted after decompression.</summary>
        public const int MaxJsonBytes = 4 * 1024 * 1024;

        public static string Pack(string json, long unixSeconds)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            byte[] raw = Encoding.UTF8.GetBytes(json);
            byte[] zipped;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true)) gz.Write(raw, 0, raw.Length);
                zipped = ms.ToArray();
            }
            return Prefix + "." + Math.Max(0L, unixSeconds) + "." + Hash(raw) + "." + Convert.ToBase64String(zipped);
        }

        /// <summary>Whether a packed save fits in one iCloud key.</summary>
        public static bool Fits(string packed) => packed != null && packed.Length <= MaxPackedChars;

        public static bool TryUnpack(string packed, out string json, out long unixSeconds, out string error)
        {
            json = null;
            unixSeconds = 0;
            error = null;
            if (string.IsNullOrWhiteSpace(packed)) { error = "NO SAVE CODE"; return false; }
            packed = packed.Trim();
            if (packed.Length > MaxPackedChars) { error = "THAT SAVE CODE IS TOO BIG"; return false; }
            var parts = packed.Split('.');
            if (parts.Length != 4 || parts[0] != Prefix) { error = "NOT A RETRO SK8 SAVE CODE"; return false; }
            if (!long.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out unixSeconds))
            { error = "DAMAGED SAVE CODE"; return false; }
            if (parts[2].Length != 64) { error = "DAMAGED SAVE CODE"; return false; }
            byte[] zipped;
            try { zipped = Convert.FromBase64String(parts[3]); }
            catch (FormatException) { error = "DAMAGED SAVE CODE"; return false; }

            byte[] raw;
            try
            {
                using (var input = new MemoryStream(zipped))
                using (var gz = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[16 * 1024];
                    int n;
                    while ((n = gz.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + n > MaxJsonBytes) { error = "THAT SAVE CODE IS TOO BIG"; return false; }
                        output.Write(buffer, 0, n);
                    }
                    raw = output.ToArray();
                }
            }
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is NotSupportedException)
            {
                error = "DAMAGED SAVE CODE";
                return false;
            }
            if (!string.Equals(Hash(raw), parts[2], StringComparison.OrdinalIgnoreCase)) { error = "DAMAGED SAVE CODE"; return false; }
            json = Encoding.UTF8.GetString(raw);
            if (json.Length == 0 || json[0] != '{') { json = null; error = "NOT A RETRO SK8 SAVE CODE"; return false; }
            return true;
        }

        /// <summary>"JUST NOW", "5 MIN AGO", "3 HOURS AGO", "2 DAYS AGO" (future times read as just now).</summary>
        public static string Ago(long unixSeconds, long nowUnixSeconds)
        {
            long s = nowUnixSeconds - unixSeconds;
            if (s < 60) return "JUST NOW";
            if (s < 3600) return (s / 60) + " MIN AGO";
            if (s < 86400) { long h = s / 3600; return h + (h == 1 ? " HOUR AGO" : " HOURS AGO"); }
            long d = s / 86400;
            return d + (d == 1 ? " DAY AGO" : " DAYS AGO");
        }

        /// <summary>Automatic backups wait at least this long after the last one.</summary>
        public const long AutoBackupSeconds = 10 * 60;

        public static bool AutoBackupDue(long lastBackupUnix, long nowUnix) => nowUnix - lastBackupUnix >= AutoBackupSeconds || nowUnix < lastBackupUnix;

        private static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(data);
                var sb = new StringBuilder(64);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
