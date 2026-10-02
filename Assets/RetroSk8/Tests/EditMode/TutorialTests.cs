using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class TutorialTests
    {
        private static void Push(TutorialFlow f)
        {
            for (int i = 0; i < 100; i++) f.OnRolling(0.02f, 9f);
        }

        [Test]
        public void FullLesson_InOrder()
        {
            var f = new TutorialFlow();
            var done = new List<TutorialStep>();
            f.StepCompleted += s => done.Add(s);

            Assert.AreEqual(TutorialStep.Push, f.Step);
            Push(f);
            Assert.AreEqual(TutorialStep.Ollie, f.Step);
            Assert.IsTrue(f.OnLanded(LandingQuality.Clean, 0, false, false));
            Assert.IsTrue(f.OnLanded(LandingQuality.Sketchy, 0, true, false), "sketchy flips still teach the flip");
            Assert.IsTrue(f.OnLanded(LandingQuality.Clean, 0, false, true));
            Assert.IsTrue(f.OnLanded(LandingQuality.Clean, 1, false, false));
            Assert.AreEqual(TutorialStep.Grind, f.Step);
            Assert.IsTrue(f.OnGrinding(0.8f));
            Assert.IsTrue(f.OnManual(1.6f));
            Assert.IsTrue(f.OnBanked(3));
            Assert.IsTrue(f.IsDone);
            Assert.AreEqual(8, done.Count);
            Assert.AreEqual(TutorialStep.Combo, done[7]);
        }

        [Test]
        public void Push_NeedsSpeed_AndTime()
        {
            var f = new TutorialFlow();
            for (int i = 0; i < 200; i++) f.OnRolling(0.02f, 4f);
            Assert.AreEqual(TutorialStep.Push, f.Step, "too slow");
            for (int i = 0; i < 50; i++) f.OnRolling(0.02f, 9f);
            Assert.AreEqual(1f * 0.02f * 50f / TutorialFlow.PushSeconds, f.Progress, 1e-3f);
            Assert.AreEqual(TutorialStep.Push, f.Step);
        }

        [Test]
        public void WrongTrick_OrBail_DoesNotAdvance()
        {
            var f = new TutorialFlow();
            Push(f);
            Assert.IsFalse(f.OnLanded(LandingQuality.Bail, 0, false, false));
            Assert.AreEqual(TutorialStep.Ollie, f.Step);
            f.OnLanded(LandingQuality.Clean, 0, false, false);
            Assert.IsFalse(f.OnLanded(LandingQuality.Clean, 0, false, true), "a grab isn't a flip");
            Assert.AreEqual(TutorialStep.Flip, f.Step);
        }

        [Test]
        public void Manual_MustBeHeldInOneGo()
        {
            var f = new TutorialFlow();
            Push(f);
            f.OnLanded(LandingQuality.Clean, 0, false, false);
            f.OnLanded(LandingQuality.Clean, 0, true, false);
            f.OnLanded(LandingQuality.Clean, 0, false, true);
            f.OnLanded(LandingQuality.Clean, 2, false, false);
            f.OnGrinding(1f);
            Assert.AreEqual(TutorialStep.Manual, f.Step);
            Assert.IsFalse(f.OnManual(1f));
            f.OnBailed();
            Assert.AreEqual(0f, f.Progress, 1e-4f);
            Assert.IsFalse(f.OnManual(1.2f));
            Assert.IsTrue(f.OnManual(1.5f));
        }

        [Test]
        public void Combo_NeedsThreeTricks()
        {
            var f = new TutorialFlow();
            f.SkipAll();
            Assert.IsTrue(f.IsDone);
            Assert.IsFalse(f.OnBanked(5), "nothing happens after the end");

            var g = new TutorialFlow();
            Push(g);
            g.OnLanded(LandingQuality.Clean, 0, false, false);
            g.OnLanded(LandingQuality.Clean, 0, true, false);
            g.OnLanded(LandingQuality.Clean, 0, false, true);
            g.OnLanded(LandingQuality.Clean, 1, false, false);
            g.OnGrinding(1f);
            g.OnManual(2f);
            Assert.IsFalse(g.OnBanked(2));
            Assert.IsTrue(g.OnBanked(3));
        }

        [Test]
        public void StepNumbers_AreOneBased()
        {
            var f = new TutorialFlow();
            Assert.AreEqual(1, f.StepNumber);
            Assert.AreEqual(8, TutorialFlow.StepCount);
            f.SkipAll();
            Assert.AreEqual(8, f.StepNumber);
        }
    }
}
