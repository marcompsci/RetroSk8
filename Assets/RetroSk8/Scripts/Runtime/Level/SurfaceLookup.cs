using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    public enum SurfaceKind
    {
        Concrete = 0,
        Wood = 1,
        Metal = 2,
        Rubber = 3,
    }

    /// <summary>Optional explicit surface for a collider (overrides the colour-based guess).</summary>
    public sealed class SurfaceTag : MonoBehaviour
    {
        public SurfaceKind kind = SurfaceKind.Concrete;
    }

    /// <summary>
    /// What the skater is rolling on, for sound. Explicit <see cref="SurfaceTag"/>s and conveyor belts win;
    /// otherwise the placeholder material's palette colour decides (plywood and wood → Wood, metal and
    /// coping → Metal, rubber → Rubber, anything else → Concrete). Results are cached per material.
    /// </summary>
    public static class SurfaceLookup
    {
        private static readonly Dictionary<Material, SurfaceKind> s_byMaterial = new Dictionary<Material, SurfaceKind>();

        private static readonly (Color color, SurfaceKind kind)[] s_palette =
        {
            (Palette.Plywood, SurfaceKind.Wood),
            (Palette.Wood, SurfaceKind.Wood),
            (Palette.Metal, SurfaceKind.Metal),
            (Palette.Coping, SurfaceKind.Metal),
            (Palette.Rubber, SurfaceKind.Rubber),
        };

        public static SurfaceKind FromCollider(Collider c)
        {
            if (c == null) return SurfaceKind.Concrete;
            var tag = c.GetComponent<SurfaceTag>();
            if (tag != null) return tag.kind;
            if (c.GetComponent<ConveyorSurface>() != null) return SurfaceKind.Rubber;
            var r = c.GetComponent<Renderer>();
            return r != null ? FromMaterial(r.sharedMaterial) : SurfaceKind.Concrete;
        }

        public static SurfaceKind FromMaterial(Material m)
        {
            if (m == null) return SurfaceKind.Concrete;
            if (s_byMaterial.TryGetValue(m, out var kind)) return kind;
            Color col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.gray);
            kind = Classify(col);
            s_byMaterial[m] = kind;
            return kind;
        }

        /// <summary>Nearest palette colour within a small tolerance, else concrete.</summary>
        public static SurfaceKind Classify(Color col)
        {
            foreach (var (color, kind) in s_palette)
            {
                float d = Mathf.Abs(col.r - color.r) + Mathf.Abs(col.g - color.g) + Mathf.Abs(col.b - color.b);
                if (d < 0.03f) return kind;
            }
            return SurfaceKind.Concrete;
        }
    }
}
