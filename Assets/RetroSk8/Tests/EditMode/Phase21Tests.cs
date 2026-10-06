using System;
using System.Linq;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    /// <summary>Phase 21: Off-Season Rink content, the story retry message fix.</summary>
    public class OffseasonRinkTests
    {
        [Test]
        public void Rink_IsShareable_AtTheEndOfTheList()
        {
            Assert.AreEqual(8, Array.IndexOf(ShareCodes.BuiltInParks, "offseason_rink"), "appended, so old codes keep their parks");
            Assert.AreEqual(7, Array.IndexOf(ShareCodes.BuiltInParks, "drive_in"));
            Assert.Less(ShareCodes.BuiltInParks.Length, 16, "park indexes are written in 4 bits");
        }

        [Test]
        public void RinkChallenge_RoundTrips()
        {
            var c = new ScoreChallenge { LocationId = "offseason_rink", Target = 42000, From = "OMARI" };
            string code = ShareCodes.EncodeChallenge(c);
            Assert.IsTrue(ShareCodes.TryDecodeChallenge(code, out var back, out string error), error);
            Assert.AreEqual("offseason_rink", back.LocationId);
            Assert.AreEqual(42000, back.Target);
            Assert.AreEqual("OMARI", back.From);
        }

        [Test]
        public void RinkSong_IsAFullLoop_BoundedAndNotSilent()
        {
            const int rate = 22050;
            var spec = MusicComposer.Rink;
            var data = MusicComposer.Render(spec, rate);
            Assert.AreEqual(spec.Progression.Length * 4f * 60f / spec.Bpm * rate, data.Length, 1.01f);
            double energy = 0;
            foreach (float v in data)
            {
                Assert.IsFalse(float.IsNaN(v));
                Assert.IsTrue(v <= 1f && v >= -1f, "clipped");
                energy += v * v;
            }
            Assert.Greater((float)(energy / data.Length), 1e-4f);
            Assert.AreNotEqual(MusicComposer.DriveIn.Seed, spec.Seed, "its own tune");
        }
    }

    public class StoryRetryMessageTests
    {
        private static StoryStep Bonky() => Story.Chapters.First(c => c.Id == "double_feature").Steps[0];

        [Test]
        public void RunsAtOtherParks_SayNothing()
        {
            var s = Bonky();
            Assert.IsNull(Story.RetryMessage(s, "harbor_plaza", 0, 0), "a run somewhere else isn't a failed story attempt");
            Assert.IsNull(Story.RetryMessage(s, "offseason_rink", s.Target + 1, 99));
        }

        [Test]
        public void ShortOnBonks_SaysHowMany()
        {
            var s = Bonky();
            string m = Story.RetryMessage(s, s.LocationId, s.Target + 1, 1);
            StringAssert.Contains($"1/{s.Bonks} BONKS", m);
        }

        [Test]
        public void LowScore_InALineBattle_NamesTheRival()
        {
            var s = Bonky();
            StringAssert.Contains(s.Rival, Story.RetryMessage(s, s.LocationId, 10, s.Bonks));
        }

        [Test]
        public void ClearedRun_OrASkateStep_SaysNothing()
        {
            var s = Bonky();
            Assert.IsNull(Story.RetryMessage(s, s.LocationId, s.Target + 1, s.Bonks));
            var skate = Story.AllSteps().FirstOrDefault(x => x.Objective == StoryObjective.Skate);
            if (skate != null) Assert.IsNull(Story.RetryMessage(skate, skate.LocationId, 0, 0));
            Assert.IsNull(Story.RetryMessage(null, "drive_in", 0, 0));
        }

        [Test]
        public void ScoreSteps_AskForMorePoints()
        {
            var s = Story.AllSteps().FirstOrDefault(x => x.Objective == StoryObjective.ScoreRun);
            Assert.IsNotNull(s, "the story has plain score steps");
            StringAssert.Contains("NEED MORE THAN", Story.RetryMessage(s, s.LocationId, 0, 0));
        }
    }
}
