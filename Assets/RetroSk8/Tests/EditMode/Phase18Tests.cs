using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class TestFlightRulesTests
    {
        [Test]
        public void TeamId_IsTenCapitalLettersOrDigits()
        {
            Assert.IsTrue(TestFlightRules.IsValidTeamId("X6LZQ3FS36"));
            Assert.IsFalse(TestFlightRules.IsValidTeamId("x6lzq3fs36"), "lower case");
            Assert.IsFalse(TestFlightRules.IsValidTeamId("X6LZQ3FS3"), "9 characters");
            Assert.IsFalse(TestFlightRules.IsValidTeamId("X6LZQ3FS36A"), "11 characters");
            Assert.IsFalse(TestFlightRules.IsValidTeamId("X6LZQ-FS36"), "punctuation");
            Assert.IsFalse(TestFlightRules.IsValidTeamId(null));
            Assert.IsFalse(TestFlightRules.IsValidTeamId(""));
        }

        [Test]
        public void ExportOptions_UploadsToAppStoreConnect_WithTheTeam()
        {
            string plist = TestFlightRules.ExportOptions("X6LZQ3FS36", upload: true);
            StringAssert.Contains("<string>app-store-connect</string>", plist);
            StringAssert.Contains("<key>destination</key>\n\t<string>upload</string>", plist);
            StringAssert.Contains("<string>X6LZQ3FS36</string>", plist);
            StringAssert.Contains("<key>manageAppVersionAndBuildNumber</key>\n\t<false/>", plist);
            StringAssert.Contains("<string>export</string>", TestFlightRules.ExportOptions("X6LZQ3FS36", upload: false));
            StringAssert.Contains("&lt;", TestFlightRules.ExportOptions("<", true), "values are escaped");
        }

        [Test]
        public void ArchiveArgs_SignWithTheTeam_AndAllowProvisioning()
        {
            var args = TestFlightRules.ArchiveArgs("X6LZQ3FS36");
            Assert.GreaterOrEqual(Array.IndexOf(args, "DEVELOPMENT_TEAM=X6LZQ3FS36"), 0, "DEVELOPMENT_TEAM=X6LZQ3FS36");
            Assert.GreaterOrEqual(Array.IndexOf(args, "-allowProvisioningUpdates"), 0, "-allowProvisioningUpdates");
            Assert.GreaterOrEqual(Array.IndexOf(args, "generic/platform=iOS"), 0, "generic/platform=iOS");
            Assert.AreEqual("archive", args[args.Length - 1]);
            Assert.GreaterOrEqual(Array.IndexOf(TestFlightRules.ExportArgs(), "-exportArchive"), 0, "-exportArchive");
        }

        [Test]
        public void Join_QuotesArgumentsWithSpaces()
        {
            Assert.AreEqual("-a \"b c\" d \"\"", TestFlightRules.Join(new[] { "-a", "b c", "d", "" }));
        }

        [Test]
        public void ImportantLines_KeepErrorsAndResults_DropNoise()
        {
            var output = new[]
            {
                "CompileC /tmp/foo.o",
                "error: No profiles for 'com.omariibell.retrosk8' were found",
                "error: No profiles for 'com.omariibell.retrosk8' were found",
                "Signing Identity: Apple Development",
                "** ARCHIVE FAILED **",
            };
            var lines = TestFlightRules.ImportantLines(output);
            Assert.AreEqual(2, lines.Count, string.Join(" | ", lines));
            StringAssert.StartsWith("error:", lines[0]);
            Assert.AreEqual("** ARCHIVE FAILED **", lines[1]);
            Assert.AreEqual(0, TestFlightRules.ImportantLines(null).Count);
        }

        [Test]
        public void Advice_NamesTheNextStep_ForCommonFailures()
        {
            StringAssert.Contains("Accounts", TestFlightRules.Advice(new[] { "error: No Accounts: Add a new account in Accounts settings." }));
            StringAssert.Contains("New App", TestFlightRules.Advice(new[] { "Error: No suitable application records were found." }));
            StringAssert.Contains("build number", TestFlightRules.Advice(new[] { "The bundle version must be higher than the previously uploaded version." }));
            Assert.IsNull(TestFlightRules.Advice(new[] { "all good" }));
        }
    }

    public class GameCenterSetupTests
    {
        private static List<BoardInfo> Boards() => LeaderboardHub.Boards(new[] { ("harbor_plaza", "Harbor Plaza"), ("drive_in", "Twin Screen Drive-In") });

        [Test]
        public void Points_StayWithinGameCentersLimit()
        {
            int total = GameCenterSetup.TotalPoints(Achievements.All);
            Assert.Greater(total, 0);
            Assert.LessOrEqual(total, GameCenterSetup.MaxPoints);
            foreach (var a in Achievements.All) Assert.Greater(GameCenterSetup.PointsFor(a.Id), 0, a.Id);
        }

        [Test]
        public void Markdown_ListsEveryBoardAndAchievement_WithTheGamesIds()
        {
            var boards = Boards();
            string md = GameCenterSetup.Markdown(boards, Achievements.All);
            foreach (var b in boards) StringAssert.Contains("`" + b.Id + "`", md);
            foreach (var a in Achievements.All) StringAssert.Contains("`" + Achievements.GameCenterId(a) + "`", md);
            StringAssert.Contains("`retrosk8.score.drive_in`", md, "the new park gets a board");
        }

        [Test]
        public void RaceBoards_AreTimes_LowToHigh_AndTheWeeklyBoardRecurs()
        {
            foreach (var b in Boards())
            {
                if (b.Group == BoardGroup.Races)
                {
                    Assert.AreEqual("Low to High", GameCenterSetup.SortOrder(b), b.Id);
                    StringAssert.StartsWith("Elapsed Time", GameCenterSetup.ScoreFormat(b));
                }
                else Assert.AreEqual("High to Low", GameCenterSetup.SortOrder(b), b.Id);
                Assert.AreEqual(b.Id == WeeklyEvents.LeaderboardId, GameCenterSetup.IsRecurring(b), b.Id);
            }
        }
    }

    public class BonkRulesTests
    {
        [Test]
        public void InTheAir_HittingAnObject_IsABonk()
        {
            Assert.AreEqual(BonkMove.Bonk, BonkRules.Classify(true, false, 6f, false, 10f, 0f));
            Assert.AreEqual(BonkMove.Bonk, BonkRules.Classify(true, false, 6f, true, 10f, 0.3f), "posts can be bonked in the air too");
            Assert.AreEqual(BonkMove.None, BonkRules.Classify(true, false, 0.5f, false, 10f, 0f), "too slow");
        }

        [Test]
        public void RollingFastIntoAPost_IsAPoleJam_ButNotIntoOtherThings()
        {
            Assert.AreEqual(BonkMove.PoleJam, BonkRules.Classify(false, true, 6f, true, 10f, 0f));
            Assert.AreEqual(BonkMove.None, BonkRules.Classify(false, true, 3f, true, 10f, 0f), "too slow to ride up");
            Assert.AreEqual(BonkMove.None, BonkRules.Classify(false, true, 8f, false, 10f, 0f), "rolling into a cone isn't a trick");
            Assert.AreEqual(BonkMove.None, BonkRules.Classify(false, false, 8f, true, 10f, 0f), "grinding/stalling doesn't count");
        }

        [Test]
        public void TopsAndRepeats_DontCount()
        {
            Assert.AreEqual(BonkMove.None, BonkRules.Classify(true, false, 6f, false, 10f, 0.9f), "landing on top is landing");
            Assert.AreEqual(BonkMove.None, BonkRules.Classify(true, false, 6f, false, 0.2f, 0f), "same object straight away");
        }

        [Test]
        public void Lift_NeverSlowsYourRise()
        {
            Assert.AreEqual(BonkRules.BonkLift, BonkRules.LiftAfter(BonkMove.Bonk, -3f));
            Assert.AreEqual(9f, BonkRules.LiftAfter(BonkMove.Bonk, 9f));
            Assert.AreEqual(BonkRules.PoleJamLift, BonkRules.LiftAfter(BonkMove.PoleJam, 0f));
            Assert.AreEqual(-2f, BonkRules.LiftAfter(BonkMove.None, -2f));
        }

        [Test]
        public void Bonks_AreInTheTrickBook_AndHaveALesson()
        {
            var book = TrickCatalog.Build(new List<TrickInfo>(), null);
            Assert.IsTrue(book.Exists(t => t.Id == BonkRules.BonkId && t.Category == TrickCategory.Bonk));
            Assert.IsTrue(book.Exists(t => t.Id == BonkRules.PoleJamId && TrickCatalog.TabFor(t) == TrickBookTab.SpinsAndMore));
            Assert.AreEqual("bonks", TrickLessons.For(BonkRules.BonkId, false)?.Id);
            Assert.AreEqual("drive_in", TrickLessons.Find("bonks").ParkId);
            Assert.IsTrue(BonkRules.IsBonk("pole_jam") && BonkRules.IsBonk("bonk") && !BonkRules.IsBonk("kick"));
        }

        [Test]
        public void DriveIn_IsShareable_AtTheEndOfTheList()
        {
            Assert.AreEqual(7, Array.IndexOf(ShareCodes.BuiltInParks, "drive_in"), "appended, so old codes keep their parks");
            Assert.AreEqual(6, Array.IndexOf(ShareCodes.BuiltInParks, "moonlight_pier"));
        }
    }

    public class BonkHuntTests
    {
        private static int BonkDay()
        {
            for (int d = 9000; ; d++) if (CityJam.KindFor(d) == JamKind.BonkHunt) return d;
        }

        [Test]
        public void EveryThirdDay_IsABonkHunt()
        {
            int hunts = 0;
            for (int d = 0; d < 30; d++) if (CityJam.KindFor(d) == JamKind.BonkHunt) hunts++;
            Assert.AreEqual(10, hunts);
            Assert.AreEqual("BONK HUNT JAM", CityJam.Title(JamKind.BonkHunt));
            Assert.AreEqual(JamKind.Classic, CityJam.KindFor(42));
        }

        [Test]
        public void BonkHuntStops_NeedABonk_AndHaveLowerTargets()
        {
            int day = BonkDay();
            var stops = CityJam.Plan(day, RetroCityLayout.Spots);
            Assert.AreEqual(CityJam.StopCount, stops.Count);
            foreach (var s in stops)
            {
                Assert.IsTrue(s.NeedsBonk, s.Spot.Id);
                Assert.LessOrEqual(s.Target, Math.Max(500, (long)Math.Round(s.Spot.Bronze * CityJam.TargetShare / 100.0) * 100), s.Spot.Id);
            }
            foreach (var s in CityJam.Plan(day + 1, RetroCityLayout.Spots)) Assert.IsFalse(s.NeedsBonk, "classic day");
        }

        [Test]
        public void AtABonkHuntStop_OnlyLinesWithABonkCount()
        {
            var run = new JamRun(CityJam.Plan(BonkDay(), RetroCityLayout.Spots), JamKind.BonkHunt);
            Assert.AreEqual(JamKind.BonkHunt, run.Kind);
            run.UpdatePosition(true);
            long target = run.Stops[0].Target;
            Assert.IsFalse(run.AddBanked(target * 3, true, hadBonk: false), "no bonk, no count");
            Assert.AreEqual(0, run.StopScore);
            Assert.IsTrue(run.AddBanked(target, true, hadBonk: true));
            Assert.AreEqual(1, run.Cleared);
        }

        [Test]
        public void ClassicStops_IgnoreTheBonkFlag()
        {
            var run = new JamRun(CityJam.Plan(42, RetroCityLayout.Spots));
            run.UpdatePosition(true);
            Assert.IsTrue(run.AddBanked(run.Stops[0].Target, true));
        }
    }

    public class PerfStatsTests
    {
        [Test]
        public void SmoothSixty_ReadsSmooth()
        {
            var s = new PerfStats();
            for (int i = 0; i < 600; i++) s.Add(16.7f, -1);
            Assert.AreEqual(600, s.Frames);
            Assert.AreEqual(16.7f, s.AverageMs, 0.01f);
            Assert.AreEqual(60f, s.AverageFps, 0.5f);
            Assert.LessOrEqual(s.Percentile(0.95f), 17f);
            Assert.AreEqual(0, s.Hitches);
            Assert.IsFalse(s.GcMeasured);
            Assert.AreEqual("SMOOTH", s.Verdict());
            StringAssert.Contains("gc n/a", s.Summary("drive_in", 1.5f));
            StringAssert.Contains("load 1.5 s", s.Summary("drive_in", 1.5f));
        }

        [Test]
        public void HitchesAndSlowFrames_AreCalledOut()
        {
            var s = new PerfStats();
            for (int i = 0; i < 300; i++) s.Add(16.7f, 0);
            for (int i = 0; i < 5; i++) s.Add(80f, 0);
            Assert.AreEqual(5, s.Hitches);
            Assert.AreEqual(80f, s.WorstMs);
            Assert.AreEqual("HITCHY: occasional long frames", s.Verdict());

            var slow = new PerfStats();
            for (int i = 0; i < 300; i++) slow.Add(i % 4 == 0 ? 16.7f : 33.3f, 0);
            Assert.Greater(slow.Percentile(0.95f), 30f);
            StringAssert.StartsWith("SLOW", slow.Verdict());
        }

        [Test]
        public void Garbage_IsAveragedPerFrame_WhenMeasured()
        {
            var s = new PerfStats();
            for (int i = 0; i < 100; i++) s.Add(16.7f, i % 10 == 0 ? 20480 : 0);
            Assert.IsTrue(s.GcMeasured);
            Assert.AreEqual(2048f, s.GcBytesPerFrame, 0.01f);
            Assert.AreEqual(10, s.GcFrames);
            Assert.AreEqual("GARBAGE: allocating every frame", s.Verdict());
            s.Reset();
            Assert.AreEqual(0, s.Frames);
            Assert.AreEqual("TOO SHORT TO JUDGE", s.Verdict());
        }

        [Test]
        public void BadSamples_AreIgnored()
        {
            var s = new PerfStats();
            s.Add(0f, 0);
            s.Add(-5f, 0);
            s.Add(float.NaN, 0);
            Assert.AreEqual(0, s.Frames);
            Assert.AreEqual(0f, s.Percentile(0.5f));
        }
    }
}
