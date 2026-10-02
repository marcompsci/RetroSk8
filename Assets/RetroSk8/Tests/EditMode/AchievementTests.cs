using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class AchievementTests
    {
        [Test]
        public void FreshPlayer_HasNothing()
        {
            Assert.AreEqual(0, Achievements.NewlyUnlocked(new PlayerProgress { ParksTotal = 3, ContractsTotal = 3 }, null).Count);
        }

        [Test]
        public void Thresholds_UnlockExactlyAtTarget()
        {
            var p = new PlayerProgress { BestCombo = 9999, ParksTotal = 3, ContractsTotal = 3 };
            Assert.IsFalse(Ids(p).Contains("line_10k"));
            p.BestCombo = 10000;
            p.CombosBanked = 1;
            var ids = Ids(p);
            Assert.IsTrue(ids.Contains("line_10k"));
            Assert.IsTrue(ids.Contains("first_bank"));
            Assert.IsFalse(ids.Contains("line_50k"));
        }

        [Test]
        public void AlreadyUnlocked_IsNotRepeated()
        {
            var p = new PlayerProgress { TutorialDone = true, MaxHalfTurns = 3 };
            var first = Achievements.NewlyUnlocked(p, null);
            Assert.AreEqual(2, first.Count);
            var owned = new HashSet<string>();
            foreach (var a in first) owned.Add(a.Id);
            Assert.AreEqual(0, Achievements.NewlyUnlocked(p, owned).Count);
        }

        [Test]
        public void AllContracts_NeedsEveryContract_AndANonZeroTotal()
        {
            var p = new PlayerProgress { ContractsComplete = 2, ContractsTotal = 3 };
            Assert.IsTrue(Ids(p).Contains("contractor"));
            Assert.IsFalse(Ids(p).Contains("all_contracts"));
            p.ContractsComplete = 3;
            Assert.IsTrue(Ids(p).Contains("all_contracts"));
            Assert.IsFalse(Ids(new PlayerProgress()).Contains("all_contracts"), "no contracts defined must not unlock");
        }

        [Test]
        public void Progress_IsClamped_AndPartial()
        {
            var spin = Achievements.Find("spin_540");
            Assert.AreEqual(2f / 3f, spin.Progress(new PlayerProgress { MaxHalfTurns = 2 }), 1e-4f);
            Assert.AreEqual(1f, spin.Progress(new PlayerProgress { MaxHalfTurns = 9 }), 1e-4f);
        }

        [Test]
        public void Ids_AreUnique_AndGameCenterSafe()
        {
            var seen = new HashSet<string>();
            foreach (var a in Achievements.All)
            {
                Assert.IsTrue(seen.Add(a.Id), "duplicate id " + a.Id);
                string gc = Achievements.GameCenterId(a);
                foreach (char c in gc)
                    Assert.IsTrue(char.IsLetterOrDigit(c) || c == '.' || c == '_', "bad character in " + gc);
                Assert.IsTrue(gc.Length <= 100);
            }
            Assert.AreEqual("retrosk8.score.harbor_plaza", Achievements.LeaderboardId("harbor_plaza"));
        }

        private static List<string> Ids(PlayerProgress p)
        {
            var list = new List<string>();
            foreach (var a in Achievements.NewlyUnlocked(p, null)) list.Add(a.Id);
            return list;
        }
    }
}
