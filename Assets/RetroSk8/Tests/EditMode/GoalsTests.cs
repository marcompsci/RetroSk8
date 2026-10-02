using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class GoalTrackerTests
    {
        private static GoalTracker Harbor() => new GoalTracker(new[]
        {
            new GoalDefinition("line", "Land a 10,000-point line", GoalType.ComboScore, 10000),
            new GoalDefinition("rails", "Grind three separate rails", GoalType.DistinctRails, 3),
            new GoalDefinition("fountain", "Clear the fountain gap", GoalType.ClearGap, 1, "fountain_gap"),
        });

        [Test]
        public void ComboScore_CompletesOnlyAtTarget()
        {
            var t = Harbor();
            t.OnBanked(9999, 3, 1f);
            Assert.AreEqual(0, t.CompletedCount);
            t.OnBanked(10000, 3, 1f);
            Assert.AreEqual(1, t.CompletedCount);
            var done = t.ConsumeCompleted();
            Assert.AreEqual(1, done.Count);
            Assert.AreEqual("line", done[0].Goal.id);
            Assert.AreEqual(0, t.ConsumeCompleted().Count);
        }

        [Test]
        public void DistinctRails_CountsUniqueRailsOnly()
        {
            var t = Harbor();
            t.OnRailsBanked(new[] { 1, 1, 2 });
            Assert.IsFalse(t.Goals[1].Completed);
            Assert.AreEqual(2f, t.Goals[1].Progress, 1e-5f);
            t.OnRailsBanked(new[] { 2 });
            Assert.IsFalse(t.Goals[1].Completed);
            t.OnRailsBanked(new[] { 7 });
            Assert.IsTrue(t.Goals[1].Completed);
        }

        [Test]
        public void ClearGap_MatchesGapId()
        {
            var t = Harbor();
            t.OnGapCleared("container_gap");
            Assert.IsFalse(t.Goals[2].Completed);
            t.OnGapCleared("fountain_gap");
            Assert.IsTrue(t.Goals[2].Completed);
        }

        [Test]
        public void AllComplete_WhenEveryGoalDone()
        {
            var t = Harbor();
            t.OnBanked(20000, 5, 1.3f);
            t.OnRailsBanked(new[] { 1, 2, 3 });
            Assert.IsFalse(t.AllComplete);
            t.OnGapCleared("fountain_gap");
            Assert.IsTrue(t.AllComplete);
            Assert.AreEqual(3, t.CompletedCount);
        }

        [Test]
        public void CompletedGoal_DoesNotReport_Twice()
        {
            var t = Harbor();
            t.OnBanked(10000, 1, 1f);
            t.ConsumeCompleted();
            t.OnBanked(50000, 1, 1f);
            Assert.AreEqual(0, t.ConsumeCompleted().Count);
        }

        [Test]
        public void OtherGoalTypes_Track()
        {
            var t = new GoalTracker(new[]
            {
                new GoalDefinition("m", "manual", GoalType.ManualSeconds, 3),
                new GoalDefinition("s", "spin", GoalType.SpinHalfTurns, 2),
                new GoalDefinition("d", "tricks", GoalType.DistinctTricks, 3),
                new GoalDefinition("c", "count", GoalType.ComboTrickCount, 4),
                new GoalDefinition("f", "flow", GoalType.LineFlow, 1.2f),
                new GoalDefinition("t", "total", GoalType.TotalScore, 5000),
            });
            t.OnManualFinished(2.5f);
            t.OnSpinLanded(1);
            t.OnTricksBanked(new[] { "a", "b", "a" });
            t.OnBanked(100, 3, 1.1f);
            t.OnScoreChanged(4999);
            Assert.AreEqual(0, t.CompletedCount);

            t.OnManualFinished(3f);
            t.OnSpinLanded(2);
            t.OnTricksBanked(new[] { "c" });
            t.OnBanked(100, 4, 1.25f);
            t.OnScoreChanged(5000);
            Assert.IsTrue(t.AllComplete);
        }

        [Test]
        public void NullGoals_IsEmpty()
        {
            var t = new GoalTracker(null);
            Assert.AreEqual(0, t.Total);
            Assert.IsFalse(t.AllComplete);
        }
    }

    public class DailyLineTests
    {
        private static readonly DailyLineGenerator.Gap[] Gaps =
        {
            new DailyLineGenerator.Gap("fountain_gap", "Fountain Gap"),
            new DailyLineGenerator.Gap("container_gap", "Container Gap"),
        };

        [Test]
        public void SameDay_SameLine()
        {
            var a = DailyLineGenerator.Generate(20260929, "harbor_plaza", Gaps);
            var b = DailyLineGenerator.Generate(20260929, "harbor_plaza", Gaps);
            Assert.AreEqual(a.TargetScore, b.TargetScore);
            Assert.AreEqual(3, a.Goals.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(a.Goals[i].description, b.Goals[i].description);
                Assert.AreEqual(a.Goals[i].target, b.Goals[i].target, 1e-5f);
            }
        }

        [Test]
        public void GoalsAreDistinctTypes_AndTargetsSane()
        {
            for (int day = 1; day <= 60; day++)
            {
                var line = DailyLineGenerator.Generate(20260900 + day, "harbor_plaza", Gaps);
                var seen = new HashSet<GoalType>();
                foreach (var g in line.Goals)
                {
                    Assert.IsTrue(seen.Add(g.type), $"duplicate goal type on day {day}");
                    Assert.IsTrue(g.target > 0f);
                    Assert.IsFalse(string.IsNullOrEmpty(g.description));
                    if (g.type == GoalType.ClearGap) Assert.IsTrue(g.param == "fountain_gap" || g.param == "container_gap");
                }
                Assert.IsTrue(line.TargetScore >= 15000 && line.TargetScore <= 30000);
            }
        }

        [Test]
        public void DifferentDays_Vary()
        {
            var descriptions = new HashSet<string>();
            for (int day = 1; day <= 14; day++)
            {
                var line = DailyLineGenerator.Generate(20261000 + day, "harbor_plaza", Gaps);
                descriptions.Add(line.Goals[0].description + "|" + line.Goals[1].description + "|" + line.Goals[2].description);
            }
            Assert.Greater(descriptions.Count, 7);
        }

        [Test]
        public void NoGaps_NeverAsksForGap()
        {
            for (int day = 1; day <= 30; day++)
                foreach (var g in DailyLineGenerator.Generate(20261100 + day, "rooftop_run", null).Goals)
                    Assert.AreNotEqual(GoalType.ClearGap, g.type);
        }

        [Test]
        public void DateKey_Format()
        {
            Assert.AreEqual(20260929, DailyLineGenerator.DateKey(new System.DateTime(2026, 9, 29)));
        }

        [Test]
        public void PickIndex_RotatesOneParkPerDay()
        {
            Assert.AreEqual(0, DailyLineGenerator.PickIndex(20261001, 1));
            Assert.AreEqual(0, DailyLineGenerator.PickIndex(20261001, 0));
            int a = DailyLineGenerator.PickIndex(20261001, 3);
            int b = DailyLineGenerator.PickIndex(20261002, 3);
            int c = DailyLineGenerator.PickIndex(20261003, 3);
            int d = DailyLineGenerator.PickIndex(20261004, 3);
            Assert.AreEqual((a + 1) % 3, b);
            Assert.AreEqual((b + 1) % 3, c);
            Assert.AreEqual(a, d);
            // Month and year boundaries keep rotating by one.
            Assert.AreEqual((DailyLineGenerator.PickIndex(20260930, 3) + 1) % 3, DailyLineGenerator.PickIndex(20261001, 3));
            Assert.AreEqual((DailyLineGenerator.PickIndex(20261231, 3) + 1) % 3, DailyLineGenerator.PickIndex(20270101, 3));
        }

        [Test]
        public void PickIndex_InRange_ForAnyKey()
        {
            foreach (int key in new[] { 20000101, 19991231, 20240229, 99999999, 0, -5 })
            {
                int i = DailyLineGenerator.PickIndex(key, 3);
                Assert.IsTrue(i >= 0 && i < 3, $"key {key} gave {i}");
            }
        }

        [Test]
        public void ParksGetDifferentLines_SameDay()
        {
            var gaps = new List<DailyLineGenerator.Gap> { new DailyLineGenerator.Gap("g", "Gap") };
            var a = DailyLineGenerator.Generate(20261001, "harbor_plaza", gaps);
            var b = DailyLineGenerator.Generate(20261001, "rooftop_run", gaps);
            bool differ = a.TargetScore != b.TargetScore;
            for (int i = 0; i < 3 && !differ; i++) differ = a.Goals[i].description != b.Goals[i].description;
            Assert.IsTrue(differ, "the park id seeds the line");
        }
    }

    public class EconomyTests
    {
        private readonly ScoringConfig _cfg = new ScoringConfig
        {
            pointsPerTapeToken = 5000, tokensPerGoal = 5, tokensAllGoalsBonus = 10, tokensDailyBonus = 20,
        };

        [Test]
        public void Rewards_AddUp()
        {
            var a = TokenRewards.ForRun(_cfg, 12000, true, 3, true, true);
            Assert.AreEqual(2, a.FromScore);
            Assert.AreEqual(25, a.FromGoals);
            Assert.AreEqual(20, a.FromDailyBonus);
            Assert.AreEqual(47, a.Total);
        }

        [Test]
        public void RepeatCompletions_PayOnlyScore()
        {
            var a = TokenRewards.ForRun(_cfg, 10000, true, 0, false, false);
            Assert.AreEqual(2, a.Total);
        }

        [Test]
        public void FreeSkate_ScoreDoesNotPay()
        {
            Assert.AreEqual(0, TokenRewards.ForRun(_cfg, 90000, false, 0, false, false).Total);
        }

        [Test]
        public void Shop_Rules()
        {
            Assert.AreEqual(PurchaseResult.Ok, ShopRules.Check(50, 50, false));
            Assert.AreEqual(PurchaseResult.NotEnoughTokens, ShopRules.Check(49, 50, false));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, ShopRules.Check(500, 50, true));
            Assert.AreEqual(PurchaseResult.InvalidPrice, ShopRules.Check(500, -1, false));
        }
    }
}
