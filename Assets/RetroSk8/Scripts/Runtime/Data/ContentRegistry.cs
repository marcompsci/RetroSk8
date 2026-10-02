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
