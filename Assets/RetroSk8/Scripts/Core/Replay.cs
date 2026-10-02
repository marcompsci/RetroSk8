using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RetroSk8.Core
{
    /// <summary>Engine-free 3D vector used by replays (Unity's Vector3 lives in the Runtime layer).</summary>
    public struct RVec3
    {
        public float X, Y, Z;
        public RVec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static RVec3 Lerp(RVec3 a, RVec3 b, float t) =>
            new RVec3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
    }

    /// <summary>Engine-free quaternion used by replays.</summary>
    public struct RQuat
    {
        public float X, Y, Z, W;
        public RQuat(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
        public static RQuat Identity => new RQuat(0f, 0f, 0f, 1f);

        /// <summary>Normalized lerp along the shorter arc. Plenty for 20 Hz samples.</summary>
        public static RQuat Nlerp(RQuat a, RQuat b, float t)
        {
            float dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
            float s = dot < 0f ? -1f : 1f;
            var q = new RQuat(
                a.X + (s * b.X - a.X) * t,
                a.Y + (s * b.Y - a.Y) * t,
                a.Z + (s * b.Z - a.Z) * t,
                a.W + (s * b.W - a.W) * t);
            float len = (float)Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
            if (len < 1e-6f) return Identity;
            return new RQuat(q.X / len, q.Y / len, q.Z / len, q.W / len);
        }
    }

    /// <summary>One sampled skater pose: root transform plus the three visual joints (whole-body pose, body, board).</summary>
    public struct ReplayFrame
    {
        public float Time;
        public RVec3 Position;
        public RQuat Rotation;
        public RQuat Pose;
        public RQuat Body;
        public RVec3 BoardPosition;
        public RQuat Board;

        public static ReplayFrame Lerp(ReplayFrame a, ReplayFrame b, float t) => new ReplayFrame
        {
            Time = a.Time + (b.Time - a.Time) * t,
            Position = RVec3.Lerp(a.Position, b.Position, t),
            Rotation = RQuat.Nlerp(a.Rotation, b.Rotation, t),
            Pose = RQuat.Nlerp(a.Pose, b.Pose, t),
            Body = RQuat.Nlerp(a.Body, b.Body, t),
            BoardPosition = RVec3.Lerp(a.BoardPosition, b.BoardPosition, t),
            Board = RQuat.Nlerp(a.Board, b.Board, t),
        };
    }

    /// <summary>
    /// A recorded run: frames sampled at a fixed rate against run time (pauses excluded).
    /// Used for the best-run ghost. Sampling between frames interpolates; outside the range it clamps.
    /// </summary>
    public sealed class ReplayTrack
    {
        public const float DefaultSampleRate = 20f;
        /// <summary>Hard cap so a forgotten Free Skate session can't grow the file without bound (10 minutes at 20 Hz).</summary>
        public const int MaxFrames = 12000;

        private readonly List<ReplayFrame> _frames = new List<ReplayFrame>();

        public string LocationId = "";
        public long Score;
        public float SampleInterval { get; }

        public ReplayTrack(float sampleRate = DefaultSampleRate)
        {
            SampleInterval = 1f / Math.Max(1f, sampleRate);
        }

        public int Count => _frames.Count;
        public IReadOnlyList<ReplayFrame> Frames => _frames;
        public float Duration => _frames.Count == 0 ? 0f : _frames[_frames.Count - 1].Time;

        /// <summary>True when enough run time has passed since the last frame that a new sample is due.</summary>
        public bool IsDue(float time) =>
            _frames.Count == 0 || time - _frames[_frames.Count - 1].Time >= SampleInterval - 1e-4f;

        /// <summary>Appends a frame. Frames must move forward in time; out-of-order or over-cap frames are ignored.</summary>
        public bool Add(ReplayFrame frame)
        {
            if (_frames.Count >= MaxFrames) return false;
            if (_frames.Count > 0 && frame.Time <= _frames[_frames.Count - 1].Time) return false;
            _frames.Add(frame);
            return true;
        }

        public void Clear() => _frames.Clear();

        public bool Sample(float time, out ReplayFrame frame)
        {
            frame = default;
            int n = _frames.Count;
            if (n == 0) return false;
            if (time <= _frames[0].Time) { frame = _frames[0]; return true; }
            if (time >= _frames[n - 1].Time) { frame = _frames[n - 1]; return true; }

            // Binary search for the last frame at or before `time`.
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_frames[mid].Time <= time) lo = mid; else hi = mid;
            }
            var a = _frames[lo];
            var b = _frames[hi];
            float span = b.Time - a.Time;
            frame = ReplayFrame.Lerp(a, b, span > 1e-6f ? (time - a.Time) / span : 0f);
            return true;
        }
    }

    /// <summary>Compact binary ghost format. Version-tagged so later phases can extend it.</summary>
    public static class ReplayCodec
    {
        private const uint Magic = 0x47384B52; // "RK8G"
        public const int Version = 1;

        public static void Write(ReplayTrack track, Stream stream)
        {
            using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                w.Write(Magic);
                w.Write(Version);
                w.Write(track.LocationId ?? "");
                w.Write(track.Score);
                w.Write(1f / track.SampleInterval);
                w.Write(track.Count);
                foreach (var f in track.Frames)
                {
                    w.Write(f.Time);
                    Write(w, f.Position);
                    Write(w, f.Rotation);
                    Write(w, f.Pose);
                    Write(w, f.Body);
                    Write(w, f.BoardPosition);
                    Write(w, f.Board);
                }
            }
        }

        /// <summary>Reads a ghost. Returns null for anything that isn't a valid ghost (wrong magic, newer version, truncated).</summary>
        public static ReplayTrack Read(Stream stream)
        {
            try
            {
                using (var r = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    if (r.ReadUInt32() != Magic) return null;
                    int version = r.ReadInt32();
                    if (version < 1 || version > Version) return null;
                    string location = r.ReadString();
                    long score = r.ReadInt64();
                    float rate = r.ReadSingle();
                    int count = r.ReadInt32();
                    if (count < 0 || count > ReplayTrack.MaxFrames || rate <= 0f || float.IsNaN(rate)) return null;

                    var track = new ReplayTrack(rate) { LocationId = location, Score = score };
                    for (int i = 0; i < count; i++)
                    {
                        var f = new ReplayFrame
                        {
                            Time = r.ReadSingle(),
                            Position = ReadV(r),
                            Rotation = ReadQ(r),
                            Pose = ReadQ(r),
                            Body = ReadQ(r),
                            BoardPosition = ReadV(r),
                            Board = ReadQ(r),
                        };
                        track.Add(f);
                    }
                    return track;
                }
            }
            catch (EndOfStreamException) { return null; }
            catch (IOException) { return null; }
        }

        private static void Write(BinaryWriter w, RVec3 v) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }
        private static void Write(BinaryWriter w, RQuat q) { w.Write(q.X); w.Write(q.Y); w.Write(q.Z); w.Write(q.W); }
        private static RVec3 ReadV(BinaryReader r) => new RVec3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        private static RQuat ReadQ(BinaryReader r) => new RQuat(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }

    /// <summary>
    /// Keeps the game at its target frame rate on device by trading resolution for speed.
    /// Feed it the frame time every frame; it lowers the render scale quickly when frames run long
    /// and raises it slowly once there is headroom. Engine-free so it is unit-tested.
    /// </summary>
    public sealed class FrameGovernor
    {
        public float TargetFps = 60f;
        public float MinScale = 0.7f;
        public float MaxScale = 1f;
        public float StepDown = 0.1f;
        public float StepUp = 0.05f;
        /// <summary>Seconds of slow frames before stepping down.</summary>
        public float DownWindow = 1.5f;
        /// <summary>Seconds of fast frames before stepping up.</summary>
        public float UpWindow = 5f;
        /// <summary>Off when only capped frame times are available (they can't show headroom).</summary>
        public bool AllowStepUp = true;

        private float _avg;
        private float _slowFor;
        private float _fastFor;
        private bool _primed;

        public float Scale { get; private set; }
        public float AverageFrameTime => _avg;
        public float AverageFps => _avg > 1e-5f ? 1f / _avg : 0f;

        public FrameGovernor(float startScale = 1f) { Scale = startScale; }

        /// <summary>Returns true when <see cref="Scale"/> changed this frame.</summary>
        public bool Tick(float frameTime)
        {
            if (frameTime <= 0f || frameTime > 0.5f) return false; // pauses, hitches and scene loads aren't representative
            _avg = _primed ? _avg + (frameTime - _avg) * 0.1f : frameTime;
            _primed = true;

            float budget = 1f / TargetFps;
            if (_avg > budget * 1.08f) { _slowFor += frameTime; _fastFor = 0f; }
            else if (_avg < budget * 0.8f) { _fastFor += frameTime; _slowFor = 0f; }
            else { _slowFor = 0f; _fastFor = 0f; }

            if (_slowFor >= DownWindow && Scale > MinScale + 1e-4f)
            {
                Scale = Math.Max(MinScale, Scale - StepDown);
                _slowFor = 0f;
                return true;
            }
            if (AllowStepUp && _fastFor >= UpWindow && Scale < MaxScale - 1e-4f)
            {
                Scale = Math.Min(MaxScale, Scale + StepUp);
                _fastFor = 0f;
                return true;
            }
            return false;
        }
    }
}
