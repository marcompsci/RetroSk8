namespace RetroSk8.Core
{
    /// <summary>
    /// Phase 29: which languages ship. 1.0 goes out in English only; Spanish and Japanese stay in preview until native
    /// speakers have checked them, then ship as the 1.1 update (flip <see cref="Shipped"/>). Testers turn the preview on
    /// with the code LANG PREVIEW on the Codes screen. Engine-free so the rules are unit-tested.
    /// </summary>
    public static class LangRelease
    {
        /// <summary>Indexed by <see cref="Lang"/>: English, Spanish, Japanese.</summary>
        private static readonly bool[] Shipped = { true, false, false };

        public const string PreviewCode = "LANG PREVIEW";

        public static bool IsShipped(Lang lang)
        {
            int i = (int)lang;
            return i >= 0 && i < Shipped.Length && Shipped[i];
        }

        public static bool Available(Lang lang, bool preview) => preview || IsShipped(lang);

        /// <summary>The language actually used: anything not shipped falls back to English unless the preview is on.</summary>
        public static Lang Gate(Lang wanted, bool preview) => Available(wanted, preview) ? wanted : Lang.English;

        /// <summary>The settings language button only appears when there's more than one language to pick.</summary>
        public static bool ShowPicker(bool preview)
        {
            int n = 0;
            for (int i = 0; i < Loc.LangCount; i++) if (Available((Lang)i, preview)) n++;
            return n > 1;
        }

        /// <summary>The settings cycle: AUTO → each available language → AUTO (-1 = follow the phone).</summary>
        public static int NextChoice(int saved, bool preview)
        {
            int next = saved;
            for (int step = 0; step <= Loc.LangCount; step++)
            {
                next++;
                if (next >= Loc.LangCount) return -1;
                if (Available((Lang)next, preview)) return next;
            }
            return -1;
        }

        /// <summary>True for "LANG PREVIEW" however it's typed (spaces, dashes, case).</summary>
        public static bool IsPreviewCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var sb = new System.Text.StringBuilder();
            foreach (char c in text) if (char.IsLetter(c)) sb.Append(char.ToUpperInvariant(c));
            return sb.ToString() == "LANGPREVIEW";
        }
    }
}
