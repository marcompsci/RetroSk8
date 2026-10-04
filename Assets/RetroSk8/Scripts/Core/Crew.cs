using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>What a crew member brings when they ride with you.</summary>
    public enum CrewPerk
    {
        /// <summary>Banked combos score a little more.</summary>
        ScoreBoost = 0,
        /// <summary>Runs pay more Tape Tokens.</summary>
        TokenBoost = 1,
        /// <summary>The special meter fills faster.</summary>
        SpecialBoost = 2,
    }

    public enum RecruitKind
    {
        /// <summary>Beat them in a game of S.K.A.T.E.</summary>
        Duel = 0,
        /// <summary>Beat their score in a Two-Minute Run at their home park.</summary>
        Score = 1,
    }

    /// <summary>An original crew member (fictional; any resemblance to real skaters is unintended).</summary>
    public sealed class CrewMember
    {
        public string Id;
        public string Name;
        public SkaterStyle Style;
        public string HomePark;
        public string Bio;
        public RecruitKind Recruit;
        public DuelBot.Level DuelLevel;
        public long ScoreTarget;
        public CrewPerk Perk;
        /// <summary>Their look (Create-a-Skater values) for cards and ghost riders.</summary>
        public int SkinTone, HairStyle, HairColor;
    }

    /// <summary>The eight skaters you can recruit, each with a home park, a way to win them over and a perk.</summary>
    public static class CrewRoster
    {
        public const int MaxActive = 2;

        public static readonly CrewMember[] Members =
        {
            new CrewMember { Id = "pilar", Name = "Pilar \"Pier\" Ochoa", Style = SkaterStyle.Street, HomePark = "harbor_plaza", Recruit = RecruitKind.Score, ScoreTarget = 15000, Perk = CrewPerk.ScoreBoost,
                Bio = "Knows every crack in the harbor. Skates fast and lands everything clean.", SkinTone = 3, HairStyle = 4, HairColor = 0 },
            new CrewMember { Id = "dex", Name = "Dex Calloway", Style = SkaterStyle.Tech, HomePark = "neon_warehouse", Recruit = RecruitKind.Duel, DuelLevel = DuelBot.Level.Easy, Perk = CrewPerk.SpecialBoost,
                Bio = "Warehouse flatground wizard. Talks a lot of trash in S.K.A.T.E.", SkinTone = 5, HairStyle = 2, HairColor = 0 },
            new CrewMember { Id = "juno", Name = "Juno Park", Style = SkaterStyle.Vert, HomePark = "rooftop_run", Recruit = RecruitKind.Score, ScoreTarget = 18000, Perk = CrewPerk.TokenBoost,
                Bio = "Films every session from the rooftops. Will put you on the tape if you earn it.", SkinTone = 1, HairStyle = 3, HairColor = 6 },
            new CrewMember { Id = "marlo", Name = "Marlo Vance", Style = SkaterStyle.Flow, HomePark = "sunset_bowls", Recruit = RecruitKind.Duel, DuelLevel = DuelBot.Level.Medium, Perk = CrewPerk.ScoreBoost,
                Bio = "Bowl rat. Carves the deep end like it's a wave.", SkinTone = 6, HairStyle = 6, HairColor = 0 },
            new CrewMember { Id = "tess", Name = "Tess Ramirez", Style = SkaterStyle.Street, HomePark = "retro_city", Recruit = RecruitKind.Score, ScoreTarget = 20000, Perk = CrewPerk.TokenBoost,
                Bio = "Runs the Gridline Crew. Every ledge downtown has her wax on it.", SkinTone = 2, HairStyle = 1, HairColor = 4 },
            new CrewMember { Id = "kofi", Name = "Kofi Mensah", Style = SkaterStyle.Tech, HomePark = "harbor_plaza", Recruit = RecruitKind.Duel, DuelLevel = DuelBot.Level.Medium, Perk = CrewPerk.SpecialBoost,
                Bio = "Manual pad specialist. Never misses a nose manual.", SkinTone = 7, HairStyle = 5, HairColor = 0 },
            new CrewMember { Id = "rook", Name = "Rook Halvorsen", Style = SkaterStyle.Vert, HomePark = "sunset_bowls", Recruit = RecruitKind.Score, ScoreTarget = 24000, Perk = CrewPerk.ScoreBoost,
                Bio = "Old-school bowl legend who still drops in every day at sunset.", SkinTone = 0, HairStyle = 4, HairColor = 5 },
            new CrewMember { Id = "nia", Name = "Nia Okafor", Style = SkaterStyle.Flow, HomePark = "retro_city", Recruit = RecruitKind.Duel, DuelLevel = DuelBot.Level.Hard, Perk = CrewPerk.TokenBoost,
                Bio = "Undefeated at S.K.A.T.E. Beat her and the whole city hears about it.", SkinTone = 4, HairStyle = 2, HairColor = 7 },
        };

        public static CrewMember Find(string id)
        {
            foreach (var m in Members) if (m.Id == id) return m;
            return null;
        }

        public static string PerkText(CrewPerk p) =>
            p == CrewPerk.ScoreBoost ? "+6% POINTS" : p == CrewPerk.TokenBoost ? "+20% TAPE TOKENS" : "SPECIAL FILLS 25% FASTER";

        public static string RecruitText(CrewMember m) =>
            m.Recruit == RecruitKind.Duel ? $"BEAT THEM AT S.K.A.T.E. ({m.DuelLevel.ToString().ToUpperInvariant()})" : $"BEAT {m.ScoreTarget:N0} AT THEIR PARK";
    }

    /// <summary>Crew levels: XP comes from every banked combo (1 XP per 100 points).</summary>
    public static class CrewLevels
    {
        public static readonly long[] Thresholds = { 0, 300, 800, 1500, 2500, 4000, 6000, 8500, 11500, 15000 };
        public const int MaxLevel = 10;
        public const int PointsPerXp = 100;

        public static int LevelFor(long xp)
        {
            int level = 1;
            for (int i = 1; i < Thresholds.Length; i++) if (xp >= Thresholds[i]) level = i + 1;
            return level;
        }

        /// <summary>0..1 toward the next level (1 at max).</summary>
        public static float Progress(long xp)
        {
            int level = LevelFor(xp);
            if (level >= MaxLevel) return 1f;
            long a = Thresholds[level - 1], b = Thresholds[level];
            return (xp - a) / (float)(b - a);
        }

        /// <summary>Tokens paid when you reach a level.</summary>
        public static int RewardTokens(int level) => 25 * level;

        public static string Title(int level) =>
            level >= 10 ? "CITY LEGENDS" : level >= 7 ? "LOCAL HEROES" : level >= 4 ? "STREET CREW" : "NEW CREW";
    }

    /// <summary>Saved crew progress: who you've recruited, who rides with you, and crew XP. JsonUtility-friendly.</summary>
    [Serializable]
    public sealed class CrewState
    {
        public List<string> recruited = new List<string>();
        public List<string> active = new List<string>();
        public long xp;

        public int Level => CrewLevels.LevelFor(xp);
        public bool IsRecruited(string id) => recruited.Contains(id);
        public bool IsActive(string id) => active.Contains(id);

        /// <summary>True the first time. New recruits ride with you right away when there's room.</summary>
        public bool Recruit(string id)
        {
            if (CrewRoster.Find(id) == null || recruited.Contains(id)) return false;
            recruited.Add(id);
            if (active.Count < CrewRoster.MaxActive) active.Add(id);
            return true;
        }

        /// <summary>Puts a recruited member in or out of your riding pair. False when full or not recruited.</summary>
        public bool ToggleActive(string id)
        {
            if (active.Remove(id)) return true;
            if (!recruited.Contains(id) || active.Count >= CrewRoster.MaxActive) return false;
            active.Add(id);
            return true;
        }

        /// <summary>Adds XP; returns how many levels were gained (each pays <see cref="CrewLevels.RewardTokens"/>).</summary>
        public int AddXp(long amount, out int tokens)
        {
            tokens = 0;
            if (amount <= 0) return 0;
            int before = Level;
            xp += amount;
            int after = Level;
            for (int l = before + 1; l <= after; l++) tokens += CrewLevels.RewardTokens(l);
            return after - before;
        }

        /// <summary>Combined perks of the active riders.</summary>
        public void Perks(out float scoreFactor, out float tokenFactor, out float specialFactor)
        {
            scoreFactor = tokenFactor = specialFactor = 1f;
            foreach (var id in active)
            {
                var m = CrewRoster.Find(id);
                if (m == null) continue;
                if (m.Perk == CrewPerk.ScoreBoost) scoreFactor += 0.06f;
                else if (m.Perk == CrewPerk.TokenBoost) tokenFactor += 0.2f;
                else specialFactor += 0.25f;
            }
        }

        public void Sanitize()
        {
            if (recruited == null) recruited = new List<string>();
            if (active == null) active = new List<string>();
            recruited.RemoveAll(id => CrewRoster.Find(id) == null);
            active.RemoveAll(id => !recruited.Contains(id));
            while (active.Count > CrewRoster.MaxActive) active.RemoveAt(active.Count - 1);
            if (xp < 0) xp = 0;
        }
    }
}
