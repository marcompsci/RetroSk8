using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Data
{
    /// <summary>Designer-facing wrapper around the engine-free tuning classes.</summary>
    [CreateAssetMenu(menuName = "Retro Sk8/Scoring Profile", fileName = "ScoringProfile")]
    public sealed class ScoringProfile : ScriptableObject
    {
        public ScoringConfig scoring = new ScoringConfig();
        public BalanceSettings grindBalance = new BalanceSettings();
        public BalanceSettings manualBalance = new BalanceSettings { baseTip = 1.3f, tipGrowthPerSecond = 0.45f, control = 2.8f };
        public LandingRules landing = new LandingRules();

        [Header("Combo flow")]
        [Tooltip("Seconds after touchdown in which ACTION starts a manual instead of banking.")]
        public float manualWindow = 0.35f;
        [Tooltip("ACTION pressed this long before touchdown still counts for the manual window.")]
        public float manualPreBuffer = 0.15f;
        [Tooltip("Banked points needed to fill the special meter.")]
        public float specialFillPoints = 12000f;
    }
}
