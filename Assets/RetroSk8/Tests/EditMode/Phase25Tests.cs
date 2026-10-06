using System;
using System.Linq;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    /// <summary>Phase 25: photo looks, story chapter 9, the achievements pass.</summary>
    public class PhotoFxTests
    {
        private static byte[] Grey(int w, int h, byte v = 128)
        {
            var p = new byte[w * h * 4];
            for (int i = 0; i < p.Length; i += 4) { p[i] = v; p[i + 1] = (byte)(v / 2); p[i + 2] = (byte)(v / 3); p[i + 3] = 255; }
            return p;
        }

        [Test]
        public void BlackWhite_MakesEveryPixelGrey()
        {
            var p = Grey(40, 20);
            PhotoFx.Filter(p, 40, 20, PhotoFilter.BlackWhite, 1);
            for (int i = 0; i < p.Length; i += 4) { Assert.AreEqual(p[i], p[i + 1]); Assert.AreEqual(p[i + 1], p[i + 2]); }
        }

        [Test]
        public void EveryLook_RunsOnOddSizes_AndKeepsAlphaOpaque()
        {
            foreach (PhotoFilter f in Enum.GetValues(typeof(PhotoFilter)))
                foreach (PhotoFrame fr in Enum.GetValues(typeof(PhotoFrame)))
                    foreach (PhotoStickers st in Enum.GetValues(typeof(PhotoStickers)))
                        foreach (var (w, h) in new[] { (1, 1), (3, 7), (97, 45) })
                        {
                            var p = Grey(w, h);
                            Assert.DoesNotThrow(() => PhotoFx.Apply(p, w, h, f, fr, st, 7), $"{f} {fr} {st} {w}x{h}");
                            for (int i = 3; i < p.Length; i += 4) Assert.AreEqual(255, p[i]);
                        }
        }

        [Test]
        public void SameSeed_SameResult()
        {
            var a = Grey(64, 32);
            var b = Grey(64, 32);
            PhotoFx.Apply(a, 64, 32, PhotoFilter.FilmGrain, PhotoFrame.Comic, PhotoStickers.Stars, 42);
            PhotoFx.Apply(b, 64, 32, PhotoFilter.FilmGrain, PhotoFrame.Comic, PhotoStickers.Stars, 42);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void TapeFrame_PaintsTheTopAndBottomBands()
        {
            var p = Grey(140, 140, 10);
            PhotoFx.Frame(p, 140, 140, PhotoFrame.Tape);
            Assert.AreEqual(242, p[0], "bottom-left is tape yellow");
            int top = ((139 * 140) + 70) * 4;
            Assert.AreEqual(242, p[top]);
            int mid = ((70 * 140) + 70) * 4;
            Assert.AreEqual(10, p[mid], "the middle is untouched");
        }

        [Test]
        public void BadInput_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => PhotoFx.Apply(new byte[10], 4, 4, PhotoFilter.None, PhotoFrame.None, PhotoStickers.None, 0));
            Assert.Throws<ArgumentException>(() => PhotoFx.Apply(null, 4, 4, PhotoFilter.None, PhotoFrame.None, PhotoStickers.None, 0));
        }

        [Test]
        public void Pickers_Cycle()
        {
            Assert.AreEqual(PhotoFilter.Vhs, PhotoFx.Next(PhotoFilter.None));
            Assert.AreEqual(PhotoFilter.None, PhotoFx.Next(PhotoFilter.BlackWhite));
            Assert.AreEqual("INSTANT", PhotoFx.FrameName(PhotoFrame.Polaroid));
        }
    }

    public class StoryChapterNineTests
    {
        [Test]
        public void ChapterNine_IsAtTheShipyard_WithABonkBattle()
        {
            var c = Story.Chapters.First(x => x.Id == "dry_dock");
            Assert.AreEqual(9, c.Number);
            Assert.IsTrue(c.Steps.All(s => s.LocationId == "shipyard"));
            Assert.Greater(c.Steps[1].Bonks, 0);
            Assert.AreEqual(StoryObjective.Skate, c.Steps[2].Objective);
            Assert.AreEqual("Deckhand Jacket", c.Steps[2].RewardItem);
            Assert.IsNotNull(StoryCast.LookFor("RIVET", null));
            Assert.IsNotNull(StoryCast.LookFor("ANCHOR", null));
            foreach (var s in c.Steps)
                foreach (var p in s.Intro.Concat(s.Outro))
                    if (!string.IsNullOrEmpty(p.Speaker)) Assert.IsNotNull(StoryCast.LookFor(p.Speaker, new SkaterLook()), p.Speaker);
        }
    }

    public class AchievementsPassTests
    {
        [Test]
        public void NewAchievements_HavePoints_AndFitTheLimit()
        {
            foreach (var id in new[] { "dry_dock", "daily_driver", "called_it", "bonk_collector", "mind_the_gap" })
            {
                Assert.IsNotNull(Achievements.All.FirstOrDefault(a => a.Id == id), id);
                Assert.Greater(GameCenterSetup.PointsFor(id), 0, id);
            }
            Assert.AreEqual(870, GameCenterSetup.TotalPoints(Achievements.All));
            Assert.LessOrEqual(GameCenterSetup.TotalPoints(Achievements.All), GameCenterSetup.MaxPoints);
        }

        [Test]
        public void Progress_ReadsTheNewStats()
        {
            float P(string id, PlayerProgress pr) => Achievements.All.First(a => a.Id == id).Progress(pr);
            Assert.AreEqual(0.5f, P("bonk_collector", new PlayerProgress { TotalBonks = 50 }), 1e-4f);
            Assert.AreEqual(1f, P("daily_driver", new PlayerProgress { DailyTrickBestStreak = 9 }));
            Assert.AreEqual(1f, P("called_it", new PlayerProgress { TrickBattlesFinished = 1 }));
            Assert.AreEqual(1f, P("mind_the_gap", new PlayerProgress { ClearedContainerCanyon = true }));
            Assert.AreEqual(0f, P("dry_dock", new PlayerProgress()));
        }

        [Test]
        public void LongestStreak_FindsTheBestRun()
        {
            var s = new DailyTrickState();
            s.doneDays.AddRange(new[] { 1, 2, 3, 10, 11, 12, 13, 14, 20, 14 });
            Assert.AreEqual(5, DailyTricks.LongestStreak(s));
            Assert.AreEqual(0, DailyTricks.LongestStreak(new DailyTrickState()));
            Assert.AreEqual(0, DailyTricks.LongestStreak(null));
        }

        [Test]
        public void Bluntslides_CountAsGrinds()
        {
            var c = new DailyTrickChallenge { Kind = DailyTrickKind.GrindChain, Count = 2 };
            Assert.AreEqual(1, DailyTricks.Matches(c, new[] { "bluntslide", "grind_crossbar" }, 100));
        }
    }
}
