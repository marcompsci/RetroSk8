using System;
using System.Collections.Generic;
using System.Text;

namespace RetroSk8.Core
{
    public enum GalleryKind { Park = 0, Ghost = 1 }

    /// <summary>One shared park or ghost in the online gallery (Phase 16).</summary>
    public sealed class GalleryEntry
    {
        public string Id;
        public GalleryKind Kind;
        public string Name;
        public string Author;
        /// <summary>Pieces for a park, the score for a ghost.</summary>
        public long Detail;
        /// <summary>Built-in park id a ghost was skated at (empty for parks and ghosts on custom parks).</summary>
        public string Location = "";
        /// <summary>Unix seconds.</summary>
        public long Created;
        /// <summary>
        /// Phase 19: the poster's iCloud user record (set by CloudKit, so it can't be faked like the typed author
        /// name). BLOCK uses it when present. Empty for older bridges and the editor's test gallery.
        /// </summary>
        public string Creator = "";
    }

    /// <summary>Your gallery settings and moderation choices (JsonUtility-friendly).</summary>
    [Serializable]
    public sealed class GalleryState
    {
        public List<string> hidden = new List<string>();
        public List<string> blockedAuthors = new List<string>();
        public List<string> reported = new List<string>();
        public List<string> posted = new List<string>();
        public int uploadDay;
        public int uploadsToday;
        /// <summary>Phase 19: Unix seconds of the last post (posts are spaced out) and fingerprints of what you posted.</summary>
        public long lastUploadAt;
        public List<string> postedHashes = new List<string>();

        public void Sanitize()
        {
            if (hidden == null) hidden = new List<string>();
            if (blockedAuthors == null) blockedAuthors = new List<string>();
            if (reported == null) reported = new List<string>();
            if (posted == null) posted = new List<string>();
            if (postedHashes == null) postedHashes = new List<string>();
            Trim(hidden); Trim(blockedAuthors); Trim(reported); Trim(posted); Trim(postedHashes);
        }

        private static void Trim(List<string> list)
        {
            list.RemoveAll(string.IsNullOrEmpty);
            while (list.Count > Gallery.MaxRemembered) list.RemoveAt(0);
        }
    }

    /// <summary>
    /// Rules for the online gallery (Phase 16): the wire format the native CloudKit bridge uses, name cleaning and a
    /// word filter for anything typed by players, what you can see (hidden posts and blocked authors), and a daily
    /// upload limit. Engine-free and unit-tested; the network part lives in GalleryService and RetroSk8Gallery.mm.
    /// </summary>
    public static class Gallery
    {
        public const int PageSize = 30;
        public const int UploadsPerDay = 5;
        public const int MaxRemembered = 500;
        /// <summary>Phase 19: at least this long between two posts (seconds).</summary>
        public const int UploadSpacingSeconds = 60;
        /// <summary>Blocked creators are stored with this prefix next to blocked author names.</summary>
        public const string CreatorPrefix = "u:";

        public static readonly string[] ReportReasons = { "OFFENSIVE NAME", "SPAM", "BROKEN OR EMPTY", "OTHER" };

        // A short list of words no player-typed text may contain. Matching ignores spacing, punctuation and common
        // number swaps (so "B4D W0RD" style tricks still match). Extend it as reports come in.
        private static readonly string[] Blocked =
        {
            "FUCK", "SHIT", "CUNT", "BITCH", "DICK", "COCK", "PUSSY", "WHORE", "SLUT", "FAG", "NIGG", "RETARD",
            "RAPE", "NAZI", "HITLER", "KILLYOURSELF", "KYS", "PENIS", "VAGINA", "PORN",
            // Phase 19 additions.
            // (Short words that hide inside ordinary ones, like the ones in "cucumber" or "Janus", are left out.)
            "NIGA", "KIKE", "CHINK", "TRANNY", "MOLEST", "PEDO", "NUDES", "JIZZ",
            "WANK", "TWAT", "BOLLOCK", "HEIL", "KKK", "SUICIDE", "SHOOTUP",
        };

