using RetroSk8.Core;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Phase 26: the assists the current skate scene plays with, fixed when the scene is installed (changing them in
    /// Settings applies from the next run). Off while watching a replay and in online S.K.A.T.E., where both phones
    /// must play by the same rules. Also owns the game-speed time scale, so pause and slow-mo come back to it.
    /// </summary>
    public static class ActiveAssists
    {
        private static float s_baseFixed = -1f;

        public static AssistLevels Levels { get; private set; }
        public static bool Any => Levels.Any;
        public static float SpeedScale => Assists.SpeedScale(Levels.Speed);

        public static void Begin(RunMode mode)
        {
            bool online = mode == RunMode.Duel && RetroSk8.Duel.DuelSession.Current != null && RetroSk8.Duel.DuelSession.Current.IsOnline;
            Levels = mode == RunMode.Replay || online ? default : SaveManager.Data.settings.Assists;
            ApplyTimeScale();
        }

        /// <summary>Back to normal rules and speed (scene teardown, menus).</summary>
        public static void End()
        {
            Levels = default;
            ApplyTimeScale();
        }

        /// <summary>
        /// Sets Time.timeScale to the game speed times <paramref name="multiplier"/> (slow-mo), scaling the physics
        /// step with it so motion stays smooth at any speed.
        /// </summary>
        public static void ApplyTimeScale(float multiplier = 1f)
        {
            if (s_baseFixed <= 0f) s_baseFixed = Time.fixedDeltaTime;
            float scale = Mathf.Max(0.01f, SpeedScale * multiplier);
            Time.timeScale = scale;
            Time.fixedDeltaTime = s_baseFixed * scale;
        }

        /// <summary>Balance settings for a grind or manual meter with the session's assist applied.</summary>
        public static BalanceSettings Balance(BalanceSettings settings) =>
            Levels.Balance > 0 ? Assists.Balance(settings, Levels.Balance) : settings;

        /// <summary>Landing rules with the session's assist applied (the profile's own object when the assist is off).</summary>
        public static LandingRules Landing(LandingRules rules) =>
            Levels.Landing > 0 ? Assists.Landing(rules, Levels.Landing) : rules;
    }
}
