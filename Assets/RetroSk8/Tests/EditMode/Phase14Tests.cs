using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class StoryCastTests
    {
        [Test]
        public void EverySpeakingCharacter_HasALook()
        {
            foreach (var chapter in Story.Chapters)
            foreach (var step in chapter.Steps)
            foreach (var panels in new[] { step.Intro, step.Outro })
            {
                if (panels == null) continue;
                foreach (var p in panels)
                {
                    if (p.Mood == StoryMood.Narration) continue;
                    Assert.IsNotNull(StoryCast.LookFor(p.Speaker, new SkaterLook()), $"no look for {p.Speaker} in {step.Id}");
                }
            }
        }

        [Test]
        public void Narration_AndStrangers_ShowNobody()
        {
            Assert.IsNull(StoryCast.LookFor("", new SkaterLook()));
            Assert.IsNull(StoryCast.LookFor(null, new SkaterLook()));
            Assert.IsNull(StoryCast.LookFor("SOMEONE NEW", new SkaterLook()));
        }

        [Test]
        public void You_IsACopyOfThePlayer()
        {
            var me = new SkaterLook { skinTone = 3, shirtStyle = (int)ShirtStyle.Hoodie };
            var shown = StoryCast.LookFor("you", me);
            Assert.AreEqual(3, shown.skinTone);
            Assert.AreEqual((int)ShirtStyle.Hoodie, shown.shirtStyle);
            shown.skinTone = 1;
            Assert.AreEqual(3, me.skinTone, "the portrait must not edit the save");
        }

        [Test]
        public void CrewLooks_AreStable()
        {
            foreach (var m in CrewRoster.Members)
            {
                var a = StoryCast.LookFor(StoryCast.FirstName(m.Name), null);
                var b = StoryCast.LookFor(StoryCast.FirstName(m.Name), null);
                Assert.IsNotNull(a, m.Name);
                Assert.AreEqual(a.shirtColor, b.shirtColor);
                Assert.AreEqual(m.SkinTone, a.skinTone);
            }
        }
    }

    public class TrickBookTests
    {
        private static List<TrickInfo> Library() => new List<TrickInfo>
        {
            new TrickInfo { Id = "kick", Name = "Kick", Category = TrickCategory.BoardFlip, Family = TrickFamily.Flip, Variation = 0, Points = 400 },
            new TrickInfo { Id = "heel", Name = "Heel", Category = TrickCategory.BoardFlip, Family = TrickFamily.Flip, Variation = 1, Points = 400 },
            new TrickInfo { Id = "melon", Name = "Melon", Category = TrickCategory.Grab, Family = TrickFamily.Grab, Variation = 2, Points = 300 },
            new TrickInfo { Id = "shuv", Name = "Shuv", Category = TrickCategory.BoardFlip, Family = TrickFamily.Shove, Variation = 3, Points = 300 },
            new TrickInfo { Id = "sp_flip", Name = "Flip Special", Category = TrickCategory.Special, Family = TrickFamily.Flip, IsSpecial = true },
            new TrickInfo { Id = "sp_grab", Name = "Grab Special", Category = TrickCategory.Special, Family = TrickFamily.Grab, IsSpecial = true },
            new TrickInfo { Id = "fifty", Name = "Fifty", Category = TrickCategory.Grind, Variation = 0 },
            new TrickInfo { Id = "manual", Name = "Manual", Category = TrickCategory.Manual },
        };

        [Test]
        public void Catalog_SortsTricksIntoTabs_AndSkipsTheUnreachableFlipSpecial()
        {
            var book = TrickCatalog.Build(Library(), new[] { "Half", "Full" });
            TrickInfo Get(string id) => book.Find(t => t.Id == id);
            Assert.AreEqual(TrickBookTab.Flips, Get("kick").Tab);
            Assert.AreEqual(TrickBookTab.Grabs, Get("melon").Tab);
            Assert.AreEqual(TrickBookTab.Shoves, Get("shuv").Tab);
            Assert.AreEqual(TrickBookTab.Grinds, Get("fifty").Tab);
            Assert.AreEqual(TrickBookTab.Grinds, Get(StyleTricks.Bluntslide.Id).Tab);
            Assert.AreEqual(TrickBookTab.Manuals, Get(StyleTricks.Casper.Id).Tab);
            Assert.AreEqual(TrickBookTab.LipsAndWalls, Get(LipRules.IdFor(StickZone.Up)).Tab);
            Assert.AreEqual(TrickBookTab.LipsAndWalls, Get("wallie").Tab);
            Assert.AreEqual(TrickBookTab.SpinsAndMore, Get("spin_360").Tab);
            Assert.AreEqual(TrickBookTab.SpinsAndMore, Get(RevertRules.Id).Tab);
            Assert.AreEqual(TrickBookTab.Signatures, Get("sp_grab").Tab);
            foreach (var s in StyleTricks.Signatures) Assert.AreEqual(TrickBookTab.Signatures, Get(s.Id).Tab);
            Assert.IsNull(Get("sp_flip"), "a full-meter flip swipe does your signature instead");
            Assert.AreEqual("Full", Get("spin_360").Name);
            Assert.AreEqual("720", Get("spin_720").Name, "no name given → degrees");
        }

        [Test]
        public void Catalog_IdsAreUnique_AndEveryTrickHasAHowTo()
        {
            var book = TrickCatalog.Build(Library(), null);
            var ids = new HashSet<string>();
            foreach (var t in book)
            {
                Assert.IsTrue(ids.Add(t.Id), "duplicate " + t.Id);
                Assert.IsFalse(string.IsNullOrEmpty(t.HowTo), t.Id);
                Assert.IsFalse(t.Id.StartsWith("gap_"));
            }
            foreach (TrickBookTab tab in Enum.GetValues(typeof(TrickBookTab)))
                Assert.IsTrue(book.Exists(t => t.Tab == tab), "empty tab " + tab);
        }

        [Test]
        public void HowTo_MatchesTheRealInputs()
        {
            var book = TrickCatalog.Build(Library(), null);
            StringAssert.Contains("SWIPE UP", book.Find(t => t.Id == "kick").HowTo);
            StringAssert.Contains("STICK CENTRED", book.Find(t => t.Id == "kick").HowTo);
            StringAssert.Contains("STICK LEFT", book.Find(t => t.Id == "heel").HowTo);
            StringAssert.Contains("SWIPE DOWN", book.Find(t => t.Id == "melon").HowTo);
            StringAssert.Contains("STICK RIGHT", book.Find(t => t.Id == "melon").HowTo);
            StringAssert.Contains("SWIPE LEFT OR RIGHT", book.Find(t => t.Id == "shuv").HowTo);
            StringAssert.Contains("UP OR DOWN", book.Find(t => t.Id == "shuv").HowTo);
            StringAssert.Contains("SPECIAL METER", book.Find(t => t.Id == "sp_grab").HowTo);
            StringAssert.Contains("ACTION", book.Find(t => t.Id == "fifty").HowTo);
            StringAssert.Contains("STICK UP", book.Find(t => t.Id == LipRules.IdFor(StickZone.Up)).HowTo);
        }

        [Test]
        public void Record_CountsLandings_AndIgnoresGaps()
        {
            var s = new TrickBookState();
            s.Record(new[] { "kick", "gap_rail", "kick" }, 900, "harbor");
            var r = s.Find("kick");
            Assert.AreEqual(2, r.landed);
            Assert.AreEqual(2, r.longestLine, "gaps aren't tricks");
            Assert.AreEqual(900, r.bestLine);
            CollectionAssert.AreEqual(new[] { "harbor" }, r.parks);
            Assert.IsNull(s.Find("gap_rail"));
        }

        [Test]
        public void Record_IgnoresEmptyAndPointlessLines()
        {
            var s = new TrickBookState();
            Assert.AreEqual(0, s.Record(new string[0], 500, "a").Count);
            Assert.AreEqual(0, s.Record(new[] { "kick" }, 0, "a").Count);
            Assert.AreEqual(0, s.Record(null, 500, "a").Count);
            Assert.AreEqual(0, s.tricks.Count);
        }

        [Test]
        public void LandTen_PaysOnce()
        {
            var s = new TrickBookState();
            List<(string id, TrickGoal goal)> done = null;
            for (int i = 0; i < 9; i++) Assert.AreEqual(0, s.Record(new[] { "kick" }, 100, "a").Count);
            done = s.Record(new[] { "kick" }, 100, "a");
            Assert.AreEqual(1, done.Count);
            Assert.AreEqual(("kick", TrickGoal.LandTen), done[0]);
            Assert.AreEqual(0, s.Record(new[] { "kick" }, 100, "a").Count, "already claimed");
            Assert.AreEqual(1, s.GoalsDone("kick"));
        }

        [Test]
        public void LongLine_BigLine_AndThreeParks()
        {
            var s = new TrickBookState();
            var done = s.Record(new[] { "a", "b", "c", "d", "e" }, 12000, "p1");
            // Every trick in a 5-trick, 12K line clears both line challenges.
            Assert.AreEqual(10, done.Count);
            Assert.IsTrue(done.Contains(("c", TrickGoal.LongLine)));
            Assert.IsTrue(done.Contains(("e", TrickGoal.BigLine)));
            s.Record(new[] { "a" }, 100, "p2");
            Assert.AreEqual(0, s.Record(new[] { "a" }, 100, "p2").Count, "same park twice");
            done = s.Record(new[] { "a" }, 100, "p3");
            CollectionAssert.AreEqual(new[] { ("a", TrickGoal.ThreeParks) }, done);
            Assert.AreEqual(3, s.GoalsDone("a"));
        }

        [Test]
        public void RepeatsInOneLine_CountAsLandings_NotAsExtraParksOrLength()
        {
            var s = new TrickBookState();
            s.Record(new[] { "kick", "kick", "kick", "kick", "kick" }, 2000, "a");
            var r = s.Find("kick");
            Assert.AreEqual(5, r.landed);
            Assert.AreEqual(5, r.longestLine);
            Assert.AreEqual(1, r.parks.Count);
        }

        [Test]
        public void Progress_AndTokens()
        {
            var s = new TrickBookState();
            s.Record(new[] { "kick", "heel" }, 4000, "a");
            var r = s.Find("kick");
            Assert.AreEqual((1L, 10L), TrickBookState.Progress(r, TrickGoal.LandTen));
            Assert.AreEqual((2L, 5L), TrickBookState.Progress(r, TrickGoal.LongLine));
            Assert.AreEqual((4000L, 10000L), TrickBookState.Progress(r, TrickGoal.BigLine));
            Assert.AreEqual((0L, 10L), TrickBookState.Progress(null, TrickGoal.LandTen));
            Assert.AreEqual(40, TrickBookState.Tokens(TrickGoal.BigLine));
            Assert.AreEqual(25, TrickBookState.Tokens(TrickGoal.LandTen));
        }

        [Test]
        public void Sanitize_RepairsOldSaves()
        {
            var s = new TrickBookState { tricks = null };
            s.Sanitize();
            Assert.IsNotNull(s.tricks);
            s.tricks.Add(null);
            s.tricks.Add(new TrickRecord { id = "" });
            s.tricks.Add(new TrickRecord { id = "kick", parks = null });
            s.Sanitize();
            Assert.AreEqual(1, s.tricks.Count);
            Assert.IsNotNull(s.tricks[0].parks);
        }
    }

    public class LeaderboardHubTests
    {
        private static BoardInfo Points => new BoardInfo { Id = "b", Title = "B", Format = BoardFormat.Points };
        private static BoardInfo Time => new BoardInfo { Id = "t", Title = "T", Format = BoardFormat.Time };

        [Test]
        public void Boards_CoverEveryBoardTheGameSubmitsTo()
        {
            var boards = LeaderboardHub.Boards(new[] { ("harbor", "Harbor Plaza") });
            var ids = new HashSet<string>();
            foreach (var b in boards) Assert.IsTrue(ids.Add(b.Id), "duplicate " + b.Id);
            Assert.IsTrue(ids.Contains(Achievements.LeaderboardId("harbor")));
            Assert.IsTrue(ids.Contains(WeeklyEvents.LeaderboardId));
            Assert.IsTrue(ids.Contains(Leaderboards.SkateWins));
            foreach (var r in RetroCityLayout.Races) Assert.IsTrue(ids.Contains(Leaderboards.Race(r.Id)), r.Id);
            foreach (var s in RetroCityLayout.Spots) Assert.IsTrue(ids.Contains(Leaderboards.Challenge(s.Id)), s.Id);
            Assert.AreEqual("HARBOR PLAZA", boards[0].Title);
            Assert.AreEqual("harbor", boards[0].SourceId);
            Assert.IsTrue(boards.Find(b => b.Group == BoardGroup.Races).LowerIsBetter);
        }

        [Test]
        public void Page_ParsesTotal_AndDropsDuplicateRanks()
        {
            var page = BoardPage.Parse("#total\t812\n1\tAce\t9000\t0\n2\tBee\t8000\t0\n40\tMe\t100\t1\n40\tMe\t100\t1\nbad line\n");
            Assert.AreEqual(812, page.Total);
            Assert.AreEqual(3, page.Rows.Count);
            Assert.AreEqual(40, page.You.Rank);
            Assert.IsTrue(LeaderboardHub.GapBefore(page, 2));
            Assert.IsFalse(LeaderboardHub.GapBefore(page, 1));
            Assert.AreEqual(0, BoardPage.Parse("").Rows.Count);
            Assert.AreEqual(2, BoardPage.Parse("1\ta\t5\t0\n2\tb\t4\t0").Total, "no total line → the rows");
        }

        [Test]
        public void NextTarget_IsTheClosestPlayerAboveYou()
        {
            var page = BoardPage.Parse("1\tA\t900\t0\n2\tB\t800\t0\n3\tC\t700\t0\n4\tMe\t600\t1\n5\tE\t500\t0");
            Assert.AreEqual("C", LeaderboardHub.NextTarget(page).Name);
            StringAssert.Contains("BEAT #3 C: 101 MORE", LeaderboardHub.TargetText(page, Points, 0));
        }

        [Test]
        public void NumberOne_DefendsIt_AndUnrankedChasesTheBottom()
        {
            var top = BoardPage.Parse("1\tMe\t900\t1\n2\tB\t800\t0");
            Assert.IsNull(LeaderboardHub.NextTarget(top));
            Assert.AreEqual("YOU'RE #1. DEFEND IT.", LeaderboardHub.TargetText(top, Points, 900));
            var unranked = BoardPage.Parse("1\tA\t900\t0\n2\tB\t800\t0");
            Assert.AreEqual("B", LeaderboardHub.NextTarget(unranked).Name);
            StringAssert.Contains("BEAT #2 B: 301 MORE", LeaderboardHub.TargetText(unranked, Points, 500));
            StringAssert.Contains("NEXT UP: #2 B", LeaderboardHub.TargetText(unranked, Points, 0));
            StringAssert.Contains("GO SET ONE", LeaderboardHub.TargetText(null, Points, 0));
            StringAssert.Contains("YOUR BEST: 1,234", LeaderboardHub.TargetText(new BoardPage(), Points, 1234));
        }

        [Test]
        public void RaceBoards_AreTimes_LowerIsBetter()
        {
            Assert.AreEqual("42.07", LeaderboardHub.Format(4207, BoardFormat.Time));
            Assert.AreEqual("1:02.35", LeaderboardHub.Format(6235, BoardFormat.Time));
            Assert.AreEqual("--", LeaderboardHub.Format(0, BoardFormat.Time));
            Assert.AreEqual("12,345", LeaderboardHub.Format(12345, BoardFormat.Points));
            Assert.IsTrue(LeaderboardHub.Better(4000, 4100, BoardFormat.Time));
            Assert.IsFalse(LeaderboardHub.Better(0, 4100, BoardFormat.Time));
            Assert.IsTrue(LeaderboardHub.Better(4100, 0, BoardFormat.Time));
            Assert.IsTrue(LeaderboardHub.Better(4100, 4000, BoardFormat.Points));
            var page = BoardPage.Parse("1\tA\t4000\t0\n2\tMe\t4250\t1");
            StringAssert.Contains("BEAT #1 A: 2.51S FASTER", LeaderboardHub.TargetText(page, Time, 0));
        }

        [Test]
        public void SkateWins_CountWins()
        {
            var page = BoardPage.Parse("1\tA\t5\t0\n2\tMe\t4\t1");
            StringAssert.Contains("2 MORE WINS", LeaderboardHub.TargetText(page, new BoardInfo { Format = BoardFormat.Count }, 0));
            var close = BoardPage.Parse("1\tA\t4\t0\n2\tMe\t4\t1");
            StringAssert.Contains("1 MORE WIN", LeaderboardHub.TargetText(close, new BoardInfo { Format = BoardFormat.Count }, 0));
        }
    }
}