        /// <summary>Uppercase, only the characters codes can carry, single spaces, trimmed to <paramref name="max"/>.</summary>
        public static string CleanName(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder();
            bool space = false;
            foreach (char raw in text.ToUpperInvariant())
            {
                char c = raw;
                bool ok = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '.' || c == '!' || c == '\'';
                if (char.IsWhiteSpace(c) || c == '_') { space = sb.Length > 0; continue; }
                if (!ok) continue;
                if (space && sb.Length + 2 > max) break; // Phase 19: never go past max with the separating space
                if (space) { sb.Append(' '); space = false; }
                sb.Append(c);
                if (sb.Length >= max) break;
            }
            return sb.ToString().Trim();
        }

        /// <summary>True when a name contains a blocked word (spacing, punctuation and number swaps ignored).</summary>
        public static bool IsBlocked(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (text.Length > 256) text = text.Substring(0, 256);
            var plain = new StringBuilder();
            var collapsed = new StringBuilder();
            foreach (char raw in text.ToUpperInvariant())
            {
                char c = raw == '0' ? 'O' : raw == '1' || raw == '!' || raw == '|' ? 'I' : raw == '3' ? 'E' : raw == '4' || raw == '@' ? 'A'
                    : raw == '5' || raw == '$' ? 'S' : raw == '7' || raw == '+' ? 'T' : raw == '8' ? 'B' : raw == '9' || raw == '6' ? 'G' : raw;
                if (c < 'A' || c > 'Z') continue;
                plain.Append(c);
                // Phase 19: also check with repeats squashed, so "FUUUCK" reads as "FUCK".
                if (collapsed.Length == 0 || collapsed[collapsed.Length - 1] != c) collapsed.Append(c);
            }
            string a = plain.ToString(), b = collapsed.ToString();
            string a1 = a.Replace('I', 'L'), b1 = b.Replace('I', 'L'); // "1" can stand in for L too
            foreach (var w in Blocked)
                if (a.Contains(w) || b.Contains(w) || a1.Contains(w) || b1.Contains(w)) return true;
            return false;
        }

        /// <summary>The name shown for player text: cleaned, and replaced when it trips the filter.</summary>
        public static string SafeName(string text, int max, string fallback)
        {
            string clean = CleanName(text, max);
            return clean.Length == 0 || IsBlocked(clean) ? fallback : clean;
        }

        public static bool CanUpload(GalleryState s, int today) => s != null && (s.uploadDay != today || s.uploadsToday < UploadsPerDay);

        /// <summary>Phase 19: seconds until the next post is allowed (0 = now).</summary>
        public static long WaitBeforeUpload(GalleryState s, long now) =>
            s == null || s.lastUploadAt <= 0 ? 0 : Math.Max(0, s.lastUploadAt + UploadSpacingSeconds - now);

        /// <summary>A short fingerprint of a code (FNV-1a), so the same park or ghost isn't posted twice.</summary>
        public static string Fingerprint(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            unchecked
            {
                ulong h = 14695981039346656037UL;
                foreach (char c in code) { h ^= c; h *= 1099511628211UL; }
                return h.ToString("x16");
            }
        }

        public static bool AlreadyPosted(GalleryState s, string code) => s != null && s.postedHashes.Contains(Fingerprint(code));

        public static void CountUpload(GalleryState s, int today, string id, string code = null, long now = 0)
        {
            if (s.uploadDay != today) { s.uploadDay = today; s.uploadsToday = 0; }
            s.uploadsToday++;
            if (!string.IsNullOrEmpty(id)) s.posted.Add(id);
            if (!string.IsNullOrEmpty(code)) s.postedHashes.Add(Fingerprint(code));
            if (now > 0) s.lastUploadAt = now;
            s.Sanitize();
        }

        /// <summary>The key BLOCK stores for a post's author: its iCloud creator when known, else the typed name.</summary>
        public static string BlockKey(GalleryEntry e) =>
            e == null ? "" : !string.IsNullOrEmpty(e.Creator) ? CreatorPrefix + e.Creator : (e.Author ?? "");

