using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class ScoreLedgerTests
    {
        [Test]
        public void Bank_AccumulatesAndTracksBest()
        {
            var l = new ScoreLedger();
            l.Bank(new ComboResult { Points = 1000 }, "first");
            l.Bank(new ComboResult { Points = 5000 }, "big");
            l.Bank(new ComboResult { Points = 200 }, "small");
            Assert.AreEqual(6200, l.Total);
            Assert.AreEqual(5000, l.BestCombo);
            Assert.AreEqual("big", l.BestComboLabel);
            Assert.AreEqual(3, l.CombosBanked);
        }

        [Test]
        public void ZeroBank_IsIgnored()
        {
            var l = new ScoreLedger();
            l.Bank(ComboResult.Empty);
            Assert.AreEqual(0, l.CombosBanked);
        }

        [Test]
        public void TapeTokens_FloorDivision()
        {
            var l = new ScoreLedger();
            l.Bank(new ComboResult { Points = 12999 });
            Assert.AreEqual(2, l.TapeTokensFor(new ScoringConfig { pointsPerTapeToken = 5000 }));
        }

        [Test]
        public void RecordBail_CountsLoss()
        {
            var l = new ScoreLedger();
            l.RecordBail(300);
            l.RecordBail(-5);
            Assert.AreEqual(2, l.Bails);
            Assert.AreEqual(300, l.PointsLostToBails);
        }
    }

    public class BalanceMeterTests
    {
        [Test]
        public void NoInput_EventuallyFails()
        {
            var m = new BalanceMeter(new BalanceSettings());
            m.Begin(1);
            bool failed = false;
            for (int i = 0; i < 60 * 10 && !failed; i++) failed = m.Step(1f / 60f, 0f, 0f);
            Assert.IsTrue(failed);
            Assert.IsTrue(m.HasFailed);
        }

        [Test]
        public void PerfectCorrection_SurvivesTenSeconds()
        {
            var m = new BalanceMeter(new BalanceSettings());
            m.Begin(-1);
            float noise = 1f;
            for (int i = 0; i < 60 * 10; i++)
            {
                noise = -noise; // alternating worst-case jitter
                float input = m.Lean > 0 ? -1f : 1f;
                Assert.IsFalse(m.Step(1f / 60f, input, noise), $"failed at step {i}, lean {m.Lean}");
            }
        }

        [Test]
        public void RepeatCount_IncreasesTip()
        {
            var a = new BalanceMeter(new BalanceSettings());
            var b = new BalanceMeter(new BalanceSettings());
            a.Begin(1, 0);
            b.Begin(1, 3);
            Assert.Greater(b.CurrentTip, a.CurrentTip);
        }

        [Test]
        public void Step_WhenInactive_DoesNothing()
        {
            var m = new BalanceMeter(new BalanceSettings());
            Assert.IsFalse(m.Step(1f, 1f, 1f));
            Assert.AreEqual(0f, m.Lean);
        }
    }

    public class LandingJudgeTests
    {
        private readonly LandingRules _rules = new LandingRules();

        [Test]
        public void StraightLanding_IsClean()
        {
            var v = LandingJudge.Evaluate(new LandingInput { AirYawDegrees = 5f }, _rules);
            Assert.AreEqual(LandingQuality.Clean, v.Quality);
            Assert.AreEqual(0, v.HalfTurns);
        }

        [Test]
        public void FullTurn_IsCleanTwoHalfTurns_NotSwitch()
        {
            var v = LandingJudge.Evaluate(new LandingInput { AirYawDegrees = -370f }, _rules);
            Assert.AreEqual(LandingQuality.Clean, v.Quality);
            Assert.AreEqual(2, v.HalfTurns);
            Assert.IsFalse(v.Switch);
        }

        [Test]
        public void HalfTurn_LandsSwitch()
        {
            var v = LandingJudge.Evaluate(new LandingInput { AirYawDegrees = 175f }, _rules);
            Assert.IsTrue(v.Switch);
        }

        [Test]
        public void SidewaysLanding_Bails()
        {
            var v = LandingJudge.Evaluate(new LandingInput { AirYawDegrees = 90f }, _rules);
            Assert.AreEqual(LandingQuality.Bail, v.Quality);
            Assert.AreEqual(BailReason.OverRotated, v.Reason);
        }

        [Test]
        public void SlightlyOffLanding_IsSketchy()
        {
            var v = LandingJudge.Evaluate(new LandingInput { AirYawDegrees = 215f }, _rules);
            Assert.AreEqual(LandingQuality.Sketchy, v.Quality);
        }

        [Test]
        public void UnfinishedTrick_Bails()
        {
            var v = LandingJudge.Evaluate(new LandingInput { UnfinishedTrickFraction = 0.6f }, _rules);
            Assert.AreEqual(BailReason.TrickUnfinished, v.Reason);
        }

        [Test]
        public void SteepSurface_Bails()
        {
            var v = LandingJudge.Evaluate(new LandingInput { SurfaceAngleDegrees = 70f }, _rules);
            Assert.AreEqual(BailReason.BadAngle, v.Reason);
        }

        [Test]
        public void SpinPoints_ScaleWithChainBonus()
        {
            var cfg = new ScoringConfig { spinPointsPerHalfTurn = 100, spinChainBonusPerHalfTurn = 0.5f };
            Assert.AreEqual(0, SpinRules.SpinPoints(0, cfg));
            Assert.AreEqual(100, SpinRules.SpinPoints(1, cfg));
            Assert.AreEqual(300, SpinRules.SpinPoints(2, cfg)); // 100*2*1.5
        }
    }

    public class RunTimerTests
    {
        [Test]
        public void Tick_ExpiresOnce()
        {
            var t = new RunTimer(1f);
            Assert.IsFalse(t.Tick(0.6f));
            Assert.IsTrue(t.Tick(0.6f));
            Assert.IsFalse(t.Tick(0.6f));
            Assert.IsTrue(t.IsExpired);
            Assert.AreEqual(0f, t.Remaining);
        }

        [Test]
        public void Paused_DoesNotTick()
        {
            var t = new RunTimer(10f) { IsPaused = true };
            t.Tick(5f);
            Assert.AreEqual(10f, t.Remaining);
        }

        [Test]
        public void Untimed_NeverExpires()
        {
            var t = new RunTimer(0f);
            Assert.IsFalse(t.Tick(999f));
            Assert.IsFalse(t.IsExpired);
        }

        [TestCase(120f, "2:00")]
        [TestCase(59.2f, "1:00")]
        [TestCase(9f, "0:09")]
        [TestCase(0f, "0:00")]
        public void Format(float s, string expected) => Assert.AreEqual(expected, RunTimer.Format(s));
    }

    public class GestureRulesTests
    {
        [TestCase(0f, 80f, SwipeDirection.Up)]
        [TestCase(0f, -80f, SwipeDirection.Down)]
        [TestCase(-80f, 10f, SwipeDirection.Left)]
        [TestCase(80f, -10f, SwipeDirection.Right)]
        [TestCase(5f, 5f, SwipeDirection.None)]
        public void ClassifySwipe(float dx, float dy, SwipeDirection expected)
            => Assert.AreEqual(expected, GestureRules.ClassifySwipe(dx, dy, 40f));

        [Test]
        public void Families_MapFromSwipe()
        {
            Assert.AreEqual(TrickFamily.Flip, GestureRules.FamilyFor(SwipeDirection.Up));
            Assert.AreEqual(TrickFamily.Grab, GestureRules.FamilyFor(SwipeDirection.Down));
            Assert.AreEqual(TrickFamily.Shove, GestureRules.FamilyFor(SwipeDirection.Left));
            Assert.AreEqual(TrickFamily.Shove, GestureRules.FamilyFor(SwipeDirection.Right));
        }

        [Test]
        public void Variation_FromStick()
        {
            Assert.AreEqual(0, GestureRules.VariationFor(GestureRules.ClassifyStick(0.1f, 0.1f, 0.35f)));
            Assert.AreEqual(1, GestureRules.VariationFor(GestureRules.ClassifyStick(-0.9f, 0f, 0.35f)));
            Assert.AreEqual(2, GestureRules.VariationFor(GestureRules.ClassifyStick(0.9f, 0.2f, 0.35f)));
            Assert.AreEqual(3, GestureRules.VariationFor(GestureRules.ClassifyStick(0f, -0.9f, 0.35f)));
        }
    }
}

namespace RetroSk8.Tests
{
    public class SpecialMeterTests
    {
        [Test]
        public void FillsAndConsumes()
        {
            var m = new RetroSk8.Core.SpecialMeter(1000f);
            m.Add(600);
            Assert.IsFalse(m.IsReady);
            Assert.IsFalse(m.TryConsume());
            m.Add(600);
            Assert.IsTrue(m.IsReady);
            Assert.AreEqual(1f, m.Value, 1e-5f);
            Assert.IsTrue(m.TryConsume());
            Assert.AreEqual(0f, m.Value, 1e-5f);
        }

        [Test]
        public void RepeatCount_TracksUsesInCombo()
        {
            var c = new RetroSk8.Core.ComboTracker(new RetroSk8.Core.ScoringConfig());
            Assert.AreEqual(0, c.RepeatCount("g"));
            c.StartContinuous("g", "G", RetroSk8.Core.TrickCategory.Grind, 10);
            c.StartContinuous("g", "G", RetroSk8.Core.TrickCategory.Grind, 10);
            Assert.AreEqual(2, c.RepeatCount("g"));
        }
    }
}
