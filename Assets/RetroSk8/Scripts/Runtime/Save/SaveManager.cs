using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RetroSk8.Save
{
    [Serializable]
    public sealed class SettingsData
    {
        public float musicVolume = 0.6f;
        public float effectsVolume = 1f;
        public float ambienceVolume = 0.7f;
        public bool hapticsEnabled = true;
        public bool showTouchControlsInEditor = true;
        /// <summary>Hide the best-run ghost in Two-Minute Runs. Stored inverted so older saves default to showing it.</summary>
        public bool ghostHidden;
        /// <summary>Record each run with ReplayKit so it can be shared from Results (iOS only, asks permission).</summary>
        public bool recordClips;
        /// <summary>The first-run lesson was finished or skipped.</summary>
        public bool tutorialDone;
        /// <summary>The lesson's one-time Tape Token reward was paid.</summary>
        public bool tutorialRewarded;

        // Phase 7: controls and accessibility.
        public RetroSk8.Core.TouchLayout touchLayout = RetroSk8.Core.TouchLayout.Default();
        /// <summary>No camera shake, speed-FOV punch or speed lines.</summary>
        public bool reducedMotion;
        /// <summary>Small UI text drawn ~15% larger.</summary>
        public bool largeText;
        /// <summary>HUD success/fail colours swap teal/coral for blue/orange (safe for red-green colour blindness).</summary>
        public bool colorSafe;
        /// <summary>Turns off bloom/post-processing and particles (battery or older phones).</summary>
        public bool lowEffects;

        // Phase 10
        /// <summary>Shown on challenge codes and in online S.K.A.T.E.</summary>
        public string playerName = "SKATER";

        // Phase 12
        /// <summary>RetroSk8.Core.MusicMode: 0 park themes, 1 radio, 2 off.</summary>
        public int musicMode;
        public int radioStation;
        /// <summary>Distant crowd cheers for big lines in the parks.</summary>
        public bool crowdOff;

        // Phase 15
        /// <summary>Opt-in local reminders (streak, Daily Line, weekly event). Off by default.</summary>
        public bool reminders;
    }

    [Serializable]
    public sealed class LocationRecord
    {
        public string locationId;
        public long bestScore;
        public long bestCombo;
        public string bestComboLabel;
    }

    [Serializable]
    public sealed class ContractRecord
    {
        public string locationId;
        public List<string> completedGoalIds = new List<string>();
        public bool allComplete;
    }

    [Serializable]
    public sealed class DailyRecord
    {
        public int claimedDate;   // yyyymmdd of the last Daily Line bonus paid
        public int bestDate;      // yyyymmdd that bestScore belongs to
        public long bestScore;
        /// <summary>How many days the Daily Line bonus was earned (achievements).</summary>
        public int clears;
    }

    /// <summary>Lifetime counters behind the achievements.</summary>
    [Serializable]
    public sealed class ProgressStats
    {
        public int combosBanked;
        public int maxHalfTurns;
        public List<string> gapIds = new List<string>();
        public List<string> parksPlayed = new List<string>();
    }

    [Serializable]
    public sealed class EquipEntry
    {
        public int slot;
        public string id;
    }

    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public int tapeTokens;
        public List<LocationRecord> locations = new List<LocationRecord>();
        public List<string> ownedCosmetics = new List<string>();
        public List<EquipEntry> equipped = new List<EquipEntry>();
        public List<ContractRecord> contracts = new List<ContractRecord>();
        public DailyRecord daily = new DailyRecord();
        public SettingsData settings = new SettingsData();
        public ProgressStats stats = new ProgressStats();
        /// <summary>Unlocked achievement ids (RetroSk8.Core.Achievements).</summary>
        public List<string> achievements = new List<string>();
        /// <summary>Retro City: spots found, tapes collected, challenge and race medals.</summary>
        public RetroSk8.Core.CityProgress city = new RetroSk8.Core.CityProgress();
        /// <summary>Create-a-Park layouts (one per slot, ids "custom_1".."custom_6").</summary>
        public List<RetroSk8.Core.CustomPark> customParks = new List<RetroSk8.Core.CustomPark>();
        /// <summary>Career chapters paid and goals announced.</summary>
        public RetroSk8.Core.CareerState career = new RetroSk8.Core.CareerState();
        /// <summary>Create-a-Skater body/face choices and the board maker graphic.</summary>
        public RetroSk8.Core.SkaterLook look = new RetroSk8.Core.SkaterLook();
        /// <summary>Crew mode: recruits, riding pair, crew XP.</summary>
        public RetroSk8.Core.CrewState crew = new RetroSk8.Core.CrewState();
        /// <summary>This week's event counters and paid goals.</summary>
        public RetroSk8.Core.WeeklyState weekly = new RetroSk8.Core.WeeklyState();
        /// <summary>Games of S.K.A.T.E. won (Game Center leaderboard).</summary>
        public int skateWins;
        /// <summary>Cosmetic packs bought with real money (App Store product ids; restored from the store).</summary>
        public List<string> ownedPacks = new List<string>();
        /// <summary>Friends' ghosts you've raced (newest first, capped).</summary>
        public List<RetroSk8.Core.RivalRecord> rivals = new List<RetroSk8.Core.RivalRecord>();
        /// <summary>Story mode: steps cleared.</summary>
        public RetroSk8.Core.StoryState story = new RetroSk8.Core.StoryState();
        /// <summary>The guided first session has been shown (Phase 13 onboarding).</summary>
        public bool onboardingDone;
        /// <summary>One-time hints already shown.</summary>
        public RetroSk8.Core.TipState tips = new RetroSk8.Core.TipState();
        /// <summary>Trick Book records and challenge claims (Phase 14).</summary>
        public RetroSk8.Core.TrickBookState trickBook = new RetroSk8.Core.TrickBookState();
        /// <summary>Daily streak (Phase 15).</summary>
        public RetroSk8.Core.StreakState streak = new RetroSk8.Core.StreakState();
        /// <summary>Today's City Jam medal and jam stats (Phase 15).</summary>
        public RetroSk8.Core.JamRecord jam = new RetroSk8.Core.JamRecord();

        public ContractRecord Contract(string locationId)
        {
            foreach (var c in contracts) if (c.locationId == locationId) return c;
            var rec = new ContractRecord { locationId = locationId };
            contracts.Add(rec);
            return rec;
        }

        public LocationRecord Record(string locationId)
        {
            foreach (var r in locations) if (r.locationId == locationId) return r;
            var rec = new LocationRecord { locationId = locationId };
            locations.Add(rec);
            return rec;
        }
    }

    /// <summary>Local JSON persistence (MVP). Writes atomically via a temp file so a crash can't corrupt the save.</summary>
    public static class SaveManager
    {
        private const string FileName = "retrosk8_save.json";
        private static SaveData s_data;
        private static string s_fileOverride;

        public static string FilePath => Path.Combine(Application.persistentDataPath, s_fileOverride ?? FileName);

        /// <summary>Redirects saving to another file (tests use this so they never touch the player's progress). Null restores the default.</summary>
        public static void UseFile(string fileName)
        {
            s_fileOverride = fileName;
            s_data = null;
        }

        public static SaveData Data
        {
            get
            {
                if (s_data == null) Load();
                return s_data;
            }
        }

        public static event Action Changed;

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    s_data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RetroSk8] Save file unreadable, starting fresh. {e.Message}");
                s_data = null;
            }
            if (s_data == null) s_data = new SaveData();
            if (s_data.settings == null) s_data.settings = new SettingsData();
            if (s_data.locations == null) s_data.locations = new List<LocationRecord>();
            if (s_data.ownedCosmetics == null) s_data.ownedCosmetics = new List<string>();
            if (s_data.equipped == null) s_data.equipped = new List<EquipEntry>();
            if (s_data.contracts == null) s_data.contracts = new List<ContractRecord>();
            if (s_data.daily == null) s_data.daily = new DailyRecord();
            if (s_data.stats == null) s_data.stats = new ProgressStats();
            if (s_data.stats.gapIds == null) s_data.stats.gapIds = new List<string>();
            if (s_data.stats.parksPlayed == null) s_data.stats.parksPlayed = new List<string>();
            if (s_data.achievements == null) s_data.achievements = new List<string>();
            if (s_data.city == null) s_data.city = new RetroSk8.Core.CityProgress();
            if (s_data.city.spots == null) s_data.city.spots = new List<string>();
            if (s_data.city.tapes == null) s_data.city.tapes = new List<string>();
            if (s_data.city.challenges == null) s_data.city.challenges = new List<RetroSk8.Core.MedalEntry>();
            if (s_data.city.races == null) s_data.city.races = new List<RetroSk8.Core.MedalEntry>();
            if (s_data.customParks == null) s_data.customParks = new List<RetroSk8.Core.CustomPark>();
            s_data.customParks.RemoveAll(p => p == null || !RetroSk8.Core.CustomParkIds.IsCustom(p.id));
            foreach (var p in s_data.customParks) p.Sanitize();
            if (s_data.career == null) s_data.career = new RetroSk8.Core.CareerState();
            if (s_data.career.paidChapters == null) s_data.career.paidChapters = new List<string>();
            if (s_data.career.announcedGoals == null) s_data.career.announcedGoals = new List<string>();
            if (s_data.career.title == null) s_data.career.title = "";
            if (s_data.crew == null) s_data.crew = new RetroSk8.Core.CrewState();
            s_data.crew.Sanitize();
            if (s_data.weekly == null) s_data.weekly = new RetroSk8.Core.WeeklyState();
            s_data.weekly.Sanitize();
            if (s_data.look == null) s_data.look = new RetroSk8.Core.SkaterLook();
            s_data.look.Sanitize();
            if (string.IsNullOrWhiteSpace(s_data.settings.playerName)) s_data.settings.playerName = "SKATER";
            if (s_data.settings.touchLayout == null) s_data.settings.touchLayout = RetroSk8.Core.TouchLayout.Default();
            s_data.settings.touchLayout.Clamp();
            if (s_data.settings.musicMode < 0 || s_data.settings.musicMode > 2) s_data.settings.musicMode = 0;
            if (s_data.settings.radioStation < 0 || s_data.settings.radioStation >= RetroSk8.Core.Radio.Stations.Length) s_data.settings.radioStation = 0;
            if (s_data.ownedPacks == null) s_data.ownedPacks = new List<string>();
            s_data.ownedPacks.RemoveAll(string.IsNullOrEmpty);
            if (s_data.rivals == null) s_data.rivals = new List<RetroSk8.Core.RivalRecord>();
            s_data.rivals.RemoveAll(r => r == null);
            if (s_data.story == null) s_data.story = new RetroSk8.Core.StoryState();
            s_data.story.Sanitize();
            if (s_data.tips == null) s_data.tips = new RetroSk8.Core.TipState();
            s_data.tips.Sanitize();
            if (s_data.trickBook == null) s_data.trickBook = new RetroSk8.Core.TrickBookState();
            s_data.trickBook.Sanitize();
            if (s_data.streak == null) s_data.streak = new RetroSk8.Core.StreakState();
            s_data.streak.Sanitize();
            if (s_data.jam == null) s_data.jam = new RetroSk8.Core.JamRecord();
            s_data.version = SaveData.CurrentVersion;
        }

        public static void Save()
        {
            try
            {
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(Data, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RetroSk8] Failed to write save: {e.Message}");
            }
            Changed?.Invoke();
        }

        /// <returns>True if this is a new best score for the location.</returns>
        public static bool RecordRun(string locationId, long score, long bestCombo, string bestComboLabel, int tokensEarned)
        {
            var rec = Data.Record(locationId);
            bool newBest = score > rec.bestScore;
            if (newBest) rec.bestScore = score;
            if (bestCombo > rec.bestCombo)
            {
                rec.bestCombo = bestCombo;
                rec.bestComboLabel = bestComboLabel;
            }
            Data.tapeTokens += Mathf.Max(0, tokensEarned);
            Save();
            return newBest;
        }

        /// <summary>Stores contract goals completed this run.</summary>
        /// <returns>How many were completed for the first time, and whether the whole contract is newly complete.</returns>
        public static (int firstTime, bool allFirstTime) RecordContract(string locationId, IEnumerable<string> completedGoalIds, int goalCount)
        {
            var rec = Data.Contract(locationId);
            int first = 0;
            foreach (var id in completedGoalIds)
            {
                if (rec.completedGoalIds.Contains(id)) continue;
                rec.completedGoalIds.Add(id);
                first++;
            }
            bool allNow = goalCount > 0 && rec.completedGoalIds.Count >= goalCount;
            bool allFirst = allNow && !rec.allComplete;
            rec.allComplete = allNow;
            Save();
            return (first, allFirst);
        }

        public static int ContractStars(string locationId)
        {
            foreach (var c in Data.contracts) if (c.locationId == locationId) return c.completedGoalIds.Count;
            return 0;
        }

        /// <summary>Records today's Daily Line score and pays the bonus at most once per day.</summary>
        /// <returns>True when the bonus is earned by this run.</returns>
        public static bool RecordDaily(int dateKey, long score, bool allGoalsComplete)
        {
            var d = Data.daily;
            if (d.bestDate != dateKey) { d.bestDate = dateKey; d.bestScore = 0; }
            if (score > d.bestScore) d.bestScore = score;
            bool bonus = allGoalsComplete && d.claimedDate != dateKey;
            if (bonus) { d.claimedDate = dateKey; d.clears++; }
            Save();
            return bonus;
        }

        public static bool DailyClaimed(int dateKey) => Data.daily.claimedDate == dateKey;

        public static bool SpendTokens(int amount)
        {
            if (amount < 0 || Data.tapeTokens < amount) return false;
            Data.tapeTokens -= amount;
            Save();
            return true;
        }

        public static void AddTokens(int amount)
        {
            Data.tapeTokens = Mathf.Max(0, Data.tapeTokens + amount);
            Save();
        }

        // ---------------------------------------------------------------- Create-a-Park

        /// <summary>The saved park for an id, or null.</summary>
        public static RetroSk8.Core.CustomPark FindCustomPark(string id)
        {
            foreach (var p in Data.customParks) if (p.id == id) return p;
            return null;
        }

        /// <summary>Stores a copy of the park (replacing the same id) and writes the save.</summary>
        public static void SaveCustomPark(RetroSk8.Core.CustomPark park)
        {
            if (park == null || !RetroSk8.Core.CustomParkIds.IsCustom(park.id)) return;
            var copy = park.Clone();
            copy.Sanitize();
            int i = Data.customParks.FindIndex(p => p.id == park.id);
            if (i >= 0) Data.customParks[i] = copy; else Data.customParks.Add(copy);
            Save();
        }

        public static void DeleteCustomPark(string id)
        {
            Data.customParks.RemoveAll(p => p.id == id);
            Save();
        }

        public static void ResetAll()
        {
            var settings = Data.settings; // keep audio/haptic preferences across a progress reset
            var look = Data.look; // and how your skater looks
            bool onboarded = Data.onboardingDone;
            var tips = Data.tips; // hints already shown stay shown
            var packs = Data.ownedPacks; // paid for with real money: never erased (Restore Purchases would bring them back anyway)
            s_data = new SaveData { settings = settings, look = look, ownedPacks = packs ?? new List<string>(), onboardingDone = onboarded, tips = tips ?? new RetroSk8.Core.TipState() };
            RetroSk8.Replay.GhostStore.DeleteAll(); // ghosts are progress too
            Save();
        }
    }
}
