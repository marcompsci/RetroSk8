using RetroSk8.Core;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>Phase 28: loads the translation tables and picks the language (the saved choice or the phone's).</summary>
    public static class LocSetup
    {
        private static readonly bool[] s_loaded = new bool[Loc.LangCount];

        /// <summary>The phone's language as a code Loc.Resolve understands.</summary>
        public static string SystemCode
        {
            get
            {
                switch (Application.systemLanguage)
                {
                    case SystemLanguage.Spanish: return "es";
                    case SystemLanguage.Japanese: return "ja";
                    default: return "en";
                }
            }
        }

        /// <summary>Phase 29: 1.0 ships English only; other languages need the tester preview (LangRelease).</summary>
        public static Lang Chosen => LangRelease.Gate(Loc.Resolve(SaveManager.Data.settings.language, SystemCode), Preview);

        public static bool Preview => SaveManager.Data.settings.langPreview;

        public static bool ShowPicker => LangRelease.ShowPicker(Preview);

        /// <summary>Turns the tester language preview on or off (the LANG PREVIEW code). Off drops back to AUTO.</summary>
        public static void SetPreview(bool on)
        {
            var s = SaveManager.Data.settings;
            s.langPreview = on;
            if (!on) s.language = -1;
            SaveManager.Save();
            Apply();
        }

        public static void Apply() => Use(Chosen);

        /// <summary>Switches to <paramref name="lang"/>, loading its table the first time.</summary>
        public static void Use(Lang lang)
        {
            int i = (int)lang;
            if (i > 0 && i < s_loaded.Length && !s_loaded[i])
            {
                s_loaded[i] = true;
                Loc.Register(lang, Loc.Parse(LocTables.For(lang)));
            }
            Loc.Set(lang);
        }

        /// <summary>The settings button: AUTO → ENGLISH → ESPAÑOL → 日本語 → AUTO.</summary>
        public static void Cycle()
        {
            var s = SaveManager.Data.settings;
            s.language = LangRelease.NextChoice(s.language, Preview);
            SaveManager.Save();
            Apply();
        }

        public static string ButtonLabel
        {
            get
            {
                var s = SaveManager.Data.settings;
                string name = Loc.LangNames[(int)Chosen];
                // The language's own name is shown untranslated so you can always find your way back.
                return s.language < 0 ? $"LANGUAGE: AUTO ({name})" : $"LANGUAGE: {name}";
            }
        }
    }
}
