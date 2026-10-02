using System.IO;
using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class ReplayTests
    {
        private static ReplayFrame F(float t, float x, float yaw01 = 0f) => new ReplayFrame
        {
            Time = t,
            Position = new RVec3(x, 0f, 0f),
            Rotation = new RQuat(0f, yaw01, 0f, 1f - yaw01),
            Pose = RQuat.Identity,
            Body = RQuat.Identity,
            BoardPosition = new RVec3(0f, 0.12f, 0f),
            Board = RQuat.Identity,
        };

        [Test]
        public void Sample_Interpolates_AndClamps()
        {
            var track = new ReplayTrack();
            track.Add(F(0f, 0f));
            track.Add(F(1f, 10f));
            track.Add(F(2f, 30f));

            Assert.IsTrue(track.Sample(0.5f, out var mid));
            Assert.AreEqual(5f, mid.Position.X, 1e-4f);
            Assert.IsTrue(track.Sample(1.5f, out var mid2));
            Assert.AreEqual(20f, mid2.Position.X, 1e-4f);
            Assert.IsTrue(track.Sample(-3f, out var before));
            Assert.AreEqual(0f, before.Position.X, 1e-4f);
            Assert.IsTrue(track.Sample(99f, out var after));
            Assert.AreEqual(30f, after.Position.X, 1e-4f);
            Assert.AreEqual(2f, track.Duration, 1e-4f);
        }

        [Test]
        public void EmptyTrack_DoesNotSample()
        {
            Assert.IsFalse(new ReplayTrack().Sample(1f, out _));
        }

        [Test]
        public void Add_RejectsOutOfOrderFrames()
        {
            var track = new ReplayTrack();
            Assert.IsTrue(track.Add(F(1f, 0f)));
            Assert.IsFalse(track.Add(F(1f, 1f)));
            Assert.IsFalse(track.Add(F(0.5f, 1f)));
            Assert.AreEqual(1, track.Count);
        }

        [Test]
        public void IsDue_FollowsSampleRate()
        {
            var track = new ReplayTrack(20f);
            Assert.IsTrue(track.IsDue(0f));
            track.Add(F(0f, 0f));
            Assert.IsFalse(track.IsDue(0.02f));
            Assert.IsTrue(track.IsDue(0.05f));
        }

        [Test]
        public void Nlerp_TakesShortArc_AndStaysUnit()
        {
            var a = RQuat.Identity;
            var b = new RQuat(0f, 0f, 0f, -1f); // same rotation, opposite sign
            var q = RQuat.Nlerp(a, b, 0.5f);
            Assert.AreEqual(1f, q.W, 1e-4f, "should not collapse through zero");
            var c = RQuat.Nlerp(new RQuat(0f, 0.7071f, 0f, 0.7071f), RQuat.Identity, 0.3f);
            float len = c.X * c.X + c.Y * c.Y + c.Z * c.Z + c.W * c.W;
            Assert.AreEqual(1f, len, 1e-4f);
        }

        [Test]
        public void Codec_RoundTrips()
        {
            var track = new ReplayTrack(20f) { LocationId = "rooftop_run", Score = 123456 };
            for (int i = 0; i < 50; i++) track.Add(F(i * 0.05f, i, 0.01f * i));

            var ms = new MemoryStream();
            ReplayCodec.Write(track, ms);
            ms.Position = 0;
            var back = ReplayCodec.Read(ms);

            Assert.IsNotNull(back);
            Assert.AreEqual("rooftop_run", back.LocationId);
            Assert.AreEqual(123456, back.Score);
            Assert.AreEqual(50, back.Count);
            Assert.AreEqual(track.SampleInterval, back.SampleInterval, 1e-5f);
            Assert.AreEqual(track.Frames[49].Position.X, back.Frames[49].Position.X, 1e-5f);
            Assert.AreEqual(track.Frames[20].Rotation.Y, back.Frames[20].Rotation.Y, 1e-5f);
        }

        [Test]
        public void Codec_RejectsGarbage_AndTruncation()
        {
            Assert.IsNull(ReplayCodec.Read(new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })));
            Assert.IsNull(ReplayCodec.Read(new MemoryStream()));

            var track = new ReplayTrack { LocationId = "harbor_plaza", Score = 1 };
            for (int i = 0; i < 10; i++) track.Add(F(i * 0.05f, i));
            var ms = new MemoryStream();
            ReplayCodec.Write(track, ms);
            var bytes = ms.ToArray();
            var cut = new byte[bytes.Length - 7];
            System.Array.Copy(bytes, cut, cut.Length);
            Assert.IsNull(ReplayCodec.Read(new MemoryStream(cut)));
        }
    }

    public class FrameGovernorTests
    {
        [Test]
        public void SlowFrames_StepDown_ToFloor()
        {
            var g = new FrameGovernor();
            for (int i = 0; i < 600; i++) g.Tick(1f / 40f);
            Assert.AreEqual(g.MinScale, g.Scale, 1e-4f);
        }

        [Test]
        public void OnBudget_StaysPut()
        {
            var g = new FrameGovernor();
            for (int i = 0; i < 600; i++) g.Tick(1f / 60f);
            Assert.AreEqual(1f, g.Scale, 1e-4f);
        }

        [Test]
        public void Headroom_StepsBackUp_Slowly()
        {
            var g = new FrameGovernor(0.7f);
            int changes = 0;
            for (int i = 0; i < 60 * 6; i++) if (g.Tick(1f / 100f)) changes++; // 10 ms frames for ~3.6 s
            Assert.AreEqual(0, changes, "needs the full up-window before rising");
            for (int i = 0; i < 100 * 30; i++) g.Tick(1f / 100f);
            Assert.AreEqual(1f, g.Scale, 1e-4f);
        }

        [Test]
        public void StepUp_CanBeDisabled()
        {
            var g = new FrameGovernor(0.8f) { AllowStepUp = false };
            for (int i = 0; i < 100 * 30; i++) g.Tick(1f / 100f);
            Assert.AreEqual(0.8f, g.Scale, 1e-4f);
        }

        [Test]
        public void IgnoresHitches()
        {
            var g = new FrameGovernor();
            for (int i = 0; i < 20; i++) Assert.IsFalse(g.Tick(2f));
            Assert.IsFalse(g.Tick(0f));
            Assert.AreEqual(1f, g.Scale, 1e-4f);
        }
    }
}
