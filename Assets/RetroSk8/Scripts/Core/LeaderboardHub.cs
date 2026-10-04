using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    public enum BoardGroup { Parks = 0, Races = 1, Challenges = 2, Events = 3 }

    public enum BoardFormat
    {
        Points = 0,
        /// <summary>Hundredths of a second, lower is better (race boards).</summary>
        Time = 1,
        /// <summary>A plain count (S.K.A.T.E. wins).</summary>
        Count = 2,
    }

    /// <summary>One Game Center leaderboard as the hub lists it.</summary>
    public sealed class BoardInfo
    {
        public string Id;
        public string Title;
        /// <summary>The park, race or spot id the board belongs to (empty for events).</summary>
        public string SourceId = "";
        public BoardGroup Group;
        public BoardFormat Format;
        public bool LowerIsBetter => Format == BoardFormat.Time;
    }

    /// <summary>A page of leaderboard rows from the native bridge, plus the board's total player count.</summary>
    public sealed class BoardPage
    {
        public readonly List<FriendScore> Rows = new List<FriendScore>();
        public int Total;

        public FriendScore You => Rows.Find(r => r.IsYou);

        /// <summary>
        /// Parses "rank\tname\tscore\tisLocal" rows plus an optional "#total\tN" line. Duplicate ranks (the bridge
        /// appends your own row when it's outside the page) keep the first.
        /// </summary>
        public static BoardPage Parse(string text)
        {
            var page = new BoardPage();
            if (string.IsNullOrEmpty(text)) return page;
            foreach (var line in text.Split('\n'))
            {
                if (!line.StartsWith("#total\t", StringComparison.Ordinal)) continue;
                int.TryParse(line.Substring(7).Trim(), out page.Total);
            }
            var seen = new HashSet<int>();
            foreach (var r in FriendScore.Parse(text))
                if (r.Rank > 0 && seen.Add(r.Rank)) page.Rows.Add(r);
            if (page.Total < page.Rows.Count) page.Total = page.Rows.Count;
            return page;
        }
    }

    /// <summary>
    /// The Leaderboards hub (Phase 14): every Game Center board in one place, with your rank and the player to beat
    /// next. Engine-free: board list, formatting and the "next target" rule are unit-tested.
    /// </summary>
    public static class LeaderboardHub
    {
        public const int PageSize = 10;

        /// <summary>All boards: one per park, then the city races and spot challenges, then the events.</summary>
        public static List<BoardInfo> Boards(IEnumerable<(string id, string name)> parks)
        {
            var list = new List<BoardInfo>();
            if (parks != null)
                foreach (var (id, name) in parks)
                    list.Add(new BoardInfo { Id = Achievements.LeaderboardId(id), SourceId = id, Title = (name ?? id).ToUpperInvariant(), Group = BoardGroup.Parks });
            foreach (var r in RetroCityLayout.Races)
                list.Add(new BoardInfo { Id = Leaderboards.Race(r.Id), SourceId = r.Id, Title = r.Name.ToUpperInvariant(), Group = BoardGroup.Races, Format = BoardFormat.Time });
            foreach (var s in RetroCityLayout.Spots)
                list.Add(new BoardInfo { Id = Leaderboards.Challenge(s.Id), SourceId = s.Id, Title = s.Name.ToUpperInvariant(), Group = BoardGroup.Challenges });
            list.Add(new BoardInfo { Id = WeeklyEvents.LeaderboardId, Title = "THIS WEEK'S BEST RUN", Group = BoardGroup.Events });
            list.Add(new BoardInfo { Id = Leaderboards.SkateWins, Title = "S.K.A.T.E. WINS", Group = BoardGroup.Events, Format = BoardFormat.Count });
            return list;
        }

        public static string GroupName(BoardGroup g) =>
            g == BoardGroup.Parks ? "PARKS" : g == BoardGroup.Races ? "RACES" : g == BoardGroup.Challenges ? "SPOT CHALLENGES" : "EVENTS";

        public static string Format(long score, BoardFormat format)
        {
            if (format == BoardFormat.Count) return score.ToString("N0");
            if (format != BoardFormat.Time) return score.ToString("N0");
            if (score <= 0) return "--";
            long minutes = score / 6000, rest = score % 6000;
            return minutes > 0 ? $"{minutes}:{rest / 100:00}.{rest % 100:00}" : $"{rest / 100}.{rest % 100:00}";
        }

        /// <summary>Is <paramref name="a"/> a better score than <paramref name="b"/> on this board?</summary>
        public static bool Better(long a, long b, BoardFormat format) => format == BoardFormat.Time ? a > 0 && (b <= 0 || a < b) : a > b;

        /// <summary>
        /// The next player to chase: the closest-ranked player above you on the page. Null when you're #1 or
        /// nobody above you is on the page. When you're not ranked, it's the last player on the page.
        /// </summary>
        public static FriendScore NextTarget(BoardPage page)
        {
            if (page == null || page.Rows.Count == 0) return null;
            var you = page.You;
            FriendScore best = null;
            foreach (var r in page.Rows)
            {
                if (r.IsYou) continue;
                if (you != null && r.Rank >= you.Rank) continue;
                if (best == null || r.Rank > best.Rank) best = r;
            }
            return best;
        }

        /// <summary>The "beat this" line under the board.</summary>
        public static string TargetText(BoardPage page, BoardInfo board, long localBest)
        {
            var fmt = board?.Format ?? BoardFormat.Points;
            var you = page?.You;
            var target = NextTarget(page);
            if (you != null && you.Rank == 1) return "YOU'RE #1. DEFEND IT.";
            if (target == null)
                return localBest > 0 ? $"YOUR BEST: {Format(localBest, fmt)} · POST IT TO GET RANKED" : "NO SCORE YET · GO SET ONE";
            long mine = you != null ? you.Score : localBest;
            string name = target.Name.Length > 16 ? target.Name.Substring(0, 16) : target.Name;
            if (mine <= 0) return $"NEXT UP: #{target.Rank} {name} · {Format(target.Score, fmt)}";
            long gap = fmt == BoardFormat.Time ? mine - target.Score : target.Score - mine;
            string gapText = fmt == BoardFormat.Time ? Format(Math.Max(1, gap + 1), fmt) + "S FASTER" : (fmt == BoardFormat.Count ? $"{Math.Max(1, gap + 1)} MORE WIN{(gap + 1 == 1 ? "" : "S")}" : $"{Math.Max(1, gap + 1):N0} MORE");
            return $"BEAT #{target.Rank} {name}: {gapText}";
        }

        /// <summary>True when the page skips ranks before row <paramref name="index"/> (draw a "…" divider).</summary>
        public static bool GapBefore(BoardPage page, int index) =>
            page != null && index > 0 && index < page.Rows.Count && page.Rows[index].Rank > page.Rows[index - 1].Rank + 1;
    }
}
