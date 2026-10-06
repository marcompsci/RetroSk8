using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class ReplayLibraryTests
    {
        private static ReplayEntry E(string id, bool fav = false) => new ReplayEntry { id = id, favorite = fav };

        [Test]
        public void Index_KeepsRecentAndFavorites()
        {
            var index = new ReplayIndex();
            index.Add(E("fav", true));
            var dropped = new List<string>();
            for (int i = 0; i < ReplayIndex.MaxRecent + 3; i++) dropped.AddRange(index.Add(E("r" + i)));
            Assert.AreEqual(3, dropped.Count, "only the oldest non-favorites go");
            CollectionAssert.AreEquivalent(new[] { "r0", "r1", "r2" }, dropped);
            Assert.IsNotNull(index.Find("fav"), "favorites are never trimmed");
            Assert.AreEqual(ReplayIndex.MaxRecent + 1, index.entries.Count);
            Assert.AreEqual("r" + (ReplayIndex.MaxRecent + 2), index.entries[0].id, "newest first");
        }

        [Test]
        public void Favorites_AreCapped()
        {
            var index = new ReplayIndex();
            for (int i = 0; i < ReplayIndex.MaxFavorites; i++) { index.entries.Add(E("f" + i, true)); }
            index.entries.Add(E("x"));
            Assert.IsFalse(index.SetFavorite("x", true));
            Assert.IsTrue(index.SetFavorite("f0", false));
            Assert.IsTrue(index.SetFavorite("x", true));
        }

        [Test]
        public void LastSeconds_TrimsAndRetimes()
        {
            var frames = new List<ReplayFrame>();
            for (int i = 0; i <= 300; i++) frames.Add(new ReplayFrame { Time = i * 0.5f });
            var cut = ReplayIndex.LastSeconds(frames, 60f, out float at);
            Assert.AreEqual(90f, at, 0.001f);
            Assert.AreEqual(0f, cut[0].Time, 0.001f);
            Assert.AreEqual(60f, cut[cut.Count - 1].Time, 0.001f);
        }

        [Test]
        public void Clock_PlaysLoopsScrubsAndMarks()
        {
            var c = new ReplayClock(10f);
            c.Tick(4f);
            Assert.AreEqual(4f, c.Time, 0.001f);
            c.MarkIn();
            c.Seek(7f);
            c.MarkOut();
            Assert.AreEqual(3f, c.ClipLength, 0.001f);
            c.SetSpeed(1); // 0.5x
            c.Seek(6.5f);
            Assert.IsTrue(c.Tick(2f), "reaches the out point");
            Assert.AreEqual(4f, c.Time, 0.001f, "loops to the in point");
            c.Loop = false;
            c.Seek(6.9f);
            c.Tick(1f);
            Assert.IsFalse(c.Playing, "stops at the out point when exporting");
            c.SeekFraction(2f);
            Assert.AreEqual(10f, c.Time, 0.001f, "seeking is clamped");
        }
    }

    public class CrewTests
    {
        [Test]
        public void Roster_IsUnique_AndHomeParksAreReal()
        {
            var ids = new HashSet<string>();
            foreach (var m in CrewRoster.Members)
            {
                Assert.IsTrue(ids.Add(m.Id));
                Assert.GreaterOrEqual(Array.IndexOf(ShareCodes.BuiltInParks, m.HomePark), 0, m.Id);
                if (m.Recruit == RecruitKind.Score) Assert.Greater(m.ScoreTarget, 0);
            }
            Assert.AreEqual(8, CrewRoster.Members.Length);
        }

        [Test]
        public void Recruit_FillsTheRidingPair_ThenStopsAutoAdding()
        {
            var s = new CrewState();
            Assert.IsTrue(s.Recruit("pilar"));
            Assert.IsTrue(s.Recruit("dex"));
            Assert.IsTrue(s.Recruit("juno"));
            Assert.IsFalse(s.Recruit("juno"), "once only");
            Assert.AreEqual(2, s.active.Count);
            Assert.IsFalse(s.IsActive("juno"));
            Assert.IsFalse(s.ToggleActive("juno"), "pair is full");
            Assert.IsTrue(s.ToggleActive("pilar"));
            Assert.IsTrue(s.ToggleActive("juno"));
            Assert.IsFalse(s.ToggleActive("nobody"));
        }

        [Test]
        public void Perks_Stack()
        {
            var s = new CrewState();
            s.Recruit("juno");  // token
            s.Recruit("tess");  // token
            s.Perks(out float score, out float tokens, out float special);
            Assert.AreEqual(1f, score, 0.001f);
            Assert.AreEqual(1.4f, tokens, 0.001f);
            Assert.AreEqual(1f, special, 0.001f);
        }

        [Test]
        public void Xp_LevelsUp_AndPaysEachLevel()
        {
            var s = new CrewState();
            Assert.AreEqual(1, s.Level);
            int gained = s.AddXp(900, out int tokens);
            Assert.AreEqual(2, gained);
            Assert.AreEqual(3, s.Level);
            Assert.AreEqual(CrewLevels.RewardTokens(2) + CrewLevels.RewardTokens(3), tokens);
            s.AddXp(1_000_000, out _);
            Assert.AreEqual(CrewLevels.MaxLevel, s.Level);
            Assert.AreEqual(1f, CrewLevels.Progress(s.xp));
        }

        [Test]
        public void CrewStickers_NeedCrewLevels()
        {
            var crown = Stickers.Find(12);
            Assert.IsFalse(crown.UnlockedFor(8, 8));
            Assert.IsTrue(crown.UnlockedFor(0, 9));
            Assert.IsTrue(Stickers.Find(1).UnlockedFor(0, 1));
        }

        [Test]
        public void Sanitize_DropsUnknownsAndOverflow()
        {
            var s = new CrewState { recruited = new List<string> { "pilar", "ghost", "dex", "juno" }, active = new List<string> { "pilar", "dex", "juno", "ghost" }, xp = -5 };
            s.Sanitize();
            Assert.AreEqual(3, s.recruited.Count);
            Assert.AreEqual(2, s.active.Count);
            Assert.AreEqual(0, s.xp);
        }
    }

    public class WeeklyTests
    {
        [Test]
        public void WeekKey_FollowsIso8601()
        {
            Assert.AreEqual(202601, WeeklyEvents.WeekKey(new DateTime(2026, 1, 1)));   // Thursday
            Assert.AreEqual(202653, WeeklyEvents.WeekKey(new DateTime(2027, 1, 1)));   // Friday: still 2026's week 53
            Assert.AreEqual(202640, WeeklyEvents.WeekKey(new DateTime(2026, 10, 3)));  // Saturday
            Assert.AreEqual(202641, WeeklyEvents.WeekKey(new DateTime(2026, 10, 5)));  // Monday starts a new week
        }

        [Test]
        public void SameWeek_SameEvent_AndEveryEventComesUp()
        {
            Assert.AreSame(WeeklyEvents.For(202640), WeeklyEvents.For(202640));
            var seen = new HashSet<string>();
            for (int w = 1; w <= 12; w++) seen.Add(WeeklyEvents.For(202700 + w).Id);
            Assert.AreEqual(WeeklyEvents.Rotation.Length, seen.Count);
        }

        [Test]
        public void Goals_PayOnce_AndTheBonusWhenAllAreDone()
        {
            var ev = WeeklyEvents.Rotation[0]; // neon nights
            var s = new WeeklyState();
            s.EnsureWeek(202640);
            var u = s.Count(ev, WeeklyCounters.BestScore, 26000);
            Assert.AreEqual(1, u.NewGoals.Count);
            Assert.AreEqual(WeeklyEvents.GoalTokens, u.Tokens);
            Assert.IsFalse(s.Count(ev, WeeklyCounters.BestScore, 30000).Any, "paid once");
            s.Count(ev, WeeklyCounters.BestScore, 100);
            Assert.AreEqual(30000, s.Get(WeeklyCounters.BestScore), "best scores keep the max");
            for (int i = 0; i < 4; i++) s.Count(ev, WeeklyCounters.Tapes, 1);
            u = s.Count(ev, WeeklyCounters.Combos, 30);
            Assert.IsTrue(u.AllDone);
            Assert.AreEqual(WeeklyEvents.GoalTokens + WeeklyEvents.AllGoalsBonus, u.Tokens);
        }

        [Test]
        public void NewWeek_Resets()
        {
            var s = new WeeklyState();
            s.EnsureWeek(202640);
            s.Count(WeeklyEvents.Rotation[0], WeeklyCounters.Combos, 5);
            Assert.IsFalse(s.EnsureWeek(202640));
            Assert.IsTrue(s.EnsureWeek(202641));
            Assert.AreEqual(0, s.Get(WeeklyCounters.Combos));
        }

        [Test]
        public void RaceScores_AreHundredths()
        {
            Assert.AreEqual(1234, Leaderboards.RaceScore(12.34f));
            Assert.AreEqual("retrosk8.race.canal_cut", Leaderboards.Race("canal_cut"));
        }
    }
}
