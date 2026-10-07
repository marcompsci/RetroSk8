using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Subway Depot (Phase 27): an original underground train yard after the last train.
    /// - Two raised platforms either side of a sunken track bed, with grindable platform ledges along both edges and
    ///   tiled station walls behind them to wallride. Jump platform to platform over the tracks: Track Gap.
    /// - South: a ticket mezzanine with a row of turnstiles to bonk, banks up from each platform, and a 13-stair set
    ///   with handrails down into the track bed: Turnstile Set.
    /// - On the track: a parked subway car to wallride, and a pair of rails to grind.
    /// - North: banks down to the depot floor, a kicker and a buffer stop in front of the tunnel mouth: Tunnel Gap.
    /// Everything here is invented: no real transit system, signage, maps or branding.
    /// </summary>
    public sealed class SubwayDepotBuilder : ParkBuilder
    {
        public const string GeneratedRootName = "SubwayDepot_Generated";
        protected override string RootName => GeneratedRootName;
        public override string LocationId => ParkCatalog.SubwayDepot;

        /// <summary>Platform deck height (6 risers).</summary>
        public const float PlatformHeight = 6 * StepRise;
        /// <summary>The track bed between the platforms runs from -TrackHalf to +TrackHalf (x).</summary>
        public const float TrackHalf = 3.5f;
        /// <summary>Platforms run from the mezzanine edge (south) to here (north), then bank down to the depot floor.</summary>
        public const float PlatformSouthZ = -44f, PlatformNorthZ = 30f;
        public const int StairSteps = 13;
        public const float MezzanineHeight = StairSteps * StepRise;
        public const float MezzanineEdgeZ = -44f;
        public const float BufferZ = 40f;
        /// <summary>The parked car on the track (z range).</summary>
        public const float CarFromZ = 6f, CarToZ = 22f;
        public const float CarHeight = 3.2f;
        public const float WallX = 30.5f, WallHeight = 6f;

        private static readonly Color Tile = new Color(0.8f, 0.82f, 0.76f);
        private static readonly Color TileDark = new Color(0.5f, 0.56f, 0.54f);
        private static readonly Color Platform = new Color(0.46f, 0.46f, 0.48f);
        private static readonly Color Ballast = new Color(0.28f, 0.27f, 0.27f);
        private static readonly Color Sleeper = new Color(0.32f, 0.24f, 0.18f);
        private static readonly Color CarBody = new Color(0.72f, 0.74f, 0.78f);

        protected override void BuildPark(LevelInfo level)
        {
            // Depot floor (track level), station walls all round.
            Box("Floor", new Vector3(0f, -0.5f, 1.5f), new Vector3(62f, 1f, 112f), Ballast);
            Box("Wall_W", new Vector3(-WallX, WallHeight * 0.5f, 1.5f), new Vector3(1f, WallHeight, 112f), Tile);
            Box("Wall_E", new Vector3(WallX, WallHeight * 0.5f, 1.5f), new Vector3(1f, WallHeight, 112f), Tile);
            Box("Wall_S", new Vector3(0f, WallHeight * 0.5f, -55f), new Vector3(62f, WallHeight, 1f), Tile);
            Box("Wall_N", new Vector3(0f, WallHeight * 0.5f, 58f), new Vector3(62f, WallHeight, 1f), TileDark);
            // A tile band and a lit strip along each side wall (visual only: the wall face is what you wallride).
            foreach (float side in new[] { -1f, 1f })
            {
                var band = Box("Wall_Band", new Vector3(side * (WallX - 0.52f), 2.2f, 1.5f), new Vector3(0.04f, 0.5f, 112f), TileDark);
                RemoveCollider(band);
                Neon("Wall_Light", new Vector3(side * (WallX - 0.55f), 5.4f, 1.5f), new Vector3(0.06f, 0.12f, 100f), Palette.Cream);
            }

            // ---- Platforms either side of the track bed, with grindable ledges along the platform edges.
            float platLen = PlatformNorthZ - PlatformSouthZ, platMidZ = (PlatformNorthZ + PlatformSouthZ) * 0.5f;
            float platWidth = WallX - 0.5f - TrackHalf;
            foreach (float side in new[] { -1f, 1f })
            {
                float cx = side * (TrackHalf + platWidth * 0.5f);
                Box("Platform", new Vector3(cx, PlatformHeight * 0.5f, platMidZ), new Vector3(platWidth, PlatformHeight, platLen), Platform);
                Neon("Platform_Line", new Vector3(side * (TrackHalf + 0.9f), PlatformHeight + 0.01f, platMidZ), new Vector3(0.25f, 0.02f, platLen), Palette.TapeYellow);
                Ledge("Platform_Ledge", new Vector3(side * (TrackHalf + 0.35f), PlatformHeight, PlatformSouthZ + 6f), new Vector3(side * (TrackHalf + 0.35f), PlatformHeight, PlatformNorthZ - 2f), 0.35f, 0.6f, Palette.Concrete);
                // Bank from the platform's north end down to the depot floor (falls toward +z).
                MeshObject("Platform_Bank", ProcMesh.Wedge(platWidth, 6f, PlatformHeight), new Vector3(cx, 0f, PlatformNorthZ + 6f), 180f, Palette.Concrete);
                // Bank from the platform's south end up onto the mezzanine.
                MeshObject("Mezz_Bank", ProcMesh.Wedge(6f, 7f, MezzanineHeight - PlatformHeight), new Vector3(side * 20f, PlatformHeight, MezzanineEdgeZ + 7f), 180f, Palette.Concrete);
                // Pillars down each platform (solid, floor to ceiling) and benches to grind.
                for (float z = -32f; z <= 24f; z += 14f)
                {
                    Cylinder("Pillar", new Vector3(side * 17f, PlatformHeight, z), 0.4f, WallHeight - PlatformHeight, TileDark);
                    if (z + 7f < PlatformNorthZ - 3f) Bench("Bench", new Vector3(side * 25f, PlatformHeight, z + 7f), 0f);
                }
            }

            // ---- The track: two grindable rails on sleepers, from the stair landing to the buffer stop.
            float stairRun = StairSteps * StepRun;
            float railFrom = MezzanineEdgeZ + stairRun + 2.5f, railTo = BufferZ - 9.5f; // stop short of the kicker
            // Two sections, so a grind along the track never runs through the parked car.
            foreach (float x in new[] { -0.75f, 0.75f })
            {
                Curb("Track_Rail", new Vector3(x, 0f, railFrom), new Vector3(x, 0f, CarFromZ - 1.5f));
                Curb("Track_Rail", new Vector3(x, 0f, CarToZ + 1.5f), new Vector3(x, 0f, railTo));
            }
            for (float z = railFrom; z < railTo; z += 1.4f)
            {
                var sleeper = Box("Sleeper", new Vector3(0f, 0.03f, z), new Vector3(2.4f, 0.06f, 0.3f), Sleeper);
                RemoveCollider(sleeper);
            }

            // ---- The parked car: a long box to wallride, with a window band and a stripe.
            float carLen = CarToZ - CarFromZ, carMid = (CarFromZ + CarToZ) * 0.5f;
            Box("Subway_Car", new Vector3(0f, CarHeight * 0.5f + 0.15f, carMid), new Vector3(3f, CarHeight, carLen), CarBody);
            foreach (float side in new[] { -1f, 1f })
            {
                var windows = Box("Car_Windows", new Vector3(side * 1.51f, 2.3f, carMid), new Vector3(0.02f, 0.8f, carLen - 1.2f), Palette.Ink);
                RemoveCollider(windows);
                Neon("Car_Stripe", new Vector3(side * 1.52f, 1.2f, carMid), new Vector3(0.02f, 0.18f, carLen), Palette.Coral);
            }

            // ---- The mezzanine (south): turnstiles to bonk and a 13-stair set down into the track bed.
            float mezzDepth = MezzanineEdgeZ + 54.5f;
            Box("Mezzanine", new Vector3(0f, MezzanineHeight * 0.5f, MezzanineEdgeZ - mezzDepth * 0.5f), new Vector3(60f, MezzanineHeight, mezzDepth), Platform);
            Neon("Mezz_Edge", new Vector3(0f, MezzanineHeight + 0.02f, MezzanineEdgeZ - 0.25f), new Vector3(60f, 0.04f, 0.3f), Palette.TapeYellow);
            Vector3 stairBottom = new Vector3(0f, 0f, MezzanineEdgeZ + stairRun);
            Vector3 stairTop = Stairs("Turnstile_Stairs", stairBottom, Vector3.forward, StairSteps, 6f, Palette.Concrete);
            Handrail("Stair_Rail_L", stairTop, Vector3.forward, StairSteps, -2.6f);
            Handrail("Stair_Rail_R", stairTop, Vector3.forward, StairSteps, 2.6f);
            for (float x = -27f; x <= 27f; x += 2.5f)
            {
                if (Mathf.Abs(x) < 5f || Mathf.Abs(Mathf.Abs(x) - 20f) < 3.5f) continue; // keep the stair top and the banks clear
                var gate = Cylinder("Turnstile", new Vector3(x, MezzanineHeight, MezzanineEdgeZ - 5f), 0.14f, 0.95f, Palette.Metal);
                Bonkable(gate, true);
            }
            Building("Ticket_Booth", new Vector3(0f, MezzanineHeight, -51f), new Vector3(6f, 2.6f, 3f), Palette.Brick, false);
            Neon("Booth_Sign", new Vector3(0f, MezzanineHeight + 2.2f, -49.45f), new Vector3(3.6f, 0.35f, 0.1f), Palette.NeonCyan);

            // ---- North end: the depot floor, a kicker, the buffer stop and the tunnel mouth.
            Kicker("Tunnel_Kicker", new Vector3(0f, 0f, BufferZ - 7f), 0f, 3f, 2.5f, 0.8f);
            Box("Buffer_Stop", new Vector3(0f, 0.5f, BufferZ), new Vector3(4f, 1f, 0.8f), Palette.Coral);
            var mouth = Box("Tunnel_Mouth", new Vector3(0f, 2.6f, 57.45f), new Vector3(8f, 5.2f, 0.1f), Palette.Ink);
            RemoveCollider(mouth);
            Neon("Tunnel_Arch", new Vector3(0f, 5.3f, 57.4f), new Vector3(9f, 0.25f, 0.1f), Palette.NeonViolet);
            Funbox("Depot_Funbox", new Vector3(-18f, 0f, 46f), 3f, 2.5f, 0.9f, Palette.Concrete, true);
            var flat = new List<Vector3> { new Vector3(18f, 0.5f, 40f), new Vector3(18f, 0.5f, 52f) };
            Rail("Depot_FlatBar", flat, GrindSurface.Rail, false, _root);
            Bar(flat[0], flat[1], 0.05f, Palette.Metal, true);
            foreach (var p in new[] { new Vector3(-8f, 0f, 50f), new Vector3(8f, 0f, 50f) })
                Cone("Cone", p);

            // Gap volumes sit above anything a plain ollie reaches from whatever is below them (the capsule top reaches
            // ~3.7 m above the feet), so each one needs the real move:
            // - Track Gap: platform to platform. A strong pop off a platform reaches it; it only pays if you land on
            //   platform height (not in the track bed), and it starts above an ollie out of a track-rail grind.
            var track = Gap(ParkCatalog.TrackGap, "Track Gap", new Vector3(0f, 4.6f, (railFrom + CarFromZ) * 0.5f - 2f), new Vector3(TrackHalf * 2f - 0.6f, 1.2f, CarFromZ - railFrom - 6f), 400);
            track.minLandingY = PlatformHeight - 0.2f;
            level.gaps.Add(track);
            // - Turnstile Set: over the bottom of the stairs and just past it, high enough that an ollie from partway
            //   down (the ramp under it is under 1 m there) can't reach it; jumping from the mezzanine can.
            level.gaps.Add(Gap(ParkCatalog.TurnstileSet, "Turnstile Set", new Vector3(0f, MezzanineHeight + 3.04f, MezzanineEdgeZ + stairRun - 0.2f), new Vector3(5f, 1.2f, 2.8f), 450));
            // - Tunnel Gap: off the kicker with a pop, above an ollie from the floor (3.7 m) or from the buffer stop (4.7 m).
            level.gaps.Add(Gap(ParkCatalog.TunnelGap, "Tunnel Gap", new Vector3(0f, 5.4f, BufferZ), new Vector3(4.4f, 1.0f, 2.4f), 350));

            KillPlane(-6f);
            level.playableBounds = new Bounds(new Vector3(0f, 10f, 1.5f), new Vector3(64f, 40f, 116f));
            Spawn(level, new Vector3(-14f, PlatformHeight + 0.05f, -34f), Vector3.forward); // on the west platform, looking up the line
        }
    }
}
