using System.Runtime.InteropServices;
using RetroSk8.Core;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Phase 20: reads the phone's accessibility settings (RetroSk8Accessibility.mm) and applies them as first-launch
    /// defaults (AccessibilityDefaults). Elsewhere it reports nothing set.
    /// </summary>
    public static class AccessibilitySupport
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int RetroSk8_A11yReduceMotion();
        [DllImport("__Internal")] private static extern int RetroSk8_A11yLargeText();
        [DllImport("__Internal")] private static extern int RetroSk8_A11yBoldText();
        [DllImport("__Internal")] private static extern int RetroSk8_A11yVoiceOver();

        public static SystemAccessibility System => new SystemAccessibility
        {
            ReduceMotion = RetroSk8_A11yReduceMotion() != 0,
            LargeText = RetroSk8_A11yLargeText() != 0,
            BoldText = RetroSk8_A11yBoldText() != 0,
            VoiceOver = RetroSk8_A11yVoiceOver() != 0,
        };
#else
        public static SystemAccessibility System => default;
#endif

        /// <summary>Copies the phone's settings into the game's the first time it runs. Call before building UI.</summary>
        public static void ApplyFirstLaunchDefaults()
        {
            var s = SaveManager.Data.settings;
            bool rm = s.reducedMotion, lt = s.largeText, applied = s.systemA11yApplied;
            if (AccessibilityDefaults.Apply(System, ref rm, ref lt, ref applied) || applied != s.systemA11yApplied)
            {
                s.reducedMotion = rm;
                s.largeText = lt;
                s.systemA11yApplied = applied;
                SaveManager.Save();
            }
        }
    }
}
