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
    /// export-compliance question in Info.plist, declares game-controller support, adds the In-App Purchase
    /// capability (cosmetic packs) and the Game Center capability when it is switched on.
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
            project.AddFrameworkToProject(framework, "StoreKit.framework", false); // cosmetic packs (RetroSk8Store.mm)
            project.AddFrameworkToProject(framework, "GameController.framework", true); // controllers (the Input System uses it)
            project.AddFrameworkToProject(framework, "UserNotifications.framework", false); // opt-in reminders (RetroSk8Notify.mm)
            project.AddFrameworkToProject(framework, "CloudKit.framework", true); // online gallery (RetroSk8Gallery.mm); weak when off
            project.AddFrameworkToProject(framework, "Security.framework", false); // save seal key in the Keychain (RetroSk8Keychain.mm, Phase 19)
            // RetroSk8Entitlements.swift (Phase 19): Swift 5 language mode for the plugin compiled into UnityFramework.
            if (string.IsNullOrEmpty(project.GetBuildPropertyForAnyConfig(framework, "SWIFT_VERSION")))
                project.SetBuildProperty(framework, "SWIFT_VERSION", "5.0");

            AddPrivacyManifest(project, app, path);
            project.WriteToFile(projectPath);

            SetPlistFlags(path);

            // Every step is written to Temp/RetroSk8PostBuild.txt (an exception here doesn't fail Unity's build, so
            // the log is the only way to see that a capability went missing).
            var log = new System.Text.StringBuilder("POSTBUILD " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            log.AppendLine("frameworks, privacy manifest, Info.plist: done");
            try
            {
                var caps = new ProjectCapabilityManager(projectPath, "Unity-iPhone/RetroSk8.entitlements", null, app);
                // Unity 6 adds no In-App Purchase entry to the Xcode project here (StoreKit needs none), so free
                // Apple accounts can still sign the build; the check below reports what actually landed.
                caps.AddInAppPurchase(); // cosmetic packs
                log.AppendLine("In-App Purchase: requested");
                if (RetroSk8BuildOptions.GameCenterEnabled)
                {
                    caps.AddGameCenter();
                    log.AppendLine("Game Center: added (needs a paid Apple developer team)");
                }
                else log.AppendLine("Game Center: off");
                if (RetroSk8BuildOptions.GalleryEnabled)
                {
                    // iCloud with CloudKit and the default container (iCloud.<bundle id>).
                    caps.AddiCloud(false, false, true, true, null);
                    log.AppendLine("iCloud (CloudKit) for the gallery: added (needs a paid Apple developer team)");
                }
                else log.AppendLine("Online gallery: off");
                caps.WriteToFile();
                // Unity 6 puts capabilities in the entitlements file (Game Center, iCloud); In-App Purchase needs none.
                string entPath = Path.Combine(path, "Unity-iPhone/RetroSk8.entitlements");
                string ent = File.Exists(entPath) ? File.ReadAllText(entPath) : "";
                log.AppendLine("Entitlements file: " + (ent.Length > 0 ? "Unity-iPhone/RetroSk8.entitlements" : "none"));
                log.AppendLine("Entitlements list Game Center: " + (ent.Contains("com.apple.developer.game-center") ? "yes" : "no"));
                log.AppendLine("Entitlements list iCloud (CloudKit): " + (ent.Contains("com.apple.developer.icloud-services") ? "yes" : "no"));
            }
            catch (System.Exception e)
            {
                log.AppendLine("CAPABILITIES FAILED: " + e);
                Debug.LogError("[RetroSk8] Adding iOS capabilities failed: " + e);
            }
            try { Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/RetroSk8PostBuild.txt", log.ToString()); }
            catch (IOException) { }
            Debug.Log("[RetroSk8] " + log);
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
            // The gallery bridge never touches CloudKit unless this is set (an app without the entitlement would crash).
            plist.root.SetBoolean("RetroSk8Gallery", RetroSk8BuildOptions.GalleryEnabled);
            // Game controllers: menus are fully navigable with a controller, and the App Store can show the badge.
            plist.root.SetBoolean("GCSupportsControllerUserInteraction", true);
            var controllers = plist.root.CreateArray("GCSupportedGameControllers");
            controllers.AddDict().SetString("ProfileName", "ExtendedGamepad");
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
