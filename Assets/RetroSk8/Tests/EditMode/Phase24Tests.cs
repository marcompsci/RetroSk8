using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    /// <summary>Phase 24: Daily Tricks, replay tools, the Shipyard's shareable index and song.</summary>
    public class DailyTrickTests
    {
        private static readonly List<(string, string)> Tricks = new List<(string, string)>
        {
            ("kickflip", "Kickflip"), ("heelflip", "Heelflip"), ("grind_crossbar", "Crossbar Slide"), ("lip_0", "Rock to Fakie"),
        };

        [Test]
        public void SameDay_SameChallenge_AndEveryKindComesUp()
        {
            var a = DailyTricks.For(20261006, Tricks);
            var b = DailyTricks.For(20261006, Tricks);
            Assert.AreEqual(a.Kind, b.Kind);
            Assert.AreEqual(a.Text, b.Text);
            var kinds = new HashSet<DailyTrickKind>();
            for (int d = 1; d <= 60; d++) kinds.Add(DailyTricks.For(20261000 + d, Tricks).Kind);
            Assert.AreEqual(5, kinds.Count, "all five kinds turn up within two months");
            foreach (var k in kinds) Assert.IsTrue(Enum.IsDefined(typeof(DailyTrickKind), k));
        }

        [Test]
        public void NoTricks_FallsBackToABigLine()
        {
            for (int d = 1; d <= 30; d++)
                Assert.AreNotEqual(DailyTrickKind.LandTrick, DailyTricks.For(20261100 + d, new List<(string, string)>()).Kind);
        }

        [Test]
        public void Matches_EachKind()
        {
            var land = new DailyTrickChallenge { Kind = DailyTrickKind.LandTrick, TrickId = "kickflip", Count = 3 };
            Assert.AreEqual(1, DailyTricks.Matches(land, new[] { "ollie", "kickflip", "kickflip" }, 500), "one line = one rep");
            Assert.AreEqual(0, DailyTricks.Matches(land, new[] { "heelflip" }, 500));
            Assert.AreEqual(0, DailyTricks.Matches(land, new[] { "kickflip" }, 0), "bailed lines don't count");

            var big = new DailyTrickChallenge { Kind = DailyTrickKind.BigLine, MinPoints = 10000 };
            Assert.AreEqual(1, DailyTricks.Matches(big, new[] { "x" }, 10000));
            Assert.AreEqual(0, DailyTricks.Matches(big, new[] { "x" }, 9999));

            var grind = new DailyTrickChallenge { Kind = DailyTrickKind.GrindChain, Count = 2 };
            Assert.AreEqual(1, DailyTricks.Matches(grind, new[] { "grind_crossbar", "ollie", "grind_plank_slide" }, 100));
            Assert.AreEqual(0, DailyTricks.Matches(grind, new[] { "grind_crossbar" }, 100));

            var spin = new DailyTrickChallenge { Kind = DailyTrickKind.BigSpin, HalfTurns = 3 };
            Assert.AreEqual(1, DailyTricks.Matches(spin, new[] { "spin_720" }, 100));
            Assert.AreEqual(0, DailyTricks.Matches(spin, new[] { "spin_360" }, 100));
            Assert.AreEqual(4, DailyTricks.SpinHalfTurns("spin_720"));
            Assert.AreEqual(0, DailyTricks.SpinHalfTurns("spin_abc"));

            var bonk = new DailyTrickChallenge { Kind = DailyTrickKind.BonkLines, Count = 3 };
            Assert.AreEqual(1, DailyTricks.Matches(bonk, new[] { BonkRules.BonkId }, 100));
        }

        [Test]
        public void Finishing_PaysOnce_WithAStreakBonus()
        {
            var s = new DailyTrickState();
            var c = new DailyTrickChallenge { DateKey = 20261006, Kind = DailyTrickKind.LandTrick, TrickId = "kickflip", Count = 3 };
            int today = 1000;
            s.doneDays.AddRange(new[] { 997, 998, 999 }); // three days in a row before today
            Assert.IsFalse(DailyTricks.Record(s, c, today, new[] { "kickflip" }, 100).Completed);
            Assert.IsFalse(DailyTricks.Record(s, c, today, new[] { "kickflip" }, 100).Completed);
            var u = DailyTricks.Record(s, c, today, new[] { "kickflip" }, 100);
            Assert.IsTrue(u.Completed);
            Assert.AreEqual(4, u.Streak);
            Assert.AreEqual(DailyTricks.BaseTokens + 3 * DailyTricks.StreakBonusPerDay, u.Tokens);
            Assert.IsFalse(DailyTricks.Record(s, c, today, new[] { "kickflip" }, 100).Progressed, "paid once a day");

            // Tomorrow resets the count.
            var tomorrow = new DailyTrickChallenge { DateKey = 20261007, Kind = DailyTrickKind.BigLine, MinPoints = 100 };
            var t = DailyTricks.Record(s, tomorrow, today + 1, new[] { "x" }, 500);
            Assert.IsTrue(t.Completed);
            Assert.AreEqual(5, t.Streak);
        }

        [Test]
        public void StreakBonus_IsCapped_AndGapsBreakIt()
        {
            Assert.AreEqual(DailyTricks.BaseTokens, DailyTricks.TokensFor(1));
            Assert.AreEqual(DailyTricks.BaseTokens + DailyTricks.StreakBonusPerDay * DailyTricks.MaxStreakBonusDays, DailyTricks.TokensFor(50));
            var s = new DailyTrickState();
            s.doneDays.AddRange(new[] { 10, 11, 13 });
            Assert.AreEqual(1, DailyTricks.StreakEnding(s, 13));
            Assert.AreEqual(1, DailyTricks.StreakEnding(s, 14), "today not done yet: yesterday's streak still shows");
            Assert.AreEqual(0, DailyTricks.StreakEnding(s, 16));
        }

        [Test]
        public void Calendar_ShowsTheLastTwoWeeks()
        {
            var s = new DailyTrickState();
            s.doneDays.AddRange(new[] { 90, 99, 100 });
            var cal = DailyTricks.Calendar(s, 100);
            Assert.AreEqual(DailyTricks.CalendarDays, cal.Count);
            Assert.AreEqual(87, cal[0].day);
            Assert.AreEqual(100, cal[cal.Count - 1].day);
            Assert.AreEqual(3, cal.Count(x => x.done));
        }

        [Test]
        public void State_KeepsOnlyRecentDays()
        {
            var s = new DailyTrickState();
            for (int d = 0; d < 100; d++) s.doneDays.Add(d);
            s.Sanitize();
            Assert.AreEqual(DailyTricks.KeepDays, s.doneDays.Count);
            Assert.AreEqual(99, s.doneDays.Last());
        }
    }

    public class ReplayToolsTests
    {
        [Test]
        public void SlowMo_EasesInAndOut_AroundEachMoment()
        {
            var moments = new List<float> { 10f };
            Assert.AreEqual(1f, ReplayTools.SlowMoFactor(5f, moments));
            Assert.AreEqual(1f, ReplayTools.SlowMoFactor(10f - ReplayTools.SlowBefore, moments), 1e-4f);
            Assert.AreEqual(ReplayTools.SlowSpeed, ReplayTools.SlowMoFactor(9.5f, moments), 1e-4f, "fully slow in the middle");
            float easing = ReplayTools.SlowMoFactor(10f - ReplayTools.SlowBefore + 0.1f, moments);
            Assert.Greater(easing, ReplayTools.SlowSpeed);
            Assert.Less(easing, 1f);
            Assert.AreEqual(1f, ReplayTools.SlowMoFactor(10f + ReplayTools.SlowAfter + 0.01f, moments));
            Assert.AreEqual(1f, ReplayTools.SlowMoFactor(3f, null));
        }

        [Test]
        public void SlowMo_SlowsTheClock()
        {
            var normal = new ReplayClock(20f);
            var slow = new ReplayClock(20f) { SpeedAt = t => ReplayTools.SlowMoFactor(t, new List<float> { 2f }) };
            for (int i = 0; i < 120; i++) { normal.Tick(1f / 60f); slow.Tick(1f / 60f); }
            Assert.AreEqual(2f, normal.Time, 0.01f);
            Assert.Less(slow.Time, normal.Time - 0.3f, "the run-up to the moment plays slower");
        }

        [Test]
        public void Vertical_IsACentred9By16Strip()
        {
            var (x, w) = ReplayTools.VerticalViewport(2796, 1290);
            Assert.AreEqual(1290f * 9f / 16f / 2796f, w, 1e-5f);
            Assert.AreEqual((1f - w) * 0.5f, x, 1e-5f);
            Assert.AreEqual((0f, 1f), ReplayTools.VerticalViewport(1000, 3000), "already tall: unchanged");
            Assert.AreEqual((0f, 1f), ReplayTools.VerticalViewport(0, 0));
        }

        [Test]
        public void SevenCameras()
        {
            Assert.AreEqual(ReplayTools.CameraCount, Enum.GetValues(typeof(ReplayCamera)).Length);
        }
    }

    public class ShipyardTests
    {
        [Test]
        public void Shipyard_IsShareable_AtTheEnd()
        {
            Assert.AreEqual(9, Array.IndexOf(ShareCodes.BuiltInParks, "shipyard"));
            Assert.AreEqual(8, Array.IndexOf(ShareCodes.BuiltInParks, "offseason_rink"));
            Assert.Less(ShareCodes.BuiltInParks.Length, 16);
        }

        [Test]
        public void ShipyardSong_IsAFullLoop()
        {
            var spec = MusicComposer.Shipyard;
            var data = MusicComposer.Render(spec, 22050);
            Assert.AreEqual(spec.Progression.Length * 4f * 60f / spec.Bpm * 22050, data.Length, 1.01f);
            Assert.IsFalse(data.Any(v => float.IsNaN(v) || v > 1f || v < -1f));
        }
    }
}
