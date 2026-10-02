using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Data
{
    /// <summary>
    /// Single entry point to authored content, referenced by scene installers.
    /// Everything here is a direct reference today; each list can later become an Addressables label without touching gameplay code.
    /// </summary>
    [CreateAssetMenu(menuName = "Retro Sk8/Content Registry", fileName = "ContentRegistry")]
    public sealed class ContentRegistry : ScriptableObject
    {
        public TrickLibrary trickLibrary;
        public ScoringProfile scoringProfile;
        public List<LocationDefinition> locations = new List<LocationDefinition>();
        public List<ContractDefinition> contracts = new List<ContractDefinition>();
        public List<CosmeticDefinition> cosmetics = new List<CosmeticDefinition>();
        [Tooltip("Lit material cloned for placeholder geometry. Referencing it keeps the shader in player builds.")]
        public Material baseLitMaterial;

        private static readonly Dictionary<ContentRegistry, ContentRegistry> s_withDefaults = new Dictionary<ContentRegistry, ContentRegistry>();

        /// <summary>
        /// A runtime copy of <paramref name="source"/> topped up with every built-in park and contract it's missing
        /// (and parks saved as "coming soon" unlocked). Lets new parks play in projects whose content assets were made
        /// by an older Setup. The asset on disk is never modified.
        /// </summary>
        public static ContentRegistry WithDefaults(ContentRegistry source)
        {
            if (source == null) return DefaultContent.CreateRegistry();
            if (s_withDefaults.TryGetValue(source, out var cached) && cached != null) return cached;
            if (s_withDefaults.ContainsValue(source)) return source; // already a topped-up copy

            var copy = Instantiate(source);
            copy.name = source.name + "_Runtime";
            var defaults = DefaultContent.CreateRegistry();
            foreach (var d in defaults.locations)
            {
                int i = copy.locations.FindIndex(l => l != null && l.id == d.id);
                if (i < 0) { copy.locations.Add(d); continue; }
                var existing = copy.locations[i];
                if (!existing.isPlayable && d.isPlayable)
                {
                    var unlocked = Instantiate(existing);
                    unlocked.isPlayable = true;
                    unlocked.skyColor = d.skyColor;
                    unlocked.ambientColor = d.ambientColor;
                    unlocked.fogColor = d.fogColor;
                    unlocked.fogDensity = d.fogDensity;
                    unlocked.sunColor = d.sunColor;
                    unlocked.sunEuler = d.sunEuler;
                    copy.locations[i] = unlocked;
                }
            }
            foreach (var c in defaults.contracts)
                if (copy.FindContract(c.locationId) == null) copy.contracts.Add(c);
            if (copy.cosmetics.Count == 0) copy.cosmetics.AddRange(defaults.cosmetics);
            if (copy.trickLibrary == null) copy.trickLibrary = defaults.trickLibrary;
            if (copy.scoringProfile == null) copy.scoringProfile = defaults.scoringProfile;
            s_withDefaults[source] = copy;
            return copy;
        }

        public ContractDefinition FindContract(string locationId)
        {
            foreach (var c in contracts) if (c != null && c.locationId == locationId) return c;
            return null;
        }

        /// <summary>Exact match only (null when absent), unlike <see cref="FindLocation"/> which falls back to the first park.</summary>
        public LocationDefinition FindLocationExact(string id)
        {
            foreach (var l in locations) if (l != null && l.id == id) return l;
            return null;
        }

        /// <summary>Parks a player can pick, in registry order.</summary>
        public List<LocationDefinition> PlayableLocations()
        {
            var list = new List<LocationDefinition>();
            foreach (var l in locations) if (l != null && l.isPlayable) list.Add(l);
            return list;
        }

        public LocationDefinition FindLocation(string id)
        {
            foreach (var l in locations) if (l != null && l.id == id) return l;
            return locations.Count > 0 ? locations[0] : null;
        }
    }
}
