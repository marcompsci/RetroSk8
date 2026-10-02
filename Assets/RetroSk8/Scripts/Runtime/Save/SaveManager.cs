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
            if (s_data.settings.touchLayout == null) s_data.settings.touchLayout = RetroSk8.Core.TouchLayout.Default();
            s_data.settings.touchLayout.Clamp();
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

        public static void ResetAll()
        {
            var settings = Data.settings; // keep audio/haptic preferences across a progress reset
            s_data = new SaveData { settings = settings };
            RetroSk8.Replay.GhostStore.DeleteAll(); // ghosts are progress too
            Save();
        }
    }
}
