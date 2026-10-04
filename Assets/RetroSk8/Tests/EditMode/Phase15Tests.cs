using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class StreakTests
    {
        [Test]
        public void FirstCheckIn_IsDayOne_AndPays()
        {
            var s = new StreakState();
            var r = Streaks.CheckIn(s, 9000);
            Assert.IsTrue(r.NewDay);
            Assert.AreEqual(1, r.Day);
            Assert.AreEqual(Streaks.Card[0], r.Tokens);
            Assert.IsFalse(r.Reset, "a first ever check-in isn't a reset");
        }

        [Test]
        public void SameDay_CountsOnce()
        {
            var s = new StreakState();
            Streaks.CheckIn(s, 9000);
            var again = Streaks.CheckIn(s, 9000);
            Assert.IsFalse(again.NewDay);
            Assert.AreEqual(0, again.Tokens);
            Assert.AreEqual(1, s.current);
        }

        [Test]
        public void DaysInARow_Climb_AndDaySevenPaysBig_AndEarnsASaver()
        {
            var s = new StreakState();
            StreakResult r = null;
            for (int d = 0; d < 7; d++) r = Streaks.CheckIn(s, 9000 + d);
            Assert.AreEqual(7, r.Day);
            Assert.AreEqual(50, r.Tokens);
            Assert.IsTrue(r.EarnedSaver);
            Assert.AreEqual(1, s.savers);
            Assert.AreEqual(7, s.best);
            Assert.AreEqual(Streaks.Card[0], Streaks.CheckIn(s, 9007).Tokens, "the card loops");
        }

        [Test]
        public void MissedDay_ResetsWithoutASaver()
        {
            var s = new StreakState();
            Streaks.CheckIn(s, 9000);
            Streaks.CheckIn(s, 9001);
            var r = Streaks.CheckIn(s, 9003);
            Assert.IsTrue(r.Reset);
            Assert.AreEqual(1, r.Day);
            Assert.AreEqual(2, s.best);
        }

        [Test]
        public void Savers_CoverMissedDays_UpToWhatYouHave()
        {
            var s = new StreakState { lastDay = 9000, current = 10, best = 10, savers = 2 };
            var r = Streaks.CheckIn(s, 9003); // missed 2 days
            Assert.IsFalse(r.Reset);
            Assert.AreEqual(2, r.SaversUsed);
            Assert.AreEqual(11, r.Day);
            Assert.AreEqual(0, s.savers);

            var t = new StreakState { lastDay = 9000, current = 10, best = 10, savers = 1 };
            Assert.IsTrue(Streaks.CheckIn(t, 9003).Reset, "one saver can't cover two days");
            Assert.AreEqual(1, t.savers, "a reset doesn't spend savers");
        }

        [Test]
        public void SaversCapAtTwo_AndMilestonesPayExtra()
        {
            var s = new StreakState();
            StreakResult r = null;
            for (int d = 0; d < 30; d++) r = Streaks.CheckIn(s, 100 + d);
            Assert.AreEqual(Streaks.MaxSavers, s.savers);
            Assert.IsTrue(r.Milestone);
            Assert.AreEqual(Streaks.Card[(30 - 1) % 7] + Streaks.MilestoneBonus, r.Tokens);
        }

        [Test]
        public void ClockGoingBackwards_NeverPunishes()
        {
            var s = new StreakState();
            Streaks.CheckIn(s, 9000);
            Streaks.CheckIn(s, 9001);
            var r = Streaks.CheckIn(s, 8990);
            Assert.IsFalse(r.NewDay);
            Assert.AreEqual(2, s.current);
            Assert.AreEqual(9001, s.lastDay);
        }

        [Test]
        public void DayNumber_IsLocalCalendarDays()
        {
            Assert.AreEqual(1, Streaks.DayNumber(new DateTime(2000, 1, 2, 23, 59, 0)) - Streaks.DayNumber(new DateTime(2000, 1, 1, 0, 1, 0)));
            Assert.AreEqual(0, Streaks.DayNumber(new DateTime(2026, 10, 4, 0, 0, 1)) - Streaks.DayNumber(new DateTime(2026, 10, 4, 23, 59, 59)));
            Assert.AreEqual(6, Streaks.CardSlot(7));
            Assert.AreEqual(0, Streaks.CardSlot(8));
        }

        [Test]
        public void Sanitize_FixesBadNumbers()
        {
            var s = new StreakState { current = -3, best = 1, savers = 9 };
            s.Sanitize();
            Assert.AreEqual(0, s.current);
            Assert.AreEqual(Streaks.MaxSavers, s.savers);
        }
    }

    public class NotificationPlanTests
    {
        // Wednesday 2026-09-30, 4 pm.
        private static readonly DateTime Wed = new DateTime(2026, 9, 30, 16, 0, 0);

        private static StreakState CheckedIn(DateTime now, int current) =>
            new StreakState { lastDay = Streaks.DayNumber(now), current = current, best = current };

        [Test]
        public void StreakReminder_TomorrowEvening_WhenThereIsAStreak()
        {
            var plan = NotificationPlan.Build(Wed, CheckedIn(Wed, 4), 0, "X");
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(NotificationPlan.StreakId, plan[0].Id);
            Assert.AreEqual(new DateTime(2026, 10, 1, 18, 0, 0), plan[0].FireAt);
            StringAssert.Contains("DAY 5", plan[0].Title);
            StringAssert.Contains("4-day", plan[0].Body);
        }

        [Test]
        public void NoStreakYet_GetsTheDailyLineNudge()
        {
            var plan = NotificationPlan.Build(Wed, CheckedIn(Wed, 1), 0, "X");
            Assert.AreEqual(NotificationPlan.DailyId, plan[0].Id);
            Assert.AreEqual(new DateTime(2026, 10, 1, 10, 0, 0), plan[0].FireAt);
            Assert.AreEqual(NotificationPlan.DailyId, NotificationPlan.Build(Wed, null, 0, null)[0].Id);
        }

        [Test]
        public void WeeklyReminder_SundayEvening_OnlyWithGoalsLeft()
        {
            var plan = NotificationPlan.Build(Wed, CheckedIn(Wed, 4), 2, "Gap Week");
            var weekly = plan.Find(n => n.Id == NotificationPlan.WeeklyId);
            Assert.IsNotNull(weekly);
            Assert.AreEqual(new DateTime(2026, 10, 4, 18, 0, 0), weekly.FireAt);
            StringAssert.Contains("Gap Week", weekly.Body);
            StringAssert.Contains("2 goals", weekly.Body);
            Assert.IsNull(NotificationPlan.Build(Wed, CheckedIn(Wed, 4), 0, "Gap Week").Find(n => n.Id == NotificationPlan.WeeklyId));
        }

        [Test]
        public void OnePerDay_WeeklyWinsItsDay()
        {
            // Saturday: tomorrow's streak reminder and the weekly one both land on Sunday 6 pm.
            var sat = new DateTime(2026, 10, 3, 12, 0, 0);
            var plan = NotificationPlan.Build(sat, CheckedIn(sat, 3), 1, "X");
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(NotificationPlan.WeeklyId, plan[0].Id);
        }

        [Test]
        public void SundayNight_SkipsAWeeklyReminderThatAlreadyPassed()
        {
            var sun = new DateTime(2026, 10, 4, 19, 30, 0);
            var plan = NotificationPlan.Build(sun, CheckedIn(sun, 3), 1, "X");
            Assert.IsNull(plan.Find(n => n.Id == NotificationPlan.WeeklyId));
            foreach (var n in plan) Assert.IsTrue(n.FireAt > sun, n.Id);
        }

        [Test]
        public void QuietHours_PushToMorning()
        {
            Assert.IsTrue(NotificationPlan.InQuietHours(new DateTime(2026, 1, 1, 22, 0, 0)));
            Assert.IsTrue(NotificationPlan.InQuietHours(new DateTime(2026, 1, 1, 3, 0, 0)));
            Assert.IsFalse(NotificationPlan.InQuietHours(new DateTime(2026, 1, 1, 12, 0, 0)));
            Assert.AreEqual(new DateTime(2026, 1, 2, 9, 0, 0), NotificationPlan.OutOfQuietHours(new DateTime(2026, 1, 1, 22, 0, 0)));
            Assert.AreEqual(new DateTime(2026, 1, 1, 9, 0, 0), NotificationPlan.OutOfQuietHours(new DateTime(2026, 1, 1, 3, 0, 0)));
            foreach (var n in NotificationPlan.Build(Wed, CheckedIn(Wed, 9), 3, "X"))
                Assert.IsFalse(NotificationPlan.InQuietHours(n.FireAt), n.Id);
        }
    }

    public class CityJamTests
    {
        [Test]
        public void Plan_IsFourDifferentSpots_SameForEveryoneToday()
        {
            var a = CityJam.Plan(9500, RetroCityLayout.Spots);
            var b = CityJam.Plan(9500, RetroCityLayout.Spots);
            Assert.AreEqual(CityJam.StopCount, a.Count);
            var ids = new HashSet<string>();
            for (int i = 0; i < a.Count; i++)
            {
                Assert.IsTrue(ids.Add(a[i].Spot.Id), "repeat stop");
                Assert.AreEqual(a[i].Spot.Id, b[i].Spot.Id);
                Assert.AreEqual(0, a[i].Target % 100);
                Assert.IsTrue(a[i].Target >= 500 && a[i].Target <= a[i].Spot.Bronze, a[i].Spot.Id);
            }
            bool differs = false;
            for (int d = 9501; d < 9510 && !differs; d++)
                differs = CityJam.Plan(d, RetroCityLayout.Spots)[0].Spot.Id != a[0].Spot.Id || CityJam.Plan(d, RetroCityLayout.Spots)[1].Spot.Id != a[1].Spot.Id;
            Assert.IsTrue(differs, "routes change day to day");
        }

        private static JamRun Run() => new JamRun(CityJam.Plan(42, RetroCityLayout.Spots));

        [Test]
        public void ClearingEveryStop_IsGold()
        {
            var run = Run();
            foreach (var stop in run.Stops)
            {
                Assert.AreEqual(JamPhase.Travel, run.Phase);
                Assert.IsFalse(run.AddBanked(999999, true), "points while travelling don't count");
                Assert.IsTrue(run.UpdatePosition(true));
                run.Tick(5f);
                Assert.IsFalse(run.AddBanked(stop.Target - 1, true));
                Assert.IsTrue(run.AddBanked(1, true));
            }
            Assert.IsTrue(run.Finished);
            Assert.AreEqual(4, run.Cleared);
            Assert.AreEqual(Medal.Gold, run.Result);
            Assert.AreEqual("", run.EndReason);
        }

        [Test]
        public void SessionClock_EndsTheJam()
        {
            var run = Run();
            run.UpdatePosition(true);
            run.AddBanked(run.Stops[0].Target, true);
            run.UpdatePosition(true);
            run.AddBanked(run.Stops[1].Target, true);
            run.UpdatePosition(true);
            run.Tick(CityJam.SessionSeconds + 0.1f);
            Assert.IsTrue(run.Finished);
            Assert.AreEqual("SESSION OVER", run.EndReason);
            Assert.AreEqual(Medal.Bronze, run.Result);
        }

        [Test]
        public void OverallClock_LeavingAndOutside_AllCount()
        {
            var a = Run();
            a.Tick(CityJam.TotalSeconds + 1f);
            Assert.AreEqual("OUT OF TIME", a.EndReason);
            Assert.AreEqual(Medal.None, a.Result);

            var b = Run();
            b.UpdatePosition(true);
            Assert.IsFalse(b.AddBanked(b.Stops[0].Target, false), "lines banked outside the spot don't count");
            b.LeftStop();
            Assert.AreEqual("LEFT THE SPOT", b.EndReason);

            var c = Run();
            c.LeftStop(); // still travelling: nothing happens
            Assert.IsFalse(c.Finished);
            c.Abandon();
            Assert.IsTrue(c.Finished);
        }

        [Test]
        public void Rewards_PayOnlyTheImprovement_EachDay()
        {
            var rec = new JamRecord();
            Assert.AreEqual(20, CityJam.Record(rec, 100, Medal.Bronze, 5000));
            Assert.AreEqual(0, CityJam.Record(rec, 100, Medal.Bronze, 6000));
            Assert.AreEqual(60, CityJam.Record(rec, 100, Medal.Gold, 9000), "bronze to gold pays the difference");
            Assert.AreEqual(1, rec.golds);
            Assert.AreEqual(40, CityJam.Record(rec, 101, Medal.Silver, 100), "a new day starts fresh");
            Assert.AreEqual(9000, rec.bestTotal);
            Assert.AreEqual(4, rec.jamsPlayed);
            Assert.AreEqual(0, CityJam.Record(rec, 101, Medal.None, 0));
        }
    }

    public class MoonlightPierTests
    {
        [Test]
        public void Pier_IsAppendedToShareCodes()
        {
            Assert.AreEqual(6, Array.IndexOf(ShareCodes.BuiltInParks, "moonlight_pier"), "appended, so old codes keep their parks");
            Assert.AreEqual(5, Array.IndexOf(ShareCodes.BuiltInParks, "floodgate_ditch"));
            var code = ShareCodes.EncodeChallenge(new ScoreChallenge { LocationId = "moonlight_pier", Target = 15000, From = "OMARI" });
            Assert.IsTrue(ShareCodes.TryDecodeChallenge(code, out var c, out _));
            Assert.AreEqual("moonlight_pier", c.LocationId);
        }

        [Test]
        public void PierSong_RendersASeamlessLoop()
        {
            var data = MusicComposer.Render(MusicComposer.Pier, 8000);
            Assert.AreEqual(MusicComposer.LoopSamples(MusicComposer.Pier, 8000), data.Length);
            float peak = 0f;
            foreach (var v in data) peak = Math.Max(peak, Math.Abs(v));
            Assert.IsTrue(peak > 0.05f && peak <= 1f, "audible and not clipping: " + peak);
        }
    }
}
