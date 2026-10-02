using System;

namespace RetroSk8.Core
{
    /// <summary>
    /// Plain data for all score tuning. Wrapped by the ScoringProfile ScriptableObject in the runtime layer
    /// so designers edit it in the Inspector while tests construct it directly.
    /// </summary>
    [Serializable]
    public class ScoringConfig
    {
        // Repeat decay: value of the Nth repeat of the same trick in one combo = base * repeatDecay^N (floored at minRepeatFactor).
        public float repeatDecay = 0.5f;
        public float minRepeatFactor = 0.1f;

        // Multiplier grows by this per distinct scoring event, clamped.
        public float multiplierPerTrick = 1f;
        public float maxMultiplier = 20f;

        // Line Flow: extra multiplier for variety and for linking different line elements without stopping.
        public float flowPerExtraCategory = 0.10f;
        public float flowPerLink = 0.05f;
        public float maxFlowBonus = 1.0f;

        // Continuous tricks.
        public float grindPointsPerSecond = 220f;
        public float manualPointsPerSecond = 160f;
        public float lipPointsPerSecond = 300f;
        public float wallridePointsPerSecond = 420f;

        // Air spins.
        public int spinPointsPerHalfTurn = 150;
        public float spinChainBonusPerHalfTurn = 0.25f;

        // A sketchy landing banks this fraction of the combo.
        public float sketchyBankFactor = 0.8f;

        // Tape Tokens awarded per this many points at the end of a run (MVP placeholder economy).
        public int pointsPerTapeToken = 5000;

        // Goal rewards pay only the first time a goal is ever completed, so they can't be farmed.
        public int tokensPerGoal = 5;
        public int tokensAllGoalsBonus = 10;
        public int tokensDailyBonus = 20;

        public static ScoringConfig CreateDefault() => new ScoringConfig();

        public float RatePerSecond(TrickCategory category)
        {
            switch (category)
            {
                case TrickCategory.Grind: return grindPointsPerSecond;
                case TrickCategory.Manual: return manualPointsPerSecond;
                case TrickCategory.Lip: return lipPointsPerSecond;
                case TrickCategory.Wall: return wallridePointsPerSecond;
                default: return 0f;
            }
        }
    }
}