        /// <summary>What you see: your hidden posts and blocked authors are gone, and names are filtered.</summary>
        public static List<GalleryEntry> Visible(IEnumerable<GalleryEntry> entries, GalleryState s)
        {
            var list = new List<GalleryEntry>();
            if (entries == null) return list;
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.Id)) continue;
                if (s != null && (s.hidden.Contains(e.Id) || s.blockedAuthors.Contains(e.Author ?? "")
                                  || (!string.IsNullOrEmpty(e.Creator) && s.blockedAuthors.Contains(CreatorPrefix + e.Creator)))) continue;
                list.Add(e);
            }
            return list;
        }

        public static string DisplayName(GalleryEntry e) => SafeName(e?.Name, CustomPark.MaxNameLength, e != null && e.Kind == GalleryKind.Ghost ? "A RUN" : "A PARK");

        public static string DisplayAuthor(GalleryEntry e) => SafeName(e?.Author, ShareCodes.MaxFromLength, "SKATER");

        // ---------------------------------------------------------------- wire format

        /// <summary>
        /// One entry per line: id, kind, name, author, detail, location, created, and (Phase 19) creator, tab separated.
        /// Phase 19: everything in a listing was written by other players, so each field is checked: record ids must be
        /// plain, names are capped, the park must be one this version knows (or empty), numbers are kept in range, and
        /// only a page's worth of lines is read.
        /// </summary>
        public static List<GalleryEntry> Parse(string text, long now = 0)
        {
            var list = new List<GalleryEntry>();
            if (string.IsNullOrEmpty(text) || text.Length > CodeLimits.MaxGalleryText) return list;
            if (now <= 0) now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var seen = new HashSet<string>();
            int lines = 0;
            foreach (var line in text.Split('\n'))
            {
                if (++lines > CodeLimits.MaxGalleryLines) break;
                var f = line.Split('\t');
                if (f.Length < 7) continue;
                string id = f[0].Trim();
                if (!CodeLimits.IsSafeRecordId(id) || !seen.Add(id)) continue;
                if (!int.TryParse(f[1], out int kind) || kind < 0 || kind > 1) continue;
                long.TryParse(f[4], out long detail);
                long.TryParse(f[6], out long created);
                string location = f[5].Trim();
                if (location.Length > 0 && Array.IndexOf(ShareCodes.BuiltInParks, location) < 0) location = "";
                string creator = f.Length > 7 ? f[7].Trim() : "";
                if (!CodeLimits.IsSafeRecordId(creator)) creator = "";
                long maxDetail = kind == (int)GalleryKind.Park ? CustomPark.MaxPieces : ScoreLimits.MaxRunScore;
                list.Add(new GalleryEntry
                {
                    Id = id, Kind = (GalleryKind)kind,
                    Name = CodeLimits.Cap(f[2], CodeLimits.MaxRawNameLength), Author = CodeLimits.Cap(f[3], CodeLimits.MaxRawNameLength),
                    Detail = Math.Max(0, Math.Min(maxDetail, detail)), Location = location,
                    Created = CodeLimits.ClampCreated(created, now), Creator = creator,
                });
            }
            return list;
        }

        /// <summary>
        /// Phase 19: a downloaded post's code must be the kind the listing said (a park post holds a park code, a ghost
        /// post a ghost code) and must decode cleanly. Returns null when it's fine, or the message to show.
        /// </summary>
        public static string CheckDownload(GalleryKind kind, string code)
        {
            if (string.IsNullOrEmpty(code)) return "THAT POST IS EMPTY";
            if (kind == GalleryKind.Park)
            {
                if (code.Length > CodeLimits.MaxParkCodeChars) return "THAT POST IS TOO BIG";
                return ShareCodes.TryDecodePark(code, out _, out string err) ? null : "THAT POST IS BROKEN (" + err + ")";
            }
            if (code.Length > CodeLimits.MaxGhostCodeChars) return "THAT POST IS TOO BIG";
            return GhostCodes.TryDecode(code, out _, out string gerr) ? null : "THAT POST IS BROKEN (" + gerr + ")";
        }

        /// <summary>Line format the bridge echoes back (used by the editor's test gallery).</summary>
        public static string Format(GalleryEntry e) =>
            $"{Field(e.Id)}\t{(int)e.Kind}\t{Field(e.Name)}\t{Field(e.Author)}\t{e.Detail}\t{Field(e.Location)}\t{e.Created}\t{Field(e.Creator)}";

        private static string Field(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ');

        /// <summary>"5 MIN AGO", "3 H AGO", "2 DAYS AGO".</summary>
        public static string Age(long created, long now)
        {
            long s = Math.Max(0, now - created);
            if (s < 3600) return $"{Math.Max(1, s / 60)} MIN AGO";
            if (s < 86400) return $"{s / 3600} H AGO";
            long d = s / 86400;
            return d == 1 ? "1 DAY AGO" : $"{d} DAYS AGO";
        }
    }
}
