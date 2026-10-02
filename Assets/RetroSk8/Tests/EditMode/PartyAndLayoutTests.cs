using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class PartyTests
    {
        private static PartyRules Letters(int n)
        {
            var names = new List<string>();
            for (int i = 0; i < n; i++) names.Add("P" + (i + 1));
            return new PartyRules(PartyGame.Letters, names);
        }

        private static void Attempt(PartyRules r, long points)
        {
            Assert.AreEqual(PartyPhase.Handoff, r.Phase);
            r.BeginAttempt();
            r.EndAttempt(points);
        }

        [Test]
        public void SetThenMatch_FailTakesALetter()
        {
            var r = Letters(2);
            Assert.IsTrue(r.IsSetting);
            Attempt(r, 5000);                  // P1 sets 5,000
            Assert.AreEqual(1, r.CurrentIndex);
            Assert.AreEqual(4000, r.Target);    // 80%
            Attempt(r, 3000);                  // P2 misses
            Assert.AreEqual(1, r.Players[1].Letters);
            Assert.AreEqual("R", r.Players[1].LetterText);
            Assert.AreEqual(1, r.CurrentIndex, "P2 sets next");
            Assert.IsTrue(r.IsSetting);
        }

        [Test]
        public void MissedSet_PassesTheSet()
        {
            var r = Letters(3);
            Attempt(r, 0);
            Assert.AreEqual(1, r.CurrentIndex);
            Assert.IsTrue(r.IsSetting);
            Assert.AreEqual(0, r.Players[0].Letters, "missing your own set costs nothing");
        }

        [Test]
        public void SpellingTheWord_KnocksYouOut_AndLastOneWins()
        {
            var r = Letters(2);
            int guard = 0;
            while (r.Phase != PartyPhase.Finished && guard++ < 100)
            {
                // P1 always sets 1,000 and lands it; P2 always misses; P2 never lands a set.
                bool p1 = r.CurrentIndex == 0;
                Attempt(r, p1 ? 1000 : 0);
            }
            Assert.AreEqual(PartyPhase.Finished, r.Phase);
            Assert.IsTrue(r.Players[1].Out);
            Assert.AreEqual(PartyRules.Word.Length, r.Players[1].Letters);
            var w = r.Winners();
            Assert.AreEqual(1, w.Count);
            Assert.AreEqual("P1", w[0].Name);
        }

        [Test]
        public void OutPlayers_AreSkipped()
        {
            var r = Letters(3);
            r.Players[1].Letters = PartyRules.Word.Length; // P2 already out
            Attempt(r, 2000);
            Assert.AreEqual(2, r.CurrentIndex, "P3 matches, P2 is skipped");
        }

        [Test]
        public void ScoreTurns_BestWins_TiesShare()
        {
            var r = new PartyRules(PartyGame.ScoreTurns, new[] { "A", "B", "C" });
            Attempt(r, 900);
            Attempt(r, 1200);
            Attempt(r, 1200);
            Assert.AreEqual(PartyPhase.Finished, r.Phase);
            var w = r.Winners();
            Assert.AreEqual(2, w.Count);
        }

        [Test]
        public void PlayerCount_IsValidated()
        {
            Assert.Throws<System.ArgumentException>(() => new PartyRules(PartyGame.Letters, new[] { "Solo" }));
            Assert.Throws<System.ArgumentException>(() => new PartyRules(PartyGame.Letters, new[] { "1", "2", "3", "4", "5" }));
        }

        [Test]
        public void EndAttempt_IgnoredOutsidePlay()
        {
            var r = Letters(2);
            r.EndAttempt(5000); // not started
            Assert.IsTrue(r.IsSetting);
            Assert.AreEqual(0, r.CurrentIndex);
        }
    }

    public class TouchLayoutTests
    {
        [Test]
        public void LeftHanded_MirrorsEverything()
        {
            var d = TouchLayout.Default();
            var l = TouchLayout.LeftHanded();
            Assert.IsFalse(d.StickOnRight);
            Assert.IsTrue(l.StickOnRight);
            Assert.AreEqual(1f - d.jumpX, l.jumpX, 1e-5f);
            Assert.AreEqual(d.jumpY, l.jumpY, 1e-5f);
        }

        [Test]
        public void Clamp_KeepsControlsReachable()
        {
            var l = new TouchLayout { stickX = -3f, jumpY = 9f, actionX = float.NaN, scale = 10f, sensitivity = 0f };
            l.Clamp();
            Assert.AreEqual(TouchLayout.EdgeMargin, l.stickX, 1e-5f);
            Assert.AreEqual(1f - TouchLayout.EdgeMargin, l.jumpY, 1e-5f);
            Assert.AreEqual(0.5f, l.actionX, 1e-5f);
            Assert.AreEqual(TouchLayout.MaxScale, l.scale, 1e-5f);
            Assert.AreEqual(1f, l.sensitivity, 1e-5f);
        }

        [Test]
        public void Sensitivity_ShortensStickTravel()
        {
            var l = new TouchLayout { sensitivity = 1.5f };
            Assert.AreEqual(80f, l.StickRadius(120f), 1e-3f);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var a = TouchLayout.Default();
            var b = a.Clone();
            b.Mirror();
            Assert.AreNotEqual(a.stickX, b.stickX);
        }
    }
}
