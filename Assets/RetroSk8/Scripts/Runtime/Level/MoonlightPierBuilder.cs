using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Moonlight Pier (Phase 15): an original seaside boardwalk at night. A shore plaza (funbox, ledges, benches, a
    /// raised promenade with a stair set, handrail and hubba) leads onto a long wooden pier with grindable railings.
    /// Halfway out a section of planks is missing: hit the kicker and clear the Plank Gap. The T-shaped pier head has a
    /// quarter pipe facing back to shore. Fall in the water and you wash back up. String lights and a lit wheel out on
    /// the water; everything here is invented.
    /// </summary>
    public sealed class MoonlightPierBuilder : ParkBuilder
    {
        public const string GeneratedRootName = "MoonlightPier_Generated";
        protected override string RootName => GeneratedRootName;
        public override string LocationId => ParkCatalog.MoonlightPier;

        private const float PromenadeHeight = 6 * StepRise; // 1.32 m
        private const float RailHeight = 0.95f;
        private const float GapStart = 22f, GapEnd = 26f;   // the missing planks
        private static readonly Color Boards = new Color(0.47f, 0.36f, 0.26f);
        private static readonly Color Pile = new Color(0.25f, 0.2f, 0.17f);
        private static readonly Color Sand = new Color(0.78f, 0.7f, 0.52f);

        protected override void BuildPark(LevelInfo level)
        {
            // Water everywhere (visual) and the shore plaza.
            Visual(new Vector3(0f, -1.2f, 20f), new Vector3(320f, 0.02f, 320f), Palette.Water, "Sea");
            Box("Ground", new Vector3(0f, -0.5f, -27.5f), new Vector3(70f, 1f, 35f), Palette.Paving);
            foreach (float x in new[] { -24f, 24f })
                Visual(new Vector3(x, -0.75f, -6f), new Vector3(22f, 0.5f, 8f), Sand, "Beach");
            Box("Fence_S", new Vector3(0f, 1.5f, -45.5f), new Vector3(70f, 3f, 1f), Palette.ConcreteDark);
            Box("Fence_W", new Vector3(-35.5f, 1.5f, -27.5f), new Vector3(1f, 3f, 35f), Palette.ConcreteDark);
            Box("Fence_E", new Vector3(35.5f, 1.5f, -27.5f), new Vector3(1f, 3f, 35f), Palette.ConcreteDark);

            // Raised promenade along the back with a 6-stair, a handrail and a hubba down to the plaza.
            Box("Promenade", new Vector3(0f, PromenadeHeight * 0.5f, -41.5f), new Vector3(70f, PromenadeHeight, 7f), Palette.Concrete);
            Stairs("Stairs_W", new Vector3(-15f, 0f, -38f + 6 * StepRun), Vector3.forward, 6, 4f, Palette.ConcreteDark);
            Handrail("Stairs_W_Rail", new Vector3(-15f, PromenadeHeight, -38f), Vector3.forward, 6, 2.4f);
            Stairs("Stairs_E", new Vector3(15f, 0f, -38f + 6 * StepRun), Vector3.forward, 6, 4f, Palette.ConcreteDark);
            Hubba("Hubba_E", new Vector3(15f, PromenadeHeight, -38f), Vector3.forward, 6, -2.4f, Palette.Concrete);
            Building("Arcade", new Vector3(-24f, PromenadeHeight, -43f), new Vector3(12f, 6f, 4f), Palette.Brick, true);
            Building("Snack_Bar", new Vector3(26f, PromenadeHeight, -43f), new Vector3(8f, 4f, 4f), Palette.ContainerTeal, false);
            Neon("Arcade_Sign", new Vector3(-24f, PromenadeHeight + 6.4f, -40.9f), new Vector3(9f, 0.6f, 0.15f), Palette.NeonPink);
            Neon("Snack_Sign", new Vector3(26f, PromenadeHeight + 4.4f, -40.9f), new Vector3(5f, 0.5f, 0.15f), Palette.NeonCyan);

            // Plaza: a funbox in the middle, ledges along the seawall, benches, planters and manual pads.
            Funbox("Funbox", new Vector3(0f, 0f, -26f), 3f, 2.5f, 0.9f, Palette.Concrete, true);
            Ledge("Seawall_W", new Vector3(-30f, 0f, -11.6f), new Vector3(-9f, 0f, -11.6f), 0.5f, 0.7f, Palette.Concrete);
            Ledge("Seawall_E", new Vector3(9f, 0f, -11.6f), new Vector3(30f, 0f, -11.6f), 0.5f, 0.7f, Palette.Concrete);
            Bench("Bench_W", new Vector3(-20f, 0f, -20f), 0f);
            Bench("Bench_E", new Vector3(20f, 0f, -20f), 0f);
            Planter("Planter_W", new Vector3(-10f, 0f, -17f), new Vector2(2f, 4f), Palette.Concrete);
            Planter("Planter_E", new Vector3(10f, 0f, -17f), new Vector2(2f, 4f), Palette.Concrete);
            ManualPad("Pad_W", new Vector3(-24f, 0f, -31f), new Vector2(6f, 2.4f), 0f, Palette.ConcreteDark);
            ManualPad("Pad_E", new Vector3(24f, 0f, -31f), new Vector2(6f, 2.4f), 0f, Palette.ConcreteDark);

            // The pier: two plank sections with a gap between them, then the T-shaped head.
            Box("Pier_A", new Vector3(0f, -0.5f, (-10f + GapStart) * 0.5f), new Vector3(12f, 1f, GapStart + 10f), Boards);
            Box("Pier_B", new Vector3(0f, -0.5f, (GapEnd + 44f) * 0.5f), new Vector3(12f, 1f, 44f - GapEnd), Boards);
            Box("Pier_Head", new Vector3(0f, -0.5f, 52f), new Vector3(24f, 1f, 16f), Boards);
            for (float z = -6f; z <= 58f; z += 6f)
            {
                if (z > GapStart - 1f && z < GapEnd + 1f) continue;
                float halfWidth = z > 44f ? 11f : 5.4f;
                foreach (float x in new[] { -halfWidth, halfWidth })
                    Cylinder("Pile", new Vector3(x, -4f, z), 0.25f, 3.1f, Pile, false);
            }
            // Broken plank stubs hanging over the gap (visual only).
            foreach (float z in new[] { GapStart + 0.3f, GapEnd - 0.3f })
                Visual(new Vector3(1.5f, -0.35f, z), new Vector3(4f, 0.12f, 0.6f), Boards, "Plank_Stub");

            // Grindable railings along both sides, broken where the planks are.
            RailRun("Rail_A", -8.5f, GapStart - 1f, 5.7f);
            RailRun("Rail_B", GapEnd + 1f, 43.5f, 5.7f);
            foreach (float x in new[] { -11.7f, 11.7f })
            {
                var a = new Vector3(x, RailHeight, 45f);
                var b = new Vector3(x, RailHeight, 59.3f);
                Rail("Rail_Head", new List<Vector3> { a, b }, GrindSurface.Rail, false, _root);
                Bar(a, b, 0.05f, Palette.Metal, true);
            }

            // The kicker for the Plank Gap, and a bait shack with a ledge on the far section.
            Kicker("Plank_Kicker", new Vector3(0f, 0f, GapStart - 3.6f), 0f, 3f, 2.5f, 0.8f);
            Neon("Gap_Warning", new Vector3(0f, 0.02f, GapStart - 0.4f), new Vector3(10f, 0.04f, 0.3f), Palette.TapeYellow);
            Box("Bait_Shack", new Vector3(-3.6f, 1.4f, 36f), new Vector3(4f, 2.8f, 4f), Palette.ContainerMustard);
            Ledge("Fishing_Ledge", new Vector3(3.2f, 0f, 29f), new Vector3(3.2f, 0f, 41f), 0.45f, 0.6f, Palette.Wood);

            // Pier head: a quarter pipe facing back to shore, benches either side.
            QuarterWithCoping("Head_QP", new Vector3(0f, 0f, 53.8f), 8f, 2.2f, 1.4f);
            Bench("Head_Bench_W", new Vector3(-8f, 0f, 50f), 90f);
            Bench("Head_Bench_E", new Vector3(8f, 0f, 50f), 90f);

            // String lights along the pier, a lit wheel out on the water and the moon.
            int n = 0;
            for (float z = -6f; z <= 42f; z += 6f, n++)
                foreach (float x in new[] { -5.9f, 5.9f })
                {
                    Cylinder("LampPost", new Vector3(x, 0f, z), 0.07f, 2.6f, Palette.Metal, false);
                    Neon("Bulb", new Vector3(x, 2.65f, z), new Vector3(0.3f, 0.3f, 0.3f), n % 2 == 0 ? Palette.NeonPink : Palette.NeonCyan);
                }
            var hub = new Vector3(-36f, 14f, 30f);
            Cylinder("Wheel_Leg", new Vector3(-36f, -1.2f, 27f), 0.3f, 15f, Palette.Metal, false);
            Cylinder("Wheel_Leg", new Vector3(-36f, -1.2f, 33f), 0.3f, 15f, Palette.Metal, false);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                var c = i % 3 == 0 ? Palette.NeonLime : i % 3 == 1 ? Palette.NeonViolet : Palette.NeonPink;
                Neon("Wheel_Light", hub + new Vector3(0f, Mathf.Sin(a) * 11f, Mathf.Cos(a) * 11f), new Vector3(0.6f, 0.6f, 0.6f), c);
            }
            Visual(new Vector3(70f, 60f, 170f), new Vector3(18f, 18f, 18f), Palette.Cream, "Moon", PrimitiveType.Sphere);

            level.gaps.Add(Gap(ParkCatalog.PlankGap, "Plank Gap", new Vector3(0f, 1.4f, (GapStart + GapEnd) * 0.5f), new Vector3(10f, 2.4f, GapEnd - GapStart), 650));
            level.gaps.Add(Gap(ParkCatalog.BoardwalkHop, "Boardwalk Hop", new Vector3(0f, 2.1f, -26f), new Vector3(3f, 1.2f, 8f), 350));
            level.gaps.Add(Gap(ParkCatalog.PierEndAir, "Pier End Air", new Vector3(0f, 4.6f, 55.6f), new Vector3(7f, 2f, 2f), 450));

            KillPlane(-4f);
            level.killHeight = -2.5f; // into the sea: wash back up
            level.playableBounds = new Bounds(new Vector3(0f, 10f, 7f), new Vector3(110f, 40f, 120f));
            Spawn(level, new Vector3(0f, 0.05f, -19f), Vector3.forward); // facing the pier
        }

        /// <summary>A grindable railing pair (both sides of the pier) from z0 to z1.</summary>
        private void RailRun(string name, float z0, float z1, float x)
        {
            foreach (float side in new[] { -x, x })
            {
                var a = new Vector3(side, RailHeight, z0);
                var b = new Vector3(side, RailHeight, z1);
                Rail(name, new List<Vector3> { a, b }, GrindSurface.Rail, false, _root);
                Bar(a, b, 0.05f, Palette.Metal, true);
            }
        }

        /// <summary>One quarter pipe rising toward +z from <paramref name="toe"/>, with a grindable coping.</summary>
        private void QuarterWithCoping(string name, Vector3 toe, float width, float radius, float deck)
        {
            var qp = MeshObject(name, ProcMesh.QuarterPipe(width, radius, deck), toe, 0f, Palette.Plywood);
            float lipAlong = radius * Mathf.Sin(88f * Mathf.Deg2Rad), lipY = radius - radius * Mathf.Cos(88f * Mathf.Deg2Rad);
            var a = toe + new Vector3(-width * 0.5f + 0.3f, lipY + 0.03f, lipAlong);
            var b = toe + new Vector3(width * 0.5f - 0.3f, lipY + 0.03f, lipAlong);
            Rail(name + "_Coping", new List<Vector3> { a, b }, GrindSurface.Coping, false, qp.transform);
            Bar(a, b, 0.07f, Palette.Coping, false);
        }

        private void Visual(Vector3 pos, Vector3 scale, Color color, string name, PrimitiveType type = PrimitiveType.Cube) =>
            PrimitiveMeshes.CreateVisual(name, type, _root, pos, scale, color).isStatic = true;
    }
}
