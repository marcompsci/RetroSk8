using System.Collections.Generic;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Data
{
    [CreateAssetMenu(menuName = "Retro Sk8/Trick Library", fileName = "TrickLibrary")]
    public sealed class TrickLibrary : ScriptableObject
    {
        public List<TrickDefinition> airTricks = new List<TrickDefinition>();
        public List<TrickDefinition> specials = new List<TrickDefinition>();
        [Tooltip("Index = grind variation (0 neutral, 1 left, 2 right, 3 up/down).")]
        public List<TrickDefinition> grinds = new List<TrickDefinition>();
        public TrickDefinition tailManual;
        public TrickDefinition noseManual;

        [Tooltip("Spin names by half turns: element 0 = 180, 1 = 360, ...")]
        public List<string> spinNames = new List<string>();

        public TrickDefinition GetAir(TrickFamily family, int variation, bool special)
        {
            if (special)
            {
                foreach (var t in specials) if (t != null && t.family == family) return t;
            }
            TrickDefinition fallback = null;
            foreach (var t in airTricks)
            {
                if (t == null || t.family != family) continue;
                if (t.variation == variation) return t;
                if (fallback == null) fallback = t;
            }
            return fallback;
        }

        public TrickDefinition GetGrind(int variation)
        {
            if (grinds.Count == 0) return null;
            return grinds[Mathf.Clamp(variation, 0, grinds.Count - 1)];
        }

        public TrickDefinition GetManual(bool nose) => nose && noseManual != null ? noseManual : tailManual;

        public string SpinName(int halfTurns)
        {
            if (halfTurns <= 0) return string.Empty;
            int i = halfTurns - 1;
            if (i < spinNames.Count && !string.IsNullOrEmpty(spinNames[i])) return spinNames[i];
            return $"Cyclone {halfTurns * 180}";
        }

        public static string SpinId(int halfTurns) => $"spin_{halfTurns * 180}";

        public IEnumerable<TrickDefinition> All()
        {
            foreach (var t in airTricks) yield return t;
            foreach (var t in specials) yield return t;
            foreach (var t in grinds) yield return t;
            if (tailManual != null) yield return tailManual;
            if (noseManual != null) yield return noseManual;
        }
    }
}
