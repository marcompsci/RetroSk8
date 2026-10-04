using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class ScoreMathTests
    {
        [Test]
        public void Floor_SnapsFloatNoise_ButNotRealFractions()
        {
            Assert.AreEqual(1170, ScoreMath.Floor(1169.99996));
            Assert.AreEqual(1169, ScoreMath.Floor(1169.5));
            Assert.AreEqual(0, ScoreMath.Floor(0.0));
            Assert.AreEqual(4000, ScoreMath.Ceiling(4000.00006));
            Assert.AreEqual(4001, ScoreMath.Ceiling(4000.4));
        }

        [Test]
        public void CeilPercent_IsExact()
        {
            Assert.AreEqual(4000, ScoreMath.CeilPercent(5000, 80));
            Assert.AreEqual(1, ScoreMath.CeilPercent(1, 80));
            Assert.AreEqual(801, ScoreMath.CeilPercent(1001, 80));
            Assert.AreEqual(0, ScoreMath.CeilPercent(0, 80));
            Assert.AreEqual(0, ScoreMath.CeilPercent(-5, 80));
        }

        [Test]
        public void MatchTargets_UseTheExactPercent()
        {
            Assert.AreEqual(PartyRules.MatchPercent / 100f, PartyRules.MatchFactor, 1e-6f);
            Assert.AreEqual(SkateDuel.MatchPercent / 100f, SkateDuel.MatchFactor, 1e-6f);
        }
    }

    public class SkaterMotionTests
    {
        [Test]
        public void Push_StepsDown_SweepsBack_AndReturnsToTheBoard()
        {
            var start = SkaterMotion.Push(0f);
            Assert.AreEqual(0f, start.swing, 1e-4f);
            Assert.AreEqual(0f, start.reach, 1e-4f);
            var plant = SkaterMotion.Push(0.15f);
            Assert.AreEqual(1f, plant.reach, 1e-4f);
            Assert.Less(plant.swing, 0f, "foot steps forward first");
            var sweep = SkaterMotion.Push(0.6f);
            Assert.Greater(sweep.swing, 20f, "then sweeps back toward the tail");
            var back = SkaterMotion.Push(0.999f);
            Assert.AreEqual(0f, back.reach, 0.01f);
            Assert.AreEqual(0f, back.swing, 0.5f);
            Assert.AreEqual(SkaterMotion.Push(0.3f).swing, SkaterMotion.Push(1.3f).swing, 1e-4f, "loops");
        }

        [Test]
        public void Squash_DipsThenRecovers_HarderForBigAirs()
        {
            Assert.AreEqual(0f, SkaterMotion.Squash(-1f, 1f));
            Assert.AreEqual(0f, SkaterMotion.Squash(SkaterMotion.SquashSeconds, 1f));
            float peakSoft = SkaterMotion.Squash(SkaterMotion.SquashSeconds * 0.2f, 0f);
            float peakHard = SkaterMotion.Squash(SkaterMotion.SquashSeconds * 0.2f, 1f);
            Assert.Greater(peakHard, peakSoft);
            Assert.Greater(peakSoft, SkaterMotion.Squash(SkaterMotion.SquashSeconds * 0.8f, 0f));
        }

        [Test]
        public void Pop_PeaksNoseUp_ThenLevels()
        {
            Assert.AreEqual(0f, SkaterMotion.Pop(0f), 1e-4f);
            Assert.AreEqual(SkaterMotion.PopPitch, SkaterMotion.Pop(SkaterMotion.PopSeconds * 0.3f), 0.01f);
            Assert.AreEqual(0f, SkaterMotion.Pop(SkaterMotion.PopSeconds), 1e-4f);
        }

        [Test]
        public void CarveLean_FollowsTheTurn_AndIsCapped()
        {
            Assert.Greater(SkaterMotion.CarveLean(100f, 10f), 0f);
            Assert.Less(SkaterMotion.CarveLean(-100f, 10f), 0f);
            Assert.AreEqual(0f, SkaterMotion.CarveLean(100f, 0f), 1e-4f, "no lean standing still");
            Assert.AreEqual(SkaterMotion.MaxCarveLean, SkaterMotion.CarveLean(10000f, 20f), 1e-4f);
        }

        [Test]
        public void BoardArc_RisesFallsAndStaysOnTheGround()
        {
            var up = SkaterMotion.BoardArc(0.1f, 1f, 3f, 2f);
            Assert.Greater(up.y, 0f);
            Assert.Greater(up.z, 0f);
            for (float t = 0f; t < 3f; t += 0.05f)
                Assert.GreaterOrEqual(SkaterMotion.BoardArc(t, 1f, 3f, 2f).y, 0f);
            var late = SkaterMotion.BoardArc(3f, 1f, 3f, 2f);
            Assert.AreEqual(0f, late.y, 1e-3f, "settled");
        }
    }

    public class ParkEditor2Tests
    {
        private static CustomPark Empty() => CustomPark.Create("custom_1", "TEST");

        [Test]
        public void Stretch_GrowsTheFootprint_UpToTheMax()
        {
            var park = Empty();
            int i = park.Add(PieceKind.Ledge, 10, 20);
            Assert.AreEqual((1, 3), CustomPark.Footprint(park.pieces[i]));
            Assert.IsTrue(park.TryStretch(i, 1));
            Assert.AreEqual((1, 5), CustomPark.Footprint(park.pieces[i]));
            Assert.IsTrue(park.TryStretch(i, 1));
            Assert.IsTrue(park.TryStretch(i, 1));
            Assert.IsFalse(park.TryStretch(i, 1), "max length");
            Assert.AreEqual(CustomPark.MaxLength, park.pieces[i].length);
            Assert.IsTrue(park.TryStretch(i, -1));
            Assert.AreEqual(2, park.pieces[i].length);
        }

        [Test]
        public void QuartersAndBanks_GetWider_OtherPiecesDontStretch()
        {
            var park = Empty();
            int q = park.Add(PieceKind.QuarterPipe, 20, 30);
            Assert.IsTrue(park.TryStretch(q, 1));
            Assert.AreEqual((6, 2), CustomPark.Footprint(park.pieces[q]));
            int k = park.Add(PieceKind.Kicker, 10, 10);
            Assert.IsFalse(park.TryStretch(k, 1));
            Assert.AreEqual(0, park.pieces[k].length);
        }

        [Test]
        public void Stretch_IsBlockedByNeighbours()
        {
            var park = Empty();
            int a = park.Add(PieceKind.Ledge, 10, 20);
            var p = park.pieces[a];
            // Box it in: pieces right above and below.
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Bench, x = p.x, z = p.z + 3, rot = 0 });
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Bench, x = p.x, z = p.z - 2, rot = 0 });
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Wall, x = p.x - 1, z = p.z - 1, rot = 0 });
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Wall, x = p.x + 1, z = p.z - 1, rot = 0 });
            Assert.IsFalse(park.TryStretch(a, 1));
            Assert.AreEqual(0, park.pieces[a].length, "nothing changes when it fails");
        }

        [Test]
        public void Bend_CyclesAndWidensTheRail()
        {
            var park = Empty();
            int r = park.Add(PieceKind.FlatRail, 20, 20);
            var seen = new List<int>();
            for (int n = 0; n < 5; n++)
            {
                Assert.IsTrue(park.TryBend(r));
                seen.Add(park.pieces[r].bend);
            }
            CollectionAssert.AreEqual(new[] { 1, 2, -2, -1, 0 }, seen);
            Assert.IsTrue(park.TryBend(r));
            Assert.AreEqual(3, CustomPark.Footprint(park.pieces[r]).w);
            int l = park.Add(PieceKind.Ledge, 5, 30);
            Assert.IsFalse(park.TryBend(l), "only rails bend");
        }

        [Test]
        public void Rotate_KeepsStretchAndBend()
        {
            var park = Empty();
            int r = park.Add(PieceKind.FlatRail, 20, 20);
            park.TryStretch(r, 1);
            park.TryBend(r);
            Assert.IsTrue(park.TryRotate(r));
            Assert.AreEqual((5, 3), CustomPark.Footprint(park.pieces[r]));
            Assert.AreEqual(1, park.pieces[r].length);
            Assert.AreEqual(1, park.pieces[r].bend);
        }

        [Test]
        public void Sanitize_ClampsNewFields_AndCopiesKeepThem()
        {
            var park = Empty();
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Kicker, x = 4, z = 20, length = 3, bend = 2 });
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.FlatRail, x = 20, z = 20, length = 9, bend = -7 });
            park.Sanitize();
            Assert.AreEqual(0, park.pieces[0].length, "kickers don't stretch");
            Assert.AreEqual(0, park.pieces[0].bend);
            Assert.AreEqual(CustomPark.MaxLength, park.pieces[1].length);
            Assert.AreEqual(-CustomPark.MaxBend, park.pieces[1].bend);
            int copy = park.Duplicate(1);
            Assert.GreaterOrEqual(copy, 0);
            Assert.AreEqual(park.pieces[1].length, park.pieces[copy].length);
            Assert.AreEqual(park.pieces[1].bend, park.pieces[copy].bend);
        }

        [Test]
        public void History_UndoRedo()
        {
            var park = Empty();
            var h = new ParkHistory();
            Assert.IsNull(h.Undo(park));
            h.Record(park);
            park.Add(PieceKind.Ledge, 10, 10);
            h.Record(park);
            park.Add(PieceKind.Kicker, 30, 30);
            Assert.AreEqual(2, park.pieces.Count);

            var back = h.Undo(park);
            Assert.AreEqual(1, back.pieces.Count);
            Assert.IsTrue(h.CanRedo);
            var fwd = h.Redo(back);
            Assert.AreEqual(2, fwd.pieces.Count);
            var b2 = h.Undo(fwd);
            var b1 = h.Undo(b2);
            Assert.AreEqual(0, b1.pieces.Count);
            Assert.IsFalse(h.CanUndo);

            h.Record(b1); // a new edit clears redo
            Assert.IsFalse(h.CanRedo);
        }

        [Test]
        public void History_IsCapped_AndSnapshotsDontShareState()
        {
            var park = Empty();
            var h = new ParkHistory();
            for (int i = 0; i < ParkHistory.Limit + 10; i++) h.Record(park);
            int n = 0;
            while (h.Undo(park) != null) n++;
            Assert.AreEqual(ParkHistory.Limit, n);

            var p2 = Empty();
            p2.Add(PieceKind.Ledge, 10, 10);
            var h2 = new ParkHistory();
            h2.Record(p2);
            p2.TryMove(0, 1, 0);
            var old = h2.Undo(p2);
            Assert.AreNotEqual(old.pieces[0].x, p2.pieces[0].x, "the snapshot is a copy");
        }

        [Test]
        public void ShareCodes_PlainParksStayVersionOne_ExtendedRoundTrip()
        {
            var plain = CustomPark.Starter("custom_1", "PLAIN");
            Assert.AreEqual(1, ShareCodes.VersionFor(plain));

            var park = Empty();
            int r = park.Add(PieceKind.FlatRail, 20, 20);
            park.TryStretch(r, 1);
            park.TryBend(r);
            park.TryBend(r);
            int q = park.Add(PieceKind.QuarterPipe, 20, 34, 2);
            park.TryStretch(q, 1);
            Assert.AreEqual(ShareCodes.ExtendedVersion, ShareCodes.VersionFor(park));

            Assert.IsTrue(ShareCodes.TryDecodePark(ShareCodes.EncodePark(park), out var back, out var err), err);
            Assert.AreEqual(park.pieces.Count, back.pieces.Count);
            for (int i = 0; i < park.pieces.Count; i++)
            {
                Assert.AreEqual(park.pieces[i].length, back.pieces[i].length);
                Assert.AreEqual(park.pieces[i].bend, back.pieces[i].bend);
                Assert.AreEqual(park.pieces[i].x, back.pieces[i].x);
            }

            var challenge = new ScoreChallenge { Park = park, Target = 9000, From = "OMARI" };
            Assert.IsTrue(ShareCodes.TryDecodeChallenge(ShareCodes.EncodeChallenge(challenge), out var c, out err), err);
            Assert.AreEqual(2, c.Park.pieces[0].bend);
        }
    }

    public class GalleryTests
    {
        [Test]
        public void CleanName_KeepsCodeCharacters_AndTrims()
        {
            Assert.AreEqual("MY PARK 2!", Gallery.CleanName("  my   park_2! ", 18));
            Assert.AreEqual("ABC", Gallery.CleanName("a<b>c", 18));
            Assert.AreEqual("ABCDE", Gallery.CleanName("abcdefgh", 5));
            Assert.AreEqual("", Gallery.CleanName(null, 5));
        }

        [Test]
        public void Filter_CatchesDisguisedWords_AndLeavesNormalNamesAlone()
        {
            Assert.IsTrue(Gallery.IsBlocked("SH1T PARK"));
            Assert.IsTrue(Gallery.IsBlocked("f.u.c.k"));
            Assert.IsTrue(Gallery.IsBlocked("B 1 T C H"));
            Assert.IsFalse(Gallery.IsBlocked("RAIL GARDEN"));
            Assert.IsFalse(Gallery.IsBlocked("HARBOR LINES"));
            Assert.IsFalse(Gallery.IsBlocked(""));
            Assert.AreEqual("A PARK", Gallery.SafeName("sh1t park", 18, "A PARK"));
            Assert.AreEqual("NICE ONE", Gallery.SafeName("nice one", 18, "A PARK"));
        }

        [Test]
        public void Parse_RoundTripsTheWireFormat_AndSkipsJunk()
        {
            var e = new GalleryEntry { Id = "abc", Kind = GalleryKind.Ghost, Name = "TAB\tNAME", Author = "OMARI", Detail = 12345, Location = "harbor_plaza", Created = 1700000000 };
            string text = Gallery.Format(e) + "\nbad line\n\t1\tx\ty\t1\t\t1\nid2\t7\tx\ty\t1\t\t1\n";
            var list = Gallery.Parse(text);
            Assert.AreEqual(1, list.Count, "missing id and unknown kind are skipped");
            Assert.AreEqual("abc", list[0].Id);
            Assert.AreEqual(GalleryKind.Ghost, list[0].Kind);
            Assert.AreEqual("TAB NAME", list[0].Name, "tabs can't break the format");
            Assert.AreEqual(12345, list[0].Detail);
            Assert.AreEqual("harbor_plaza", list[0].Location);
        }

        [Test]
        public void Visible_DropsHiddenPostsAndBlockedAuthors()
        {
            var s = new GalleryState();
            s.hidden.Add("b");
            s.blockedAuthors.Add("TROLL");
            var all = new[]
            {
                new GalleryEntry { Id = "a", Author = "OMARI" },
                new GalleryEntry { Id = "b", Author = "OMARI" },
                new GalleryEntry { Id = "c", Author = "TROLL" },
                null,
            };
            var v = Gallery.Visible(all, s);
            Assert.AreEqual(1, v.Count);
            Assert.AreEqual("a", v[0].Id);
        }

        [Test]
        public void Uploads_AreLimitedPerDay()
        {
            var s = new GalleryState();
            for (int i = 0; i < Gallery.UploadsPerDay; i++)
            {
                Assert.IsTrue(Gallery.CanUpload(s, 100));
                Gallery.CountUpload(s, 100, "id" + i);
            }
            Assert.IsFalse(Gallery.CanUpload(s, 100));
            Assert.IsTrue(Gallery.CanUpload(s, 101), "a new day");
            Assert.AreEqual(Gallery.UploadsPerDay, s.posted.Count);
        }

        [Test]
        public void Age_ReadsNaturally()
        {
            Assert.AreEqual("1 MIN AGO", Gallery.Age(1000, 1010));
            Assert.AreEqual("5 MIN AGO", Gallery.Age(1000, 1300));
            Assert.AreEqual("2 H AGO", Gallery.Age(0, 7300));
            Assert.AreEqual("1 DAY AGO", Gallery.Age(0, 90000));
            Assert.AreEqual("3 DAYS AGO", Gallery.Age(0, 3 * 86400 + 5));
        }

        [Test]
        public void Sanitize_RepairsOldSaves()
        {
            var s = new GalleryState { hidden = null, blockedAuthors = null, reported = null, posted = null };
            s.Sanitize();
            Assert.IsNotNull(s.hidden);
            for (int i = 0; i < Gallery.MaxRemembered + 20; i++) s.hidden.Add("x" + i);
            s.Sanitize();
            Assert.AreEqual(Gallery.MaxRemembered, s.hidden.Count);
            Assert.AreEqual("x20", s.hidden[0], "the oldest are dropped first");
        }
    }
}
