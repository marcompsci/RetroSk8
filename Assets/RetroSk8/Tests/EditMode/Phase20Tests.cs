using System.Linq;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    /// <summary>Phase 20: system accessibility defaults, story chapter 7 (bonk steps), its achievement, TestFlight advice.</summary>
    public class AccessibilityDefaultsTests
    {
        [Test]
        public void FirstLaunch_CopiesReduceMotionAndTextSize()
        {
            bool motion = false, large = false, applied = false;
            var sys = new SystemAccessibility { ReduceMotion = true, LargeText = true };
            Assert.IsTrue(AccessibilityDefaults.Apply(sys, ref motion, ref large, ref applied));
            Assert.IsTrue(motion); Assert.IsTrue(large); Assert.IsTrue(applied);
        }

        [Test]
        public void BoldTextOrVoiceOver_TurnOnLargeText()
        {
            foreach (var sys in new[] { new SystemAccessibility { BoldText = true }, new SystemAccessibility { VoiceOver = true } })
            {
                bool motion = false, large = false, applied = false;
                AccessibilityDefaults.Apply(sys, ref motion, ref large, ref applied);
                Assert.IsTrue(large); Assert.IsFalse(motion);
            }
        }

        [Test]
        public void RunsOnlyOnce_AndNeverTurnsAnythingOff()
        {
            bool motion = false, large = false, applied = true;
            Assert.IsFalse(AccessibilityDefaults.Apply(new SystemAccessibility { ReduceMotion = true }, ref motion, ref large, ref applied));
            Assert.IsFalse(motion, "after the first launch the player's settings win");

            motion = true; large = true; applied = false;
            Assert.IsFalse(AccessibilityDefaults.Apply(new SystemAccessibility(), ref motion, ref large, ref applied));
            Assert.IsTrue(motion); Assert.IsTrue(large); Assert.IsTrue(applied);
        }
    }

    public class StoryChapterSevenTests
    {
        private static StoryChapter Seven() => Story.Chapters.First(c => c.Id == "double_feature");

        [Test]
        public void ChapterSeven_IsTwoBonkBattlesAtTheDriveIn()
        {
            var c = Seven();
            Assert.AreEqual(7, c.Number);
            Assert.AreEqual(2, c.Steps.Length);
            foreach (var s in c.Steps)
            {
                Assert.AreEqual(StoryObjective.LineBattle, s.Objective);
                Assert.AreEqual("drive_in", s.LocationId);
                Assert.Greater(s.Bonks, 0);
                StringAssert.Contains("bonks or pole jams", s.ObjectiveText("Drive-In"));
            }
            Assert.Greater(c.Steps[1].Target, c.Steps[0].Target);
            Assert.Greater(c.Steps[1].Bonks, c.Steps[0].Bonks);
        }

        [Test]
        public void BonkStep_NeedsScoreAndBonks()
        {
            var s = Seven().Steps[0];
            Assert.IsFalse(Story.RunClears(s, "drive_in", s.Target + 1, s.Bonks - 1), "short on bonks");
            Assert.IsFalse(Story.RunClears(s, "drive_in", s.Target, s.Bonks), "score must beat the target");
            Assert.IsFalse(Story.RunClears(s, "harbor_plaza", s.Target + 1, s.Bonks), "wrong park");
            Assert.IsTrue(Story.RunClears(s, "drive_in", s.Target + 1, s.Bonks));
        }

        [Test]
        public void OlderSteps_IgnoreBonks()
        {
            foreach (var s in Story.AllSteps().Where(x => x.Bonks == 0 && x.Objective != StoryObjective.Skate))
            {
                Assert.IsTrue(Story.RunClears(s, s.LocationId, s.Target + 1, 0), s.Id);
                Assert.IsTrue(Story.RunClears(s, s.LocationId, s.Target + 1), s.Id);
            }
        }

        [Test]
        public void ChapterSeven_HasPanelsAndANewRival()
        {
            foreach (var s in Seven().Steps)
            {
                Assert.IsNotNull(s.Intro); Assert.Greater(s.Intro.Length, 0, s.Id);
                Assert.IsNotNull(s.Outro); Assert.Greater(s.Outro.Length, 0, s.Id);
                Assert.IsFalse(string.IsNullOrEmpty(s.Rival));
            }
            Assert.AreNotEqual(Seven().Steps[0].Rival, Seven().Steps[1].Rival);
        }

        [Test]
        public void DoubleFeatureAchievement_HasPointsAndAnId()
        {
            var a = Achievements.All.First(x => x.Id == "double_feature");
            Assert.AreEqual(0f, a.Progress(new PlayerProgress()));
            Assert.AreEqual(1f, a.Progress(new PlayerProgress { DoubleFeature = true }));
            Assert.AreEqual(75, GameCenterSetup.PointsFor("double_feature"));
            Assert.LessOrEqual(GameCenterSetup.TotalPoints(Achievements.All), GameCenterSetup.MaxPoints);
        }
    }

    public class TestFlightAdviceTests
    {
        [Test]
        public void ErrorDownloadingAppInformation_PointsAtTheAppRecord()
        {
            string a = TestFlightRules.Advice(new[] { "error: exportArchive: Error Downloading App Information" });
            Assert.IsNotNull(a);
            StringAssert.Contains("New App", a);
        }
    }
}
