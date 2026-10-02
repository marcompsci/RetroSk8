using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class CareerTests
    {
        private sealed class Facts : ICareerFacts
        {
            public readonly Dictionary<string, long> Scores = new Dictionary<string, long>();
            public readonly Dictionary<string, long> Combos = new Dictionary<string, long>();
            public readonly HashSet<string> Gaps = new HashSet<string>();
            public readonly Dictionary<string, int> Stars = new Dictionary<string, int>();
            public CityProgress City { get; } = new CityProgress();
            public int MaxCustomPieces { get; set; }
            public long BestCustomScore { get; set; }
            public int DailyClears { get; set; }
            public long BestScoreAnywhere { get; set; }
            public long BestScore(string id) => Scores.TryGetValue(id ?? "", out var v) ? v : 0;
            public long BestCombo(string id) => Combos.TryGetValue(id ?? "", out var v) ? v : 0;
            public bool HasGap(string id) => Gaps.Contains(id);
            public int ContractStars(string id) => Stars.TryGetValue(id ?? "", out var v) ? v : 0;
        }

        private static void Finish(Facts f, CareerChapter c)
        {
            foreach (var g in c.Goals)
            {
                switch (g.Kind)
                {
                    case CareerGoalKind.ParkScore: f.Scores[g.Target] = g.Amount; break;
                    case CareerGoalKind.ParkCombo: f.Combos[g.Target] = g.Amount; break;
                    case CareerGoalKind.Gap: f.Gaps.Add(g.Target); break;
                    case CareerGoalKind.ContractStars: f.Stars[g.Target] = (int)g.Amount; break;
                    case CareerGoalKind.CitySpots: for (int i = 0; i < g.Amount; i++) f.City.FindSpot("s" + i); break;
                    case CareerGoalKind.CityTapes: for (int i = 0; i < g.Amount; i++) f.City.CollectTape("t" + i); break;
                    case CareerGoalKind.CityChallengeMedals: for (int i = 0; i < g.Amount; i++) f.City.RecordChallenge("spot" + i, 1, g.Medal); break;
                    case CareerGoalKind.RaceMedal: f.City.RecordRace(g.Target, 10f, g.Medal); break;
                    case CareerGoalKind.BuildPieces: f.MaxCustomPieces = (int)g.Amount; break;
                    case CareerGoalKind.CustomParkScore: f.BestCustomScore = g.Amount; break;
                    case CareerGoalKind.DailyClears: f.DailyClears = (int)g.Amount; break;
                    case CareerGoalKind.AnyScore: f.BestScoreAnywhere = g.Amount; break;
                }
            }
        }

        [Test]
        public void Chapters_AreNumberedInOrder_WithThreeGoalsEach_AndUniqueIds()
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < Career.Chapters.Length; i++)
            {
                var c = Career.Chapters[i];
                Assert.AreEqual(i + 1, c.Number);
                Assert.AreEqual(3, c.Goals.Length, c.Id);
                Assert.Greater(c.RewardTokens, 0);
                foreach (var g in c.Goals) Assert.IsTrue(ids.Add(g.Id), "duplicate goal id " + g.Id);
            }
            foreach (var r in new[] { "downtown_dash", "canal_cut", "ring_road" }) Assert.IsNotNull(RetroCityLayout.FindRace(r));
        }

        [Test]
        public void OnlyChapterOne_IsOpenAtStart()
        {
            var f = new Facts();
            Assert.IsTrue(Career.IsUnlocked(Career.Chapters[0], f));
            Assert.IsFalse(Career.IsUnlocked(Career.Chapters[1], f));
            Assert.AreEqual(1, Career.Current(f).Number);
        }

        [Test]
        public void Refresh_PaysEachChapterOnce_InOrder()
        {
            var f = new Facts();
            var state = new CareerState();
            Finish(f, Career.Chapters[1]); // chapter 2's goals done early: not announced while it's locked
            var u = state.Refresh(f);
            Assert.IsFalse(u.Any);

            Finish(f, Career.Chapters[0]);
            u = state.Refresh(f);
            Assert.AreEqual(2, u.NewChapters.Count, "finishing 1 also completes the already-done chapter 2");
            Assert.AreEqual(Career.Chapters[0].RewardTokens + Career.Chapters[1].RewardTokens, u.Tokens);
            Assert.AreEqual(Career.Chapters[1].RewardTitle, state.title);
            Assert.AreEqual(6, u.NewGoals.Count);

            u = state.Refresh(f);
            Assert.IsFalse(u.Any, "nothing is paid twice");
            Assert.AreEqual(3, Career.Current(f).Number);
        }

        [Test]
        public void AllChapters_CanBeFinished()
        {
            var f = new Facts();
            var state = new CareerState();
            int tokens = 0;
            foreach (var c in Career.Chapters) { Finish(f, c); tokens += c.RewardTokens; }
            var u = state.Refresh(f);
            Assert.AreEqual(Career.Chapters.Length, u.NewChapters.Count);
            Assert.AreEqual(tokens, u.Tokens);
            Assert.AreEqual(Career.Chapters.Length, state.ChaptersCompleted());
            foreach (var s in Stickers.All) Assert.IsTrue(state.StickerUnlocked(s));
        }

        [Test]
        public void GoalProgress_IsPartial_ThenComplete()
        {
            var f = new Facts();
            var goal = Career.Chapters[0].Goals[0];
            f.Scores[goal.Target] = goal.Amount / 2;
            Assert.AreEqual(0.5f, Career.Progress(goal, f), 0.01f);
            f.Scores[goal.Target] = goal.Amount * 3;
            Assert.AreEqual(1f, Career.Progress(goal, f));
        }

        [Test]
        public void CareerStickers_StayLocked_UntilTheirChapter()
        {
            var state = new CareerState();
            Sticker locked = null;
            foreach (var s in Stickers.All) if (s.UnlockChapter > 0) { locked = s; break; }
            Assert.IsNotNull(locked);
            Assert.IsFalse(state.StickerUnlocked(locked));
            for (int i = 0; i < locked.UnlockChapter; i++) state.paidChapters.Add(Career.Chapters[i].Id);
            Assert.IsTrue(state.StickerUnlocked(locked));
        }
    }

    public class SkaterLookTests
    {
        [Test]
        public void Sanitize_WrapsEveryChoiceIntoRange()
        {
            var look = new SkaterLook { skinTone = 99, hairStyle = -1, hairColor = 1000, build = 7, eyewear = -4, shoeColor = 55, board = null };
            look.Sanitize();
            Assert.Less(look.skinTone, LookPalette.SkinTones.Length);
            Assert.GreaterOrEqual(look.hairStyle, 0);
            Assert.Less(look.hairColor, LookPalette.HairColors.Length);
            Assert.Less(look.build, 3);
            Assert.GreaterOrEqual(look.eyewear, 0);
            Assert.IsNotNull(look.board);
        }

        [Test]
        public void Stickers_Are8x8_AndIdsMatchTheirIndex()
        {
            for (int i = 0; i < Stickers.All.Length; i++)
            {
                var s = Stickers.All[i];
                Assert.AreEqual(i, s.Id);
                Assert.AreEqual(8, s.Rows.Length, s.Name);
                foreach (var row in s.Rows) Assert.AreEqual(8, row.Length, s.Name);
            }
        }

        [Test]
        public void BoardPainter_PutsTheStickerAtTheChosenSpot()
        {
            var art = new BoardArt { pattern = (int)BoardPattern.Solid, sticker = 2, stickerSpot = 0 }; // heart on the nose
            int inkNose = 0, inkTail = 0;
            for (int y = 0; y < BoardPainter.Height; y++)
            for (int x = 0; x < BoardPainter.Width; x++)
            {
                if (BoardPainter.Pixel(art, x, y) != BoardPainter.StickerInk) continue;
                if (y >= BoardPainter.StickerBase[0]) inkNose++;
                if (y < BoardPainter.StickerBase[1]) inkTail++;
            }
            Assert.Greater(inkNose, 10);
            Assert.AreEqual(0, inkTail);
        }

        [Test]
        public void EveryPattern_UsesBothColours_ExceptSolid()
        {
            for (int p = 0; p < LookPalette.PatternNames.Length; p++)
            {
                int secondary = 0;
                for (int y = 0; y < BoardPainter.Height; y++)
                for (int x = 0; x < BoardPainter.Width; x++)
                    if (BoardPainter.Pattern((BoardPattern)p, x, y)) secondary++;
                if (p == (int)BoardPattern.Solid) Assert.AreEqual(0, secondary);
                else
                {
                    Assert.Greater(secondary, 0, LookPalette.PatternNames[p]);
                    Assert.Less(secondary, BoardPainter.Width * BoardPainter.Height, LookPalette.PatternNames[p]);
                }
            }
        }
    }
}
