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

        private static void Build(bool simulator, bool development)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                EditorUtility.DisplayDialog("Retro Sk8", "iOS Build Support is not installed.\n\nUnity Hub → Installs → your Unity 6 version → Add modules → iOS Build Support.", "OK");
                return;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Retro Sk8", "No scenes in Build Settings. Run 'Retro Sk8 → Setup Project' first.", "OK");
                return;
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

            if (report.summary.result == BuildResult.Succeeded)
            {
                string project = Path.GetFullPath(Path.Combine(path, "Unity-iPhone.xcodeproj"));
                Debug.Log($"[RetroSk8] Xcode project ready: {project}");
                EditorUtility.RevealInFinder(project);
            }
            else
            {
                Debug.LogError($"[RetroSk8] iOS build {report.summary.result} with {report.summary.totalErrors} error(s). See the Console.");
            }
        }
    }
}
