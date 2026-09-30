using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RetroSk8.Level
{
    /// <summary>
    /// Flat-colour materials for primitive placeholder art. Uses the registry's base Lit material when available
    /// (so the shader survives build stripping), otherwise picks URP Lit or the built-in Standard shader.
    /// </summary>
    public static class PlaceholderMaterials
    {
        private static readonly Dictionary<Color32, Material> s_cache = new Dictionary<Color32, Material>();
        private static Material s_base;

        public static void SetBase(Material baseMaterial)
        {
            if (baseMaterial != null && baseMaterial != s_base)
            {
                s_base = baseMaterial;
                s_cache.Clear();
            }
        }

        public static Material Get(Color color, float smoothness = 0.15f)
        {
            Color32 key = color;
            if (s_cache.TryGetValue(key, out var m) && m != null) return m;

            m = s_base != null ? new Material(s_base) : new Material(FindShader());
            m.name = $"Placeholder_{ColorUtility.ToHtmlStringRGB(color)}";
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            s_cache[key] = m;
            return m;
        }

        private static readonly Dictionary<Texture, Material> s_textured = new Dictionary<Texture, Material>();

        /// <summary>A lit material showing <paramref name="texture"/> (deck graphics).</summary>
        public static Material GetTextured(Texture texture)
        {
            if (texture == null) return Get(Color.white);
            if (s_textured.TryGetValue(texture, out var m) && m != null) return m;
            m = s_base != null ? new Material(s_base) : new Material(FindShader());
            m.name = "Textured_" + texture.name;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
            s_textured[texture] = m;
            return m;
        }

        private static Shader FindShader()
        {
            Shader s = null;
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Sprites/Default");
            return s;
        }
    }

    /// <summary>Retro Sk8 original placeholder palette.</summary>
    public static class Palette
    {
        public static readonly Color Concrete = new Color(0.72f, 0.69f, 0.66f);
        public static readonly Color ConcreteDark = new Color(0.52f, 0.5f, 0.5f);
        public static readonly Color Paving = new Color(0.62f, 0.58f, 0.55f);
        public static readonly Color Metal = new Color(0.78f, 0.8f, 0.84f);
        public static readonly Color Coping = new Color(0.3f, 0.32f, 0.36f);
        public static readonly Color ContainerRed = new Color(0.78f, 0.27f, 0.2f);
        public static readonly Color ContainerTeal = new Color(0.12f, 0.55f, 0.58f);
        public static readonly Color ContainerMustard = new Color(0.86f, 0.66f, 0.18f);
        public static readonly Color Water = new Color(0.1f, 0.35f, 0.5f);
        public static readonly Color Brick = new Color(0.55f, 0.3f, 0.26f);
        public static readonly Color Wood = new Color(0.55f, 0.4f, 0.25f);
        public static readonly Color TapeYellow = new Color(0.95f, 0.76f, 0.19f);
        public static readonly Color Coral = new Color(1f, 0.35f, 0.3f);
        public static readonly Color Teal = new Color(0.12f, 0.78f, 0.71f);
        public static readonly Color Ink = new Color(0.07f, 0.075f, 0.09f);
        public static readonly Color Cream = new Color(0.95f, 0.91f, 0.82f);
    }
}
