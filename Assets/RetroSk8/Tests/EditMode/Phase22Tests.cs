using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    /// <summary>Phase 22: save backup codes, story chapter 8, the rink lesson, Late Skate week.</summary>
    public class SaveBackupTests
    {
        private const string Json = "{\"version\":22,\"tapeTokens\":1240,\"settings\":{\"playerName\":\"OMARI\"}}";

        [Test]
        public void PackThenUnpack_GivesTheSameSaveAndTime()
        {
            string code = SaveBackup.Pack(Json, 1790000000L);
            StringAssert.StartsWith("RS8B1.1790000000.", code);
            Assert.IsTrue(SaveBackup.TryUnpack(code, out string json, out long time, out string error), error);
            Assert.AreEqual(Json, json);
            Assert.AreEqual(1790000000L, time);
            Assert.IsTrue(SaveBackup.TryUnpack("  " + code + "\n", out _, out _, out _), "whitespace from a paste is fine");
        }

        [Test]
        public void AnyChange_IsCaught()
        {
            string code = SaveBackup.Pack(Json, 1L);
            var parts = code.Split('.');
            // A different save with the old checksum.
            var other = SaveBackup.Pack(Json.Replace("1240", "999999"), 1L).Split('.');
            string swapped = string.Join(".", parts[0], parts[1], parts[2], other[3]);
            Assert.IsFalse(SaveBackup.TryUnpack(swapped, out _, out _, out string error));
            Assert.AreEqual("DAMAGED SAVE CODE", error);
            // Truncated and garbled codes.
            int cut = parts[0].Length + parts[1].Length + parts[2].Length + 3 + (parts[3].Length / 2) / 4 * 4; // half the data, still valid base64
            Assert.IsFalse(SaveBackup.TryUnpack(code.Substring(0, cut), out _, out _, out _), "half a save");
            Assert.IsFalse(SaveBackup.TryUnpack(code.Replace('A', '!'), out _, out _, out _));
        }

        [Test]
        public void ForeignText_IsRefused()
        {
            foreach (var bad in new[] { null, "", "hello", "RS8B2.1.aa.bb", "RS8B1.x.0000.AAAA", "RSPK1-0000", "RS8B1.1." + new string('a', 64) + ".!!!!" })
                Assert.IsFalse(SaveBackup.TryUnpack(bad, out _, out _, out _), bad ?? "null");
        }

        [Test]
        public void HugeOrBombCodes_AreRefused()
        {
            string tooLong = "RS8B1.1." + new string('a', 64) + "." + new string('A', SaveBackup.MaxPackedChars);
            Assert.IsFalse(SaveBackup.TryUnpack(tooLong, out _, out _, out string e1));
            StringAssert.Contains("TOO BIG", e1);

            // 5 MB of one character compresses to a few KB: decompression must stop at the cap.
            string bomb = SaveBackup.Pack("{" + new string(' ', SaveBackup.MaxJsonBytes + 1024) + "}", 1L);
            Assert.Less(bomb.Length, SaveBackup.MaxPackedChars);
            Assert.IsFalse(SaveBackup.TryUnpack(bomb, out _, out _, out string e2));
            StringAssert.Contains("TOO BIG", e2);
        }

        [Test]
        public void Fuzz_NeverThrows()
        {
            var rng = new Random(22);
            string real = SaveBackup.Pack(Json, 5L);
            for (int i = 0; i < 2000; i++)
            {
                string s;
                if (i % 2 == 0)
                {
                    var sb = new StringBuilder();
                    int n = rng.Next(0, 200);
                    for (int k = 0; k < n; k++) sb.Append((char)rng.Next(32, 127));
                    s = (i % 4 == 0 ? "RS8B1." : "") + sb;
                }
                else
                {
                    var c = real.ToCharArray();
                    int pos = rng.Next(c.Length);
                    c[pos] = (char)rng.Next(32, 127);
                    s = new string(c, 0, rng.Next(1, c.Length + 1));
                }
                Assert.DoesNotThrow(() => SaveBackup.TryUnpack(s, out _, out _, out _));
            }
        }

        [Test]
        public void Ago_ReadsNaturally()
        {
            Assert.AreEqual("JUST NOW", SaveBackup.Ago(1000, 1030));
            Assert.AreEqual("JUST NOW", SaveBackup.Ago(2000, 1000), "clock skew");
            Assert.AreEqual("5 MIN AGO", SaveBackup.Ago(0, 300));
            Assert.AreEqual("1 HOUR AGO", SaveBackup.Ago(0, 3600));
            Assert.AreEqual("3 HOURS AGO", SaveBackup.Ago(0, 3 * 3600 + 5));
            Assert.AreEqual("2 DAYS AGO", SaveBackup.Ago(0, 2 * 86400));
        }

        [Test]
        public void AutoBackup_WaitsTenMinutes()
        {
            Assert.IsTrue(SaveBackup.AutoBackupDue(0, 1000000));
            Assert.IsFalse(SaveBackup.AutoBackupDue(1000, 1000 + 599));
            Assert.IsTrue(SaveBackup.AutoBackupDue(1000, 1000 + 600));
            Assert.IsTrue(SaveBackup.AutoBackupDue(5000, 1000), "a clock set back doesn't block backups forever");
        }
    }

    public class StoryChapterEightTests
    {
        private static StoryChapter Eight() => Story.Chapters.First(c => c.Id == "off_season");

        [Test]
        public void ChapterEight_IsAtTheRink_AndEndsInAGameOfSkate()
        {
            var c = Eight();
            Assert.AreEqual(8, c.Number);
            Assert.AreEqual(3, c.Steps.Length);
            Assert.IsTrue(c.Steps.All(s => s.LocationId == "offseason_rink"));
            Assert.AreEqual(StoryObjective.ScoreRun, c.Steps[0].Objective);
            Assert.AreEqual(StoryObjective.LineBattle, c.Steps[1].Objective);
            Assert.AreEqual(StoryObjective.Skate, c.Steps[2].Objective);
            Assert.Greater(c.Steps[1].Target, c.Steps[0].Target);
            Assert.AreEqual(2, c.Steps[2].RivalLevel, "the hardest bot");
            Assert.AreEqual(8, Story.Chapters.First(x => x.Id == "off_season").Number);
        }

        [Test]
        public void NewRivals_HaveTheirOwnLooks()
        {
            var a = StoryCast.LookFor("FROST", null);
            var b = StoryCast.LookFor("SLAPSHOT", null);
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreEqual((int)ShirtStyle.Jersey, a.shirtStyle, "the Rink Rats wear jerseys");
            Assert.AreNotEqual(a.hairStyle, b.hairStyle);
            foreach (var s in Eight().Steps)
                foreach (var p in s.Intro.Concat(s.Outro))
                    if (!string.IsNullOrEmpty(p.Speaker)) Assert.IsNotNull(StoryCast.LookFor(p.Speaker, new SkaterLook()), p.Speaker);
        }

        [Test]
        public void RinkRatsAchievement_CountsAndFits()
        {
            var a = Achievements.All.First(x => x.Id == "rink_rats");
            Assert.AreEqual(0f, a.Progress(new PlayerProgress()));
            Assert.AreEqual(1f, a.Progress(new PlayerProgress { OffSeason = true }));
            Assert.AreEqual(75, GameCenterSetup.PointsFor("rink_rats"));
            Assert.LessOrEqual(GameCenterSetup.TotalPoints(Achievements.All), GameCenterSetup.MaxPoints);
        }
    }

    public class RinkLessonAndWeekTests
    {
        [Test]
        public void BoardsLesson_IsAtTheRink_AndTeachesEveryGrind()
        {
            var l = TrickLessons.Find("boards");
            Assert.IsNotNull(l);
            Assert.AreEqual("offseason_rink", l.ParkId);
            foreach (var id in new[] { "grind_center_glide", "grind_plank_slide", "grind_crossbar", "grind_nose_needle" })
                Assert.AreSame(l, TrickLessons.For(id, false), id);
            Assert.AreEqual(1, TrickLessons.Matches(l, new[] { "ollie", "grind_crossbar", "grind_plank_slide" }, _ => false), "one line is one rep");
        }

        [Test]
        public void LateSkate_CountsGrindsAndRinkRuns()
        {
            var ev = WeeklyEvents.Rotation.First(e => e.Id == "rink_week");
            Assert.IsTrue(ev.Goals.Any(g => g.Counter == WeeklyCounters.Grinds));
            Assert.IsTrue(ev.Goals.Any(g => g.Counter == WeeklyCounters.RinkRuns));
            Assert.IsTrue(WeeklyCounters.IsGrind("grind_nose_needle"));
            Assert.IsTrue(WeeklyCounters.IsGrind("bluntslide"), "Phase 25: bluntslides count as grinds");
            Assert.IsFalse(WeeklyCounters.IsGrind("kickflip"));
            Assert.IsFalse(WeeklyCounters.IsGrind(null));
            var s = new WeeklyState();
            s.Count(ev, WeeklyCounters.RinkRuns, 3);
            Assert.AreEqual(1f, s.Progress(ev.Goals.First(g => g.Counter == WeeklyCounters.RinkRuns)));
        }
    }
}
