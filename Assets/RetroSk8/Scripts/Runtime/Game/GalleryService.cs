using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using RetroSk8.Core;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// The online gallery (Phase 16): browse, get, post, report and hide shared parks and ghosts. On iOS it talks to
    /// CloudKit through RetroSk8Gallery.mm (only when the build turns the gallery on). In the editor it runs a
    /// labelled TEST GALLERY in memory so the screens can be tried without a server.
    /// One request at a time: start one, then poll <see cref="Poll"/> each frame.
    /// </summary>
    public static class GalleryService
    {
        public enum Status { Idle = 0, Busy = 1, Done = 2, Failed = 3 }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int RetroSk8_GalleryAvailable();
        [DllImport("__Internal")] private static extern int RetroSk8_GalleryState();
        [DllImport("__Internal")] private static extern string RetroSk8_GalleryResult();
        [DllImport("__Internal")] private static extern void RetroSk8_GalleryQuery(int kind, int limit);
        [DllImport("__Internal")] private static extern void RetroSk8_GalleryUpload(int kind, string name, string author, long detail, string location, string code);
        [DllImport("__Internal")] private static extern void RetroSk8_GalleryFetchCode(string recordName);
        [DllImport("__Internal")] private static extern void RetroSk8_GalleryReport(string recordName, string reason);
        [DllImport("__Internal")] private static extern void RetroSk8_GalleryDelete(string recordName);

        public static bool IsAvailable => RetroSk8_GalleryAvailable() != 0;
        public static bool IsTest => false;
        public static Status State => (Status)RetroSk8_GalleryState();
        public static string Result => RetroSk8_GalleryResult() ?? "";
        public static void Query(GalleryKind kind) => RetroSk8_GalleryQuery((int)kind, Gallery.PageSize);
        private static void NativeUpload(GalleryKind kind, string name, string author, long detail, string location, string code) =>
            RetroSk8_GalleryUpload((int)kind, name, author, detail, location ?? "", code);
        public static void FetchCode(string id) => RetroSk8_GalleryFetchCode(id);
        private static void NativeReport(string id, string reason) => RetroSk8_GalleryReport(id, reason);
        public static void Delete(string id) => RetroSk8_GalleryDelete(id);
#else
        // ---- editor / non-iOS: an in-memory TEST GALLERY with a few sample posts.
        private static readonly List<(GalleryEntry entry, string code)> s_test = new List<(GalleryEntry, string)>();
        private static Status s_state;
        private static string s_result = "";

        public static bool IsAvailable => Application.isEditor;
        public static bool IsTest => true;
        public static Status State => s_state;
        public static string Result => s_result;

        private static void Seed()
        {
            if (s_test.Count > 0) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var starter = CustomPark.Starter(CustomParkIds.ForSlot(1), "TEST STARTER");
            s_test.Add((new GalleryEntry { Id = "test_park_1", Kind = GalleryKind.Park, Name = "TEST STARTER", Author = "TEST GALLERY", Detail = starter.pieces.Count, Created = now - 600 }, ShareCodes.EncodePark(starter)));
            var lines = CustomPark.Create(CustomParkIds.ForSlot(1), "RAIL GARDEN");
            for (int i = 0; i < 6; i++) lines.Add(PieceKind.FlatRail, 8 + i * 5, 20);
            s_test.Add((new GalleryEntry { Id = "test_park_2", Kind = GalleryKind.Park, Name = "RAIL GARDEN", Author = "TEST GALLERY", Detail = lines.pieces.Count, Created = now - 7200 }, ShareCodes.EncodePark(lines)));
        }

        private static void Finish(bool ok, string text) { s_state = ok ? Status.Done : Status.Failed; s_result = text ?? ""; }

        public static void Query(GalleryKind kind)
        {
            Seed();
            var sb = new System.Text.StringBuilder();
            for (int i = s_test.Count - 1; i >= 0; i--)
                if (s_test[i].entry.Kind == kind) sb.Append(Gallery.Format(s_test[i].entry)).Append('\n');
            Finish(true, sb.ToString());
        }

        private static void NativeUpload(GalleryKind kind, string name, string author, long detail, string location, string code)
        {
            Seed();
            string id = "test_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            s_test.Add((new GalleryEntry { Id = id, Kind = kind, Name = name, Author = author, Detail = detail, Location = location ?? "", Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() }, code));
            Finish(true, id);
        }

        public static void FetchCode(string id)
        {
            Seed();
            foreach (var (e, code) in s_test) if (e.Id == id) { Finish(true, code); return; }
            Finish(false, "THAT POST IS GONE");
        }

        private static void NativeReport(string id, string reason) => Finish(true, "REPORTED");

        public static void Delete(string id)
        {
            s_test.RemoveAll(t => t.entry.Id == id);
            Finish(true, "DELETED");
        }
#endif

        public static GalleryState Saved => SaveManager.Data.gallery;

        public static int Today => Streaks.DayNumber(DateTime.Now);

        /// <summary>Why a post can't go up (today's limit, no code, an empty or filtered name), or null when it can.</summary>
        public static string CheckUpload(string name, string code)
        {
            if (!Gallery.CanUpload(Saved, Today)) return $"THAT'S {Gallery.UploadsPerDay} POSTS TODAY. POST MORE TOMORROW.";
            if (string.IsNullOrEmpty(code)) return "NOTHING TO POST";
            // Phase 19: posts are spaced out, the same park or ghost can't go up twice, and codes stay within size.
            long wait = Gallery.WaitBeforeUpload(Saved, Now);
            if (wait > 0) return $"WAIT {wait}s BEFORE POSTING AGAIN.";
            if (Gallery.AlreadyPosted(Saved, code)) return "YOU ALREADY POSTED THAT ONE.";
            if (code.Length > CodeLimits.MaxGhostCodeChars) return "THAT'S TOO BIG TO POST.";
            string clean = Gallery.CleanName(name, CustomPark.MaxNameLength);
            if (clean.Length == 0) return "GIVE IT A NAME FIRST";
            if (Gallery.IsBlocked(clean)) return "PICK A DIFFERENT NAME";
            return null;
        }

        /// <summary>Starts a post (check <see cref="CheckUpload"/> first). Your Codes name goes with it, filtered.</summary>
        public static void Upload(GalleryKind kind, string name, long detail, string location, string code)
        {
            string author = Gallery.SafeName(SaveManager.Data.settings.playerName, ShareCodes.MaxFromLength, "SKATER");
            NativeUpload(kind, Gallery.CleanName(name, CustomPark.MaxNameLength), author, detail, location, code);
        }

        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>Call when an upload finished: counts it and remembers it as yours (and what it was, so it isn't reposted).</summary>
        public static void UploadDone(string id, string code = null)
        {
            Gallery.CountUpload(Saved, Today, CodeLimits.IsSafeRecordId(id) ? id : null, code, Now);
            SaveManager.Save();
        }

        public static void Report(GalleryEntry e, string reason)
        {
            if (e == null) return;
            if (!Saved.reported.Contains(e.Id)) Saved.reported.Add(e.Id);
            Hide(e); // reporting also hides it for you
            NativeReport(e.Id, reason);
        }

        public static void Hide(GalleryEntry e)
        {
            if (e == null || Saved.hidden.Contains(e.Id)) return;
            Saved.hidden.Add(e.Id);
            Saved.Sanitize();
            SaveManager.Save();
        }

        public static void BlockAuthor(GalleryEntry e)
        {
            // Phase 19: block the poster's iCloud account when CloudKit told us who it is (typed names can be copied).
            string key = Gallery.BlockKey(e);
            if (string.IsNullOrEmpty(key) || Saved.blockedAuthors.Contains(key)) return;
            Saved.blockedAuthors.Add(key);
            Saved.Sanitize();
            SaveManager.Save();
        }

        public static bool IsMine(GalleryEntry e) => e != null && Saved.posted.Contains(e.Id);
    }
}
