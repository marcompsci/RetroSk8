using RetroSk8.Data;
using UnityEditor;
using UnityEngine;
#if RETROSK8_ADDRESSABLES
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
#endif

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// Organizes Retro Sk8's content into Addressables groups so parks and cosmetics can later ship as
    /// downloadable bundles. Groups: RetroSk8_Core (registry, tricks, scoring), RetroSk8_Parks (locations,
    /// contracts) and RetroSk8_Cosmetics. Every entry's address is its asset name; labels mark the kind.
    /// Runtime loading still uses the direct references in ContentRegistry until a later phase switches it.
    /// </summary>
    public static class RetroSk8AddressablesSetup
    {
        private const string RegistryPath = "Assets/RetroSk8/ScriptableObjects/ContentRegistry.asset";

        [MenuItem("Retro Sk8/Setup Addressables Groups", priority = 30)]
        public static void Setup()
        {
#if RETROSK8_ADDRESSABLES
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(RegistryPath);
            if (registry == null)
            {
                EditorUtility.DisplayDialog("Retro Sk8", "Run 'Retro Sk8 → Setup Project' first so the content assets exist.", "OK");
                return;
            }

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var core = Group(settings, "RetroSk8_Core");
            var parks = Group(settings, "RetroSk8_Parks");
            var cosmetics = Group(settings, "RetroSk8_Cosmetics");

            int count = 0;
            count += Add(settings, registry, core, "core");
            count += Add(settings, registry.trickLibrary, core, "tricks");
            count += Add(settings, registry.scoringProfile, core, "core");
            foreach (var l in registry.locations) count += Add(settings, l, parks, "park");
            foreach (var c in registry.contracts) count += Add(settings, c, parks, "contract");
            foreach (var c in registry.cosmetics) count += Add(settings, c, cosmetics, "cosmetic");

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RetroSk8] Addressables: {count} entries in RetroSk8_Core, RetroSk8_Parks and RetroSk8_Cosmetics.");
#else
            EditorUtility.DisplayDialog("Retro Sk8",
                "The Addressables package isn't installed.\n\nWindow → Package Manager → Unity Registry → Addressables → Install, then run this again.",
                "OK");
#endif
        }

#if RETROSK8_ADDRESSABLES
        private static AddressableAssetGroup Group(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name);
            if (group == null)
                group = settings.CreateGroup(name, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            return group;
        }

        private static int Add(AddressableAssetSettings settings, Object asset, AddressableAssetGroup group, string label)
        {
            if (asset == null) return 0;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            if (string.IsNullOrEmpty(guid)) return 0; // runtime-only object, not an asset
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = asset.name;
            settings.AddLabel(label, false);
            entry.SetLabel(label, true, true, false);
            return 1;
        }
#endif
    }
}
