using System.Collections.Generic;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Retro City: an original open skate city on a 70 m street grid (see <see cref="RetroCityLayout"/> for the
    /// spots, tapes and races). Each block is a district built around one spot: Civic Steps, a downtown double set,
    /// a public skatepark, schoolyard banks, a parking lot, a drained canal, mall ledges, a backyard pool and
    /// loading docks. Streets have grindable curbs, and a ring of tall buildings closes the city in.
    /// Obstacle sizes follow public skatepark design guidelines (see ParkBuilder's spot kit).
    /// </summary>
    public sealed class RetroCityBuilder : ParkBuilder
    {
        public const string GeneratedRootName = "RetroCity_Generated";
        protected override string RootName => GeneratedRootName;
        public override string LocationId => ParkCatalog.RetroCity;

        // Canal: a sunken channel through the (70, 0) block.
        private const float CanalX0 = 62f, CanalX1 = 78f, CanalZ = 29f, CanalDepth = 2.2f;

        private static readonly Color Asphalt = new Color(0.2f, 0.2f, 0.23f);
        private static readonly Color Sidewalk = new Color(0.58f, 0.56f, 0.55f);
        private static readonly Color Marble = new Color(0.82f, 0.8f, 0.78f);
        private static readonly Color Granite = new Color(0.45f, 0.45f, 0.48f);

        protected override void BuildPark(LevelInfo level)
        {
            BuildGround();
            BuildStreets();
            CivicSteps(level);
            DowntownDoubleSet(level);
            RetroSkatepark();
            Schoolyard();
            ParkingLot();
            DrainedCanal(level);
            MallLedges(level);
            BackyardPool();
            LoadingDocks(level);
            RiversideYards(level);
            CityEdge();
            SpotBonkables();

            KillPlane(-8f, 600f);
            level.killHeight = -6f;
            // The grid plus the Riverside Yards to the north.
            float south = -RetroCityLayout.HalfSize - 5f, north = RetroCityLayout.YardsNorth + 5f;
            level.playableBounds = new Bounds(new Vector3(0f, 20f, (south + north) * 0.5f), new Vector3(2f * RetroCityLayout.HalfSize + 10f, 80f, north - south));
            Spawn(level, new Vector3(35f, 0.05f, -20f), Vector3.forward);
        }

        // ---------------------------------------------------------------- bonkables (Phase 18)

        /// <summary>
        /// A cone, a hydrant and a slanted signpost at every spot (for bonks and the Bonk Hunt City Jam), placed on
        /// clear flat ground a few metres from the spot's marker. Candidates that would sit on an obstacle are skipped.
        /// </summary>
        private void SpotBonkables()
        {
            Physics.SyncTransforms();
            foreach (var spot in RetroCityLayout.Spots)
            {
                var marker = new Vector3(spot.MarkerX, 0f, spot.MarkerZ);
                int placed = 0;
                for (int ring = 0; ring < 3 && placed < 3; ring++)
                    for (int k = 0; k < 8 && placed < 3; k++)
                    {
                        float a = (k * 45f + ring * 22.5f) * Mathf.Deg2Rad, r = 3.5f + ring * 1.5f;
                        var p = marker + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                        if (!ClearFlatGround(p, out var ground)) continue;
                        string name = spot.Id + "_Bonk" + placed;
                        if (placed == 0) Cone(name, ground);
                        else if (placed == 1) Hydrant(name, ground);
                        else SlantedPost(name, ground, ground - marker, 1.3f, 22f, Palette.Metal, Palette.TapeYellow);
                        placed++;
                        Physics.SyncTransforms();
                    }
            }
        }

        private static bool ClearFlatGround(Vector3 p, out Vector3 ground)
        {
            ground = p;
            if (!Physics.Raycast(p + Vector3.up * 12f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (hit.normal.y < 0.97f || hit.point.y < -0.2f || hit.point.y > 0.35f) return false;
            ground = hit.point;
            // Nothing else within a metre and a half around or above it.
            return !Physics.CheckBox(hit.point + Vector3.up * 1.0f, new Vector3(0.75f, 0.85f, 0.75f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- ground and streets

        private void BuildGround()
        {
            float H = RetroCityLayout.HalfSize + 5f;
            // Ground is four slabs around the canal so the canal can be sunken.
            Slab("Ground_West", -H, CanalX0, -H, H);
            Slab("Ground_East", CanalX1, H, -H, H);
            Slab("Ground_South", CanalX0, CanalX1, -H, -CanalZ);
            Slab("Ground_North", CanalX0, CanalX1, CanalZ, H);
            Slab("Ground_Yards", -RetroCityLayout.YardsHalfWidth - 5f, RetroCityLayout.YardsHalfWidth + 5f, H, RetroCityLayout.YardsNorth + 5f);
        }

        private void Slab(string name, float x0, float x1, float z0, float z1)
        {
            Box(name, new Vector3((x0 + x1) * 0.5f, -0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 1f, z1 - z0), Asphalt);
        }

        private void BuildStreets()
        {
            float H = RetroCityLayout.HalfSize;
            foreach (float line in RetroCityLayout.StreetLines)
            {
                // Lane dashes (visual).
                for (float t = -H + 4f; t < H - 4f; t += 8f)
                {
                    RemoveCollider(Box("LaneDash", new Vector3(line, 0.01f, t), new Vector3(0.25f, 0.02f, 3.5f), Palette.TapeYellow));
                    RemoveCollider(Box("LaneDash", new Vector3(t, 0.01f, line), new Vector3(3.5f, 0.02f, 0.25f), Palette.TapeYellow));
                }
            }
            // Sidewalk borders and grindable curbs around every block.
            foreach (float cx in RetroCityLayout.BlockCenters)
            foreach (float cz in RetroCityLayout.BlockCenters)
            {
                float b = RetroCityLayout.BlockHalf;
                var c = new Vector3(cx, 0f, cz);
                bool canal = Mathf.Approximately(cx, 70f) && Mathf.Approximately(cz, 0f);
                foreach (var (a, d) in new[]
                {
                    (new Vector3(-b, 0, -b), new Vector3(b, 0, -b)), (new Vector3(b, 0, -b), new Vector3(b, 0, b)),
                    (new Vector3(b, 0, b), new Vector3(-b, 0, b)), (new Vector3(-b, 0, b), new Vector3(-b, 0, -b)),
                })
                {
                    if (canal && Mathf.Abs(a.z - d.z) < 0.1f) continue; // the canal ends open to the street
                    Vector3 p0 = c + a + (d - a).normalized * 1.5f, p1 = c + d - (d - a).normalized * 1.5f;
                    Curb("Curb", p0, p1);
                    var walk = Box("Sidewalk", (p0 + p1) * 0.5f + Vector3.up * 0.005f - Vector3.Cross(Vector3.up, (d - a).normalized) * 1.5f,
                        new Vector3(3f, 0.01f, Vector3.Distance(p0, p1)), Sidewalk);
                    walk.transform.rotation = Quaternion.LookRotation(d - a);
                    RemoveCollider(walk);
                }
            }
            // A few benches along the avenues.
            foreach (var p in new[] { new Vector3(28f, 0f, -12f), new Vector3(-28f, 0f, 14f), new Vector3(12f, 0f, 28f), new Vector3(-12f, 0f, -28f) })
                Bench("StreetBench", p, Mathf.Abs(p.x) > Mathf.Abs(p.z) ? 0f : 90f, 3f);
        }

        // ---------------------------------------------------------------- districts

        /// <summary>Civic Steps (0, 0): a raised plaza with an 8-stair, a middle handrail and hubbas both sides.</summary>
        private void CivicSteps(LevelInfo level)
        {
            const int steps = 8;
            float h = steps * StepRise;
            Box("Civic_Plaza", new Vector3(0f, h * 0.5f, 9f), new Vector3(40f, h, 22f), Granite);
            var top = Stairs("Civic_Stairs", new Vector3(0f, 0f, -2f - steps * StepRun), Vector3.back, steps, 12f, Granite);
            Handrail("Civic_Rail", top, Vector3.back, steps, 0f);
            Hubba("Civic_HubbaL", top, Vector3.back, steps, -6.3f, Granite);
            Hubba("Civic_HubbaR", top, Vector3.back, steps, 6.3f, Granite);
            // Back bank up onto the plaza and two ledges on top.
            MeshObject("Civic_BackBank", ProcMesh.Wedge(14f, 6f, h), new Vector3(0f, 0f, 26f), 180f, Palette.Concrete);
            Ledge("Civic_TopLedgeA", new Vector3(-14f, h, 4f), new Vector3(-14f, h, 16f), 0.45f, 0.8f, Marble);
            Ledge("Civic_TopLedgeB", new Vector3(14f, h, 4f), new Vector3(14f, h, 16f), 0.45f, 0.8f, Marble);
            // Lower plaza: manual pads and benches.
            ManualPad("Civic_ManualPad", new Vector3(-14f, 0f, -18f), new Vector2(6f, 3f), 0f, Marble);
            ManualPad("Civic_ManualPad2", new Vector3(14f, 0f, -18f), new Vector2(6f, 3f), 0f, Marble);
            Bench("Civic_Bench", new Vector3(0f, 0f, -22f), 90f, 4f);
            level.gaps.Add(Gap(ParkCatalog.CivicEight, "Civic 8", new Vector3(0f, 1.6f, -2f - steps * StepRun * 0.5f), new Vector3(12f, 3f, steps * StepRun + 1f), 900));
        }

        /// <summary>Downtown Double Set (0, 70): two 5-stairs on stacked tiers with rails.</summary>
        private void DowntownDoubleSet(LevelInfo level)
        {
            const int steps = 5;
            float h = steps * StepRise;
            var z0 = 70f;
            Box("Downtown_Tier1", new Vector3(0f, h * 0.5f, z0 + 14f), new Vector3(48f, h, 24f), Granite);
            Box("Downtown_Tier2", new Vector3(0f, h * 1.5f, z0 + 20f), new Vector3(24f, h, 12f), Granite);
            var top1 = Stairs("Downtown_Stairs1", new Vector3(-2f, 0f, z0 + 2f - steps * StepRun), Vector3.back, steps, 12f, Granite);
            Handrail("Downtown_Rail1L", top1, Vector3.back, steps, -6.4f);
            Handrail("Downtown_Rail1R", top1, Vector3.back, steps, 6.4f);
            var top2 = Stairs("Downtown_Stairs2", new Vector3(2f, h, z0 + 14f - steps * StepRun), Vector3.back, steps, 8f, Granite);
            Handrail("Downtown_Rail2", top2, Vector3.back, steps, -4.4f);
            Hubba("Downtown_Hubba2", top2, Vector3.back, steps, 4.3f, Granite);
            MeshObject("Downtown_BankUp1", ProcMesh.Wedge(10f, 5f, h), new Vector3(29f, 0f, z0 + 14f), -90f, Palette.Concrete);
            MeshObject("Downtown_BankUp2", ProcMesh.Wedge(8f, 5f, h), new Vector3(17f, h, z0 + 20f), -90f, Palette.Concrete);
            Planter("Downtown_Planter", new Vector3(-18f, 0f, z0 - 14f), new Vector2(3f, 6f), Granite);
            Planter("Downtown_Planter2", new Vector3(18f, 0f, z0 - 14f), new Vector2(3f, 6f), Granite);
            level.gaps.Add(Gap(ParkCatalog.DoubleSet, "Downtown Double Set", new Vector3(0f, h + 1.2f, z0 + 2f - steps * StepRun * 0.5f), new Vector3(14f, 2.4f, steps * StepRun + 1f), 700));
        }

        /// <summary>Retro Skatepark (70, 70): mini ramp, round bowl, funbox with hubba edges, flat rail and quarter pipe.</summary>
        private void RetroSkatepark()
        {
            var c = new Vector3(70f, 0f, 70f);
            RemoveCollider(Box("Skatepark_Slab", c + Vector3.up * 0.004f, new Vector3(56f, 0.008f, 56f), Palette.Concrete));
            MiniRamp("Skatepark_Mini", c + new Vector3(-8f, 0f, 18f), 90f, 8f, 1.5f, 5f, 1.2f);
            BowlWithDeck("Skatepark_Bowl", c + new Vector3(14f, 0f, -12f), 2.5f, 2.6f, 2.2f, 7f, Palette.Concrete);
            MeshObject("Skatepark_BowlBank", ProcMesh.Wedge(6f, 6f, 2.2f), c + new Vector3(14f, 0f, -25f), 0f, Palette.Concrete);
            Funbox("Skatepark_Funbox", c + new Vector3(-12f, 0f, -10f), 3f, 2.5f, 0.9f, Palette.Concrete, true);
            var a = c + new Vector3(-22f, 0.5f, 2f);
            var b = c + new Vector3(-22f, 0.5f, 14f);
            Rail("Skatepark_FlatRail", new List<Vector3> { a, b }, GrindSurface.Rail, false, _root);
            Bar(a, b, 0.05f, Palette.Metal, true);
            var qp = MeshObject("Skatepark_QP", ProcMesh.QuarterPipe(10f, 2.2f, 1.2f), c + new Vector3(8f, 0f, 22f), 0f, Palette.Concrete);
            float lipAlong = 2.2f * Mathf.Sin(88f * Mathf.Deg2Rad), lipY = 2.2f - 2.2f * Mathf.Cos(88f * Mathf.Deg2Rad);
            var ca = c + new Vector3(3.3f, lipY + 0.03f, 22f + lipAlong);
            var cb = c + new Vector3(12.7f, lipY + 0.03f, 22f + lipAlong);
            Rail("Skatepark_QPCoping", new List<Vector3> { ca, cb }, GrindSurface.Coping, false, qp.transform);
            Bar(ca, cb, 0.07f, Palette.Coping, false);
        }

        /// <summary>Schoolyard (-70, 70): bank to wall along the school, picnic-table ledges and a 3-stair.</summary>
        private void Schoolyard()
        {
            var c = new Vector3(-70f, 0f, 70f);
            Building("School", c + new Vector3(0f, 0f, 22f), new Vector3(58f, 12f, 14f), Palette.Brick, true);
            MeshObject("School_BankToWall", ProcMesh.Wedge(30f, 7f, 2.5f), c + new Vector3(0f, 0f, 8f), 0f, Palette.Concrete);
            Bench("Picnic_A", c + new Vector3(-16f, 0f, -6f), 0f, 3f);
            Bench("Picnic_B", c + new Vector3(-10f, 0f, -6f), 0f, 3f);
            Bench("Picnic_C", c + new Vector3(16f, 0f, -12f), 90f, 3f);
            Box("School_Steps_Platform", c + new Vector3(-22f, 0.33f, -20f), new Vector3(14f, 0.66f, 14f), Palette.ConcreteDark);
            var top = Stairs("School_Stairs", c + new Vector3(-15f + 3 * StepRun, 0f, -20f), Vector3.right, 3, 6f, Palette.ConcreteDark);
            Handrail("School_Rail", top, Vector3.right, 3, 3.3f);
            // Court lines (visual).
            RemoveCollider(Box("Court", c + new Vector3(10f, 0.006f, -10f), new Vector3(18f, 0.01f, 12f), new Color(0.3f, 0.4f, 0.55f)));
        }

        /// <summary>Parking Lot (-70, 0): parking blocks, curb islands, a store wall and a kicker.</summary>
        private void ParkingLot()
        {
            var c = new Vector3(-70f, 0f, 0f);
            Building("Store", c + new Vector3(-23f, 0f, 0f), new Vector3(12f, 9f, 50f), Palette.ContainerTeal, true);
            for (int row = -1; row <= 1; row += 2)
            for (int i = -3; i <= 3; i++)
            {
                var p = c + new Vector3(i * 4f + 4f, 0f, row * 12f);
                Ledge("ParkingBlock", p - Vector3.right * 1f, p + Vector3.right * 1f, 0.2f, 0.4f, Palette.Concrete);
            }
            Ledge("Island_Curb", c + new Vector3(-8f, 0f, -2f), c + new Vector3(16f, 0f, -2f), 0.25f, 1.6f, Palette.Concrete);
            Kicker("Lot_Kicker", c + new Vector3(10f, 0f, 20f), 90f);
            foreach (var p in new[] { new Vector3(-8f, 0f, 8f), new Vector3(16f, 0f, 8f), new Vector3(-8f, 0f, -14f), new Vector3(16f, 0f, -14f) })
            {
                var pole = PrimitiveMeshes.CreateVisual("LightPole", PrimitiveType.Cylinder, _root, c + p + Vector3.up * 3f, new Vector3(0.15f, 3f, 0.15f), Palette.Ink);
                pole.isStatic = true;
                Neon("PoleLight", c + p + Vector3.up * 6.1f, new Vector3(0.8f, 0.15f, 0.4f), Palette.Cream);
            }
        }

        /// <summary>Drained Canal (70, 0): a sunken concrete channel with banks on both sides, open to the streets at the ends.</summary>
        private void DrainedCanal(LevelInfo level)
        {
            float d = CanalDepth;
            float len = CanalZ * 2f;
            Box("Canal_Floor", new Vector3(70f, -d - 0.5f, 0f), new Vector3(CanalX1 - CanalX0, 1f, len), Palette.ConcreteDark);
            MeshObject("Canal_BankWest", ProcMesh.Wedge(len, 3f, d), new Vector3(CanalX0 + 3f, -d, 0f), -90f, Palette.Concrete);
            MeshObject("Canal_BankEast", ProcMesh.Wedge(len, 3f, d), new Vector3(CanalX1 - 3f, -d, 0f), 90f, Palette.Concrete);
            MeshObject("Canal_RampSouth", ProcMesh.Wedge(10f, 6f, d), new Vector3(70f, -d, -CanalZ + 6f), 180f, Palette.Concrete);
            MeshObject("Canal_RampNorth", ProcMesh.Wedge(10f, 6f, d), new Vector3(70f, -d, CanalZ - 6f), 0f, Palette.Concrete);
            Rail("Canal_LipWest", new List<Vector3> { new Vector3(CanalX0 + 0.1f, 0.03f, -CanalZ + 1f), new Vector3(CanalX0 + 0.1f, 0.03f, CanalZ - 1f) }, GrindSurface.Ledge, false, _root);
            Rail("Canal_LipEast", new List<Vector3> { new Vector3(CanalX1 - 0.1f, 0.03f, -CanalZ + 1f), new Vector3(CanalX1 - 0.1f, 0.03f, CanalZ - 1f) }, GrindSurface.Ledge, false, _root);
            // DIY spot on the floor: a small hump and a ledge, off the race line.
            Funbox("Canal_DIYHump", new Vector3(66.5f, -d, -12f), 1f, 1.2f, 0.5f, Palette.ConcreteDark, false);
            Ledge("Canal_DIYLedge", new Vector3(73.5f, -d, 6f), new Vector3(73.5f, -d, 16f), 0.4f, 0.6f, Palette.Concrete);
            level.gaps.Add(Gap(ParkCatalog.CanalJump, "Canal Jump", new Vector3(70f, 1.2f, 0f), new Vector3(CanalX1 - CanalX0, 2.2f, 40f), 800));
        }

        /// <summary>Mall Ledges (0, -70): long marble ledges and a 4-stair with hubbas, plus a planter gap.</summary>
        private void MallLedges(LevelInfo level)
        {
            var c = new Vector3(0f, 0f, -70f);
            Building("Mall", c + new Vector3(0f, 0f, -24f), new Vector3(58f, 14f, 10f), Palette.ContainerMustard, true);
            const int steps = 4;
            float h = steps * StepRise;
            Box("Mall_Terrace", c + new Vector3(0f, h * 0.5f, -14f), new Vector3(30f, h, 10f), Marble);
            var top = Stairs("Mall_Stairs", c + new Vector3(0f, 0f, -9f + steps * StepRun), Vector3.forward, steps, 8f, Marble);
            Hubba("Mall_HubbaL", top, Vector3.forward, steps, -4.3f, Marble);
            Hubba("Mall_HubbaR", top, Vector3.forward, steps, 4.3f, Marble);
            for (int i = 0; i < 3; i++)
            {
                float x = -16f + i * 16f;
                Ledge("Mall_Ledge", c + new Vector3(x - 6f, 0f, 8f), c + new Vector3(x + 6f, 0f, 8f), 0.45f, 0.9f, Marble);
            }
            Planter("Mall_Planter", c + new Vector3(18f, 0f, -2f), new Vector2(4f, 3f), Granite);
            level.gaps.Add(Gap(ParkCatalog.PlanterGap, "Planter Gap", c + new Vector3(18f, 1.6f, -2f), new Vector3(4.4f, 2.2f, 3.4f), 500));
        }

        /// <summary>Backyard Pool (70, -70): a deep round pool on a raised deck behind a fence line.</summary>
        private void BackyardPool()
        {
            var c = new Vector3(70f, 0f, -70f);
            BowlWithDeck("Pool", c, 3f, 3f, 2.6f, 9f, new Color(0.45f, 0.7f, 0.82f));
            MeshObject("Pool_DeckRamp", ProcMesh.Wedge(6f, 7f, 2.6f), c + new Vector3(0f, 0f, -16f), 0f, Palette.Concrete);
            Building("Pool_House", c + new Vector3(-20f, 0f, 14f), new Vector3(14f, 6f, 14f), Palette.Cream, false);
        }

        /// <summary>Loading Docks (-70, -70): a warehouse with a split dock (gap), a kicker and a free-standing wallride wall.</summary>
        private void LoadingDocks(LevelInfo level)
        {
            var c = new Vector3(-70f, 0f, -70f);
            Building("Warehouse", c + new Vector3(-6f, 0f, -22f), new Vector3(44f, 10f, 14f), Palette.WarehouseWall, true);
            foreach (var (x0, x1) in new[] { (-28f, -4f), (2f, 16f) })
            {
                Box("Dock", c + new Vector3((x0 + x1) * 0.5f, 0.6f, -12.5f), new Vector3(x1 - x0, 1.2f, 5f), Palette.Concrete);
                Rail("Dock_Edge", new List<Vector3> { c + new Vector3(x0 + 0.3f, 1.22f, -10f), c + new Vector3(x1 - 0.3f, 1.22f, -10f) }, GrindSurface.Ledge, false, _root);
            }
            MeshObject("Dock_Bank", ProcMesh.Wedge(6f, 4f, 1.2f), c + new Vector3(-16f, 0f, -6f), 180f, Palette.Concrete);
            Box("WallrideWall", c + new Vector3(14f, 2f, 10f), new Vector3(0.5f, 4f, 18f), Palette.Brick);
            Kicker("Dock_Kicker", c + new Vector3(4f, 0f, 2f), 60f);
            level.gaps.Add(Gap(ParkCatalog.DockGapCity, "Dock Gap", c + new Vector3(-1f, 2.4f, -12.5f), new Vector3(6f, 2.8f, 5f), 700));
        }

        /// <summary>The city edge: a ring of tall buildings (with lit windows) past the outer ring road.</summary>
        private void CityEdge()
        {
            float H = RetroCityLayout.HalfSize;
            var rng = new System.Random(808);
            Color[] colors = { Palette.Brick, Palette.Skyline, Palette.ContainerTeal, Palette.Cream, Palette.WarehouseWall };
            for (float t = -H; t < H; t += 18f)
            {
                foreach (var (pos, size) in new[]
                {
                    (new Vector3(t + 9f, 0f, H + 3f), new Vector3(17f, 0f, 12f)),
                    (new Vector3(t + 9f, 0f, -H - 3f), new Vector3(17f, 0f, 12f)),
                    (new Vector3(H + 3f, 0f, t + 9f), new Vector3(12f, 0f, 17f)),
                    (new Vector3(-H - 3f, 0f, t + 9f), new Vector3(12f, 0f, 17f)),
                })
                {
                    float height = 14f + (float)rng.NextDouble() * 30f;
                    // The north wall opens onto the Riverside Yards (rng still advances so the rest of the skyline is unchanged).
                    if (pos.z > H && Mathf.Abs(pos.x) < RetroCityLayout.YardsHalfWidth + 6f) continue;
                    Building("EdgeBuilding", pos, new Vector3(size.x, height, size.z), colors[rng.Next(colors.Length)], rng.Next(3) == 0);
                }
            }
            // Warehouses around the yards.
            float yw = RetroCityLayout.YardsHalfWidth, yn = RetroCityLayout.YardsNorth;
            for (float z = H + 4f; z < yn; z += 18f)
            {
                Building("YardShed", new Vector3(-yw - 9f, 0f, z + 9f), new Vector3(12f, 10f + (float)rng.NextDouble() * 8f, 17f), Palette.WarehouseWall, false);
                Building("YardShed", new Vector3(yw + 9f, 0f, z + 9f), new Vector3(12f, 10f + (float)rng.NextDouble() * 8f, 17f), Palette.Brick, false);
            }
            for (float x = -yw - 3f; x < yw + 3f; x += 18f)
                Building("YardShed", new Vector3(x + 9f, 0f, yn + 9f), new Vector3(17f, 12f, 12f), Palette.WarehouseWall, rng.Next(2) == 0);
        }

        // ---------------------------------------------------------------- Riverside Yards (Phase 13)

        /// <summary>
        /// Riverside Yards (0, 162): an old freight yard through a gap in the north wall. Two boxcars with ramps make a
        /// gap to clear, a pair of track rails runs the length of the yard, a loading platform has a long ledge,
        /// and a bank meets a brick wall for wallrides.
        /// </summary>
        private void RiversideYards(LevelInfo level)
        {
            // Track rails along the yard (low, grindable, with sleepers as decoration).
            foreach (float x in new[] { -6.7f, -5.3f })
            {
                var a = new Vector3(x, 0.15f, 132f);
                var b = new Vector3(x, 0.15f, 196f);
                Rail("Yard_Track", new List<Vector3> { a, b }, GrindSurface.Rail, false, _root);
                Bar(a, b, 0.05f, Palette.Metal, false);
            }
            for (float z = 133f; z < 196f; z += 1.6f)
                RemoveCollider(Box("Sleeper", new Vector3(-6f, 0.04f, z), new Vector3(2.4f, 0.08f, 0.3f), Palette.Wood));

            // Two boxcars end to end with a 4 m gap; ramps up onto the first and off the second.
            const float carH = 3.2f;
            Box("Boxcar_A", new Vector3(14f, carH * 0.5f, 150f), new Vector3(3.2f, carH, 12f), Palette.ContainerRed);
            Box("Boxcar_B", new Vector3(14f, carH * 0.5f, 166f), new Vector3(3.2f, carH, 12f), Palette.ContainerTeal);
            MeshObject("Boxcar_Ramp", ProcMesh.Wedge(3.2f, 7f, carH), new Vector3(14f, 0f, 137f), 0f, Palette.Wood);
            MeshObject("Boxcar_Exit", ProcMesh.Wedge(3.2f, 7f, carH), new Vector3(14f, 0f, 179f), 180f, Palette.Wood);
            foreach (float z in new[] { 150f, 166f })
                foreach (float x in new[] { 12.45f, 15.55f })
                {
                    var a = new Vector3(x, carH + 0.03f, z - 5.6f);
                    var b = new Vector3(x, carH + 0.03f, z + 5.6f);
                    Rail("Boxcar_Edge", new List<Vector3> { a, b }, GrindSurface.Ledge, false, _root);
                }
            level.gaps.Add(Gap(ParkCatalog.BoxcarGap, "Boxcar Gap", new Vector3(14f, carH + 1.2f, 158f), new Vector3(4f, 2.4f, 4f), 600));

            // Loading platform with a long ledge and stairs down to the tracks.
            const int steps = 5;
            float ph = steps * StepRise;
            Box("Yard_Platform", new Vector3(-26f, ph * 0.5f, 165f), new Vector3(12f, ph, 30f), Palette.Concrete);
            Ledge("Yard_PlatformLedge", new Vector3(-28f, ph, 154f), new Vector3(-28f, ph, 176f), 0.45f, 0.7f, Palette.ConcreteDark);
            Stairs("Yard_Stairs", new Vector3(-20f + steps * StepRun, 0f, 160f), Vector3.right, steps, 4f, Palette.ConcreteDark);
            Handrail("Yard_StairsRail", new Vector3(-20f, ph, 160f), Vector3.right, steps, 2.3f);

            // Bank to wall in the north-east corner.
            MeshObject("Yard_Bank", ProcMesh.Wedge(12f, 5f, 2.4f), new Vector3(30f, 0f, 186f), 0f, Palette.Concrete);
            Box("Yard_Wall", new Vector3(30f, 3f, 192.5f), new Vector3(16f, 6f, 1f), Palette.Brick);

            // Signal lamps and a water tower silhouette (visual only, no text).
            Neon("SignalLamp", new Vector3(-9f, 4f, 140f), new Vector3(0.4f, 0.4f, 0.4f), Palette.NeonPink);
            Neon("SignalLamp", new Vector3(-9f, 4f, 190f), new Vector3(0.4f, 0.4f, 0.4f), Palette.NeonLime);
            PrimitiveMeshes.CreateVisual("WaterTower", PrimitiveType.Cylinder, _root, new Vector3(38f, 12f, 140f), new Vector3(6f, 3f, 6f), Palette.ContainerMustard).isStatic = true;
            foreach (float dx in new[] { -2f, 2f })
                PrimitiveMeshes.CreateVisual("TowerLeg", PrimitiveType.Cylinder, _root, new Vector3(38f + dx, 4.5f, 140f), new Vector3(0.3f, 4.5f, 0.3f), Palette.Metal).isStatic = true;
        }
    }
}
