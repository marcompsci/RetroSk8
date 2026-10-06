using System;

namespace RetroSk8.Core
{
    /// <summary>
    /// Frame statistics for one play session (Phase 18 on-device profiling): average and 95th-percentile frame
    /// time, the worst frame, hitches (frames over <see cref="HitchMs"/>), and managed garbage per frame when the
    /// build can measure it. Allocation-free while recording (a fixed histogram), engine-free and unit-tested.
    /// </summary>
    public sealed class PerfStats
    {
        public const float HitchMs = 50f;
        private const int Buckets = 200; // 0.5 ms buckets up to 100 ms; slower frames land in the last one

        private readonly int[] _histogram = new int[Buckets];
        public int Frames { get; private set; }
        public double TotalMs { get; private set; }
        public float WorstMs { get; private set; }
        public int Hitches { get; private set; }
        public long GcBytes { get; private set; }
        public int GcFrames { get; private set; }
        /// <summary>False when the build can't count allocations (Release players).</summary>
        public bool GcMeasured { get; private set; }

        public void Reset()
        {
            Array.Clear(_histogram, 0, Buckets);
            Frames = 0; TotalMs = 0; WorstMs = 0; Hitches = 0; GcBytes = 0; GcFrames = 0; GcMeasured = false;
        }

        /// <param name="frameMs">This frame's time in milliseconds.</param>
        /// <param name="gcBytes">Managed bytes allocated this frame, or a negative number when unknown.</param>
        public void Add(float frameMs, long gcBytes)
        {
            if (frameMs <= 0f || float.IsNaN(frameMs)) return;
            Frames++;
            TotalMs += frameMs;
            if (frameMs > WorstMs) WorstMs = frameMs;
            if (frameMs > HitchMs) Hitches++;
            int b = (int)(frameMs * 2f);
            _histogram[b < 0 ? 0 : b >= Buckets ? Buckets - 1 : b]++;
            if (gcBytes >= 0)
            {
                GcMeasured = true;
                GcBytes += gcBytes;
                if (gcBytes > 0) GcFrames++;
            }
        }

        public float AverageMs => Frames == 0 ? 0f : (float)(TotalMs / Frames);
        public float AverageFps => AverageMs <= 0f ? 0f : 1000f / AverageMs;
        public float GcBytesPerFrame => Frames == 0 || !GcMeasured ? 0f : (float)GcBytes / Frames;

        /// <summary>Frame time that <paramref name="fraction"/> of frames were at or under (bucket upper edge, ms).</summary>
        public float Percentile(float fraction)
        {
            if (Frames == 0) return 0f;
            int need = (int)Math.Ceiling(Frames * Math.Max(0f, Math.Min(1f, fraction)));
            int seen = 0;
            for (int i = 0; i < Buckets; i++)
            {
                seen += _histogram[i];
                if (seen >= need) return Math.Min((i + 1) * 0.5f, WorstMs);
            }
            return WorstMs;
        }

        /// <summary>One log line: "drive_in  1800 frames  avg 16.7 ms (60 fps)  p95 17.5  worst 41.0  hitches 0  gc 120 B/frame  load 1.4 s".</summary>
        public string Summary(string scene, float loadSeconds)
        {
            string gc = GcMeasured ? $"gc {GcBytesPerFrame:0} B/frame ({GcFrames} frames allocated)" : "gc n/a (Development build only)";
            string load = loadSeconds > 0f ? $"  load {loadSeconds:0.0} s" : "";
            return $"{scene}  {Frames} frames  avg {AverageMs:0.0} ms ({AverageFps:0} fps)  p95 {Percentile(0.95f):0.0}  worst {WorstMs:0.0}  hitches {Hitches}  {gc}{load}";
        }

        /// <summary>A plain-English verdict for the summary (what to look at first).</summary>
        public string Verdict()
        {
            if (Frames < 60) return "TOO SHORT TO JUDGE";
            if (Percentile(0.95f) > 20f) return "SLOW: frames often miss 60 fps";
            if (Hitches > Frames / 300) return "HITCHY: occasional long frames";
            if (GcMeasured && GcBytesPerFrame > 1024f) return "GARBAGE: allocating every frame";
            return "SMOOTH";
        }
    }
}
