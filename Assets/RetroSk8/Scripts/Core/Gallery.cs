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

        public void Sanitize()
        {
            if (hidden == null) hidden = new List<string>();
            if (blockedAuthors == null) blockedAuthors = new List<string>();
            if (reported == null) reported = new List<string>();
            if (posted == null) posted = new List<string>();
            Trim(hidden); Trim(blockedAuthors); Trim(reported); Trim(posted);
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

        public static readonly string[] ReportReasons = { "OFFENSIVE NAME", "SPAM", "BROKEN OR EMPTY", "OTHER" };

        // A short list of words no player-typed text may contain. Matching ignores spacing, punctuation and common
        // number swaps (so "B4D W0RD" style tricks still match). Extend it as reports come in.
        private static readonly string[] Blocked =
        {
            "FUCK", "SHIT", "CUNT", "BITCH", "DICK", "COCK", "PUSSY", "WHORE", "SLUT", "FAG", "NIGG", "RETARD",
            "RAPE", "NAZI", "HITLER", "KILLYOURSELF", "KYS", "PENIS", "VAGINA", "PORN",
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
            var sb = new StringBuilder();
            foreach (char raw in text.ToUpperInvariant())
            {
                char c = raw == '0' ? 'O' : raw == '1' ? 'I' : raw == '3' ? 'E' : raw == '4' ? 'A' : raw == '5' ? 'S' : raw == '7' ? 'T' : raw == '@' ? 'A' : raw == '$' ? 'S' : raw;
                if (c >= 'A' && c <= 'Z') sb.Append(c);
            }
            string squashed = sb.ToString();
            string ones = squashed.Replace('I', 'L'); // "1" can stand in for L too
            foreach (var w in Blocked)
                if (squashed.Contains(w) || ones.Contains(w)) return true;
            return false;
        }

        /// <summary>The name shown for player text: cleaned, and replaced when it trips the filter.</summary>
        public static string SafeName(string text, int max, string fallback)
        {
            string clean = CleanName(text, max);
            return clean.Length == 0 || IsBlocked(clean) ? fallback : clean;
        }

        public static bool CanUpload(GalleryState s, int today) => s != null && (s.uploadDay != today || s.uploadsToday < UploadsPerDay);

        public static void CountUpload(GalleryState s, int today, string id)
        {
            if (s.uploadDay != today) { s.uploadDay = today; s.uploadsToday = 0; }
            s.uploadsToday++;
            if (!string.IsNullOrEmpty(id)) s.posted.Add(id);
            s.Sanitize();
        }

        /// <summary>What you see: your hidden posts and blocked authors are gone, and names are filtered.</summary>
        public static List<GalleryEntry> Visible(IEnumerable<GalleryEntry> entries, GalleryState s)
        {
            var list = new List<GalleryEntry>();
            if (entries == null) return list;
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.Id)) continue;
                if (s != null && (s.hidden.Contains(e.Id) || s.blockedAuthors.Contains(e.Author ?? ""))) continue;
                list.Add(e);
            }
            return list;
        }

        public static string DisplayName(GalleryEntry e) => SafeName(e?.Name, CustomPark.MaxNameLength, e != null && e.Kind == GalleryKind.Ghost ? "A RUN" : "A PARK");

        public static string DisplayAuthor(GalleryEntry e) => SafeName(e?.Author, ShareCodes.MaxFromLength, "SKATER");

        // ---------------------------------------------------------------- wire format

        /// <summary>One entry per line: id, kind, name, author, detail, location, created (tab separated).</summary>
        public static List<GalleryEntry> Parse(string text)
        {
            var list = new List<GalleryEntry>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (var line in text.Split('\n'))
            {
                var f = line.Split('\t');
                if (f.Length < 7 || string.IsNullOrEmpty(f[0])) continue;
                if (!int.TryParse(f[1], out int kind) || kind < 0 || kind > 1) continue;
                long.TryParse(f[4], out long detail);
                long.TryParse(f[6], out long created);
                list.Add(new GalleryEntry
                {
                    Id = f[0].Trim(), Kind = (GalleryKind)kind, Name = f[2], Author = f[3],
                    Detail = detail, Location = f[5].Trim(), Created = created,
                });
            }
            return list;
        }

        /// <summary>Line format the bridge echoes back (used by the editor's test gallery).</summary>
        public static string Format(GalleryEntry e) =>
            $"{Field(e.Id)}\t{(int)e.Kind}\t{Field(e.Name)}\t{Field(e.Author)}\t{e.Detail}\t{Field(e.Location)}\t{e.Created}";

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
