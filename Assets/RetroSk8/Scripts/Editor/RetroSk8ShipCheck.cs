using System.Collections.Generic;
using System.IO;
using System.Text;
using RetroSk8.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// Retro Sk8 → Ship Check: one click that lists everything standing between this project and an iPhone build
    /// (scenes, bundle id, version, input system, URP, content, icons, last test run) and writes the result to
    /// Temp/RetroSk8ShipCheck.txt so it can be read back outside Unity. Offers to run Setup Project when that fixes it.
    /// </summary>
    public static class RetroSk8ShipCheck
    {
        public const string ReportPath = "Temp/RetroSk8ShipCheck.txt";
        private const string RegistryPath = "Assets/RetroSk8/ScriptableObjects/ContentRegistry.asset";
        private const string IconPath = "Assets/RetroSk8/Art/AppIcon/RetroSk8_AppIcon.png";

        private struct Item
        {
            public bool Ok;
            public string Name;
            public string Detail;
            public bool SetupFixes;
        }

        [MenuItem("Retro Sk8/Ship Check (writes report)", priority = 50)]
        public static void Run() => Run(interactive: true);

        /// <summary>Writes the report; <paramref name="interactive"/> false skips the dialogs (remote runs).</summary>
        public static void Run(bool interactive)
        {
            var items = Collect();
            int failed = 0;
            bool setupHelps = false;
            var sb = new StringBuilder();
            sb.AppendLine("RETRO SK8 SHIP CHECK  " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            foreach (var i in items)
            {
                sb.AppendLine((i.Ok ? "[ OK ] " : "[FAIL] ") + i.Name + (string.IsNullOrEmpty(i.Detail) ? "" : "  -  " + i.Detail));
                if (!i.Ok) { failed++; setupHelps |= i.SetupFixes; }
            }
            sb.AppendLine(failed == 0 ? "READY: build with Retro Sk8 > Build iOS > Xcode Project for iPhone." : $"{failed} item(s) to fix.");
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("[RetroSk8] " + sb);

            if (!interactive) return;
            if (failed > 0 && setupHelps &&
                EditorUtility.DisplayDialog("Retro Sk8 Ship Check", $"{failed} item(s) need fixing. Most are fixed by Setup Project (scenes, bundle id, version, icon). Run it now?", "Run Setup", "Not now"))
            {
                RetroSk8ProjectSetup.SetupProject();
                Run(true); // re-check and rewrite the report
            }
            else if (failed == 0)
                EditorUtility.DisplayDialog("Retro Sk8 Ship Check", "Everything checks out. Next: Retro Sk8 > Build iOS > Xcode Project for iPhone.", "OK");
        }

        private static List<Item> Collect()
        {
            var list = new List<Item>();

            // Scenes on disk and in Build Settings.
            var inBuild = new HashSet<string>();
            if (EditorBuildSettings.scenes != null)
                foreach (var s in EditorBuildSettings.scenes) if (s.enabled) inBuild.Add(s.path);
            var missingFiles = new List<string>();
            var missingBuild = new List<string>();
            foreach (var name in RetroSk8ProjectSetup.SceneOrder)
            {
                string path = RetroSk8ProjectSetup.ScenePath(name);
                if (!File.Exists(path)) missingFiles.Add(name);
                else if (!inBuild.Contains(path)) missingBuild.Add(name);
            }
            list.Add(new Item { Ok = missingFiles.Count == 0, Name = "All scenes exist", Detail = missingFiles.Count > 0 ? "missing: " + string.Join(", ", missingFiles.ToArray()) : "", SetupFixes = true });
            list.Add(new Item { Ok = missingFiles.Count == 0 && missingBuild.Count == 0, Name = "Scenes are in Build Settings", Detail = missingBuild.Count > 0 ? "not added: " + string.Join(", ", missingBuild.ToArray()) : "", SetupFixes = true });

            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            list.Add(new Item { Ok = !RetroSk8ProjectSetup.IsPlaceholderBundleId(id), Name = "Bundle identifier is your own", Detail = id, SetupFixes = true });

            string version = PlayerSettings.bundleVersion;
            list.Add(new Item { Ok = !RetroSk8ProjectSetup.NeedsVersionBump(version), Name = "Version is current", Detail = $"{version} (build {PlayerSettings.iOS.buildNumber}), expected {RetroSk8ProjectSetup.AppVersion} or later", SetupFixes = true });

            bool osOk = System.Version.TryParse(PlayerSettings.iOS.targetOSVersionString, out var os) && os.Major >= 15;
            list.Add(new Item { Ok = osOk, Name = "Minimum iOS 15", Detail = PlayerSettings.iOS.targetOSVersionString, SetupFixes = true });
            list.Add(new Item { Ok = PlayerSettings.iOS.targetDevice == iOSTargetDevice.iPhoneAndiPad, Name = "Runs on iPhone and iPad", Detail = PlayerSettings.iOS.targetDevice.ToString(), SetupFixes = true });

#if ENABLE_INPUT_SYSTEM
            list.Add(new Item { Ok = true, Name = "New Input System is active" });
#else
            list.Add(new Item { Ok = false, Name = "New Input System is active", Detail = "Player Settings > Active Input Handling > Input System Package (New) or Both, then restart Unity" });
#endif

            list.Add(new Item { Ok = GraphicsSettings.defaultRenderPipeline != null, Name = "URP pipeline asset assigned", SetupFixes = true });
            list.Add(new Item { Ok = AssetDatabase.LoadAssetAtPath<RetroSk8.Data.ContentRegistry>(RegistryPath) != null, Name = "Content registry asset", Detail = RegistryPath, SetupFixes = true });
            list.Add(new Item { Ok = File.Exists(IconPath), Name = "App icon art", Detail = IconPath });

            // Last test run (written by Retro Sk8 > Run All Tests or any Test Runner run).
            string tests = File.Exists(RetroSk8TestReportPaths.Report) ? File.ReadAllText(RetroSk8TestReportPaths.Report) : null;
            bool testsOk = tests != null && tests.Contains("FAILED 0") && tests.Contains("PlayMode");
            list.Add(new Item
            {
                Ok = testsOk,
                Name = "PlayMode tests pass",
                Detail = tests == null ? "no report yet: run Retro Sk8 > Run All Tests" : FirstLine(tests),
            });
            return list;
        }

        private static string FirstLine(string s)
        {
            int i = s.IndexOf('\n');
            return (i < 0 ? s : s.Substring(0, i)).Trim();
        }
    }

    /// <summary>Shared with the test report writer (which lives in its own assembly so it only exists when the Test Framework does).</summary>
    public static class RetroSk8TestReportPaths
    {
        public const string Report = "Temp/RetroSk8TestReport.txt";
    }
}
