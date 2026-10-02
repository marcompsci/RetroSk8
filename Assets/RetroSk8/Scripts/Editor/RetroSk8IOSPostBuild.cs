#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// Finishes the exported Xcode project so it can go straight to a phone or TestFlight:
    /// links the frameworks the native plugins use, adds the app privacy manifest, answers the
    /// export-compliance question in Info.plist, and adds the Game Center capability when it is switched on.
    /// </summary>
    public static class RetroSk8IOSPostBuild
    {
        private const string PrivacySource = "Assets/RetroSk8/Platform/iOS/PrivacyInfo.xcprivacy";
        private const string PrivacyFolder = "RetroSk8";

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string framework = project.GetUnityFrameworkTargetGuid();
            string app = project.GetUnityMainTargetGuid();

            // Plugins compile into UnityFramework, so their system frameworks are linked there.
            project.AddFrameworkToProject(framework, "ReplayKit.framework", false);
            project.AddFrameworkToProject(framework, "GameKit.framework", true); // weak: harmless when Game Center is off

            AddPrivacyManifest(project, app, path);
            project.WriteToFile(projectPath);

            SetPlistFlags(path);

            if (RetroSk8BuildOptions.GameCenterEnabled)
            {
                var caps = new ProjectCapabilityManager(projectPath, "Unity-iPhone/RetroSk8.entitlements", null, app);
                caps.AddGameCenter();
                caps.WriteToFile();
                Debug.Log("[RetroSk8] Added the Game Center capability (needs a paid Apple developer team).");
            }
        }

        private static void AddPrivacyManifest(PBXProject project, string appTarget, string buildPath)
        {
            if (!File.Exists(PrivacySource))
            {
                Debug.LogWarning("[RetroSk8] Privacy manifest missing at " + PrivacySource);
                return;
            }
            string relative = PrivacyFolder + "/PrivacyInfo.xcprivacy";
            Directory.CreateDirectory(Path.Combine(buildPath, PrivacyFolder));
            File.Copy(PrivacySource, Path.Combine(buildPath, relative), true);
            if (project.FindFileGuidByProjectPath(relative) != null) return; // "Append" builds keep the existing reference
            string guid = project.AddFile(relative, relative, PBXSourceTree.Source);
            project.AddFileToBuild(appTarget, guid);
        }

        private static void SetPlistFlags(string buildPath)
        {
            string plistPath = Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            // Retro Sk8 uses no encryption beyond what iOS provides, so App Store Connect can skip the export question.
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            // The native Game Center bridge only wakes up when this is set (see RetroSk8GameCenter.mm).
            plist.root.SetBoolean("RetroSk8GameCenter", RetroSk8BuildOptions.GameCenterEnabled);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
