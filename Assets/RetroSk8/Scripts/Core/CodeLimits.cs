using System;

namespace RetroSk8.Core
{
    /// <summary>
    /// Phase 19 input limits for everything that arrives from outside the game: share codes typed or pasted in, parks
    /// and ghosts downloaded from the online gallery, gallery listings and Game Center rows. Every decoder checks the
    /// size first, so a huge or hostile payload is refused before any work is done. Sizes are a few times larger than
    /// the biggest thing the game itself can write.
    /// </summary>
    public static class CodeLimits
    {
        /// <summary>A full 60-piece park is 398 characters as a code, a challenge on it 424 (measured in Phase19Tests).</summary>
        public const int MaxParkCodeChars = 1500;
        /// <summary>A worst-case 3-minute ghost (1,800 samples) on a full custom park is about 63,000 characters.</summary>
        public const int MaxGhostCodeChars = 100000;
        /// <summary>Samples in a ghost: <see cref="GhostCodes.MaxSeconds"/> at <see cref="GhostCodes.SampleRate"/>, plus one.</summary>
        public const int MaxGhostSamples = (int)(GhostCodes.MaxSeconds * GhostCodes.SampleRate) + 1;
        /// <summary>A whole gallery page as text from the CloudKit bridge.</summary>
        public const int MaxGalleryText = 256 * 1024;
        /// <summary>Lines read from one gallery page (anything after is ignored).</summary>
        public const int MaxGalleryLines = Gallery.PageSize * 2;
        /// <summary>CloudKit record names and user ids: letters, digits, '-', '_' and '.', at most this long.</summary>
        public const int MaxRecordIdLength = 128;
        /// <summary>Raw name/author text kept from a gallery line before cleaning.</summary>
        public const int MaxRawNameLength = 64;
        /// <summary>A Game Center leaderboard page as text.</summary>
        public const int MaxScoresText = 64 * 1024;

        /// <summary>True for a safe record id: non-empty, short, and only [A-Za-z0-9-_.:].</summary>
        public static bool IsSafeRecordId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > MaxRecordIdLength) return false;
            foreach (char c in id)
            {
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == ':';
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>Cuts text to <paramref name="max"/> characters (null becomes "").</summary>
        public static string Cap(string text, int max) =>
            string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text.Substring(0, max);

        /// <summary>A Unix time (seconds) kept to a sane window: not before 2020, not more than a day in the future.</summary>
        public static long ClampCreated(long created, long now) =>
            created < 1577836800L ? 1577836800L : created > now + 86400L ? now : created;
    }
}
