using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// One-click Xcode project exports. The Simulator export runs on Apple-silicon and Intel Macs without a signing team;
    /// open the generated Unity-iPhone.xcodeproj, pick an iPhone simulator and press Run.
    /// </summary>
    public static class RetroSk8IOSBuild
    {
        private const string SimulatorPath = "Builds/iOS-Simulator";
        private const string DevicePath = "Builds/iOS-Device";

        [MenuItem("Retro Sk8/Build iOS/Xcode Project for iPhone (Release)", priority = 60)]
        public static void BuildDevice() => Build(simulator: false, development: false);

        [MenuItem("Retro Sk8/Build iOS/Xcode Project for iPhone (Development + Profiler)", priority = 61)]
        public static void BuildDeviceDevelopment() => Build(simulator: false, development: true);

        [MenuItem("Retro Sk8/Build iOS/Xcode Project for Simulator", priority = 62)]
        public static void BuildSimulator() => Build(simulator: true, development: true);

        public const string ReportPath = "Temp/RetroSk8BuildReport.txt";

        /// <summary>
        /// Builds the device Xcode project without any dialogs and writes a summary (result, size, time, every error
        /// and warning from the build) to <see cref="ReportPath"/>. Used by the remote bridge ("build-ios").
        /// </summary>
        public static string BuildForReport(bool development) => Build(simulator: false, development: development, interactive: false);

        private static string Build(bool simulator, bool development, bool interactive = true)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                const string missing = "iOS Build Support is not installed.\n\nUnity Hub → Installs → your Unity 6 version → Add modules → iOS Build Support.";
                if (interactive) EditorUtility.DisplayDialog("Retro Sk8", missing, "OK");
                return WriteReport("BUILD NOT STARTED: " + missing);
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                if (interactive) EditorUtility.DisplayDialog("Retro Sk8", "No scenes in Build Settings. Run 'Retro Sk8 → Setup Project' first.", "OK");
                return WriteReport("BUILD NOT STARTED: no scenes in Build Settings (run Setup Project)");
            }

            RetroSk8ProjectSetup.ConfigurePlayerSettings();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

            // Every upload to TestFlight needs a new build number; bump it on each build so you never have to.
            int.TryParse(PlayerSettings.iOS.buildNumber, out int build);
            PlayerSettings.iOS.buildNumber = (build + 1).ToString();
            AssetDatabase.SaveAssets();
            Debug.Log($"[RetroSk8] Building version {PlayerSettings.bundleVersion} ({PlayerSettings.iOS.buildNumber}).");

            var previousSdk = PlayerSettings.iOS.sdkVersion;
            PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;

            string path = simulator ? SimulatorPath : DevicePath;
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                // Release plays at full speed (what you want in your hands). Development builds add the profiler
                // connection and readable logs in Xcode's console, at some cost to frame rate.
                options = development ? BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            PlayerSettings.iOS.sdkVersion = previousSdk;

            string xcode = Path.GetFullPath(Path.Combine(path, "Unity-iPhone.xcodeproj"));
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[RetroSk8] Xcode project ready: {xcode}");
                if (interactive) EditorUtility.RevealInFinder(xcode);
            }
            else
            {
                Debug.LogError($"[RetroSk8] iOS build {report.summary.result} with {report.summary.totalErrors} error(s). See the Console.");
            }
            return WriteReport(Summarize(report, xcode));
        }

        private static string Summarize(BuildReport report, string xcode)
        {
            var sb = new System.Text.StringBuilder();
            var sum = report.summary;
            sb.AppendLine($"BUILD {sum.result.ToString().ToUpperInvariant()}  {System.DateTime.Now:yyyy-MM-dd HH:mm}  version {PlayerSettings.bundleVersion} ({PlayerSettings.iOS.buildNumber})");
            sb.AppendLine($"errors {sum.totalErrors}  warnings {sum.totalWarnings}  time {sum.totalTime.TotalMinutes:0.0} min  size {sum.totalSize / (1024f * 1024f):0.0} MB");
            if (sum.result == BuildResult.Succeeded) sb.AppendLine("xcode " + xcode);
            int shown = 0;
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                {
                    if (m.type != LogType.Error && m.type != LogType.Exception && m.type != LogType.Warning) continue;
                    if (shown++ >= 60) break;
                    sb.AppendLine((m.type == LogType.Warning ? "WARN " : "ERROR ") + m.content.Replace("\n", " ").Trim());
                }
            return sb.ToString();
        }

        private static string WriteReport(string text)
        {
            try
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText(ReportPath, text);
            }
            catch (IOException e) { Debug.LogWarning("[RetroSk8] build report: " + e.Message); }
            return text;
        }
    }
}
