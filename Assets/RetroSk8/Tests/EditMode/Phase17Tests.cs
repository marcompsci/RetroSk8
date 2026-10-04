using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class TrickLessonTests
    {
        [Test]
        public void Lessons_HaveUniqueIds_StepsAndAPark()
        {
            var ids = new HashSet<string>();
            foreach (var l in TrickLessons.All)
            {
                Assert.IsTrue(ids.Add(l.Id), "duplicate " + l.Id);
                Assert.IsTrue(l.AnySpecial || l.TrickIds.Length > 0, l.Id);
                Assert.GreaterOrEqual(l.Steps.Length, 2, l.Id);
                Assert.Greater(l.Count, 0, l.Id);
                Assert.GreaterOrEqual(Array.IndexOf(ShareCodes.BuiltInParks, l.ParkId), 0, l.Id + " park");
            }
        }

        [Test]
        public void EveryAdvancedTrick_HasALesson()
        {
            foreach (StickZone z in Enum.GetValues(typeof(StickZone)))
                Assert.IsNotNull(TrickLessons.For(LipRules.IdFor(z), false), "lip " + z);
            foreach (var id in new[] { "wallride", "wallplant", "wallie", StyleTricks.FootplantId, RevertRules.Id,
                         StyleTricks.Bluntslide.Id, StyleTricks.OneFootManual.Id, StyleTricks.Casper.Id,
                         StyleTricks.NoComply.Id, StyleTricks.Boneless.Id, "spin_360", "spin_720" })
                Assert.IsNotNull(TrickLessons.For(id, false), id);
            foreach (var s in StyleTricks.Signatures) Assert.AreEqual("specials", TrickLessons.For(s.Id, true).Id);
            Assert.IsNull(TrickLessons.For("kickflip_basic", false), "basic tricks use the basics lesson");
            Assert.IsNull(TrickLessons.For("", false));
        }

        [Test]
        public void Matches_CountsOneRepPerLine()
        {
            var lips = TrickLessons.Find("lips");
            Assert.AreEqual(1, TrickLessons.Matches(lips, new[] { "kick", "lip_1", "lip_2" }, null));
            Assert.AreEqual(0, TrickLessons.Matches(lips, new[] { "kick", "revert" }, null));
            var specials = TrickLessons.Find("specials");
            Assert.AreEqual(1, TrickLessons.Matches(specials, new[] { "kick", "sig_vert" }, id => id.StartsWith("sig_")));
            Assert.AreEqual(0, TrickLessons.Matches(specials, new[] { "kick" }, id => false));
            Assert.AreEqual(0, TrickLessons.Matches(null, new[] { "lip_1" }, null));
        }

        [Test]
        public void Steps_FollowProgress_AndStopAtTheLast()
        {
            var l = TrickLessons.Find("walls");
            Assert.AreEqual(0, TrickLessons.StepFor(l, 0));
            Assert.AreEqual(1, TrickLessons.StepFor(l, 1));
            Assert.AreEqual(l.Steps.Length - 1, TrickLessons.StepFor(l, 99));
        }

        [Test]
        public void Complete_PaysOnce()
        {
            var s = new LessonState();
            var l = TrickLessons.Find("spins");
            Assert.AreEqual(l.Tokens, TrickLessons.Complete(s, l));
            Assert.AreEqual(0, TrickLessons.Complete(s, l));
            Assert.IsTrue(s.IsDone("spins"));
            var old = new LessonState { done = null };
            old.Sanitize();
            Assert.IsNotNull(old.done);
        }
    }

    public class SkaterShapeTests
    {
        [Test]
        public void EveryOutline_FitsTheCapsuleBox_AndCloses()
        {
            foreach (var p in SkaterShapes.All) Assert.IsTrue(SkaterShapes.Valid(p), p.Name);
        }

        [Test]
        public void VertexCount_PolesAreSinglePoints()
        {
            // Limb: 8 points, 2 poles, 6 rings of 14.
            Assert.AreEqual(2 + 6 * 14, SkaterShapes.VertexCount(SkaterShapes.Limb, 14));
        }

        [Test]
        public void Valid_RejectsBrokenOutlines()
        {
            Assert.IsFalse(SkaterShapes.Valid(null));
            Assert.IsFalse(SkaterShapes.Valid(new SkaterShapes.Profile { Y = new[] { 1f, 0f, -1f }, R = new[] { 0f, 0.6f, 0f } }), "too wide");
            Assert.IsFalse(SkaterShapes.Valid(new SkaterShapes.Profile { Y = new[] { 1f, 0f, -1f }, R = new[] { 0.2f, 0.4f, 0f } }), "open top");
            Assert.IsFalse(SkaterShapes.Valid(new SkaterShapes.Profile { Y = new[] { -1f, 0f, 1f }, R = new[] { 0f, 0.4f, 0f } }), "upside down");
        }

        [Test]
        public void Limbs_TaperTowardTheJointBelow()
        {
            var l = SkaterShapes.Limb;
            Assert.Greater(l.R[2], l.R[l.R.Length - 3]);
        }
    }
}
