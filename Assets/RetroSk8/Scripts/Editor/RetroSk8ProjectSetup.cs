using System.Collections.Generic;
using System.IO;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Level;
using RetroSk8.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if RETROSK8_URP
using UnityEngine.Rendering.Universal;
#endif

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// One-click project setup: URP asset, placeholder material, ScriptableObject content, scenes, build settings
    /// and iOS player settings. Safe to run repeatedly; it never overwrites existing assets or scenes.
    /// </summary>
    public static class RetroSk8ProjectSetup
    {
        private const string Root = "Assets/RetroSk8";
        private const string ScenesDir = Root + "/Scenes";
        private const string SoDir = Root + "/ScriptableObjects";
        private const string FirstRunKey = "RetroSk8.SetupPrompted";

        internal static readonly string[] SceneOrder =
        {
            SceneNames.Boot, SceneNames.MainMenu, SceneNames.HarborPlaza, SceneNames.NeonWarehouse,
            SceneNames.RooftopRun, SceneNames.SunsetBowls, SceneNames.FloodgateDitch, SceneNames.MoonlightPier, SceneNames.DriveIn, SceneNames.OffseasonRink, SceneNames.Shipyard, SceneNames.RetroCity, SceneNames.Results, SceneNames.Customization,
        };

        [InitializeOnLoadMethod]
        private static void PromptOnFirstOpen()
        {
            // The key carries a content version so projects set up in an earlier phase get asked once more.
            string key = FirstRunKey + ".v25." + Application.dataPath;
            if (EditorPrefs.GetBool(key, false)) return;
            EditorApplication.delayCall += () =>
            {
                bool missing = !File.Exists(ScenePath(SceneNames.HarborPlaza))
                               || !File.Exists(ScenePath(SceneNames.NeonWarehouse))
                               || !File.Exists(ScenePath(SceneNames.RooftopRun))
                               || !File.Exists(ScenePath(SceneNames.RetroCity))
                               || !File.Exists(ScenePath(SceneNames.SunsetBowls))
                               || !File.Exists(ScenePath(SceneNames.FloodgateDitch))
                               || !File.Exists(ScenePath(SceneNames.MoonlightPier))
                               || !File.Exists(ScenePath(SceneNames.DriveIn))
                               || !File.Exists(ScenePath(SceneNames.OffseasonRink))
                               || !File.Exists(ScenePath(SceneNames.Shipyard));
                if (!missing) return;
                EditorPrefs.SetBool(key, true);
                if (RetroSk8RemoteBridge.RemoteActive)
                {
                    // Driven from outside Unity: no dialog (nobody may be there to click it).
                    RetroSk8RemoteBridge.Log("setup: new content found, running Setup Project");
                    SetupProject();
                    return;
                }
                if (EditorUtility.DisplayDialog("Retro Sk8", "Run project setup now?\n\nCreates or updates URP settings, content assets (including the Neon Warehouse and Rooftop Run parks), scenes and build settings. Existing assets are kept. You can run it later from the 'Retro Sk8' menu.", "Set Up", "Later"))
                    SetupProject();
            };
        }

        [MenuItem("Retro Sk8/Setup Project", priority = 0)]
        public static void SetupProject()
        {
            EnsureFolder(Root, "Settings");
            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Materials");
            EnsureFolder(SoDir, "Tricks");
            EnsureFolder(SoDir, "Locations");
            EnsureFolder(SoDir, "Scoring");
            EnsureFolder(SoDir, "Contracts");
            EnsureFolder(SoDir, "Cosmetics");

            EnsureRenderPipeline();
            var content = EnsureContent();
            EnsureScenes(content, false);
            ConfigureBuildSettings();
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();

#if !ENABLE_INPUT_SYSTEM
            Debug.LogWarning("[RetroSk8] Set Project Settings > Player > Active Input Handling to 'Input System Package (New)' or 'Both', then restart the editor.");
#endif
            EditorSceneManager.OpenScene(ScenePath(SceneNames.HarborPlaza));
            Debug.Log("[RetroSk8] Setup complete. Open BootScene and press Play for the full flow (menu → park → results), or play SkateScene_HarborPlaza directly.");
        }

        [MenuItem("Retro Sk8/Recreate Scenes (overwrite)", priority = 20)]
        public static void RecreateScenes()
        {
            if (!EditorUtility.DisplayDialog("Retro Sk8", "Overwrite BootScene, MainMenuScene, all three park scenes, ResultsScene and CustomizationScene?", "Overwrite", "Cancel")) return;
            EnsureScenes(EnsureContent(), true);
            ConfigureBuildSettings();
        }

        [MenuItem("Retro Sk8/Open Save Folder", priority = 40)]
        public static void OpenSaveFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        // ------------------------------------------------------------------ render pipeline

        private static void EnsureRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;
#if RETROSK8_URP
            try
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, Root + "/Settings/URP_Renderer.asset");
                var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                pipeline.name = "URP_RetroSk8";
                AssetDatabase.CreateAsset(pipeline, Root + "/Settings/URP_RetroSk8.asset");
                GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.renderPipeline = pipeline;
                Debug.Log("[RetroSk8] Created and assigned a URP pipeline asset.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RetroSk8] Could not create a URP asset automatically (" + e.Message +
                                 "). Create one via Assets > Create > Rendering > URP Asset (with Universal Renderer) and assign it in Project Settings > Graphics.");
            }
