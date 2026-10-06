using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using RetroSk8.Core;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// Retro Sk8 → Build iOS → TestFlight (Phase 18): builds the Release Xcode project, archives it with xcodebuild
    /// and uploads it to App Store Connect, without opening Xcode. xcodebuild runs in the background so Unity stays
    /// usable; its full output goes to Temp/RetroSk8Xcode.log and a short summary (result, the important lines and
    /// a plain-English next step) to Temp/RetroSk8TestFlightReport.txt. The remote bridge runs it with
    /// <c>testflight</c> (build + archive + upload) and <c>testflight-upload</c> (upload the last archive again).
    /// Needs: the Team ID set, Xcode signed in to that Apple account, and the app created in App Store Connect.
    /// Don't recompile scripts while it runs (Unity forgets the background job; xcodebuild itself carries on).
    /// </summary>
    [InitializeOnLoad]
    public static class RetroSk8TestFlight
    {
        public const string ReportPath = "Temp/RetroSk8TestFlightReport.txt";
        public const string XcodeLogPath = "Temp/RetroSk8Xcode.log";

        private static Process s_process;
        private static string s_stage;
        private static bool s_uploadAfterArchive;
        private static readonly ConcurrentQueue<string> s_lines = new ConcurrentQueue<string>();
        private static readonly List<string> s_output = new List<string>();
        private static DateTime s_started;

        public static bool Busy => s_process != null;

        static RetroSk8TestFlight()
        {
            EditorApplication.update += Pump;
        }

        [MenuItem("Retro Sk8/Build iOS/TestFlight: Build, Archive + Upload", priority = 70)]
        private static void MenuUpload() => Start(build: true, upload: true);

        [MenuItem("Retro Sk8/Build iOS/TestFlight: Upload Last Archive Again", priority = 71)]
        private static void MenuReupload() => Start(build: false, upload: true);

        /// <summary>Starts the pipeline; returns a one-line status for the bridge log.</summary>
        public static string Start(bool build, bool upload)
        {
            if (Busy) return "TestFlight: already running (" + s_stage + ")";
            string team = RetroSk8BuildOptions.TeamId;
            if (!TestFlightRules.IsValidTeamId(team))
                return Finish(false, "No valid Team ID. Bridge: \"team XXXXXXXXXX\", or set teamId in ProjectSettings/RetroSk8BuildOptions.json.");
#if !UNITY_EDITOR_OSX
            return Finish(false, "TestFlight uploads need a Mac (xcodebuild).");
#else
            s_output.Clear();
            s_started = DateTime.Now;
            s_uploadAfterArchive = upload;
            try
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText(XcodeLogPath, "");
                Directory.CreateDirectory("Builds");
                File.WriteAllText(TestFlightRules.ExportOptionsPath, TestFlightRules.ExportOptions(team, upload));
            }
            catch (IOException e) { return Finish(false, "Could not write build files: " + e.Message); }

            if (build)
            {
                string summary = RetroSk8IOSBuild.BuildForReport(false);
                if (!summary.StartsWith("BUILD SUCCEEDED"))
                    return Finish(false, "Unity's Xcode export failed, see " + RetroSk8IOSBuild.ReportPath + ": " + FirstLine(summary));
                try { if (Directory.Exists(TestFlightRules.ArchivePath)) Directory.Delete(TestFlightRules.ArchivePath, true); }
                catch (IOException e) { return Finish(false, "Could not clear the old archive: " + e.Message); }
                return Run("archive", TestFlightRules.ArchiveArgs(team));
            }
            if (!Directory.Exists(TestFlightRules.ArchivePath))
                return Finish(false, "No archive yet at " + TestFlightRules.ArchivePath + ": use testflight first.");
            return Run("upload", TestFlightRules.ExportArgs());
#endif
        }

        private static string Run(string stage, string[] args)
        {
            s_stage = stage;
            var info = new ProcessStartInfo
            {
                FileName = "/usr/bin/xcrun",
                Arguments = "xcodebuild " + TestFlightRules.Join(args),
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            try
            {
                var p = new Process { StartInfo = info, EnableRaisingEvents = true };
                p.OutputDataReceived += (_, e) => { if (e.Data != null) s_lines.Enqueue(e.Data); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) s_lines.Enqueue(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                s_process = p;
            }
            catch (Exception e)
            {
                return Finish(false, "Could not start xcodebuild: " + e.Message);
            }
            string msg = "TestFlight: " + stage + " started (xcodebuild; output in " + XcodeLogPath + ")";
            RetroSk8RemoteBridge.Log(msg);
            return msg;
        }

        private static void Pump()
        {
            if (s_lines.IsEmpty && s_process == null) return;
            var sb = new StringBuilder();
            while (s_lines.TryDequeue(out var line)) { s_output.Add(line); sb.AppendLine(line); }
            if (sb.Length > 0)
            {
                try { File.AppendAllText(XcodeLogPath, sb.ToString()); } catch (IOException) { }
            }
            if (s_process == null || !s_process.HasExited) return;
            if (!s_lines.IsEmpty) return; // drain the last lines first

            int code = s_process.ExitCode;
            string stage = s_stage;
            s_process.Dispose();
            s_process = null;
            if (code != 0)
            {
                Finish(false, stage + " failed (xcodebuild exit " + code + ")");
                return;
            }
            if (stage == "archive")
            {
                Run("upload", TestFlightRules.ExportArgs());
                return;
            }
            Finish(true, s_uploadAfterArchive
                ? "Uploaded to App Store Connect. It shows in TestFlight after Apple processes it (often 10-30 minutes)."
                : "Exported to " + TestFlightRules.ExportPath + ".");
        }

        private static string Finish(bool ok, string message)
        {
            s_stage = null;
            var sb = new StringBuilder();
            sb.AppendLine((ok ? "TESTFLIGHT SUCCEEDED  " : "TESTFLIGHT FAILED  ") + DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                + "  version " + PlayerSettings.bundleVersion + " (" + PlayerSettings.iOS.buildNumber + ")");
            if (s_started != default) sb.AppendLine("time " + (DateTime.Now - s_started).TotalMinutes.ToString("0.0") + " min");
            sb.AppendLine(message);
            string advice = ok ? null : TestFlightRules.Advice(s_output);
            if (advice != null) sb.AppendLine("NEXT STEP: " + advice);
            foreach (var line in TestFlightRules.ImportantLines(s_output)) sb.AppendLine("  " + line);
            try
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText(ReportPath, sb.ToString());
            }
            catch (IOException) { }
            string summary = FirstLine(sb.ToString()) + ": " + message;
            RetroSk8RemoteBridge.Log("TestFlight: " + (ok ? "done" : "failed") + " (report: " + ReportPath + ")");
            if (ok) Debug.Log("[RetroSk8] " + sb); else Debug.LogWarning("[RetroSk8] " + sb);
            return summary;
        }

        private static string FirstLine(string s)
        {
            int nl = s.IndexOf('\n');
            return nl > 0 ? s.Substring(0, nl).Trim() : s.Trim();
        }
    }
}
