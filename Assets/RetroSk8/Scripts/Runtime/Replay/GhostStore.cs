using System;
using System.IO;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Replay
{
    /// <summary>
    /// Saves and loads the best-run ghost for each park as a small binary file in persistent data.
    /// Writes go through a temp file so a crash never leaves a half-written ghost.
    /// </summary>
    public static class GhostStore
    {
        private const string DefaultFolder = "ghosts";
        private static string s_folderOverride;

        public static string Folder => Path.Combine(Application.persistentDataPath, s_folderOverride ?? DefaultFolder);

        /// <summary>Redirects ghost files to another folder (tests). Null restores the default.</summary>
        public static void UseFolder(string folderName) => s_folderOverride = folderName;

        public static string PathFor(string locationId) => Path.Combine(Folder, Sanitize(locationId) + ".ghost");

        public static bool Save(ReplayTrack track)
        {
            if (track == null || track.Count < 2 || string.IsNullOrEmpty(track.LocationId)) return false;
            try
            {
                Directory.CreateDirectory(Folder);
                string path = PathFor(track.LocationId);
                string tmp = path + ".tmp";
                using (var fs = File.Create(tmp)) ReplayCodec.Write(track, fs);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RetroSk8] Could not save ghost: {e.Message}");
                return false;
            }
        }

        public static ReplayTrack Load(string locationId)
        {
            try
            {
                string path = PathFor(locationId);
                if (!File.Exists(path)) return null;
                using (var fs = File.OpenRead(path))
                {
                    var track = ReplayCodec.Read(fs);
                    return track != null && track.LocationId == locationId && track.Count >= 2 ? track : null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RetroSk8] Could not read ghost: {e.Message}");
                return null;
            }
        }

        public static void DeleteAll()
        {
            try
            {
                if (Directory.Exists(Folder))
                    foreach (var f in Directory.GetFiles(Folder, "*.ghost")) File.Delete(f);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RetroSk8] Could not delete ghosts: {e.Message}");
            }
        }

        private static string Sanitize(string id)
        {
            var chars = (id ?? "park").ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-') chars[i] = '_';
            return new string(chars);
        }
    }

    /// <summary>Conversions between Unity math types and the engine-free replay types.</summary>
    public static class ReplayMath
    {
        public static RVec3 ToR(this Vector3 v) => new RVec3(v.x, v.y, v.z);
        public static RQuat ToR(this Quaternion q) => new RQuat(q.x, q.y, q.z, q.w);
        public static Vector3 ToUnity(this RVec3 v) => new Vector3(v.X, v.Y, v.Z);
        public static Quaternion ToUnity(this RQuat q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    }
}