#else
            Debug.LogWarning("[RetroSk8] URP package not found. Placeholder materials will use the built-in Standard shader.");
#endif
        }

        // ------------------------------------------------------------------ content

        private static ContentRegistry EnsureContent()
        {
            string registryPath = SoDir + "/ContentRegistry.asset";
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(registryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<ContentRegistry>();
                AssetDatabase.CreateAsset(registry, registryPath);
            }

            if (registry.trickLibrary == null) registry.trickLibrary = EnsureTrickLibrary();
            if (registry.scoringProfile == null)
                registry.scoringProfile = LoadOrCreate(SoDir + "/Scoring/ScoringProfile_Default.asset", DefaultContent.CreateScoringProfile);
            EnsureLocation(registry, "Location_HarborPlaza", DefaultContent.CreateHarborPlaza);
            EnsureLocation(registry, "Location_NeonWarehouse", DefaultContent.CreateNeonWarehouse);
            EnsureLocation(registry, "Location_RooftopRun", DefaultContent.CreateRooftopRun);
            EnsureLocation(registry, "Location_SunsetBowls", DefaultContent.CreateSunsetBowls);
            EnsureLocation(registry, "Location_RetroCity", DefaultContent.CreateRetroCity);
            EnsureLocation(registry, "Location_FloodgateDitch", DefaultContent.CreateFloodgateDitch);
            EnsureLocation(registry, "Location_MoonlightPier", DefaultContent.CreateMoonlightPier);
            EnsureLocation(registry, "Location_DriveIn", DefaultContent.CreateDriveIn);
            EnsureLocation(registry, "Location_OffseasonRink", DefaultContent.CreateOffseasonRink);
            EnsureLocation(registry, "Location_Shipyard", DefaultContent.CreateShipyard);
            if (registry.baseLitMaterial == null) registry.baseLitMaterial = EnsureBaseMaterial();
            EnsureContract(registry, "Contract_HarborPlaza", DefaultContent.CreateHarborContract);
            EnsureContract(registry, "Contract_NeonWarehouse", DefaultContent.CreateNeonContract);
            EnsureContract(registry, "Contract_RooftopRun", DefaultContent.CreateRooftopContract);
            EnsureContract(registry, "Contract_SunsetBowls", DefaultContent.CreateSunsetContract);
            EnsureContract(registry, "Contract_RetroCity", DefaultContent.CreateCityContract);
            EnsureContract(registry, "Contract_FloodgateDitch", DefaultContent.CreateDitchContract);
            EnsureContract(registry, "Contract_MoonlightPier", DefaultContent.CreatePierContract);
            EnsureContract(registry, "Contract_DriveIn", DefaultContent.CreateDriveInContract);
            EnsureContract(registry, "Contract_OffseasonRink", DefaultContent.CreateRinkContract);
            EnsureContract(registry, "Contract_Shipyard", DefaultContent.CreateShipyardContract);
            // Adds any default cosmetic the registry is missing (new packs in later versions); existing assets are kept.
            foreach (var c in DefaultContent.CreateCosmetics())
            {
                if (registry.cosmetics.Exists(x => x != null && x.id == c.id)) continue;
                string path = $"{SoDir}/Cosmetics/{c.id}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<CosmeticDefinition>(path);
                if (existing == null) AssetDatabase.CreateAsset(c, path);
                registry.cosmetics.Add(existing != null ? existing : c);
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            return registry;
        }

        /// <summary>
        /// Adds the location if the registry lacks it. Parks that earlier phases created as "coming soon"
        /// (isPlayable = false) are upgraded to the current defaults so they become selectable.
        /// </summary>
        private static void EnsureLocation(ContentRegistry registry, string assetName, System.Func<LocationDefinition> factory)
        {
            var defaults = factory();
            var loc = registry.FindLocationExact(defaults.id);
            if (loc == null)
            {
                loc = LoadOrCreate($"{SoDir}/Locations/{assetName}.asset", () => defaults);
                registry.locations.Add(loc);
            }
            if (!loc.isPlayable && defaults.isPlayable)
            {
                loc.isPlayable = true;
                loc.skyColor = defaults.skyColor;
                loc.ambientColor = defaults.ambientColor;
                loc.fogColor = defaults.fogColor;
                loc.fogDensity = defaults.fogDensity;
                loc.sunColor = defaults.sunColor;
                loc.sunEuler = defaults.sunEuler;
                EditorUtility.SetDirty(loc);
                Debug.Log($"[RetroSk8] {loc.displayName} is now playable.");
            }
            if (!AssetDatabase.Contains(defaults)) Object.DestroyImmediate(defaults);
        }

        private static void EnsureContract(ContentRegistry registry, string assetName, System.Func<ContractDefinition> factory)
        {
            var defaults = factory();
            bool present = false;
            foreach (var c in registry.contracts)
                if (c != null && c.locationId == defaults.locationId) { present = true; break; }
            if (!present)
                registry.contracts.Add(LoadOrCreate($"{SoDir}/Contracts/{assetName}.asset", () => defaults));
            if (!AssetDatabase.Contains(defaults)) Object.DestroyImmediate(defaults);
        }

        private static TrickLibrary EnsureTrickLibrary()
        {
            string path = SoDir + "/Tricks/TrickLibrary.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TrickLibrary>(path);
            if (existing != null) return existing;

            var lib = DefaultContent.CreateTrickLibrary();
            // Each trick becomes its own asset so designers can tune values individually.
            SaveTricks(lib.airTricks);
            SaveTricks(lib.specials);
            SaveTricks(lib.grinds);
            lib.tailManual = SaveTrick(lib.tailManual);
            lib.noseManual = SaveTrick(lib.noseManual);
            AssetDatabase.CreateAsset(lib, path);
            return lib;
        }

        private static void SaveTricks(List<TrickDefinition> list)
        {
            for (int i = 0; i < list.Count; i++) list[i] = SaveTrick(list[i]);
        }

        private static TrickDefinition SaveTrick(TrickDefinition t)
        {
            string path = $"{SoDir}/Tricks/{t.id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TrickDefinition>(path);
            if (existing != null) return existing;
            AssetDatabase.CreateAsset(t, path);
            return t;
        }

        private static T LoadOrCreate<T>(string path, System.Func<T> factory) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = factory();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static Material EnsureBaseMaterial()
        {
            string path = Root + "/Materials/M_Placeholder_Lit.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = null;
            if (GraphicsSettings.defaultRenderPipeline != null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = "M_Placeholder_Lit" };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ------------------------------------------------------------------ scenes

        private static void EnsureScenes(ContentRegistry content, bool overwrite)
        {
            var harbor = content.FindLocation("harbor_plaza");

            CreateScene(SceneNames.Boot, overwrite, () =>
            {
                AddCamera(Theme.Ink);
                var go = new GameObject("Bootstrap");
                go.AddComponent<GameBootstrap>().content = content;
            });

            CreateParkScene(SceneNames.HarborPlaza, content, ParkCatalog.HarborPlaza, overwrite);
            CreateParkScene(SceneNames.NeonWarehouse, content, ParkCatalog.NeonWarehouse, overwrite);
            CreateParkScene(SceneNames.RooftopRun, content, ParkCatalog.RooftopRun, overwrite);
            CreateParkScene(SceneNames.SunsetBowls, content, ParkCatalog.SunsetBowls, overwrite);
            CreateParkScene(SceneNames.RetroCity, content, ParkCatalog.RetroCity, overwrite);
            CreateParkScene(SceneNames.FloodgateDitch, content, ParkCatalog.FloodgateDitch, overwrite);
            CreateParkScene(SceneNames.MoonlightPier, content, ParkCatalog.MoonlightPier, overwrite);
            CreateParkScene(SceneNames.DriveIn, content, ParkCatalog.DriveIn, overwrite);
            CreateParkScene(SceneNames.OffseasonRink, content, ParkCatalog.OffseasonRink, overwrite);
            CreateParkScene(SceneNames.Shipyard, content, ParkCatalog.Shipyard, overwrite);

            CreateScene(SceneNames.Results, overwrite, () =>
            {
                AddCamera(Theme.Ink);
                new GameObject("Results").AddComponent<ResultsView>();
            });

            CreateScene(SceneNames.MainMenu, overwrite, () =>
            {
                AddCamera(Theme.Ink);
                new GameObject("MainMenu").AddComponent<MainMenuView>().content = content;
            });

            CreateScene(SceneNames.Customization, overwrite, () =>
            {
                var light = new GameObject("Key Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
                AddCamera(new Color(0.12f, 0.12f, 0.15f));
                new GameObject("Customization").AddComponent<CustomizationView>().content = content;
            });
        }

        private static void CreateParkScene(string sceneName, ContentRegistry content, string locationId, bool overwrite)
        {
            var location = content.FindLocationExact(locationId);
            if (location == null) return;
            CreateScene(sceneName, overwrite, () =>
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(location.sunEuler);
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;
                AddCamera(location.skyColor);

                // The builder generates the park at runtime, so scenes stay tiny and layout edits live in code.
                ParkCatalog.AddBuilder(new GameObject(location.displayName), locationId);

                var installer = new GameObject("SkateSceneInstaller").AddComponent<SkateSceneInstaller>();
                installer.content = content;
                installer.location = location;
            });
        }

        private static void CreateScene(string name, bool overwrite, System.Action populate)
        {
            string path = ScenePath(name);
            if (!overwrite && File.Exists(path)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            populate();
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void AddCamera(Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 3f, 32f);
        }

        internal static string ScenePath(string name) => $"{ScenesDir}/{name}.unity";

        private static void ConfigureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var name in SceneOrder)
            {
                string path = ScenePath(name);
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Player settings for a phone build. Also called by the iOS build menu, so builds never ship template ids.</summary>
        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "Retro Sk8";
            if (string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany")
                PlayerSettings.companyName = "RetroSk8 Prototype";

            string iosId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            if (IsPlaceholderBundleId(iosId))
            {
                // Bundle ids are unique across all Apple developer accounts, so include the Mac user name
                // to avoid colliding with someone else's "retrosk8" app when Xcode registers it.
                string id = "com." + SanitizeIdPart(System.Environment.UserName) + ".retrosk8";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, id);
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, id);
                Debug.Log($"[RetroSk8] Bundle identifier set to {id}. Change it in Player Settings if Xcode says it is unavailable.");
            }

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            // Phase 18: the paid team signs every device build, so Xcode and TestFlight uploads need no clicking.
            string team = RetroSk8BuildOptions.TeamId;
            if (RetroSk8BuildOptions.IsValidTeamId(team)) PlayerSettings.iOS.appleDeveloperTeamID = team;
            PlayerSettings.statusBarHidden = true;
            // Real CPU/GPU frame times drive the adaptive resolution (DevicePerformance) and the performance HUD.
            PlayerSettings.enableFrameTimingStats = true;
            // Landscape-only games must opt out of iPad multitasking, or App Store validation rejects the build.
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad; // Phase 12: the UI scales to iPad
            if (NeedsVersionBump(PlayerSettings.bundleVersion)) PlayerSettings.bundleVersion = AppVersion;
            if (string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber)) PlayerSettings.iOS.buildNumber = "0";
            ConfigureIconAndLaunchScreen();
        }

        /// <summary>Marketing version shown in TestFlight / the App Store (major.minor.patch). Bump it per release.</summary>
        public const string AppVersion = "0.26.0";

        /// <summary>True for template versions and older prototype versions (never lowers a version you set yourself).</summary>
        internal static bool NeedsVersionBump(string current)
        {
            if (string.IsNullOrEmpty(current) || current == "0.1" || current == "0.1.0" || current == "1.0") return true;
            return System.Version.TryParse(current, out var have) && System.Version.TryParse(AppVersion, out var want) && have < want;
        }
        private const string IconPath = "Assets/RetroSk8/Art/AppIcon/RetroSk8_AppIcon.png";
        private const string LaunchPath = "Assets/RetroSk8/Art/AppIcon/RetroSk8_LaunchScreen.png";

        /// <summary>Original icon and launch art (generated by Tools/make_icons.py). Uncompressed so they stay crisp.</summary>
        private static void ConfigureIconAndLaunchScreen()
        {
            var icon = LoadUncompressed(IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            var launch = LoadUncompressed(LaunchPath);
            if (launch != null)
            {
                PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
                PlayerSettings.iOS.SetiPadLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
                PlayerSettings.iOS.SetLaunchScreenImage(launch, iOSLaunchScreenImageType.iPhoneLandscapeImage);
                PlayerSettings.iOS.SetLaunchScreenImage(launch, iOSLaunchScreenImageType.iPadImage);
            }
        }

        private static Texture2D LoadUncompressed(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            bool dirty = false;
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (importer.npotScale != TextureImporterNPOTScale.None) { importer.npotScale = TextureImporterNPOTScale.None; dirty = true; }
            if (importer.maxTextureSize < 4096) { importer.maxTextureSize = 4096; dirty = true; }
            if (dirty) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static bool IsPlaceholderBundleId(string id) =>
            string.IsNullOrEmpty(id) || id.Contains("DefaultCompany") || id.Contains("Unity-Technologies")
            || id.Contains("com.unity.template") || id == "com.retrosk8.prototype";

        private static string SanitizeIdPart(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in (s ?? "").ToLowerInvariant()) if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
            return sb.Length > 0 ? sb.ToString() : "player";
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent))
            {
                string p = Path.GetDirectoryName(parent)?.Replace('\\', '/');
                EnsureFolder(p, Path.GetFileName(parent));
            }
            if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
