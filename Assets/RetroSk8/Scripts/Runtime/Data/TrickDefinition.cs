using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Data
{
    /// <summary>One original trick. Animation fields drive the procedural placeholder pose in SkaterVisual.</summary>
    [CreateAssetMenu(menuName = "Retro Sk8/Trick", fileName = "Trick_")]
    public sealed class TrickDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "trick_id";
        public string displayName = "Unnamed Trick";
        public TrickCategory category = TrickCategory.BoardFlip;
        public TrickFamily family = TrickFamily.Flip;
        [Range(0, 3)] public int variation;
        public bool isSpecial;

        [Header("Scoring")]
        [Tooltip("Points for discrete tricks; starting points for grinds/manuals (which then accrue per second).")]
        public int baseValue = 400;

        [Header("Timing")]
        [Tooltip("Seconds the air animation takes. Landing before it finishes risks a bail.")]
        public float duration = 0.45f;

        [Header("Procedural pose")]
        [Tooltip("Full board rotations around its length axis.")]
        public float rollTurns;
        [Tooltip("Full board rotations around its width axis.")]
        public float pitchTurns;
        [Tooltip("Board yaw in degrees (sign flipped for left swipes).")]
        public float yawDegrees;
        [Tooltip("Peak board tilt (euler) for grabs.")]
        public Vector3 grabTilt;
        [Tooltip("Body spin in degrees during the trick (specials).")]
        public float bodySpinDegrees;

        public static TrickDefinition CreateRuntime(string id, string name, TrickCategory category, TrickFamily family,
            int variation, int baseValue, float duration)
        {
            var t = CreateInstance<TrickDefinition>();
            t.name = id;
            t.id = id;
            t.displayName = name;
            t.category = category;
            t.family = family;
            t.variation = variation;
            t.baseValue = baseValue;
            t.duration = duration;
            return t;
        }
    }
}
