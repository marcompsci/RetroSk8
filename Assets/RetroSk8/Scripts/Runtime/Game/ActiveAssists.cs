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
        /// <summary>Phase 30: the board setup's handling for this session (neutral in replays and online S.K.A.T.E.).</summary>
        public static SetupFactors Setup { get; private set; } = SetupFactors.Neutral;

        public static void Begin(RunMode mode)
        {
            bool online = mode == RunMode.Duel && RetroSk8.Duel.DuelSession.Current != null && RetroSk8.Duel.DuelSession.Current.IsOnline;
            Levels = mode == RunMode.Replay || online ? default : SaveManager.Data.settings.Assists;
            var look = SaveManager.Data.look;
            Setup = mode == RunMode.Replay || online || look == null ? SetupFactors.Neutral : BoardSetup.Factors(look.trucks, look.wheels);
            ApplyTimeScale();
        }

        /// <summary>Back to normal rules and speed (scene teardown, menus).</summary>
        public static void End()
        {
            Levels = default;
            Setup = SetupFactors.Neutral;
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
        public static BalanceSettings Balance(BalanceSettings settings)
        {
            var s = Levels.Balance > 0 ? Assists.Balance(settings, Levels.Balance) : settings;
            if (Mathf.Approximately(Setup.BalanceNoise, 1f) || s == null) return s;
            if (ReferenceEquals(s, settings)) s = Assists.Balance(settings, 0); // a copy: never change the shared profile
            s.jitter *= Setup.BalanceNoise; // Phase 30: loose trucks wobble more, tight ones less
            return s;
        }

        /// <summary>Phase 30: applies the board setup to the skater's motor and grind tuning (call once, after any saved tuning).</summary>
        public static void ApplySetup(RetroSk8.Player.PlayerController player)
        {
            if (player == null) return;
            var f = Setup;
            var m = player.motor;
            m.turnRateSlow *= f.TurnSlow;
            m.turnRateFast *= f.TurnFast;
            m.landingAlignAssist = Mathf.Clamp01(m.landingAlignAssist * f.LandingAlign);
            m.cruiseSpeed *= f.Cruise;
            m.pushSpeed *= f.Push;
            m.maxSpeed *= f.MaxSpeed;
            m.rollingDrag *= f.RollingDrag;
            var grind = player.GetComponent<RetroSk8.Player.GrindController>();
            if (grind != null) grind.settings.deceleration *= f.GrindDecel;
        }

        /// <summary>Landing rules with the session's assist applied (the profile's own object when the assist is off).</summary>
        public static LandingRules Landing(LandingRules rules) =>
            Levels.Landing > 0 ? Assists.Landing(rules, Levels.Landing) : rules;
    }
}
