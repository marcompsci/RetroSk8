using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class SkateDuelTests
    {
        [Test]
        public void SetThenMiss_GivesALetter_AndSetterSetsAgain()
        {
            var d = new SkateDuel(0);
            Assert.AreEqual(0, d.Actor);
            Assert.IsTrue(d.Apply(0, 5000));
            Assert.AreEqual(DuelPhase.Matching, d.Phase);
            Assert.AreEqual(1, d.Actor);
            Assert.AreEqual(4000, d.Target);
            Assert.IsTrue(d.Apply(1, 3999));
            Assert.AreEqual(1, d.Letters(1));
            Assert.AreEqual("S", SkateDuel.LetterText(d.Letters(1)));
            Assert.AreEqual(DuelPhase.Setting, d.Phase);
            Assert.AreEqual(0, d.Setter, "setter keeps setting");
        }

        [Test]
        public void MissedSet_PassesTheSet()
        {
            var d = new SkateDuel(1);
            d.Apply(0, 0);
            Assert.AreEqual(0, d.Setter);
            Assert.AreEqual(DuelPhase.Setting, d.Phase);
        }

        [Test]
        public void StaleTurns_AreIgnored()
        {
            var d = new SkateDuel(0);
            Assert.IsTrue(d.Apply(0, 1000));
            Assert.IsFalse(d.Apply(0, 1000), "a duplicated result changes nothing");
            Assert.AreEqual(1, d.Turn);
        }

        [Test]
        public void FiveMisses_SpellSKATE_AndEndIt()
        {
            var d = new SkateDuel(0);
            int turn = 0;
            for (int i = 0; i < 5; i++)
            {
                d.Apply(turn++, 2000);
                d.Apply(turn++, 0);
            }
            Assert.AreEqual(DuelPhase.Finished, d.Phase);
            Assert.AreEqual(0, d.Winner);
            Assert.AreEqual("SKATE", SkateDuel.LetterText(d.Letters(1)));
            Assert.IsFalse(d.Apply(turn, 5000), "nothing after the end");
        }

        [Test]
        public void BothPhones_StayInSync()
        {
            var a = new SkateDuel(1);
            var b = new SkateDuel(1);
            long[] results = { 3000, 2500, 0, 4000, 1000, 6000, 6000, 0, 900, 100 };
            for (int t = 0; t < results.Length; t++)
            {
                a.Apply(t, results[t]);
                b.Apply(t, results[t]);
                Assert.AreEqual(a.Phase, b.Phase);
                Assert.AreEqual(a.Actor, b.Actor);
                Assert.AreEqual(a.Letters(0), b.Letters(0));
                Assert.AreEqual(a.Letters(1), b.Letters(1));
            }
        }

        [Test]
        public void Forfeit_EndsWithTheOtherAsWinner()
        {
            var d = new SkateDuel(0);
            d.Forfeit(1);
            Assert.AreEqual(DuelPhase.Finished, d.Phase);
            Assert.AreEqual(0, d.Winner);
        }

        [Test]
        public void Messages_RoundTrip()
        {
            var frame = new ReplayFrame { Time = 1.5f, Position = new RVec3(1, 2, 3), Rotation = RQuat.Identity, Pose = RQuat.Identity, Body = RQuat.Identity, Board = RQuat.Identity, BoardPosition = new RVec3(0, 0.1f, 0) };
            var msgs = new[]
            {
                new DuelMessage { Type = DuelMessageType.Hello, Name = "OMARI", Nonce = 12345, Style = 2 },
                new DuelMessage { Type = DuelMessageType.Start, ParkIndex = 3, FirstSetter = 1 },
                new DuelMessage { Type = DuelMessageType.AttemptBegin, Turn = 7 },
                new DuelMessage { Type = DuelMessageType.AttemptResult, Turn = 7, Points = 4321, Label = "TIDE FLIP + BLUNTSLIDE" },
                new DuelMessage { Type = DuelMessageType.Frame, Frame = frame },
                new DuelMessage { Type = DuelMessageType.Leave },
            };
            foreach (var m in msgs)
            {
                Assert.IsTrue(DuelMessage.TryParse(m.ToBytes(), out var back), m.Type.ToString());
                Assert.AreEqual(m.Type, back.Type);
                Assert.AreEqual(m.Name, back.Name);
                Assert.AreEqual(m.Nonce, back.Nonce);
                Assert.AreEqual(m.Style, back.Style);
                Assert.AreEqual(m.ParkIndex, back.ParkIndex);
                Assert.AreEqual(m.FirstSetter, back.FirstSetter);
                Assert.AreEqual(m.Turn, back.Turn);
                Assert.AreEqual(m.Points, back.Points);
                Assert.AreEqual(m.Label, back.Label);
                Assert.AreEqual(m.Frame.Position.Z, back.Frame.Position.Z);
            }
            Assert.IsFalse(DuelMessage.TryParse(new byte[] { 9, 1 }, out _), "other protocol versions are refused");
            Assert.IsFalse(DuelMessage.TryParse(new byte[] { 1, 5, 1 }, out _), "truncated messages are refused");
        }

        [Test]
        public void Bot_HardIsBetterThanEasy()
        {
            int easy = 0, hard = 0;
            var e = new DuelBot(DuelBot.Level.Easy, 1);
            var h = new DuelBot(DuelBot.Level.Hard, 1);
            for (int i = 0; i < 400; i++)
            {
                if (e.Match(3000) >= 3000) easy++;
                if (h.Match(3000) >= 3000) hard++;
            }
            Assert.Greater(hard, easy);
            Assert.Greater(easy, 0);
        }
    }
}
