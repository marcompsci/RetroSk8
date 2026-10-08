using System.Collections.Generic;
using RetroSk8.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Phase 28: shows every label in the chosen language. UIFactory.Label registers each Text it makes; once a frame
    /// (after the screens have written their English) any label whose text changed is translated with
    /// <see cref="Loc.T"/>, gets the language's font, and is shrunk a little if the translation no longer fits its
    /// box. Typed text (InputField contents) is never touched. Changing the language re-translates everything on
    /// screen from the English each label last showed.
    /// </summary>
    public sealed class LocText : MonoBehaviour
    {
        private sealed class Entry
        {
            public Text Text;
            public string Source;   // the English the screen last wrote
            public string Output;   // what we turned it into
            public int BaseSize;    // the size the screen asked for
            public int LastSet;     // the size we last gave it (anything else means the screen changed it)
            public bool Skip;       // typed text
            public bool AsWritten;  // the screen translates it itself (only the font follows the language)
            public bool Checked;
        }

        private static readonly List<Entry> s_entries = new List<Entry>(512);
        private static readonly Dictionary<Text, Entry> s_byText = new Dictionary<Text, Entry>();
        private static LocText s_instance;
        private static Font s_japaneseFont;
        private static bool s_dirtyAll;

        /// <summary>Smallest a translated label may shrink to, as a share of its intended size.</summary>
        public const float MinShrink = 0.66f;

        public static void Register(Text t)
        {
            if (t == null || s_byText.ContainsKey(t)) return;
            var e = new Entry { Text = t, BaseSize = t.fontSize, LastSet = t.fontSize };
            s_entries.Add(e);
            s_byText[t] = e;
            Ensure();
        }

        /// <summary>
        /// For labels a screen translates itself (the comic typewriter types out an already translated line): LocText
        /// leaves the text alone and only gives it the language's font.
        /// </summary>
        public static void KeepAsWritten(Text t)
        {
            Register(t);
            if (t != null && s_byText.TryGetValue(t, out var e)) e.AsWritten = true;
        }

        /// <summary>The English behind a label (what the screen wrote), for code that checks what a label says.</summary>
        public static string Source(Text t)
        {
            if (t == null) return "";
            if (s_byText.TryGetValue(t, out var e) && e.Output != null && t.text == e.Output && e.Source != null) return e.Source;
            return t.text;
        }

        /// <summary>The font for the current language (Japanese needs one with kana and kanji).</summary>
        public static Font FontFor(Lang lang, Font latin)
        {
            if (lang != Lang.Japanese) return latin;
            if (s_japaneseFont == null)
            {
                s_japaneseFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Hiragino Sans", "HiraginoSans-W6", "Hiragino Kaku Gothic ProN", "Hiragino Kaku Gothic Pro", "Noto Sans CJK JP", "Noto Sans JP", "Yu Gothic", "Meiryo", "Arial Unicode MS" }, 32);
                if (s_japaneseFont == null) return latin;
            }
            return s_japaneseFont;
        }

        private static void Ensure()
        {
            if (s_instance != null) return;
            var go = new GameObject("LocText") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<LocText>();
            Loc.Changed += () => s_dirtyAll = true;
        }

        private void LateUpdate() => Pump();

        /// <summary>Translates every label that changed since the last frame (public for tests).</summary>
        public static void Pump()
        {
            bool all = s_dirtyAll;
            s_dirtyAll = false;
            Font font = null;
            for (int i = s_entries.Count - 1; i >= 0; i--)
            {
                var e = s_entries[i];
                var t = e.Text;
                if (t == null)
                {
                    s_entries.RemoveAt(i);
                    s_byText.Remove(t ?? e.Text);
                    continue;
                }
                if (!e.Checked)
                {
                    e.Checked = true;
                    var field = t.GetComponentInParent<InputField>(true);
                    e.Skip = field != null && field.textComponent == t;
                }
                if (e.Skip) continue;
                string now = t.text;
                bool changed = !ReferenceEquals(now, e.Output) && now != e.Output;
                if (!changed && !all) continue;
                if (changed) e.Source = now;
                if (t.fontSize != e.LastSet) e.BaseSize = e.LastSet = t.fontSize; // the screen resized it
                if (font == null) font = FontFor(Loc.Current, UIFactory.LatinFont);
                if (t.font != font) t.font = font;
                if (e.AsWritten) { e.Output = now; continue; }
                string output = Loc.T(e.Source ?? now);
                e.Output = output;
                if (!ReferenceEquals(t.text, output)) t.text = output;
                Fit(e);
            }
            // Text created before the first label registered with Loc.Changed: keep the dictionary in step.
            if (s_byText.Count > s_entries.Count * 2) Rebuild();
        }

        private static void Rebuild()
        {
            s_byText.Clear();
            foreach (var e in s_entries) if (e.Text != null) s_byText[e.Text] = e;
        }

        /// <summary>Shrinks a label whose translation is wider (or taller) than its box; English keeps its size.</summary>
        private static void Fit(Entry e)
        {
            var t = e.Text;
            if (t.resizeTextForBestFit) return;
            if (t.fontSize != e.BaseSize) SetSize(e, e.BaseSize);
            if (Loc.Current == Lang.English || e.Output == e.Source) return;
            var rect = t.rectTransform.rect;
            if (rect.width < 40f || rect.height < 10f) return;
            float scale = 1f;
            if (t.horizontalOverflow == HorizontalWrapMode.Overflow)
            {
                // Some English labels were laid out to run past their box; the translation may use as much room.
                float room = Mathf.Max(rect.width, Measure(t, e.Source, true));
                float w = t.preferredWidth;
                if (w > room * 1.02f) scale = room / w;
            }
            else
            {
                float room = Mathf.Max(rect.height, Measure(t, e.Source, false));
                float h = t.preferredHeight;
                if (h > room * 1.02f) scale = Mathf.Sqrt(room / h);
            }
            if (scale >= 1f) return;
            SetSize(e, Mathf.Max(Mathf.RoundToInt(e.BaseSize * MinShrink), Mathf.FloorToInt(e.BaseSize * scale)));
        }

        private static void SetSize(Entry e, int size)
        {
            e.Text.fontSize = size;
            e.LastSet = size;
        }

        /// <summary>The width (or height) <paramref name="text"/> takes in this label at its current settings.</summary>
        private static float Measure(Text t, string text, bool width)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            var settings = t.GetGenerationSettings(width ? Vector2.zero : new Vector2(t.rectTransform.rect.width, 0f));
            var gen = t.cachedTextGeneratorForLayout;
            float ppu = t.pixelsPerUnit;
            return (width ? gen.GetPreferredWidth(text, settings) : gen.GetPreferredHeight(text, settings)) / (ppu > 0f ? ppu : 1f);
        }

        /// <summary>How many labels are being looked after (tests).</summary>
        public static int Count => s_entries.Count;
    }
}
