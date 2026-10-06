using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// Sends the online gallery's CloudKit schema (Tools/CloudKit/RetroSk8.ckdb) to the app's container in the
    /// Development environment with Apple's cktool (Phase 18), so nobody has to click record types together in the
    /// CloudKit console. Needs a CloudKit management token saved once in Terminal: <c>xcrun cktool save-token --type management</c>
    /// (make the token in the CloudKit console → your account → Tokens). Bridge: <c>cloudkit-check</c> (validate only)
    /// and <c>cloudkit</c> (import). Deploying the schema to Production stays a console click, on purpose.
    /// </summary>
    public static class RetroSk8CloudKit
    {
        public const string SchemaPath = "Tools/CloudKit/RetroSk8.ckdb";
        public const string ReportPath = "Temp/RetroSk8CloudKit.txt";

        public static string ContainerId => "iCloud." + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);

        [MenuItem("Retro Sk8/Build iOS/CloudKit: Check Gallery Schema", priority = 84)]
        private static void MenuCheck() => UnityEngine.Debug.Log("[RetroSk8] " + Run(importIt: false));

        [MenuItem("Retro Sk8/Build iOS/CloudKit: Send Gallery Schema (Development)", priority = 85)]
        private static void MenuImport() => UnityEngine.Debug.Log("[RetroSk8] " + Run(importIt: true));

        /// <summary>Runs cktool and writes its output to <see cref="ReportPath"/>; returns a one-line summary.</summary>
        public static string Run(bool importIt)
        {
            string team = RetroSk8BuildOptions.TeamId;
            if (!RetroSk8BuildOptions.IsValidTeamId(team)) return Write("CLOUDKIT NOT RUN: no Team ID set.");
            string schemaFull = Path.GetFullPath(SchemaPath); // the project folder (next to Assets)
            if (!File.Exists(schemaFull)) return Write("CLOUDKIT NOT RUN: schema file missing at " + SchemaPath);
#if !UNITY_EDITOR_OSX
            return Write("CLOUDKIT NOT RUN: needs a Mac (xcrun cktool).");
#else
            string verb = importIt ? "import-schema" : "validate-schema";
            string args = $"cktool {verb} --team-id {team} --container-id {ContainerId} --environment development --file \"{schemaFull}\"";
            var info = new ProcessStartInfo("/usr/bin/xcrun", args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            try
            {
                using (var p = Process.Start(info))
                {
                    var outTask = p.StandardOutput.ReadToEndAsync();
                    var errTask = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(90000)) { try { p.Kill(); } catch (InvalidOperationException) { } return Write("CLOUDKIT TIMED OUT after 90 s"); }
                    string output = (outTask.Result + "\n" + errTask.Result).Trim();
                    bool ok = p.ExitCode == 0;
                    string head = (ok ? "CLOUDKIT OK: " : "CLOUDKIT FAILED: ") + verb + " → " + ContainerId + " (development)";
                    if (!ok && output.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0)
                        head += "\nNEXT STEP: in Terminal run  xcrun cktool save-token --type management  and paste a management token from the CloudKit console.";
                    return Write(head + "\n" + output);
                }
            }
            catch (Exception e)
            {
                return Write("CLOUDKIT NOT RUN: " + e.Message);
            }
#endif
        }

        private static string Write(string text)
        {
            try
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText(ReportPath, text + "\n");
            }
            catch (IOException) { }
            int nl = text.IndexOf('\n');
            return nl > 0 ? text.Substring(0, nl) : text;
        }
    }
}
