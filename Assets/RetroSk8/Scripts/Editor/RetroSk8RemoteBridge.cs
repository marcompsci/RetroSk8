using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// Lets tools outside Unity ask the open editor to refresh, run Ship Check or run all tests, without anyone
    /// clicking a menu (Phase 15). Drop a text file at Temp/RetroSk8Remote.request with one command per line:
    /// <c>refresh</c> (import changed scripts), <c>shipcheck</c>, <c>tests</c> (Ship Check runs again after the tests
    /// finish), <c>build-ios</c> / <c>build-ios-dev</c> (device Xcode project; summary in Temp/RetroSk8BuildReport.txt),
    /// <c>testflight</c> / <c>testflight-upload</c> and <c>team XXXXXXXXXX</c> (Phase 18, see RetroSk8TestFlight). Progress is appended to Temp/RetroSk8Remote.log. Only reads the project's own Temp folder, never
    /// runs while playing or compiling, and Ship Check runs without dialogs when asked this way.
    /// </summary>
    [InitializeOnLoad]
    public static class RetroSk8RemoteBridge
    {
        public const string RequestPath = "Temp/RetroSk8Remote.request";
        public const string LogPath = "Temp/RetroSk8Remote.log";
        private const string ShipAfterTestsKey = "RetroSk8.Remote.ShipAfterTests";
        private const double PollSeconds = 2.0;

        /// <summary>Set by the test report assembly (it only exists when the Test Framework does).</summary>
        public static Action RunTests;

        private static double s_next;

        static RetroSk8RemoteBridge()
        {
            EditorApplication.update += Poll;
            // Compile errors land in the log too, so a remote session can see why a change didn't load.
            UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished += (assembly, messages) =>
            {
                foreach (var m in messages)
                    if (m.type == UnityEditor.Compilation.CompilerMessageType.Error) Log("COMPILE ERROR " + m.message);
            };
        }

        private const string RemoteActiveKey = "RetroSk8.Remote.Active";

        /// <summary>True once this editor session has taken a remote request: setup steps skip their dialogs then.</summary>
        public static bool RemoteActive => SessionState.GetBool(RemoteActiveKey, false);

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < s_next) return;
            s_next = EditorApplication.timeSinceStartup + PollSeconds;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(RequestPath)) return;

            string[] commands;
            try
            {
                commands = File.ReadAllLines(RequestPath);
                File.Delete(RequestPath);
            }
            catch (IOException) { return; } // still being written; next poll

            SessionState.SetBool(RemoteActiveKey, true);
            foreach (var raw in commands)
            {
                string cmd = raw.Trim().ToLowerInvariant();
                if (cmd.Length == 0) continue;
                Log("request: " + cmd);
                if (cmd.StartsWith("team "))
                {
                    string team = raw.Trim().Substring(5).Trim().ToUpperInvariant();
                    if (!RetroSk8BuildOptions.IsValidTeamId(team)) { Log("team: not a valid Team ID (10 letters/digits): " + team); continue; }
                    RetroSk8BuildOptions.SetTeamId(team);
                    RetroSk8ProjectSetup.ConfigurePlayerSettings();
                    Log("team: device builds now sign with " + team);
                    continue;
                }
                if (RetroSk8TestFlight.Busy && cmd != "shipcheck")
                {
                    // A recompile or another build would cut off the running xcodebuild job's tracking.
                    Log("busy: a TestFlight job is running; try again when the log says it finished");
                    continue;
                }
                switch (cmd)
                {
                    case "refresh":
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                        Log("refreshed");
                        break;
                    case "setup":
                        RetroSk8ProjectSetup.SetupProject();
                        Log("setup: Setup Project finished (content, scenes, build settings)");
                        break;
                    case "cloudkit":
                    case "cloudkit-check":
                        Log(RetroSk8CloudKit.Run(importIt: cmd == "cloudkit") + " (details: " + RetroSk8CloudKit.ReportPath + ")");
                        break;
                    case "native-check":
                        Log(RetroSk8NativeCheck.Run() + " (details: " + RetroSk8NativeCheck.ReportPath + ")");
                        break;
                    case "gamecenter":
                        Log("gamecenter: " + RetroSk8GameCenterList.Write());
                        break;
                    case "shipcheck":
                        RetroSk8ShipCheck.Run(interactive: false);
                        Log("ship check written to " + RetroSk8ShipCheck.ReportPath);
                        break;
                    case "build-ios":
                    case "build-ios-dev":
                        Log("iOS build started (device Xcode project" + (cmd.EndsWith("-dev") ? ", development" : "") + ")");
                        string summary = RetroSk8IOSBuild.BuildForReport(cmd.EndsWith("-dev"));
                        int nl = summary.IndexOf('\n');
                        Log("iOS build: " + (nl > 0 ? summary.Substring(0, nl) : summary) + "  (full report: " + RetroSk8IOSBuild.ReportPath + ")");
                        break;
                    case "testflight":
                    case "testflight-upload":
                        Log(RetroSk8TestFlight.Start(build: cmd == "testflight", upload: true));
                        break;
                    case "tests":
                        if (RunTests == null) { Log("tests unavailable: Test Framework package missing"); break; }
                        SessionState.SetBool(ShipAfterTestsKey, true);
                        RunTests();
                        Log("tests started");
                        break;
                    default:
                        Log("unknown command: " + cmd);
                        break;
                }
            }
        }

        /// <summary>Called by the test report writer when a run finishes.</summary>
        public static void OnTestsFinished()
        {
            Log("tests finished; report at " + RetroSk8TestReportPaths.Report);
            if (!SessionState.GetBool(ShipAfterTestsKey, false)) return;
            SessionState.EraseBool(ShipAfterTestsKey);
            RetroSk8ShipCheck.Run(interactive: false);
            Log("ship check written to " + RetroSk8ShipCheck.ReportPath);
        }

        public static void Log(string line)
        {
            try
            {
                Directory.CreateDirectory("Temp");
                File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + line + "\n");
            }
            catch (IOException e) { Debug.LogWarning("[RetroSk8] remote log: " + e.Message); }
        }
    }
}
