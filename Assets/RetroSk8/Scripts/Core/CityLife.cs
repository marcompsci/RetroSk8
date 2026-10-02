using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    public enum Weather { Clear = 0, Cloudy = 1, Rain = 2 }

    public enum StreetEventKind
    {
        /// <summary>Double points for banked combos inside a district for a minute.</summary>
        BlockParty = 0,
        /// <summary>A golden tape appears somewhere nearby for 45 s.</summary>
        GoldenTape = 1,
        /// <summary>Land a big trick near the photographer within 45 s.</summary>
        PhotoShoot = 2,
    }

    /// <summary>
    /// Retro City's living layer as plain rules: an 8-minute day, a deterministic weather schedule, the two traffic
    /// loops and the pop-up street events. The runtime (CityLifeController) only turns these numbers into light,
    /// rain, moving cars and markers.
    /// </summary>
    public static class CityLife
    {
        // ---------------------------------------------------------------- day / night

        public const float DaySeconds = 480f;
        /// <summary>Runs start mid-afternoon so the first few minutes are daylight.</summary>
        public const float StartTime = 0.58f;

        /// <summary>Time of day 0..1 (0 = midnight, 0.25 sunrise, 0.5 noon, 0.75 sunset) after <paramref name="seconds"/>.</summary>
        public static float TimeOfDay(float seconds) => Frac(StartTime + seconds / DaySeconds);

        /// <summary>Sun elevation in degrees (negative below the horizon).</summary>
        public static float SunElevation(float t) => 62f * (float)Math.Sin(2.0 * Math.PI * (t - 0.25));

        /// <summary>0 in full day, 1 in full night, blending through dusk and dawn.</summary>
        public static float Night(float t) => 1f - SmoothStep(-8f, 10f, SunElevation(t));

        /// <summary>How golden the light is: peaks around sunrise and sunset.</summary>
        public static float Golden(float t)
        {
            float e = SunElevation(t);
            return Math.Max(0f, 1f - Math.Abs(e - 4f) / 16f);
        }

        public static string Clock(float t)
        {
            int minutes = (int)(t * 24f * 60f) % (24 * 60);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        // ---------------------------------------------------------------- weather

        /// <summary>One weather spell: what it is and how long it lasts.</summary>
        public struct Spell { public Weather Weather; public float Seconds; }

        /// <summary>
        /// A repeating weather pattern (clear spells are the longest). Deterministic so the tests and a replay agree.
        /// </summary>
        public static readonly Spell[] Pattern =
        {
            new Spell { Weather = Weather.Clear, Seconds = 210f },
            new Spell { Weather = Weather.Cloudy, Seconds = 60f },
            new Spell { Weather = Weather.Rain, Seconds = 120f },
            new Spell { Weather = Weather.Cloudy, Seconds = 50f },
            new Spell { Weather = Weather.Clear, Seconds = 160f },
            new Spell { Weather = Weather.Rain, Seconds = 90f },
        };

        public static float PatternLength
        {
            get
            {
                float s = 0f;
                foreach (var p in Pattern) s += p.Seconds;
                return s;
            }
        }

        /// <summary>Weather at a time, plus 0..1 rain amount that fades in and out over ~12 s at the edges.</summary>
        public static Weather WeatherAt(float seconds, out float rain)
        {
            float t = seconds % PatternLength;
            if (t < 0f) t += PatternLength;
            for (int i = 0; i < Pattern.Length; i++)
            {
                var p = Pattern[i];
                if (t < p.Seconds)
                {
                    rain = p.Weather == Weather.Rain ? Math.Min(1f, Math.Min(t, p.Seconds - t) / 12f) : 0f;
                    return p.Weather;
                }
                t -= p.Seconds;
            }
            rain = 0f;
            return Weather.Clear;
        }

        // ---------------------------------------------------------------- traffic

        /// <summary>Lane offset from the street centre line (cars drive on the right).</summary>
        public const float LaneOffset = 3.5f;
        public const float CarSpeed = 9f;
        /// <summary>A car hitting you faster than this (relative) knocks you off your board.</summary>
        public const float BumpSpeed = 4f;

        /// <summary>
        /// Closed traffic loops as corner points (x, z). The ring road and the downtown square never cross, so
        /// cars never need to give way to each other.
        /// </summary>
        public static readonly float[][] Loops =
        {
            Square(105f - LaneOffset, clockwise: true),
            Square(35f - LaneOffset, clockwise: false),
        };

        public static readonly int[] CarsPerLoop = { 7, 3 };

        private static float[] Square(float h, bool clockwise) => clockwise
            ? new[] { -h, -h, -h, h, h, h, h, -h }
            : new[] { -h, -h, h, -h, h, h, -h, h };

        public static float LoopLength(float[] loop)
        {
            float len = 0f;
            int n = loop.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                len += Dist(loop[i * 2], loop[i * 2 + 1], loop[j * 2], loop[j * 2 + 1]);
            }
            return len;
        }

        /// <summary>Position and heading (unit x, z) a distance along a loop.</summary>
        public static void PointOnLoop(float[] loop, float distance, out float x, out float z, out float dx, out float dz)
        {
            float total = LoopLength(loop);
            float d = distance % total;
            if (d < 0f) d += total;
            int n = loop.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float ax = loop[i * 2], az = loop[i * 2 + 1], bx = loop[j * 2], bz = loop[j * 2 + 1];
                float seg = Dist(ax, az, bx, bz);
                if (d <= seg || i == n - 1)
                {
                    float k = seg > 0f ? Math.Min(1f, d / seg) : 0f;
                    x = ax + (bx - ax) * k;
                    z = az + (bz - az) * k;
                    dx = seg > 0f ? (bx - ax) / seg : 0f;
                    dz = seg > 0f ? (bz - az) / seg : 1f;
                    return;
                }
                d -= seg;
            }
            x = loop[0]; z = loop[1]; dx = 0f; dz = 1f;
        }

        // ---------------------------------------------------------------- street events

        public const float FirstEventDelay = 45f;
        public const float EventGapMin = 70f, EventGapMax = 130f;
        public const float BlockPartySeconds = 60f, GoldenTapeSeconds = 45f, PhotoShootSeconds = 45f;
        public const float BlockPartyFactor = 2f;
        public const int GoldenTapeTokens = 40;
        public const int PhotoShootTokens = 30;
        public const long PhotoShootPoints = 1500;
        public const float PhotoShootRadius = 12f;

        public static string EventName(StreetEventKind k) =>
            k == StreetEventKind.BlockParty ? "BLOCK PARTY" : k == StreetEventKind.GoldenTape ? "GOLDEN TAPE" : "PHOTO SHOOT";

        public static float EventSeconds(StreetEventKind k) =>
            k == StreetEventKind.BlockParty ? BlockPartySeconds : k == StreetEventKind.GoldenTape ? GoldenTapeSeconds : PhotoShootSeconds;

        // ---------------------------------------------------------------- helpers

        private static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
        private static float Frac(float v) => v - (float)Math.Floor(v);

        private static float SmoothStep(float a, float b, float v)
        {
            float t = Math.Max(0f, Math.Min(1f, (v - a) / (b - a)));
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>Chooses pop-up street events: timing, kind and where (seeded so tests are repeatable).</summary>
    public sealed class StreetEventPlanner
    {
        private readonly Random _rng;
        private float _next;
        private StreetEventKind _last = (StreetEventKind)(-1);

        public StreetEventPlanner(int seed)
        {
            _rng = new Random(seed);
            _next = CityLife.FirstEventDelay;
        }

        public float NextAt => _next;

        /// <summary>Returns an event kind when one is due at <paramref name="seconds"/> (never the same kind twice in a row).</summary>
        public bool TryStart(float seconds, out StreetEventKind kind)
        {
            kind = StreetEventKind.BlockParty;
            if (seconds < _next) return false;
            do kind = (StreetEventKind)_rng.Next(0, 3); while (kind == _last);
            _last = kind;
            _next = seconds + CityLife.EventSeconds(kind) + CityLife.EventGapMin + (float)_rng.NextDouble() * (CityLife.EventGapMax - CityLife.EventGapMin);
            return true;
        }

        /// <summary>A district for the event, preferring ones you've found (and never the one you're in for tapes).</summary>
        public CitySpot PickSpot(IList<string> found, string avoidId)
        {
            var pool = new List<CitySpot>();
            foreach (var s in RetroCityLayout.Spots)
                if (s.Id != avoidId && (found == null || found.Count == 0 || found.Contains(s.Id))) pool.Add(s);
            if (pool.Count == 0) foreach (var s in RetroCityLayout.Spots) if (s.Id != avoidId) pool.Add(s);
            return pool[_rng.Next(pool.Count)];
        }
    }
}
