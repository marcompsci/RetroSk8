using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    public enum Medal
    {
        None = 0,
        Bronze = 1,
        Silver = 2,
        Gold = 3,
    }

    public sealed class CitySpot
    {
        public string Id;
        public string Name;
        public float X, Z, Radius;
        /// <summary>Clear ground inside the spot where its challenge marker sits (and where the map's fast travel lands).</summary>
        public float MarkerX, MarkerZ;
        /// <summary>Best-combo points for bronze / silver / gold in this spot's challenge.</summary>
        public long Bronze, Silver, Gold;
        public string ChallengeText;
    }

    public sealed class CityTape
    {
        public string Id;
        public float X, Y, Z;
    }

    public sealed class CityRace
    {
        public string Id;
        public string Name;
        /// <summary>Gates as (x, z) pairs, in order. The first gate is the start line.</summary>
        public float[] Gates;
        /// <summary>Seconds for gold / silver / bronze (lower is better).</summary>
        public float Gold, Silver, Bronze;
        public int GateCount => Gates.Length / 2;
    }

    /// <summary>
    /// Retro City: an original open skate city. Plain data so the builder, the map, the save file and the tests
    /// all agree on where everything is. Streets run on a 70 m grid; each block is a district with its own spot.
    /// </summary>
    public static class RetroCityLayout
    {
        public const float HalfSize = 125f;
        public const float BlockHalf = 29f;
        public const float RoadHalf = 6f;
        public const float GateRadius = 7f;
        public static readonly float[] StreetLines = { -105f, -35f, 35f, 105f };
        public static readonly float[] BlockCenters = { -70f, 0f, 70f };

        // Phase 13: Riverside Yards, a district north of the old city edge, reached through a gap in the north wall.
        public const float YardsHalfWidth = 45f;
        public const float YardsSouth = 125f;
        public const float YardsNorth = 200f;

        /// <summary>True when (x, z) is in the city grid or the Riverside Yards, at least <paramref name="margin"/> from the edge.</summary>
        public static bool Contains(float x, float z, float margin = 0f)
        {
            bool grid = Math.Abs(x) <= HalfSize - margin && Math.Abs(z) <= HalfSize - margin;
            bool yards = Math.Abs(x) <= YardsHalfWidth - margin && z >= YardsSouth - margin && z <= YardsNorth - margin;
            return grid || yards;
        }

        public static readonly IReadOnlyList<CitySpot> Spots = new List<CitySpot>
        {
            Spot("civic_steps", "Civic Steps", 0, 0, 26, 0, -14, 4000, 8000, 15000, "Session the big stairs: bank the biggest combo you can."),
            Spot("downtown_double", "Downtown Double Set", 0, 70, 24, 0, 58, 3500, 7000, 13000, "Double set, rails and hubbas: bank a big line."),
            Spot("retro_skatepark", "Retro Skatepark", 70, 70, 28, 70, 62, 5000, 10000, 18000, "Mini ramp, bowl, funbox: make it flow."),
            Spot("schoolyard", "Schoolyard Banks", -70, 70, 24, -76, 60, 3000, 6000, 11000, "Banks to wall and picnic tables."),
            Spot("parking_lot", "Parking Lot Curbs", -70, 0, 26, -70, 6, 2500, 5000, 9000, "Curbs, parking blocks and a kicker."),
            Spot("drained_canal", "Drained Canal", 70, 0, 26, 52, 0, 3500, 7000, 13000, "Ride the canal banks and the DIY ledge."),
            Spot("mall_ledges", "Mall Ledges", 0, -70, 24, 0, -56, 3500, 7000, 12000, "Long marble ledges and a hubba set."),
            Spot("backyard_pool", "Backyard Pool", 70, -70, 20, 84, -80, 4000, 8000, 14000, "Carve the pool and grind the coping."),
            Spot("loading_docks", "Loading Docks", -70, -70, 24, -70, -60, 3000, 6000, 11000, "Dock gaps, a kicker and a wall to ride."),
            Spot("riverside_yards", "Riverside Yards", 0, 162, 30, 0, 146, 4000, 9000, 16000, "Boxcar gaps, track rails and the platform: link it all."),
        };

        public static readonly IReadOnlyList<CityTape> Tapes = new List<CityTape>
        {
            // Heights match the builder: ground tapes sit ~0.8 m up, others float over a ledge, platform or a gap.
            Tape("tape_01", 0, 3.2f, -6), Tape("tape_02", 12, 2.6f, 10), Tape("tape_03", -35, 0.8f, 0),
            Tape("tape_04", 35, 0.8f, -60), Tape("tape_05", 0, 2.4f, 78), Tape("tape_06", -10, 0.8f, 58),
            Tape("tape_07", 70, 0.8f, 70), Tape("tape_08", 58, 2.9f, 88), Tape("tape_09", 58, 1.7f, 60),
            Tape("tape_10", -70, 2.2f, 82), Tape("tape_11", -82, 0.8f, 60), Tape("tape_12", -70, 0.8f, -12),
            Tape("tape_13", -54, 2.0f, 20), Tape("tape_14", 70, -1.2f, 0), Tape("tape_15", 70, -1.2f, 22),
            Tape("tape_16", 0, 1.7f, -84), Tape("tape_17", 12, 1.4f, -64), Tape("tape_18", 70, 0.8f, -70),
            Tape("tape_19", -71, 2.0f, -82), Tape("tape_20", -105, 0.8f, 105),
            // Riverside Yards (Phase 13).
            Tape("tape_21", -24, 0.8f, 140), Tape("tape_22", 14, 4.4f, 158), Tape("tape_23", 0, 2.4f, 184), Tape("tape_24", -30, 2.2f, 172),
        };

        public static readonly IReadOnlyList<CityRace> Races = new List<CityRace>
        {
            new CityRace
            {
                Id = "downtown_dash", Name = "Downtown Dash",
                Gates = new float[] { 35, -105, 35, -35, 35, 35, -35, 35, -35, 105 },
                Gold = 27f, Silver = 33f, Bronze = 42f,
            },
            new CityRace
            {
                Id = "ring_road", Name = "Ring Road",
                Gates = new float[] { 105, -105, 105, 0, 105, 105, 0, 105, -105, 105, -105, 0, -105, -105, 0, -105, 100, -105 },
                Gold = 66f, Silver = 78f, Bronze = 92f,
            },
            new CityRace
            {
                Id = "canal_cut", Name = "Canal Cut",
                Gates = new float[] { 70, -35, 70, 0, 70, 35, 0, 35, -35, 35 },
                Gold = 14f, Silver = 17f, Bronze = 22f,
            },
            new CityRace
            {
                Id = "river_run", Name = "River Run",
                Gates = new float[] { 0, 100, 0, 134, 26, 156, 0, 186, -26, 160, 0, 140 },
                Gold = 17f, Silver = 21f, Bronze = 27f,
            },
        };

        public static CitySpot FindSpot(string id)
        {
            foreach (var s in Spots) if (s.Id == id) return s;
            return null;
        }

        public static CityRace FindRace(string id)
        {
            foreach (var r in Races) if (r.Id == id) return r;
            return null;
        }

        /// <summary>The spot whose area contains (x, z), or null.</summary>
        public static CitySpot SpotAt(float x, float z)
        {
            foreach (var s in Spots)
            {
                float dx = x - s.X, dz = z - s.Z;
                if (dx * dx + dz * dz <= s.Radius * s.Radius) return s;
            }
            return null;
        }

        private static CitySpot Spot(string id, string name, float x, float z, float r, float mx, float mz, long b, long s, long g, string text) =>
            new CitySpot { Id = id, Name = name, X = x, Z = z, Radius = r, MarkerX = mx, MarkerZ = mz, Bronze = b, Silver = s, Gold = g, ChallengeText = text };

        /// <summary>How long a spot challenge lasts.</summary>
        public const float ChallengeSeconds = 40f;

        private static CityTape Tape(string id, float x, float y, float z) => new CityTape { Id = id, X = x, Y = y, Z = z };
    }

    public static class MedalRules
    {
        public static Medal ForScore(long points, long bronze, long silver, long gold) =>
            points >= gold ? Medal.Gold : points >= silver ? Medal.Silver : points >= bronze ? Medal.Bronze : Medal.None;

        public static Medal ForTime(float seconds, float gold, float silver, float bronze) =>
            seconds <= 0f ? Medal.None : seconds <= gold ? Medal.Gold : seconds <= silver ? Medal.Silver : seconds <= bronze ? Medal.Bronze : Medal.None;

        /// <summary>Tape Tokens for a medal tier (paid as the difference when you improve).</summary>
        public static int Tokens(Medal m) => m == Medal.Gold ? 35 : m == Medal.Silver ? 15 : m == Medal.Bronze ? 5 : 0;

        public static string Label(Medal m) => m == Medal.None ? "NO MEDAL" : m.ToString().ToUpperInvariant();
    }

    /// <summary>One checkpoint race in progress. Gates must be passed in order; time starts at gate 0.</summary>
    public sealed class RaceRun
    {
        private readonly CityRace _race;
        public int NextGate { get; private set; }
        public float Elapsed { get; private set; }
        public bool Started => NextGate > 0;
        public bool Finished => NextGate >= _race.GateCount;
        public CityRace Race => _race;

        public RaceRun(CityRace race) { _race = race ?? throw new ArgumentNullException(nameof(race)); }

        public void Tick(float dt)
        {
            if (Started && !Finished) Elapsed += Math.Max(0f, dt);
        }

        /// <summary>Call with the skater's position; returns true when the next gate is passed.</summary>
        public bool TryPass(float x, float z)
        {
            if (Finished) return false;
            float gx = _race.Gates[NextGate * 2], gz = _race.Gates[NextGate * 2 + 1];
            float dx = x - gx, dz = z - gz;
            if (dx * dx + dz * dz > RetroCityLayout.GateRadius * RetroCityLayout.GateRadius) return false;
            NextGate++;
            return true;
        }

        public Medal Result => Finished ? MedalRules.ForTime(Elapsed, _race.Gold, _race.Silver, _race.Bronze) : Medal.None;
    }

    [Serializable]
    public sealed class MedalEntry
    {
        public string id;
        public int medal;
        public float bestTime;
        public long bestScore;
    }

    /// <summary>Saved city progress: spots found, tapes collected, challenge and race medals. JsonUtility-friendly.</summary>
    [Serializable]
    public sealed class CityProgress
    {
        public const int SpotTokens = 5;
        public const int TapeTokens = 10;

        public List<string> spots = new List<string>();
        public List<string> tapes = new List<string>();
        public List<MedalEntry> challenges = new List<MedalEntry>();
        public List<MedalEntry> races = new List<MedalEntry>();

        public bool HasSpot(string id) => spots.Contains(id);
        public bool HasTape(string id) => tapes.Contains(id);

        /// <summary>Returns tokens earned (only the first time).</summary>
        public int FindSpot(string id)
        {
            if (string.IsNullOrEmpty(id) || spots.Contains(id)) return 0;
            spots.Add(id);
            return SpotTokens;
        }

        public int CollectTape(string id)
        {
            if (string.IsNullOrEmpty(id) || tapes.Contains(id)) return 0;
            tapes.Add(id);
            return TapeTokens;
        }

        public Medal ChallengeMedal(string spotId) => (Medal)(Find(challenges, spotId)?.medal ?? 0);
        public Medal RaceMedal(string raceId) => (Medal)(Find(races, raceId)?.medal ?? 0);
        public float RaceBest(string raceId) => Find(races, raceId)?.bestTime ?? 0f;
        public long ChallengeBest(string spotId) => Find(challenges, spotId)?.bestScore ?? 0;

        /// <summary>Keeps the best score and medal; returns tokens for any medal improvement.</summary>
        public int RecordChallenge(string spotId, long score, Medal medal)
        {
            var e = Get(challenges, spotId);
            e.bestScore = Math.Max(e.bestScore, score);
            return Improve(e, medal);
        }

        public int RecordRace(string raceId, float seconds, Medal medal)
        {
            var e = Get(races, raceId);
            if (seconds > 0f && (e.bestTime <= 0f || seconds < e.bestTime)) e.bestTime = seconds;
            return Improve(e, medal);
        }

        private static int Improve(MedalEntry e, Medal medal)
        {
            if ((int)medal <= e.medal) return 0;
            int tokens = MedalRules.Tokens(medal) - MedalRules.Tokens((Medal)e.medal);
            e.medal = (int)medal;
            return tokens;
        }

        private static MedalEntry Find(List<MedalEntry> list, string id)
        {
            foreach (var e in list) if (e.id == id) return e;
            return null;
        }

        private static MedalEntry Get(List<MedalEntry> list, string id)
        {
            var e = Find(list, id);
            if (e != null) return e;
            e = new MedalEntry { id = id };
            list.Add(e);
            return e;
        }
    }
}
