using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class SubwayDepotTests
    {
        [Test]
        public void SubwayDepot_IsShareable_AtTheEnd()
        {
            Assert.AreEqual(10, Array.IndexOf(ShareCodes.BuiltInParks, "subway_depot"));
            Assert.AreEqual(9, Array.IndexOf(ShareCodes.BuiltInParks, "shipyard"), "existing codes keep their index");
            Assert.Less(ShareCodes.BuiltInParks.Length, 16f, "4 bits of park index");
        }

        [Test]
        public void SubwaySong_IsAFullLoop()
        {
            var spec = MusicComposer.Subway;
            Assert.AreEqual("music_subway", spec.Name);
            var samples = MusicComposer.Render(spec, 8000);
            Assert.AreEqual(MusicComposer.LoopSamples(spec, 8000), samples.Length);
            Assert.Greater(samples.Max(x => Math.Abs(x)), 0.01f, "not silent");
            Assert.LessOrEqual(samples.Max(x => Math.Abs(x)), 1f, "no clipping");
        }

        [Test]
        public void ChallengeCodes_RoundTrip_ForTheNewPark()
        {
            var c = new ScoreChallenge { LocationId = "subway_depot", Target = 31337, From = "RIVET" };
            string code = ShareCodes.EncodeChallenge(c);
            Assert.IsTrue(ShareCodes.TryDecodeChallenge(code, out var back, out var error), error);
            Assert.AreEqual("subway_depot", back.LocationId);
            Assert.AreEqual(31337L, back.Target);
        }
    }

    public class CustomTrickTests
    {
        private static CustomTrick T(int flip, int shove, int grab, int body, int gesture = 0, int slot = 1) =>
            new CustomTrick { slot = slot, name = "TEST", flip = flip, shove = shove, grab = grab, body = body, gesture = gesture };

        [Test]
        public void Shape_NeverOutScoresTheBuiltInTricks()
        {
            // A kick roll alone does what the Tide Flip does (0.42 s, 400 points) and pays a little less.
            var kick = CustomTricks.Shape(T((int)TrickFlip.Kick, 0, 0, 0));
            Assert.AreEqual(1f, kick.RollTurns, 0.0001f);
            Assert.AreEqual(0.42f, kick.Duration, 0.0001f);
            Assert.AreEqual(360, kick.Points, "90% of the Tide Flip's 400");
            Assert.Less(CustomTricks.Shape(T(0, 0, (int)TrickGrab.Mute, 0)).Points, 350f, "less than the Anchor Grab");
            Assert.Less(CustomTricks.Shape(T(0, (int)TrickShove.Half, 0, 0)).Points, 300f, "less than the Pivot Scoop");
            // Combining parts pays less than doing them as separate tricks (which also feeds the multiplier)...
            var combo = CustomTricks.Shape(T((int)TrickFlip.Kick, (int)TrickShove.Full, 0, 0));
            Assert.Less(combo.Points, 400f + 500f, "less than a Tide Flip plus a Gyre Scoop");
            // ...but more parts still mean more airtime and more points.
            var big = CustomTricks.Shape(T((int)TrickFlip.DoubleKick, (int)TrickShove.Full, (int)TrickGrab.Mute, 0));
            Assert.Greater(big.Duration, combo.Duration);
            Assert.Greater(big.Points, combo.Points);
            Assert.AreEqual(TrickCategory.BoardFlip, big.Category);
            Assert.AreEqual(TrickCategory.Grab, CustomTricks.Shape(T(0, 0, (int)TrickGrab.Tail, 0)).Category, "a grab alone is a grab");
        }

        [Test]
        public void Shape_IsCapped_UnderASpecial()
        {
            var max = CustomTricks.Shape(T((int)TrickFlip.TripleKick, (int)TrickShove.OneAndHalf, (int)TrickGrab.Reach, (int)TrickBody.Double));
            Assert.AreEqual(CustomTricks.MaxDuration, max.Duration, 0.0001f);
            Assert.AreEqual(CustomTricks.MaxPoints, max.Points, "capped");
            Assert.Less(max.Points, 2000f, "never worth a special");
            // Body spins are whole turns, so the skater lands facing the way they rode in.
            Assert.AreEqual(0f, CustomTricks.Shape(T(0, 0, 0, (int)TrickBody.Full)).BodySpin % 360f, 0.0001f);
            Assert.AreEqual(720f, CustomTricks.Shape(T(0, 0, 0, (int)TrickBody.Double)).BodySpin, 0.0001f);
        }

        [Test]
        public void BadParts_CountAsNone()
        {
            var weird = T(99, -3, 42, 7);
            Assert.IsTrue(CustomTricks.IsEmpty(weird));
            Assert.AreEqual(0f, CustomTricks.Shape(weird).RollTurns, 0.0001f);
        }

        [Test]
        public void Gestures_MapToFamiliesAndVariations()
        {
            Assert.AreEqual(0, CustomTricks.Gesture(TrickFamily.Flip, 0));
            Assert.AreEqual(5, CustomTricks.Gesture(TrickFamily.Grab, 1));
            Assert.AreEqual(11, CustomTricks.Gesture(TrickFamily.Shove, 3));
            Assert.AreEqual(TrickFamily.Shove, CustomTricks.FamilyOf(10));
            Assert.AreEqual("SWIPE DOWN + STICK LEFT", CustomTricks.GestureName(5));
            Assert.AreEqual("OFF", CustomTricks.GestureName(-1));
            var set = new HashSet<string>();
            for (int g = 0; g < CustomTricks.GestureCount; g++) Assert.IsTrue(set.Add(CustomTricks.GestureName(g)), "unique name " + g);
        }

        [Test]
        public void Sanitize_KeepsOneTrickPerGesture_AndValidSlots()
        {
            var list = new List<CustomTrick>
            {
                T(1, 0, 0, 0, gesture: 3, slot: 2),
                T(2, 0, 0, 0, gesture: 3, slot: 1),
                T(1, 1, 0, 0, gesture: 4, slot: 1), // duplicate slot
                T(1, 0, 0, 0, gesture: 99, slot: 3),
                T(1, 0, 0, 0, gesture: 0, slot: 9),  // no such slot
                null,
            };
            CustomTricks.Sanitize(list);
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(3, list.First(t => t.slot == 1).gesture, "the lower slot keeps the gesture");
            Assert.AreEqual(-1, list.First(t => t.slot == 2).gesture);
            Assert.AreEqual(-1, list.First(t => t.slot == 3).gesture, "out of range switches it off");
            Assert.AreEqual(4, CustomTricks.FreeSlot(list));
            Assert.AreEqual(list.First(t => t.slot == 1), CustomTricks.ForGesture(list, 3));
            Assert.IsNull(CustomTricks.ForGesture(list, 7));
        }

        [Test]
        public void Names_AreSuggested_AndFiltered()
        {
            Assert.AreEqual("360 KICK MUTE", CustomTricks.SuggestName(T(1, 2, 3, 0)));
            Assert.LessOrEqual(CustomTricks.SuggestName(T(5, 3, 5, 2)).Length, CustomTricks.MaxNameLength);
            var list = new List<CustomTrick> { new CustomTrick { slot = 1, name = new string('X', 40), flip = 1 } };
            CustomTricks.Sanitize(list);
            Assert.LessOrEqual(list[0].name.Length, CustomTricks.MaxNameLength);
        }

        [Test]
        public void Codes_RoundTrip_WithoutSlotOrGesture()
        {
            var t = T((int)TrickFlip.Heel, (int)TrickShove.Half, (int)TrickGrab.Stale, (int)TrickBody.Full, gesture: 7, slot: 3);
            t.name = "SALT SPRAY";
            string code = CustomTricks.Encode(t, "OMARI");
            Assert.IsTrue(code.StartsWith(CustomTricks.CodePrefix));
            Assert.Less(code.Length, 60f, "short enough to type");
            Assert.IsTrue(CustomTricks.TryDecode("  " + code + "\n", out var back, out var from, out var error), error);
            Assert.AreEqual("SALT SPRAY", back.name);
            Assert.AreEqual("OMARI", from);
            Assert.AreEqual(t.flip, back.flip);
            Assert.AreEqual(t.shove, back.shove);
            Assert.AreEqual(t.grab, back.grab);
            Assert.AreEqual(t.body, back.body);
            Assert.AreEqual(-1, back.gesture, "the friend picks their own swipe");
        }

        [Test]
        public void BadCodes_AreRefused()
        {
            string code = CustomTricks.Encode(T(1, 0, 0, 0), "");
            Assert.IsFalse(CustomTricks.TryDecode(code.Substring(0, code.Length - 3), out _, out _, out _));
            Assert.IsFalse(CustomTricks.TryDecode("RG:" + code.Substring(3), out _, out _, out _));
            Assert.IsFalse(CustomTricks.TryDecode("", out _, out _, out _));
            Assert.Throws<ArgumentException>(() => CustomTricks.Encode(T(0, 0, 0, 0), ""));
        }

        [Test]
        public void TrickBook_SkipsCustomTricks_ButCountsThemInTheLine()
        {
            var book = new TrickBookState();
            book.Record(new List<string> { "flip_tide", CustomTricks.IdFor(1), "grab_anchor" }, 5000, "harbor_plaza");
            Assert.IsNull(book.Find(CustomTricks.IdFor(1)));
            Assert.AreEqual(3, book.Find("flip_tide").longestLine, "the custom trick still made the line longer");
        }
    }
}
