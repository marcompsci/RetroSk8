using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>What happened in one slam, measured by the runtime from the moment the skater bailed until they stopped.</summary>
    public struct SlamInput
    {
        /// <summary>Speed the instant before the bail (m/s).</summary>
        public float Speed;
        /// <summary>How far the skater dropped from the top of the air before hitting the ground (m).</summary>
        public float Drop;
        public float AirTime;
        /// <summary>Unfinished spin at the bail (degrees, any direction).</summary>
        public float Spin;
        /// <summary>How far the skater slid and tumbled after hitting the ground (m).</summary>
        public float Slide;
        /// <summary>True for a head-on hit (wall, rail, ledge); false for a bad landing.</summary>
        public bool HeadOn;
        /// <summary>Rolled off the map: never scores.</summary>
        public bool OutOfBounds;
    }

    public struct SlamPart
    {
        public string Name;
        public long Points;
    }

    public sealed class SlamResult
    {
        public long Points;
        public string Name;
        public readonly List<SlamPart> Parts = new List<SlamPart>();
        /// <summary>The chain multiplier this slam got from the slams just before it.</summary>
        public float Chain = 1f;
    }

    /// <summary>
    /// Phase 30: HALL OF MEAT, the bail mode players keep asking skate games to bring back. Every slam scores on how hard
    /// it was: speed, height dropped, air time, the spin you didn't finish, how far you tumbled, and a bonus for hitting
    /// something head-on. Slams close together chain for a bigger multiplier. A session is two minutes; your single
    /// biggest slam goes on its own Game Center board. Engine-free and unit-tested.
    /// </summary>
    public static class HallOfMeat
    {
        public const string LeaderboardId = "retrosk8.slam.best";

        public const int SpeedPoints = 900;      // per m/s
        public const int DropPoints = 2600;      // per metre
        public const int AirPoints = 1400;       // per second
        public const int SpinPoints = 700;       // per half turn not landed
        public const int SlidePoints = 450;      // per metre
        public const float HeadOnFactor = 1.5f;
        public const float ChainStep = 0.25f;
        public const float MaxChain = 3f;
        /// <summary>Slams within this many seconds of the last one keep the chain going.</summary>
        public const float ChainWindow = 8f;
        /// <summary>A slam under this isn't worth a slot on the board (a stumble, not a slam).</summary>
        public const long MinSlam = 500;

        public static readonly string[] Names =
        {
            "STUMBLE", "TUMBLE", "SLAM", "BIG SLAM", "BONE RATTLER", "GRAVEYARD", "HALL OF FAMER",
        };
        public static readonly long[] NameFloors = { 0, 2500, 6000, 12000, 25000, 50000, 100000 };

        public static string NameFor(long points)
        {
            int idx = 0;
            for (int i = 0; i < NameFloors.Length; i++) if (points >= NameFloors[i]) idx = i;
            return Names[idx];
        }

        public static float ChainFor(int slamsInChain) =>
            Math.Min(MaxChain, 1f + ChainStep * Math.Max(0, slamsInChain));

        /// <summary>Scores one slam. <paramref name="previousInChain"/> is how many slams came just before it within the window.</summary>
        public static SlamResult Score(SlamInput s, int previousInChain = 0)
        {
            var r = new SlamResult();
            if (s.OutOfBounds) { r.Name = "OUT OF BOUNDS"; return r; }
            Add(r, "SPEED", Clamp(s.Speed, 0f, ScoreLimits.MaxSpeed) * SpeedPoints);
            Add(r, "DROP", Clamp(s.Drop, 0f, 40f) * DropPoints);
            Add(r, "AIR", Clamp(s.AirTime, 0f, 8f) * AirPoints);
            Add(r, "SPIN", (float)Math.Floor(Clamp(Math.Abs(s.Spin), 0f, 1440f) / 180f) * SpinPoints);
            Add(r, "TUMBLE", Clamp(s.Slide, 0f, 60f) * SlidePoints);
            double sum = 0;
            foreach (var p in r.Parts) sum += p.Points;
            if (s.HeadOn) { r.Parts.Add(new SlamPart { Name = "HEAD-ON x1.5", Points = (long)Math.Floor(sum * (HeadOnFactor - 1f)) }); sum *= HeadOnFactor; }
            r.Chain = ChainFor(previousInChain);
            sum *= r.Chain;
            r.Points = Math.Min(ScoreLimits.MaxSlam, (long)Math.Floor(sum));
            r.Name = NameFor(r.Points);
            return r;
        }

        private static void Add(SlamResult r, string name, float value)
        {
            long p = (long)Math.Floor(value);
            if (p > 0) r.Parts.Add(new SlamPart { Name = name, Points = p });
        }

        private static float Clamp(float v, float min, float max) => float.IsNaN(v) ? min : v < min ? min : v > max ? max : v;
    }

    /// <summary>One Hall of Meat session: counts slams, keeps the chain and the best.</summary>
    public sealed class SlamSession
    {
        private float _lastSlamTime = float.NegativeInfinity;
        private int _chain;

        public long Total { get; private set; }
        public int Count { get; private set; }
        public SlamResult Best { get; private set; }
        public SlamResult Last { get; private set; }

        /// <summary>Records a slam that happened at <paramref name="time"/> (session seconds).</summary>
        public SlamResult Add(SlamInput input, float time)
        {
            bool chained = time - _lastSlamTime <= HallOfMeat.ChainWindow;
            _chain = chained ? _chain + 1 : 0;
            var r = HallOfMeat.Score(input, _chain);
            if (r.Points < HallOfMeat.MinSlam) { _chain = Math.Max(0, _chain - 1); Last = r; return r; }
            _lastSlamTime = time;
            Total += r.Points;
            Count++;
            if (Best == null || r.Points > Best.Points) Best = r;
            Last = r;
            return r;
        }
    }
}
