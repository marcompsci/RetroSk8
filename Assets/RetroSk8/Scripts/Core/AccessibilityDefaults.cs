namespace RetroSk8.Core
{
    /// <summary>What the phone's own accessibility settings say (Phase 20).</summary>
    public struct SystemAccessibility
    {
        public bool ReduceMotion;
        public bool LargeText;
        public bool BoldText;
        public bool VoiceOver;
    }

    /// <summary>
    /// Phase 20: on first launch the game copies the phone's Reduce Motion and text size into its own settings, so
    /// players who need them never have to find the menu first. It only ever turns options on, and only once: after
    /// that the player's own choices in Settings > ACCESSIBILITY win. Engine-free and unit-tested.
    /// </summary>
    public static class AccessibilityDefaults
    {
        /// <param name="reducedMotion">The game's setting, updated in place.</param>
        /// <param name="largeText">The game's setting, updated in place.</param>
        /// <param name="applied">Whether this has run before; set to true.</param>
        /// <returns>True when anything changed.</returns>
        public static bool Apply(SystemAccessibility system, ref bool reducedMotion, ref bool largeText, ref bool applied)
        {
            if (applied) return false;
            applied = true;
            bool changed = false;
            if (system.ReduceMotion && !reducedMotion) { reducedMotion = true; changed = true; }
            if ((system.LargeText || system.BoldText || system.VoiceOver) && !largeText) { largeText = true; changed = true; }
            return changed;
        }
    }
}
