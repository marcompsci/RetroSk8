using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class AdvancedTrickTests
    {
        [Test]
        public void Wall_ClassifiesByApproach()
        {
            Assert.AreEqual(WallMove.Wallride, WallRules.Classify(20f, 9f, 0f));
            Assert.AreEqual(WallMove.Wallride, WallRules.Classify(-45f, 9f, 5f), "either side of the wall");
            Assert.AreEqual(WallMove.Wallplant, WallRules.Classify(80f, 9f, 0f));
            Assert.AreEqual(WallMove.None, WallRules.Classify(52f, 9f, 0f), "awkward angle just bounces");
        }

        [Test]
        public void Wall_NeedsSpeed_AndAnUprightWall()
        {
            Assert.AreEqual(WallMove.None, WallRules.Classify(20f, 3f, 0f));
            Assert.AreEqual(WallMove.None, WallRules.Classify(20f, 9f, 40f), "a ramp is not a wall");
        }

        [Test]
        public void Lip_NeedsSquareApproach()
        {
            Assert.IsTrue(LipRules.IsLipApproach(90f));
            Assert.IsTrue(LipRules.IsLipApproach(-70f));
            Assert.IsFalse(LipRules.IsLipApproach(20f), "parallel to the coping is a grind");
        }

        [Test]
        public void Lip_NamesAreDistinctPerZone()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (StickZone z in System.Enum.GetValues(typeof(StickZone)))
            {
                Assert.IsTrue(seen.Add(LipRules.NameFor(z)));
                Assert.IsTrue(LipRules.IdFor(z).StartsWith("lip_"));
            }
        }

        [Test]
        public void Revert_Window()
        {
            Assert.IsTrue(RevertRules.CanRevert(0.1f, true, false));
            Assert.IsFalse(RevertRules.CanRevert(0.4f, true, false), "too late");
            Assert.IsFalse(RevertRules.CanRevert(0.1f, false, false), "flat-ground landings don't revert");
            Assert.IsFalse(RevertRules.CanRevert(0.1f, true, true), "one revert per landing");
        }

        [Test]
        public void NewTricks_ScoreWhileHeld_AndCountForFlow()
        {
            var config = ScoringConfig.CreateDefault();
            Assert.Greater(config.RatePerSecond(TrickCategory.Lip), 0f);
            Assert.Greater(config.RatePerSecond(TrickCategory.Wall), 0f);
            var combo = new ComboTracker(config);
            combo.AddTrick("ollie_flip", "Tide Flip", TrickCategory.BoardFlip, 500);
            combo.StartContinuous("wallride", "Wallride", TrickCategory.Wall, WallRules.WallrideStartPoints);
            combo.TickContinuous(1f);
            combo.AddTrick(RevertRules.Id, RevertRules.Name, TrickCategory.Revert, RevertRules.Points);
            Assert.AreEqual(3, combo.DistinctCategories);
            Assert.Greater(combo.FlowMultiplier, 1f);
            Assert.AreEqual(Line(TrickCategory.Wall), LineElement.Wall);
        }

        private static LineElement Line(TrickCategory c) => c.ToLineElement();
    }
}
