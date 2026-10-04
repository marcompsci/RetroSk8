using System;
using System.Collections.Generic;
using System.IO;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Replay
{
    /// <summary>
    /// Saved replays on disk: an index (JSON) plus one compact frame file per replay (the ghost format).
    /// Finished runs are saved automatically; Free Skate can save its last two minutes from the pause menu.
    /// </summary>
    public static class ReplayLibrary
    {
        private const string DefaultFolder = "replays";
        private static string s_folderOverride;
        private static ReplayIndex s_index;

        public static string Folder => Path.Combine(Application.persistentDataPath, s_folderOverride ?? DefaultFolder);
        private static string IndexPath => Path.Combine(Folder, "index.json");
        private static string TrackPath(string id) => Path.Combine(Folder, id + ".rk8");

        /// <summary>The replay saved most recently in this session (Results' WATCH REPLAY).</summary>
        public static string LastSavedId { get; private set; }

        /// <summary>Redirects replay files (tests). Null restores the default.</summary>
        public static void UseFolder(string folderName)
        {
            s_folderOverride = folderName;
            s_index = null;
        }

        public static ReplayIndex Index
        {
            get
            {
                if (s_index != null) return s_index;
                try
                {
                    if (File.Exists(IndexPath)) s_index = JsonUtility.FromJson<ReplayIndex>(File.ReadAllText(IndexPath));
                }
                catch (Exception e) { Debug.LogWarning($"[RetroSk8] Replay index unreadable, starting fresh. {e.Message}"); }
                if (s_index == null) s_index = new ReplayIndex();
                if (s_index.entries == null) s_index.entries = new List<ReplayEntry>();
                s_index.entries.RemoveAll(e => e == null || !File.Exists(TrackPath(e.id)));
                return s_index;
            }
        }

        /// <summary>Saves a track with its details. Returns the new replay's id, or null if it couldn't be written.</summary>
        public static string Save(ReplayTrack track, ReplayEntry entry)
        {
            if (track == null || track.Count < 2) return null;
            try
            {
                Directory.CreateDirectory(Folder);
                entry.id = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + UnityEngine.Random.Range(100, 999);
                entry.savedTicks = DateTime.Now.Ticks;
                entry.duration = track.Duration;
                string tmp = TrackPath(entry.id) + ".tmp";
                using (var fs = File.Create(tmp)) ReplayCodec.Write(track, fs);
                File.Move(tmp, TrackPath(entry.id));
                foreach (var gone in Index.Add(entry)) TryDelete(TrackPath(gone));
                WriteIndex();
                LastSavedId = entry.id;
                return entry.id;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RetroSk8] Could not save replay: {e.Message}");
                return null;
            }
        }

        public static ReplayTrack Load(string id)
        {
            try
            {
                string path = TrackPath(id);
                if (!File.Exists(path)) return null;
                using (var fs = File.OpenRead(path)) return ReplayCodec.Read(fs);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RetroSk8] Could not read replay: {e.Message}");
                return null;
            }
        }

        public static bool ToggleFavorite(string id)
        {
            var e = Index.Find(id);
            if (e == null || !Index.SetFavorite(id, !e.favorite)) return false;
            WriteIndex();
            return true;
        }

        public static void Delete(string id)
        {
            if (!Index.Remove(id)) return;
            TryDelete(TrackPath(id));
            WriteIndex();
        }

        public static void DeleteAll()
        {
            foreach (var e in new List<ReplayEntry>(Index.entries)) TryDelete(TrackPath(e.id));
            Index.entries.Clear();
            WriteIndex();
        }

        private static void WriteIndex()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(IndexPath, JsonUtility.ToJson(Index, true));
            }
            catch (Exception e) { Debug.LogWarning($"[RetroSk8] Could not write replay index: {e.Message}"); }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception) { /* a stale file is harmless */ }
        }
    }
}
