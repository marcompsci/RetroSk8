using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class ComboTrackerTests
    {
        private static ScoringConfig NoFlowConfig() => new ScoringConfig
        {
            repeatDecay = 0.5f,
            minRepeatFactor = 0.1f,
            multiplierPerTrick = 1f,
            maxMultiplier = 20f,
            flowPerExtraCategory = 0f,
            flowPerLink = 0f,
            grindPointsPerSecond = 100f,
            manualPointsPerSecond = 50f,
        };

        [Test]
        public void EmptyCombo_HasZeroValue_AndBanksNothing()
        {
            var c = new ComboTracker(NoFlowConfig());
            Assert.IsFalse(c.IsActive);
            Assert.AreEqual(0, c.CurrentValue);
            Assert.AreEqual(0, c.Bank().Points);
        }

        [Test]
        public void SingleTrick_ValueIsBaseTimesOne()
        {
            var c = new ComboTracker(NoFlowConfig());
            c.AddTrick("flip_a", "A", TrickCategory.BoardFlip, 500);
            Assert.AreEqual(1f, c.Multiplier);
            Assert.AreEqual(500, c.CurrentValue);
        }

        [Test]
        public void MultiplierGrowsPerTrick()
        {
            var c = new ComboTracker(NoFlowConfig());
            c.AddTrick("a", "A", TrickCategory.BoardFlip, 100);
            c.AddTrick("b", "B", TrickCategory.Grab, 200);
            c.AddTrick("c", "C", TrickCategory.Spin, 300);
            Assert.AreEqual(3f, c.Multiplier);
            Assert.AreEqual((100 + 200 + 300) * 3, c.CurrentValue);
        }

        [Test]
        public void MultiplierIsClamped()
        {
            var cfg = NoFlowConfig();
            cfg.maxMultiplier = 3f;
            var c = new ComboTracker(cfg);
            for (int i = 0; i < 10; i++) c.AddTrick("t" + i, "T", TrickCategory.BoardFlip, 10);
            Assert.AreEqual(3f, c.Multiplier);
        }

        [Test]
        public void RepeatingTrick_DecaysValue()
        {
            var c = new ComboTracker(NoFlowConfig());
            Assert.AreEqual(400f, c.AddTrick("x", "X", TrickCategory.BoardFlip, 400));
            Assert.AreEqual(200f, c.AddTrick("x", "X", TrickCategory.BoardFlip, 400));
            Assert.AreEqual(100f, c.AddTrick("x", "X", TrickCategory.BoardFlip, 400));
        }

        [Test]
        public void RepeatDecay_IsFlooredAtMinimum()
        {
            var c = new ComboTracker(NoFlowConfig());
            for (int i = 0; i < 8; i++) c.AddTrick("x", "X", TrickCategory.BoardFlip, 1000);
            Assert.AreEqual(0.1f, c.PeekRepeatFactor("x"), 1e-5f);
        }

        [Test]
        public void RepeatCounts_ResetAfterBank()
        {
            var c = new ComboTracker(NoFlowConfig());
            c.AddTrick("x", "X", TrickCategory.BoardFlip, 400);
            c.Bank();
            Assert.AreEqual(400f, c.AddTrick("x", "X", TrickCategory.BoardFlip, 400));
        }

        [Test]
        public void Bank_ReturnsValue_AndResets()
        {
            var c = new ComboTracker(NoFlowConfig());
            c.AddTrick("a", "A", TrickCategory.BoardFlip, 100);
            c.AddTrick("b", "B", TrickCategory.Grab, 100);
            var r = c.Bank();
            Assert.AreEqual(400, r.Points);
            Assert.AreEqual(2, r.TrickCount);
            Assert.IsFalse(c.IsActive);
            Assert.AreEqual(0, c.CurrentValue);
        }

        [Test]
        public void SketchyBank_AppliesFactor()
        {
            var c = new ComboTracker(NoFlowConfig());
            c.AddTrick("a", "A", TrickCategory.BoardFlip, 1000);
            Assert.AreEqual(800, c.Bank(0.8f).Points);
        }

        [Test]
        public void Bail_ErasesUnbankedPoints_AndReportsLoss()
        {
            var c = new ComboTracker(NoFlowConfig());
            c.AddTrick("a", "A", TrickCategory.BoardFlip, 100);
            c.AddTrick("b", "B", TrickCategory.Grab, 100);
            Assert.AreEqual(400, c.Bail());
            Assert.IsFalse(c.IsActive);
            Assert.AreEqual(0, c.Bank().Points);
        }

        [Test]
        public void ContinuousTrick_AccruesPerSecond_WithRepeatFactor()
        {
            var c = new ComboTracker(NoFlowConfig());
            c.StartContinuous("g", "G", TrickCategory.Grind, 100);
            c.TickContinuous(2f); // +200
            Assert.AreEqual(300f, c.BasePoints, 1e-3f);
            c.EndContinuous();
            c.StartContinuous("g", "G", TrickCategory.Grind, 100); // repeat: factor 0.5 -> +50
            c.TickContinuous(2f); // +100
            Assert.AreEqual(450f, c.BasePoints, 1e-3f);
        }

        [Test]
        public void TickContinuous_WithoutActiveEntry_DoesNothing()
        {
            var c = new ComboTracker(NoFlowConfig());
            c.AddTrick("a", "A", TrickCategory.BoardFlip, 100);
            c.TickContinuous(5f);
            Assert.AreEqual(100f, c.BasePoints, 1e-3f);
        }

        [Test]
        public void AddingTrick_EndsActiveContinuous()
        {
            var c = new ComboTracker(NoFlowConfig());
            c.StartContinuous("m", "M", TrickCategory.Manual, 0);
            c.AddTrick("a", "A", TrickCategory.BoardFlip, 100);
            Assert.IsFalse(c.HasActiveContinuous);
        }

        [Test]
        public void LineFlow_RewardsVarietyAndLinks()
        {
            var cfg = NoFlowConfig();
            cfg.flowPerExtraCategory = 0.1f;
            cfg.flowPerLink = 0.05f;
            cfg.maxFlowBonus = 1f;
            var c = new ComboTracker(cfg);

            c.AddTrick("flip", "F", TrickCategory.BoardFlip, 100);        // Air
            c.StartContinuous("grind", "G", TrickCategory.Grind, 100);    // Air -> Grind: link 1
            c.StartContinuous("manual", "M", TrickCategory.Manual, 100);  // Grind -> Manual: link 2

            Assert.AreEqual(3, c.DistinctCategories);
            Assert.AreEqual(2, c.Links);
            // 1 + 0.1*2 + 0.05*2 = 1.3
            Assert.AreEqual(1.3f, c.FlowMultiplier, 1e-4f);
            Assert.AreEqual((long)(300 * 3 * 1.3f), c.CurrentValue);
        }

        [Test]
        public void LineFlow_SameElementRepeated_DoesNotLink()
        {
            var cfg = NoFlowConfig();
            cfg.flowPerLink = 0.05f;
            var c = new ComboTracker(cfg);
            c.AddTrick("a", "A", TrickCategory.BoardFlip, 100);
            c.AddTrick("b", "B", TrickCategory.Grab, 100);
            Assert.AreEqual(0, c.Links);
        }

        [Test]
        public void LineFlow_RampLaunchBeforeFirstTrick_CountsAsLink()
        {
            var cfg = NoFlowConfig();
            cfg.flowPerLink = 0.05f;
            var c = new ComboTracker(cfg);
            c.MarkElement(LineElement.RampAir);
            c.AddTrick("a", "A", TrickCategory.BoardFlip, 100);
            Assert.AreEqual(1, c.Links);
        }

        [Test]
        public void LineFlow_IsCapped()
        {
            var cfg = NoFlowConfig();
            cfg.flowPerExtraCategory = 1f;
            cfg.maxFlowBonus = 0.5f;
            var c = new ComboTracker(cfg);
            c.AddTrick("a", "A", TrickCategory.BoardFlip, 1);
            c.AddTrick("b", "B", TrickCategory.Grab, 1);
            c.AddTrick("c", "C", TrickCategory.Spin, 1);
            Assert.AreEqual(1.5f, c.FlowMultiplier, 1e-5f);
        }
    }
}
