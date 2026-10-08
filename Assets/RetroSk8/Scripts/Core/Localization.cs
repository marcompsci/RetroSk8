using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RetroSk8.Core
{
    /// <summary>Interface languages (Phase 28). Saved by number: append only.</summary>
    public enum Lang { English = 0, Spanish = 1, Japanese = 2 }

    /// <summary>
    /// Retro Sk8's translations (Phase 28). The game's text is written in English in code; this turns any finished
    /// English string into the chosen language, so screens don't need translation keys:
    ///   1. an exact match in the language's table (case-insensitive for ALL-CAPS text, which comes back in capitals);
    ///   2. multi-line text and " · "-separated parts are translated piece by piece;
    ///   3. templates: a table entry like "BEST {0}" matches "BEST 12,400", and each {n} is itself translated when
    ///      it's a known phrase (park names, trick names...) and left alone otherwise (numbers, player names).
    /// Anything unknown stays in English. Engine-free and unit-tested; the runtime calls <see cref="T"/>.
    /// </summary>
    public static class Loc
    {
        public static readonly string[] LangNames = { "ENGLISH", "ESPAÑOL", "日本語" };
        public static readonly string[] LangCodes = { "en", "es", "ja" };
        public const int LangCount = 3;
        private const int MaxCache = 4096;
        private const int MaxDepth = 5;

        private static Lang s_current = Lang.English;
        private static readonly Dictionary<Lang, Table> s_tables = new Dictionary<Lang, Table>();
        private static readonly Dictionary<string, string> s_cache = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Raised after the language changes (the runtime re-translates what's on screen).</summary>
        public static event Action Changed;

        public static Lang Current => s_current;

        public static void Set(Lang lang)
        {
            lang = (int)lang < 0 || (int)lang >= LangCount ? Lang.English : lang;
            if (lang == s_current) return;
            s_current = lang;
            s_cache.Clear();
            Changed?.Invoke();
        }

        /// <summary>The saved choice (-1 follows the phone) resolved against the phone's language code ("es", "ja-JP"...).</summary>
        public static Lang Resolve(int saved, string systemLanguage)
        {
            if (saved >= 0 && saved < LangCount) return (Lang)saved;
            string s = (systemLanguage ?? "").Trim().ToLowerInvariant();
            if (s.StartsWith("es") || s == "spanish") return Lang.Spanish;
            if (s.StartsWith("ja") || s == "japanese") return Lang.Japanese;
            return Lang.English;
        }

        /// <summary>Registers a language's table: pairs of (English, translation), placeholders as {0}, {1}...</summary>
        public static void Register(Lang lang, IEnumerable<KeyValuePair<string, string>> pairs)
        {
            var t = new Table(lang);
            foreach (var kv in pairs) t.Add(kv.Key, kv.Value);
            t.Finish();
            s_tables[lang] = t;
            if (lang == s_current) s_cache.Clear();
        }

        /// <summary>Parses the table text format: one entry per line, "English", a tab, then the translation.</summary>
        public static List<KeyValuePair<string, string>> Parse(string tsv)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrEmpty(tsv)) return list;
            foreach (var raw in tsv.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                int tab = line.IndexOf('\t');
                if (tab <= 0 || tab == line.Length - 1) continue;
                list.Add(new KeyValuePair<string, string>(Unescape(line.Substring(0, tab)), Unescape(line.Substring(tab + 1))));
            }
            return list;
        }

        /// <summary>"\n" in a table line stands for a line break.</summary>
        public static string Unescape(string s) => s.Replace("\\n", "\n").Replace("\\t", "\t");

        public static bool HasTable(Lang lang) => lang == Lang.English || s_tables.ContainsKey(lang);
        public static int EntryCount(Lang lang) => s_tables.TryGetValue(lang, out var t) ? t.Count : 0;

        /// <summary>Translates <paramref name="english"/> into the current language (or returns it unchanged).</summary>
        public static string T(string english) => Translate(english, s_current);

        public static string Translate(string english, Lang lang)
        {
            if (lang == Lang.English || string.IsNullOrEmpty(english) || !HasLetters(english)) return english;
            if (!s_tables.TryGetValue(lang, out var table)) return english;
            bool current = lang == s_current;
            if (current && s_cache.TryGetValue(english, out var cached)) return cached;
            string result = table.Translate(english, 0) ?? english;
            if (current)
            {
                if (s_cache.Count >= MaxCache) s_cache.Clear();
                s_cache[english] = result;
            }
            return result;
        }

        /// <summary>Translates with a one-off table (tests check the matching rules without the real tables).</summary>
        public static string TranslateWithTable(IEnumerable<KeyValuePair<string, string>> pairs, Lang lang, string english)
        {
            var t = new Table(lang);
            foreach (var kv in pairs) t.Add(kv.Key, kv.Value);
            t.Finish();
            if (string.IsNullOrEmpty(english) || !HasLetters(english)) return english;
            return t.Translate(english, 0) ?? english;
        }

        /// <summary>True when the text is (or would be) fully translated: for tests and the coverage report.</summary>
        public static bool Knows(string english, Lang lang)
        {
            if (lang == Lang.English || !HasLetters(english)) return true;
            if (!s_tables.TryGetValue(lang, out var t)) return false;
            t.Strict = true;
            try { return t.Translate(english, 0) != null; }
            finally { t.Strict = false; }
        }

        /// <summary><see cref="Knows"/> with a one-off table (tests).</summary>
        public static bool KnowsWithTable(IEnumerable<KeyValuePair<string, string>> pairs, Lang lang, string english)
        {
            if (lang == Lang.English || !HasLetters(english)) return true;
            var t = new Table(lang);
            foreach (var kv in pairs) t.Add(kv.Key, kv.Value);
            t.Finish();
            t.Strict = true;
            return t.Translate(english, 0) != null;
        }

        private static bool HasLetters(string s)
        {
            foreach (char c in s) if (char.IsLetter(c)) return true;
            return false;
        }

        // ---------------------------------------------------------------- one language's table

        private sealed class Template
        {
            public Regex Pattern;
            public string Output;
            public char First; // upper-case first character of the leading text ('\0' when it starts with a placeholder)
            public bool MultiLine; // only templates with a line break may match text with one
            public int Literal; // letters outside the placeholders: longer templates are tried first
        }

        private sealed class Table
        {
            private readonly Lang _lang;
            private readonly CultureInfo _culture;
            private readonly Dictionary<string, string> _exact = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> _upper = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly List<Template> _templates = new List<Template>();
            private static readonly Regex Placeholder = new Regex(@"\{(\d)\}");
            private static readonly string[] Separators = { "  ·  ", " · ", "   ", " | " };

            public Table(Lang lang)
            {
                _lang = lang;
                _culture = lang == Lang.Spanish ? new CultureInfo("es-ES") : CultureInfo.InvariantCulture;
            }

            public int Count => _exact.Count;

            public void Add(string english, string translated)
            {
                if (string.IsNullOrEmpty(english) || translated == null) return;
                if (Placeholder.IsMatch(english))
                {
                    var t = MakeTemplate(english, translated);
                    if (t != null) _templates.Add(t);
                    return;
                }
                _exact[english] = translated;
                _upper[english.ToUpperInvariant()] = translated;
            }

            // Templates by the first character of their leading text, so a string only tries the few that could match.
            private readonly Dictionary<char, List<Template>> _byFirst = new Dictionary<char, List<Template>>();
            private readonly List<Template> _leadingPlaceholder = new List<Template>();

            public void Finish()
            {
                _templates.Sort((a, b) => b.Literal.CompareTo(a.Literal));
                _byFirst.Clear();
                _leadingPlaceholder.Clear();
                foreach (var t in _templates)
                {
                    if (t.First == '\0') { _leadingPlaceholder.Add(t); continue; }
                    if (!_byFirst.TryGetValue(t.First, out var list)) _byFirst[t.First] = list = new List<Template>();
                    list.Add(t);
                }
            }

            private static Template MakeTemplate(string english, string translated)
            {
                var sb = new StringBuilder("^");
                int last = 0, literal = 0;
                var seen = new HashSet<int>();
                foreach (Match m in Placeholder.Matches(english))
                {
                    string lit = english.Substring(last, m.Index - last);
                    foreach (char c in lit) if (char.IsLetter(c)) literal++;
                    sb.Append(Regex.Escape(lit));
                    int n = m.Groups[1].Value[0] - '0';
                    sb.Append(seen.Add(n) ? $"(?<p{n}>.+?)" : $"\\k<p{n}>");
                    last = m.Index + m.Length;
                }
                string tail = english.Substring(last);
                foreach (char c in tail) if (char.IsLetter(c)) literal++;
                sb.Append(Regex.Escape(tail)).Append('$');
                if (literal < 2) return null; // "{0} {1}" would match anything
                // "{0} AT {1}" would swallow whole sentences; "{0}  ·  BEST {1}" is anchored by its separator.
                if (english.StartsWith("{") && literal < 5 && english.IndexOf('·') < 0 && english.IndexOf(':') < 0) return null;
                // Case-insensitive: a mixed-case template also matches the same text shown in capitals.
                return new Template
                {
                    Pattern = new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
                    Output = translated,
                    Literal = literal,
                    First = english.Length > 0 && english[0] != '{' ? char.ToUpperInvariant(english[0]) : '\0',
                    MultiLine = english.IndexOf('\n') >= 0,
                };
            }

            /// <returns>The translation, or null when nothing (or only part) is known.</returns>
            public string Translate(string s, int depth)
            {
                if (_exact.TryGetValue(s, out var hit)) return hit;
                string trimmed = s.Trim();
                if (trimmed.Length != s.Length && trimmed.Length > 0)
                {
                    var inner = Translate(trimmed, depth);
                    if (inner != null) return s.Substring(0, s.IndexOf(trimmed[0])) + inner + s.Substring(s.IndexOf(trimmed[0]) + trimmed.Length);
                    return null;
                }
                bool caps = IsAllCaps(s);
                if (caps && _upper.TryGetValue(s, out hit)) return Caps(hit);
                if (depth >= MaxDepth) return null;

                // A leading box or bullet ("□ ", "■ ", "• ") comes off first, so templates see the text itself.
                var lead = Lead.Match(s);
                if (lead.Success && lead.Length < s.Length && s.IndexOf('\n') < 0)
                {
                    string rest = Translate(s.Substring(lead.Length), depth + 1);
                    return rest != null ? lead.Value + rest : null;
                }

                var templated = Templates(s, depth, caps);
                if (templated != null) return templated;

                // Lines, then separated parts, each on their own.
                if (s.IndexOf('\n') >= 0) return Join(s.Split('\n'), "\n", depth);
                foreach (var sep in Separators)
                    if (s.Contains(sep)) return Join(s.Split(new[] { sep }, StringSplitOptions.None), sep, depth);

                // A decoration around known text: a leading box or bullet ("□ ", "■ ", "• ") or a trailing count ("  1/3").
                var d = Decor.Match(s);
                if (d.Success && (d.Groups[1].Length > 0 || d.Groups[3].Length > 0) && HasLetters(d.Groups[2].Value))
                {
                    string core = Translate(d.Groups[2].Value, depth + 1);
                    if (core != null) return d.Groups[1].Value + core + d.Groups[3].Value;
                }

                return Around(s, depth);
            }

            /// <summary>
            /// Text glued from a known phrase and something else ("LAND A BIG ONE AT" + a park name): translates the
            /// known start (or end) and whatever it can of the rest. The known part must be a real phrase (two or more
            /// words, or ending in ':' or '!'), so a single common word doesn't half-translate a sentence.
            /// </summary>
            private string Around(string s, int depth)
            {
                if (s.Length > 240 || Strict) return null;
                for (int i = s.LastIndexOf(' '); i > 0; i = s.LastIndexOf(' ', i - 1))
                {
                    string head = s.Substring(0, i).TrimEnd();
                    if (!Phrase(head) || !Known(head, out var t)) continue;
                    string rest = s.Substring(i + 1);
                    return t + " " + (HasLetters(rest) ? Translate(rest, depth + 1) ?? rest : rest);
                }
                for (int i = s.IndexOf(' '); i >= 0 && i < s.Length - 1; i = s.IndexOf(' ', i + 1))
                {
                    string tail = s.Substring(i + 1).TrimStart();
                    if (!Phrase(tail) || !Known(tail, out var t)) continue;
                    string rest = s.Substring(0, i);
                    return (HasLetters(rest) ? Translate(rest, depth + 1) ?? rest : rest) + " " + t;
                }
                return null;
            }

            private static bool Phrase(string p)
            {
                if (p.Length < 3) return false;
                char last = p[p.Length - 1];
                return p.IndexOf(' ') > 0 || last == ':' || last == '!';
            }

            private bool Known(string p, out string t)
            {
                if (_exact.TryGetValue(p, out t)) return true;
                bool caps = IsAllCaps(p);
                if (caps && _upper.TryGetValue(p, out t)) { t = Caps(t); return true; }
                t = Templates(p, MaxDepth - 1, caps);
                return t != null;
            }

            private static readonly Regex Lead = new Regex(@"^\s*[■□•★☆>»]+\s*");
            private static readonly Regex Decor = new Regex(@"^([\s■□•★☆·>»\-–]+)?(.*?)(\s+[+\-]?\d[\d,./:%×x]*)?$", RegexOptions.Singleline);

            /// <summary>A template matching the whole text, with its placeholders filled (known names translated).</summary>
            private string Templates(string s, int depth, bool caps)
            {
                if (s.Length == 0) return null;
                bool multiLine = s.IndexOf('\n') >= 0;
                _byFirst.TryGetValue(char.ToUpperInvariant(s[0]), out var first);
                int n1 = first?.Count ?? 0, n = n1 + _leadingPlaceholder.Count;
                for (int i = 0; i < n; i++)
                {
                    var t = i < n1 ? first[i] : _leadingPlaceholder[i - n1];
                    if (multiLine && !t.MultiLine) continue; // a one-line template must not swallow several lines
                    var m = t.Pattern.Match(s);
                    if (!m.Success) continue;
                    string output = Placeholder.Replace(t.Output, p =>
                    {
                        var g = m.Groups["p" + p.Groups[1].Value];
                        if (!g.Success) return p.Value;
                        bool strict = Strict;
                        Strict = false; // a placeholder's value (a name, a number) needn't be known
                        try { return Translate(g.Value, depth + 1) ?? g.Value; }
                        finally { Strict = strict; }
                    });
                    return caps ? Caps(output) : output;
                }
                return null;
            }

            /// <summary>While true, only complete translations count (no half-translated joins or glued phrases).</summary>
            public bool Strict;

            private string Join(string[] parts, string sep, int depth)
            {
                var sb = new StringBuilder();
                bool any = false;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i > 0) sb.Append(sep);
                    string p = parts[i];
                    if (p.Length == 0 || !HasLetters(p)) { sb.Append(p); continue; }
                    string t = Translate(p, depth + 1);
                    if (t != null) any = true;
                    else if (Strict) return null;
                    sb.Append(t ?? p);
                }
                return any ? sb.ToString() : null;
            }

            private static bool IsAllCaps(string s)
            {
                bool letter = false;
                foreach (char c in s)
                {
                    if (!char.IsLetter(c)) continue;
                    letter = true;
                    if (char.IsLower(c)) return false;
                }
                return letter;
            }

            private string Caps(string s) => _lang == Lang.Japanese ? s : s.ToUpper(_culture);
        }
    }
}
