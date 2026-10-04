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
    /// finish). Progress is appended to Temp/RetroSk8Remote.log. Only reads the project's own Temp folder, never
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
        }

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

            foreach (var raw in commands)
            {
                string cmd = raw.Trim().ToLowerInvariant();
                if (cmd.Length == 0) continue;
                Log("request: " + cmd);
                switch (cmd)
                {
                    case "refresh":
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        Log("refreshed");
                        break;
                    case "shipcheck":
                        RetroSk8ShipCheck.Run(interactive: false);
                        Log("ship check written to " + RetroSk8ShipCheck.ReportPath);
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
