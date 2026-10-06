using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    /// <summary>Phase 23: Trick Battle, party names, the power policy, story reward items.</summary>
    public class TrickBattleTests
    {
        private static PartyRules Battle(params string[] names) => new PartyRules(PartyGame.TrickBattle, names);

        private static void Attempt(PartyRules r, string key, params string[] landed)
        {
            r.BeginAttempt();
            r.EndTrickAttempt(key, key?.ToUpperInvariant(), new HashSet<string>(landed));
        }

        [Test]
        public void SetterCalls_OthersMustLandIt()
        {
            var r = Battle("ANA", "BEN", "CAL");
            Assert.IsTrue(r.IsSetting);
            Attempt(r, "kickflip", "ollie", "kickflip");
            Assert.AreEqual("kickflip", r.TargetTrickId);
            StringAssert.Contains("CALLED KICKFLIP", r.LastMessage);
            Assert.AreEqual("BEN", r.Current.Name);
            Attempt(r, "heelflip", "heelflip"); // BEN misses
            Assert.AreEqual(1, r.Players[1].Letters);
            Assert.AreEqual("CAL", r.Current.Name);
            Attempt(r, "kickflip", "kickflip"); // CAL lands it
            Assert.AreEqual(0, r.Players[2].Letters);
            Assert.IsTrue(r.IsSetting, "everyone tried: a new setter calls");
            Assert.AreEqual("BEN", r.Current.Name, "the call passes round");
        }

        [Test]
        public void MissedCall_PassesTheCall()
        {
            var r = Battle("A", "B");
            Attempt(r, null);
            Assert.IsTrue(r.IsSetting);
            Assert.AreEqual("B", r.Current.Name);
            StringAssert.Contains("MISSED THE CALL", r.LastMessage);
        }

        [Test]
        public void SpellingRetro_KnocksYouOut_AndTheLastOneWins()
        {
            var r = Battle("A", "B");
            int guard = 0;
            while (r.Phase != PartyPhase.Finished && guard++ < 100)
            {
                if (r.IsSetting) Attempt(r, r.Current.Name == "A" ? "kickflip" : null, "kickflip");
                else Attempt(r, "ollie", "ollie"); // B never lands the kickflip
            }
            Assert.AreEqual(PartyPhase.Finished, r.Phase);
            Assert.IsTrue(r.Players[1].Out);
            Assert.AreEqual("A", r.Winners().Single().Name);
        }

        [Test]
        public void KeyTrick_IsTheBestTrick_AndSkipsGaps()
        {
            var points = new Dictionary<string, int> { { "ollie", 100 }, { "kickflip", 400 }, { "grind_crossbar", 400 }, { "gap_fountain", 900 } };
            Assert.AreEqual("grind_crossbar", PartyRules.KeyTrick(new[] { "ollie", "kickflip", "grind_crossbar", "gap_fountain" }, id => points.TryGetValue(id, out var p) ? p : 0), "ties go to the later trick, gaps never count");
            Assert.IsNull(PartyRules.KeyTrick(new string[0], _ => 0));
            Assert.IsNull(PartyRules.KeyTrick(new[] { "gap_fountain" }, _ => 900));
        }

        [Test]
        public void TrickBattle_UsesShortAttempts()
        {
            Assert.AreEqual(PartyRules.AttemptSeconds, Battle("A", "B").AttemptLength);
        }
    }

    public class PartyNameTests
    {
        [Test]
        public void Names_AreCleanedFilteredAndUnique()
        {
            var names = PartyRules.CleanNames(new[] { "  omari ", "", "omari", "f u c k" });
            Assert.AreEqual("OMARI", names[0]);
            Assert.AreEqual("PLAYER 2", names[1], "empty becomes PLAYER n");
            Assert.AreEqual("OMARI 2", names[2], "repeats get a number");
            Assert.AreEqual("PLAYER 4", names[3], "blocked words are replaced");
            Assert.IsTrue(names.All(n => n.Length <= PartyRules.MaxNameLength));
        }

        [Test]
        public void LongRepeats_StayWithinTheLimit()
        {
            var names = PartyRules.CleanNames(new[] { "ABCDEFGHIJKLMNOP", "ABCDEFGHIJKLMNOP" });
            Assert.AreEqual(PartyRules.MaxNameLength, names[0].Length);
            Assert.AreNotEqual(names[0], names[1]);
            Assert.LessOrEqual(names[1].Length, PartyRules.MaxNameLength);
        }
    }

    public class PowerPolicyTests
    {
        private static PowerState Calm => new PowerState { Battery = 0.8f };

        [Test]
        public void Auto_Is60_UntilThePhoneAsksForLess()
        {
            Assert.AreEqual(60, PowerPolicy.Plan(FrameRateMode.Auto, Calm).TargetFps);
            Assert.AreEqual(30, PowerPolicy.Plan(FrameRateMode.Auto, new PowerState { LowPowerMode = true, Battery = 0.8f }).TargetFps);
            Assert.AreEqual(30, PowerPolicy.Plan(FrameRateMode.Auto, new PowerState { Thermal = 2, Battery = 0.8f }).TargetFps);
            Assert.AreEqual(30, PowerPolicy.Plan(FrameRateMode.Auto, new PowerState { Battery = 0.1f }).TargetFps);
            Assert.AreEqual(60, PowerPolicy.Plan(FrameRateMode.Auto, new PowerState { Battery = 0.1f, Charging = true }).TargetFps, "charging is fine");
            Assert.AreEqual(60, PowerPolicy.Plan(FrameRateMode.Auto, new PowerState { Battery = -1f }).TargetFps, "unknown battery (editor) is fine");
        }

        [Test]
        public void Smooth_Stays60_ExceptWhenCritical()
        {
            Assert.AreEqual(60, PowerPolicy.Plan(FrameRateMode.Smooth, new PowerState { LowPowerMode = true, Battery = 0.05f }).TargetFps);
            var hot = PowerPolicy.Plan(FrameRateMode.Smooth, new PowerState { Thermal = 3 });
            Assert.AreEqual(30, hot.TargetFps);
            StringAssert.Contains("HOT", hot.Reason);
        }

        [Test]
        public void BatterySaver_Is30_WithLowerScaleAndNoShadows()
        {
            var p = PowerPolicy.Plan(FrameRateMode.BatterySaver, Calm);
            Assert.AreEqual(30, p.TargetFps);
            Assert.Less(p.MaxRenderScale, 1f);
            Assert.IsFalse(p.Shadows);
        }

        [Test]
        public void Modes_Cycle()
        {
            Assert.AreEqual(FrameRateMode.Smooth, PowerPolicy.Next(FrameRateMode.Auto));
            Assert.AreEqual(FrameRateMode.BatterySaver, PowerPolicy.Next(FrameRateMode.Smooth));
            Assert.AreEqual(FrameRateMode.Auto, PowerPolicy.Next(FrameRateMode.BatterySaver));
            StringAssert.Contains("AUTO", PowerPolicy.ModeName(FrameRateMode.Auto));
        }
    }

    public class StoryRewardTests
    {
        [Test]
        public void ChapterFinales_UnlockAShirt()
        {
            var steps = Story.AllSteps().ToList();
            Assert.AreEqual("Projectionist Tee", steps.First(s => s.Id == "s7_reel").RewardItem);
            Assert.AreEqual("Rink Rats Jersey", steps.First(s => s.Id == "s8_frost").RewardItem);
            Assert.AreEqual(3, steps.Count(s => !string.IsNullOrEmpty(s.RewardItem)), "chapters 7, 8 and 9");
        }
    }

    /// <summary>Phase 24 review fixes.</summary>
    public class WeeklyCutoverTests
    {
        [Test]
        public void WeeksBeforeTheCutover_KeepTheOldRotation()
        {
            // The 7-event rotation, computed the old way, for the weeks around the update.
            for (int w = 202630; w < WeeklyEvents.RinkWeekStarts; w++)
            {
                int weeks = (w / 100) * 53 + w % 100;
                var old = WeeklyEvents.Rotation[weeks % 7];
                Assert.AreSame(old, WeeklyEvents.For(w), w.ToString());
            }
            Assert.AreNotEqual("rink_week", WeeklyEvents.For(WeeklyEvents.RinkWeekStarts - 1).Id);
        }

        [Test]
        public void LateSkate_ComesUp_AfterTheCutover()
        {
            bool seen = false;
            for (int i = 0; i < 16; i++) seen |= WeeklyEvents.For(WeeklyEvents.RinkWeekStarts + i).Id == "rink_week";
            Assert.IsTrue(seen);
        }
    }
}
