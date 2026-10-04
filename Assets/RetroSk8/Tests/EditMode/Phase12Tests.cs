using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class RadioTests
    {
        [Test]
        public void Stations_HaveOriginalSongs_WithTitles()
        {
            var names = new HashSet<string>();
            foreach (var st in Radio.Stations)
            {
                Assert.AreEqual(st.Songs.Length, st.Titles.Length, st.Id);
                Assert.GreaterOrEqual(st.Songs.Length, 3, st.Id);
                foreach (var s in st.Songs) Assert.IsTrue(names.Add(s.Name), "song names are unique: " + s.Name);
            }
            Assert.AreEqual(3, Radio.Stations.Length);
        }

        [Test]
        public void EverySong_Renders_AsALoop()
        {
            foreach (var st in Radio.Stations)
                foreach (var s in st.Songs)
                {
                    var data = MusicComposer.Render(s, 8000);
                    Assert.AreEqual(MusicComposer.LoopSamples(s, 8000), data.Length, s.Name);
                    float peak = 0f;
                    foreach (var v in data) peak = Math.Max(peak, Math.Abs(v));
                    Assert.Greater(peak, 0.05f, s.Name + " is not silent");
                    Assert.LessOrEqual(peak, 1f, s.Name + " is soft-clipped");
                }
        }

        [Test]
        public void Repeats_LastAboutARadioSong()
        {
            foreach (var st in Radio.Stations)
                foreach (var s in st.Songs)
                {
                    float seconds = Radio.RepeatsFor(s) * MusicComposer.LoopSeconds(s);
                    Assert.GreaterOrEqual(Radio.RepeatsFor(s), 2);
                    Assert.GreaterOrEqual(seconds, 50f, s.Name);
                    Assert.LessOrEqual(seconds, 120f, s.Name);
                }
        }

        [Test]
        public void Player_PlaysEverySongBeforeRepeating_AndNeverTwiceInARow()
        {
            var p = new RadioPlayer(1, 42);
            int n = p.Station.Songs.Length;
            var firstLap = new HashSet<int> { p.SongIndex };
            for (int i = 1; i < n; i++) firstLap.Add(p.Next());
            Assert.AreEqual(n, firstLap.Count, "a full lap plays each song once");
            int last = p.SongIndex;
            for (int i = 0; i < 40; i++)
            {
                int next = p.Next();
                Assert.AreNotEqual(last, next, "no back-to-back repeats across laps");
                last = next;
            }
        }

        [Test]
        public void Shuffle_IsAPermutation_AndSeeded()
        {
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, Radio.ShuffleOrder(5, 7));
            CollectionAssert.AreEqual(Radio.ShuffleOrder(9, 123), Radio.ShuffleOrder(9, 123));
            Assert.AreEqual(0, Radio.ShuffleOrder(0, 1).Length);
        }

        [Test]
        public void MusicButton_CyclesThemesStationsOff()
        {
            var mode = MusicMode.ParkThemes;
            int station = 0;
            var seen = new List<string>();
            for (int i = 0; i < Radio.Stations.Length + 2; i++)
            {
                Radio.Cycle(ref mode, ref station);
                seen.Add(Radio.ModeName(mode, station));
            }
            Assert.AreEqual("RADIO: LOW TIDE FM", seen[0]);
            Assert.AreEqual("RADIO: SUNSET CASSETTE", seen[2]);
            Assert.AreEqual("MUSIC: OFF", seen[3]);
            Assert.AreEqual("MUSIC: PARK THEMES", seen[4]);
        }
    }

    public class UiScaleTests
    {
        [Test]
        public void Phones_KeepMatchingHeight()
        {
            Assert.AreEqual(1f, UiScale.MatchFor(2556, 1179), 1e-4f);  // modern iPhone landscape
            Assert.AreEqual(1f, UiScale.MatchFor(2340, 1080), 1e-4f);
        }

        [Test]
        public void IPads_And16x9_GetAWideEnoughCanvas()
        {
            foreach (var (w, h) in new[] { (2732f, 2048f), (2388f, 1668f), (2048f, 1536f), (1334f, 750f) })
            {
                float m = UiScale.MatchFor(w, h);
                Assert.Less(m, 1f, $"{w}x{h}");
                UiScale.LogicalSize(w, h, m, out float lw, out float lh);
                Assert.AreEqual(UiScale.MinLogicalWidth, lw, 1f, $"{w}x{h} canvas width");
                Assert.GreaterOrEqual(lh, 1080f - 1f, "never shorter than the phone layout");
            }
            Assert.IsTrue(UiScale.IsTabletShape(2732, 2048));
            Assert.IsFalse(UiScale.IsTabletShape(2556, 1179));
        }
    }

    public class GhostCodeTests
    {
        private static ReplayTrack Run(float seconds, bool teleport = false)
        {
            var t = new ReplayTrack { LocationId = "harbor_plaza", Score = 31337 };
            for (int i = 0; i <= seconds * 20f; i++)
            {
                float time = i / 20f;
                float x = time * 7f, z = (float)Math.Sin(time) * 3f;
                if (teleport && time > 30f) x += 250f; // a respawn across the park
                float yaw = time * 0.5f;
                t.Add(new ReplayFrame
                {
                    Time = time,
                    Position = new RVec3(x, 0.2f + (float)Math.Abs(Math.Sin(time * 2f)), z),
                    Rotation = new RQuat(0f, (float)Math.Sin(yaw / 2f), 0f, (float)Math.Cos(yaw / 2f)),
                    Pose = RQuat.Identity,
                    Body = new RQuat(0.1f, 0f, 0f, 0.995f),
                    BoardPosition = new RVec3(0f, -0.05f, 0.02f),
                    Board = new RQuat((float)Math.Sin(time), 0f, 0f, (float)Math.Cos(time)),
                });
            }
            return t;
        }

        private static ScoreChallenge Challenge(ReplayTrack track) => new ScoreChallenge
        {
            LocationId = "harbor_plaza", Target = 31337, From = "OMARI", Ghost = track,
            GhostBanks = new List<GhostBank> { new GhostBank(12.3f, 4000), new GhostBank(70.1f, 27337) },
        };

        [Test]
        public void RoundTrip_KeepsTheLine()
        {
            var track = Run(121f, teleport: true);
            string code = GhostCodes.Encode(Challenge(track));
            Assert.IsTrue(GhostCodes.IsGhostCode(code));
            Assert.Less(code.Length, 45000, "a full Two-Minute Run fits in a pasteable code");

            Assert.IsTrue(GhostCodes.TryDecode(code, out var c, out var error), error);
            Assert.AreEqual("harbor_plaza", c.LocationId);
            Assert.AreEqual(31337, c.Target);
            Assert.AreEqual("OMARI", c.From);
            Assert.AreEqual(31337, c.Ghost.Score);
            Assert.AreEqual(2, c.GhostBanks.Count);
            Assert.AreEqual(70.1f, c.GhostBanks[1].Time, 0.051f);
            Assert.AreEqual(121f, c.Ghost.Duration, 0.11f);

            for (float t = 0f; t < 121f; t += 3.7f)
            {
                track.Sample(t, out var a);
                c.Ghost.Sample(t, out var b);
                float d = (float)Math.Sqrt(Sq(a.Position.X - b.Position.X) + Sq(a.Position.Y - b.Position.Y) + Sq(a.Position.Z - b.Position.Z));
                Assert.Less(d, 0.15f, $"position at {t:0.0}s");
                float dot = Math.Abs(a.Rotation.X * b.Rotation.X + a.Rotation.Y * b.Rotation.Y + a.Rotation.Z * b.Rotation.Z + a.Rotation.W * b.Rotation.W);
                Assert.Greater(dot, 0.995f, $"rotation at {t:0.0}s");
            }
        }

        private static float Sq(float v) => v * v;

        [Test]
        public void WrappedOrTruncatedCodes()
        {
            string code = GhostCodes.Encode(Challenge(Run(20f)));
            // Mail apps wrap long lines: whitespace is ignored.
            string wrapped = code.Substring(0, 50) + "\n  " + code.Substring(50, 100) + "\r\n" + code.Substring(150);
            Assert.IsTrue(GhostCodes.TryDecode(wrapped, out _, out _));
            Assert.IsFalse(GhostCodes.TryDecode(code.Substring(0, code.Length - 40), out _, out var e1));
            StringAssert.Contains("INCOMPLETE", e1);
            Assert.IsFalse(GhostCodes.TryDecode("RP-12345", out _, out _));
            Assert.IsFalse(GhostCodes.IsGhostCode("RC-ABCDE"));
        }

        [Test]
        public void CustomParks_TravelWithTheGhost()
        {
            var park = CustomPark.Create(CustomParkIds.ForSlot(2), "BACKYARD");
            park.pieces.Add(new ParkPiece { kind = 1, x = 10, z = 10, rot = 1, size = 1 });
            var c = Challenge(Run(10f));
            c.LocationId = null;
            c.Park = park;
            Assert.IsTrue(GhostCodes.TryDecode(GhostCodes.Encode(c), out var back, out var error), error);
            Assert.IsNotNull(back.Park);
            Assert.AreEqual("BACKYARD", back.Park.name);
            Assert.AreEqual(back.Park.id, back.Ghost.LocationId);
        }

        [Test]
        public void RivalScore_FollowsTheirBanks()
        {
            var banks = new List<GhostBank> { new GhostBank(5f, 100), new GhostBank(10f, 250) };
            Assert.AreEqual(0, RivalTimeline.ScoreAt(banks, 4.9f));
            Assert.AreEqual(100, RivalTimeline.ScoreAt(banks, 5f));
            Assert.AreEqual(350, RivalTimeline.ScoreAt(banks, 99f));
            Assert.AreEqual(0, RivalTimeline.ScoreAt(null, 99f));

            var list = new List<RivalRecord>();
            for (int i = 0; i < 20; i++) RivalTimeline.Record(list, new RivalRecord { from = "R" + i });
            Assert.AreEqual(12, list.Count);
            Assert.AreEqual("R19", list[0].from, "newest first");
        }

        [Test]
        public void FriendScores_Parse()
        {
            var rows = FriendScore.Parse("2\tDEX\t12000\t0\n1\tPILAR\t25000\t0\nbad line\n7\tME\t900\t1\n");
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("PILAR", rows[0].Name);
            Assert.IsTrue(rows[2].IsYou);
            Assert.AreEqual(0, FriendScore.Parse(null).Count);
        }
    }

    public class ShopTests
    {
        private static List<ShopItem> Items()
        {
            var list = new List<ShopItem>();
            for (int i = 0; i < 12; i++) list.Add(new ShopItem("item_" + i, 20 + i));
            list.Add(new ShopItem("free", 0));
            list.Add(new ShopItem("pack_item", 0, true));
            return list;
        }

        [Test]
        public void Featured_IsStableForADay_AndRotates()
        {
            var a = Shop.Featured(20261004, Items());
            Assert.AreEqual(Shop.FeaturedCount, a.Count);
            CollectionAssert.AreEqual(a, Shop.Featured(20261004, Items()));
            CollectionAssert.DoesNotContain(a, "free");
            CollectionAssert.DoesNotContain(a, "pack_item");
            bool changed = false;
            for (int d = 5; d < 12 && !changed; d++)
                changed = !Shop.Featured(20261000 + d, Items()).SequenceEqualTo(a);
            Assert.IsTrue(changed, "the shelf changes from day to day");

            var reversed = Items();
            reversed.Reverse();
            CollectionAssert.AreEqual(a, Shop.Featured(20261004, reversed), "content order doesn't matter");
        }

        [Test]
        public void Deal_IsAQuarterOff_TheFirstFeaturedItem()
        {
            var f = new List<string> { "a", "b" };
            Assert.AreEqual(30, Shop.PriceToday("a", 40, f));
            Assert.AreEqual(40, Shop.PriceToday("b", 40, f));
            Assert.AreEqual(1, Shop.DealPrice(1));
            Assert.AreEqual(0, Shop.DealPrice(0));
        }

        [Test]
        public void Packs_AreUnique_AndFindable()
        {
            var ids = new HashSet<string>();
            var items = new HashSet<string>();
            foreach (var p in Shop.Packs)
            {
                Assert.IsTrue(ids.Add(p.ProductId));
                StringAssert.StartsWith("com.omariibell.retrosk8.pack.", p.ProductId);
                Assert.AreSame(p, Shop.FindByProduct(p.ProductId));
                Assert.AreSame(p, Shop.FindPack(p.Id));
                foreach (var i in p.ItemIds)
                {
                    Assert.IsTrue(items.Add(i), "an item is in one pack only: " + i);
                    Assert.AreSame(p, Shop.PackFor(i));
                }
            }
            Assert.IsNull(Shop.PackFor("deck_harbor_tape"));
            Assert.AreEqual(Shop.Packs.Length, Shop.ProductIds().Length);
        }

        [Test]
        public void StoreEvents_Parse()
        {
            Assert.IsTrue(StoreEvent.TryParse("purchased|com.x.pack|", out var e));
            Assert.AreEqual(StoreEventKind.Purchased, e.Kind);
            Assert.AreEqual("com.x.pack", e.ProductId);
            Assert.IsTrue(e.Grants);
            Assert.IsTrue(StoreEvent.TryParse("failed|com.x.pack|Card declined | try again", out e));
            Assert.AreEqual("Card declined | try again", e.Message, "the message keeps any later separators");
            Assert.IsFalse(e.Grants);
            Assert.IsTrue(StoreEvent.TryParse("restoreDone||", out e));
            Assert.AreEqual(StoreEventKind.RestoreFinished, e.Kind);
            Assert.IsFalse(StoreEvent.TryParse("", out _));
            Assert.IsFalse(StoreEvent.TryParse("nonsense|x|y", out _));
        }

        [Test]
        public void PackItems_CantBeBoughtWithTokens()
        {
            Assert.AreEqual(PurchaseResult.Ok, ShopRules.Check(100, 40, false));
            Assert.AreEqual(4, (int)PurchaseResult.PackOnly);
        }
    }

    internal static class ListExtensions
    {
        public static bool SequenceEqualTo(this List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
