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

        private static readonly string[] SceneOrder =
        {
            SceneNames.Boot, SceneNames.MainMenu, SceneNames.HarborPlaza, SceneNames.NeonWarehouse,
            SceneNames.RooftopRun, SceneNames.Results, SceneNames.Customization,
        };

        [InitializeOnLoadMethod]
        private static void PromptOnFirstOpen()
        {
            if (EditorPrefs.GetBool(FirstRunKey + "." + Application.dataPath, false)) return;
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(ScenePath(SceneNames.HarborPlaza))) return;
                EditorPrefs.SetBool(FirstRunKey + "." + Application.dataPath, true);
                if (EditorUtility.DisplayDialog("Retro Sk8", "Run first-time project setup now?\n\nCreates URP settings, content assets, scenes and build settings. You can run it later from the 'Retro Sk8' menu.", "Set Up", "Later"))
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
            if (!EditorUtility.DisplayDialog("Retro Sk8", "Overwrite BootScene, MainMenuScene, SkateScene_HarborPlaza, ResultsScene and CustomizationScene?", "Overwrite", "Cancel")) return;
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
            if (registry.locations.Count == 0)
            {
                registry.locations.Add(LoadOrCreate(SoDir + "/Locations/Location_HarborPlaza.asset", DefaultContent.CreateHarborPlaza));
                registry.locations.Add(LoadOrCreate(SoDir + "/Locations/Location_NeonWarehouse.asset", DefaultContent.CreateNeonWarehouse));
                registry.locations.Add(LoadOrCreate(SoDir + "/Locations/Location_RooftopRun.asset", DefaultContent.CreateRooftopRun));
            }
            if (registry.baseLitMaterial == null) registry.baseLitMaterial = EnsureBaseMaterial();
            if (registry.contracts.Count == 0)
                registry.contracts.Add(LoadOrCreate(SoDir + "/Contracts/Contract_HarborPlaza.asset", DefaultContent.CreateHarborContract));
            if (registry.cosmetics.Count == 0)
            {
                foreach (var c in DefaultContent.CreateCosmetics())
                {
                    string path = $"{SoDir}/Cosmetics/{c.id}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<CosmeticDefinition>(path);
                    if (existing == null) AssetDatabase.CreateAsset(c, path);
                    registry.cosmetics.Add(existing != null ? existing : c);
                }
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            return registry;
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

            CreateScene(SceneNames.HarborPlaza, overwrite, () =>
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(harbor.sunEuler);
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;
                AddCamera(harbor.skyColor);

                var level = new GameObject("HarborPlaza").AddComponent<HarborPlazaBuilder>();
                level.buildOnAwake = false;

                var installer = new GameObject("SkateSceneInstaller").AddComponent<SkateSceneInstaller>();
                installer.content = content;
                installer.location = harbor;
            });

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

        private static string ScenePath(string name) => $"{ScenesDir}/{name}.unity";

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

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "Retro Sk8";
            if (string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany")
                PlayerSettings.companyName = "RetroSk8 Prototype";
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS).Contains("DefaultCompany")
                || string.IsNullOrEmpty(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS)))
            {
                // Placeholder identifier: replace with your own reverse-DNS id before shipping.
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.retrosk8.prototype");
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.retrosk8.prototype");
            }
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
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
