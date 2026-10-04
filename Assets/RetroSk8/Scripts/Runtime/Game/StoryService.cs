using System;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Story mode on top of the existing modes: score steps and line battles are Two-Minute Run challenges (a line
    /// battle adds the rival's climbing score to the HUD), S.K.A.T.E. steps are CPU games against the rival.
    /// Clearing a step pays tokens and queues its closing panels for the menu.
    /// </summary>
    public static class StoryService
    {
        public static StoryState State => SaveManager.Data.story;

        /// <summary>The step being played right now, or null.</summary>
        public static StoryStep Active => string.IsNullOrEmpty(GameSession.StoryStepId) ? null : Story.FindStep(GameSession.StoryStepId);

        /// <summary>Starts a step (call after its intro panels).</summary>
        public static void Begin(StoryStep step, ContentRegistry content)
        {
            if (step == null) return;
            GameSession.StoryStepId = step.Id;
            GameSession.CrewRecruitId = null;
            if (step.Objective == StoryObjective.Skate)
            {
                int park = Math.Max(0, Array.IndexOf(ShareCodes.BuiltInParks, step.LocationId));
                var level = (RetroSk8.Core.DuelBot.Level)Mathf.Clamp(step.RivalLevel, 0, 2);
                RetroSk8.Duel.DuelSession.StartCpu(level, park, Environment.TickCount, step.Rival).Go();
                return;
            }
            var loc = content != null ? content.FindLocationExact(step.LocationId) : null;
            float seconds = loc != null && loc.runDurationSeconds > 0f ? loc.runDurationSeconds : 120f;
            var challenge = new ScoreChallenge { LocationId = step.LocationId, Target = step.Target, From = step.Rival };
            if (step.Objective == StoryObjective.LineBattle)
                challenge.GhostBanks = RivalCurve.Banks(step.Target, seconds, Seed(step.Id));
            ShareService.StartChallenge(challenge, content);
        }

        /// <summary>A stable seed from the step id (string.GetHashCode differs between runtimes).</summary>
        private static int Seed(string id)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char c in id) h = (h ^ c) * 16777619;
                return h & 0x7fffffff;
            }
        }

        /// <summary>Called when a Two-Minute Run finishes.</summary>
        public static void OnRunFinished(RunResult r, string locationId)
        {
            var step = Active;
            if (step == null || step.Objective == StoryObjective.Skate || r == null) return;
            if (Story.RunClears(step, locationId, r.score)) Clear(step);
            else CareerService.Pending.Add(step.Objective == StoryObjective.LineBattle
                ? $"STORY: {step.Rival} STILL HAS THE BETTER LINE. RETRY!"
                : $"STORY: NEED MORE THAN {step.Target:N0}. RETRY!");
        }

        /// <summary>Called when a CPU game of S.K.A.T.E. ends with you winning.</summary>
        public static void OnSkateWon()
        {
            var step = Active;
            if (step != null && step.Objective == StoryObjective.Skate) Clear(step);
        }

        private static void Clear(StoryStep step)
        {
            bool first = State.Clear(step.Id);
            GameSession.StoryOutroPending = step.Id;
            if (first)
            {
                SaveManager.AddTokens(step.Tokens);
                CareerService.Pending.Add($"STORY: {step.Title.ToUpperInvariant()} CLEARED!  +{step.Tokens}");
                if (State.Finished) CareerService.Pending.Add($"{Story.Title} COMPLETE. THE CITY STAYS OPEN.");
            }
            else CareerService.Pending.Add($"STORY: {step.Title.ToUpperInvariant()} CLEARED AGAIN");
            SaveManager.Save();
        }

        /// <summary>Back at the menu: story play is over (the outro, if any, is still pending).</summary>
        public static void EndSession() => GameSession.StoryStepId = null;
    }
}
