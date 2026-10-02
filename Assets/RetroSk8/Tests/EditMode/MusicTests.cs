using NUnit.Framework;
using RetroSk8.Core;

namespace RetroSk8.Tests
{
    public class MusicTests
    {
        private const int Rate = 22050;

        [Test]
        public void Loop_HasExactBarLength()
        {
            var spec = MusicComposer.Harbor;
            var data = MusicComposer.Render(spec, Rate);
            float expected = spec.Progression.Length * 4f * 60f / spec.Bpm * Rate;
            Assert.AreEqual(expected, data.Length, 1.01f);
        }

        [Test]
        public void Output_IsBounded_AndNotSilent()
        {
            foreach (var spec in new[] { MusicComposer.Menu, MusicComposer.Harbor, MusicComposer.Warehouse, MusicComposer.Rooftop })
            {
                var data = MusicComposer.Render(spec, Rate);
                double energy = 0;
                foreach (float v in data)
                {
                    Assert.IsFalse(float.IsNaN(v), spec.Name + " produced NaN");
                    Assert.IsTrue(v <= 1f && v >= -1f, spec.Name + " clipped");
                    energy += v * v;
                }
                Assert.Greater((float)(energy / data.Length), 1e-4f, spec.Name + " is silent");
            }
        }

        [Test]
        public void SameSpec_SameAudio()
        {
            var a = MusicComposer.Render(MusicComposer.Rooftop, Rate);
            var b = MusicComposer.Render(MusicComposer.Rooftop, Rate);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i += 997) Assert.AreEqual(a[i], b[i], 0f);
        }

        [Test]
        public void Parks_SoundDifferent()
        {
            var harbor = MusicComposer.Render(MusicComposer.Harbor, Rate);
            var warehouse = MusicComposer.Render(MusicComposer.Warehouse, Rate);
            Assert.AreNotEqual(harbor.Length, warehouse.Length, "different tempos give different loop lengths");
            var reseeded = MusicComposer.Harbor;
            reseeded.Seed = 999;
            var other = MusicComposer.Render(reseeded, Rate);
            int differ = 0;
            for (int i = 0; i < harbor.Length; i += 101) if (System.Math.Abs(harbor[i] - other[i]) > 1e-3f) differ++;
            Assert.Greater(differ, 100, "the seed changes the melody and bass");
        }

        [Test]
        public void Loop_IsSeamless()
        {
            // The jump from the last sample back to the first should be no bigger than ordinary sample-to-sample motion.
            var data = MusicComposer.Render(MusicComposer.Warehouse, Rate);
            float wrap = System.Math.Abs(data[0] - data[data.Length - 1]);
            float maxStep = 0f;
            for (int i = 1; i < data.Length; i++) maxStep = System.Math.Max(maxStep, System.Math.Abs(data[i] - data[i - 1]));
            Assert.IsTrue(wrap <= maxStep + 1e-4f, $"wrap {wrap} vs max step {maxStep}");
        }
    }
}
