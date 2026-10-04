using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>A banked combo during a recorded run, so the replay can show what was landed and when.</summary>
    [Serializable]
    public sealed class ReplayMoment
    {
        public float time;
        public long points;
        public string label;
    }

    /// <summary>One saved replay (its frames live in a separate file named by <see cref="id"/>).</summary>
    [Serializable]
    public sealed class ReplayEntry
    {
        public string id;
        public string locationId;
        public string parkName;
        public string mode;
        public long score;
        public float duration;
        public long savedTicks;
        public bool favorite;
        public List<ReplayMoment> moments = new List<ReplayMoment>();

        /// <summary>The biggest banked line, for list titles.</summary>
        public ReplayMoment BestMoment()
        {
            ReplayMoment best = null;
            foreach (var m in moments) if (m != null && (best == null || m.points > best.points)) best = m;
            return best;
        }
    }

    /// <summary>
    /// The list of saved replays and the rule for what to keep: favorites stay (up to <see cref="MaxFavorites"/>),
    /// and only the newest <see cref="MaxRecent"/> others are kept. JsonUtility-friendly.
    /// </summary>
    [Serializable]
    public sealed class ReplayIndex
    {
        public const int MaxRecent = 6;
        public const int MaxFavorites = 20;
        /// <summary>Saving from Free Skate keeps the last stretch, not hours of cruising.</summary>
        public const float MaxSavedSeconds = 120f;

        public List<ReplayEntry> entries = new List<ReplayEntry>();

        /// <summary>Adds a replay (newest first) and returns the ids of replays that should now be deleted.</summary>
        public List<string> Add(ReplayEntry entry)
        {
            entries.RemoveAll(e => e == null || e.id == entry.id);
            entries.Insert(0, entry);
            return Trim();
        }

        public ReplayEntry Find(string id)
        {
            foreach (var e in entries) if (e != null && e.id == id) return e;
            return null;
        }

        /// <summary>Stars or un-stars a replay. Returns false when the favorites are full.</summary>
        public bool SetFavorite(string id, bool on)
        {
            var e = Find(id);
            if (e == null) return false;
            if (on && !e.favorite && FavoriteCount >= MaxFavorites) return false;
            e.favorite = on;
            return true;
        }

        public int FavoriteCount
        {
            get
            {
                int n = 0;
                foreach (var e in entries) if (e != null && e.favorite) n++;
                return n;
            }
        }

        public bool Remove(string id) => entries.RemoveAll(e => e != null && e.id == id) > 0;

        /// <summary>Drops the oldest non-favorites beyond the limit; returns the dropped ids.</summary>
        public List<string> Trim()
        {
            var dropped = new List<string>();
            int recent = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) { entries.RemoveAt(i--); continue; }
                if (e.favorite) continue;
                recent++;
                if (recent <= MaxRecent) continue;
                dropped.Add(e.id);
                entries.RemoveAt(i--);
            }
            return dropped;
        }

        /// <summary>Keeps only the last <paramref name="seconds"/> of a track's frames, re-timed to start at 0.</summary>
        public static List<ReplayFrame> LastSeconds(IReadOnlyList<ReplayFrame> frames, float seconds, out float cutAt)
        {
            var list = new List<ReplayFrame>();
            cutAt = 0f;
            if (frames == null || frames.Count == 0) return list;
            float end = frames[frames.Count - 1].Time;
            cutAt = Math.Max(0f, end - seconds);
            foreach (var f in frames)
            {
                if (f.Time < cutAt) continue;
                var c = f;
                c.Time -= cutAt;
                list.Add(c);
            }
            return list;
        }
    }

    /// <summary>Replay viewer camera styles.</summary>
    public enum ReplayCamera { Follow = 0, Fisheye = 1, Tripod = 2, Orbit = 3 }

    /// <summary>Playback clock for the replay viewer: play/pause, speed, scrubbing, and an in/out range for clips.</summary>
    public sealed class ReplayClock
    {
        public static readonly float[] Speeds = { 0.25f, 0.5f, 1f };

        public float Duration { get; }
        public float Time { get; private set; }
        public bool Playing { get; set; } = true;
        public int SpeedIndex { get; private set; } = 2;
        public float Speed => Speeds[SpeedIndex];
        public float In { get; private set; }
        public float Out { get; private set; }
        /// <summary>Loop back to the in point at the out point (off while exporting).</summary>
        public bool Loop { get; set; } = true;

        public ReplayClock(float duration)
        {
            Duration = Math.Max(0f, duration);
            Out = Duration;
        }

        /// <summary>Advances by real time; returns true when playback reached the out point this tick.</summary>
        public bool Tick(float dt)
        {
            if (!Playing || Duration <= 0f) return false;
            Time += dt * Speed;
            if (Time < Out) return false;
            if (Loop) Time = In;
            else { Time = Out; Playing = false; }
            return true;
        }

        public void Seek(float t) => Time = Clamp(t, 0f, Duration);
        public void SeekFraction(float f) => Seek(f * Duration);
        public void Skip(float seconds) => Seek(Time + seconds);
        public void CycleSpeed() => SpeedIndex = (SpeedIndex + 1) % Speeds.Length;
        public void SetSpeed(int index) => SpeedIndex = Math.Max(0, Math.Min(Speeds.Length - 1, index));

        /// <summary>Marks the clip start at the current time (moves the out point if it would come first).</summary>
        public void MarkIn()
        {
            In = Time;
            if (Out <= In) Out = Math.Min(Duration, In + 1f);
        }

        public void MarkOut()
        {
            Out = Math.Max(Time, Math.Min(Duration, In + 0.5f));
            if (Out <= In) In = Math.Max(0f, Out - 1f);
        }

        public void ClearMarks()
        {
            In = 0f;
            Out = Duration;
        }

        public float ClipLength => Out - In;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
