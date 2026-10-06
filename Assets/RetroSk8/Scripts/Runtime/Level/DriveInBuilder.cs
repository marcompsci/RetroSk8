using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Twin Screen Drive-In (Phase 18): an original old-time drive-in movie lot at night, built for bonks. Two giant
    /// screens at the north end (long banks in front of them launch you into wallrides, with a quarter pipe between
    /// them); three rows of parking curbs lined with slanted speaker posts to pole jam; parked cars to bonk or jump
    /// (the Car Hop in the centre aisle); a snack bar with picnic benches, a funbox and a mini ramp at the south end.
    /// Everything here is invented.
    /// </summary>
    public sealed class DriveInBuilder : ParkBuilder
    {
        public const string GeneratedRootName = "DriveIn_Generated";
        protected override string RootName => GeneratedRootName;
        public override string LocationId => ParkCatalog.DriveIn;

        /// <summary>The parking rows' curb lines (z). Speaker posts stand just south of each.</summary>
        public static readonly float[] Rows = { 2f, 14f, 26f };
        /// <summary>Speaker post x positions along every row (the centre aisle |x| &lt; 4 stays open).</summary>
        public static readonly float[] PostX = { -27f, -21f, -15f, -9f, 9f, 15f, 21f, 27f };
        public const float AisleHalfWidth = 4f;
        public const float CarHopZ = -3.5f;

        private static readonly Color Asphalt = new Color(0.17f, 0.17f, 0.2f);
        private static readonly Color ScreenFrame = new Color(0.82f, 0.8f, 0.76f);
        private static readonly Color Fence = new Color(0.36f, 0.27f, 0.2f);

        protected override void BuildPark(LevelInfo level)
        {
            // The lot, the wooden fence and the screens.
            Box("Lot", new Vector3(0f, -0.5f, 0f), new Vector3(90f, 1f, 100f), Asphalt);
            Box("Fence_W", new Vector3(-45.5f, 1.5f, 0f), new Vector3(1f, 3f, 100f), Fence);
            Box("Fence_E", new Vector3(45.5f, 1.5f, 0f), new Vector3(1f, 3f, 100f), Fence);
            Box("Fence_S", new Vector3(0f, 1.5f, -50.5f), new Vector3(92f, 3f, 1f), Fence);
            Box("Fence_N", new Vector3(0f, 1.5f, 50.5f), new Vector3(92f, 3f, 1f), Fence);
            foreach (float x in new[] { -17f, 17f })
            {
                string side = x < 0f ? "W" : "E";
                Box("Screen_" + side, new Vector3(x, 6.5f, 44.5f), new Vector3(24f, 13f, 1f), ScreenFrame);
                Neon("Screen_" + side + "_Picture", new Vector3(x, 7.5f, 43.95f), new Vector3(22f, 9f, 0.08f), new Color(0.9f, 0.88f, 0.95f));
                // A long bank in front of each screen: ride up it and press action at the screen to wallride.
                MeshObject("Screen_" + side + "_Bank", ProcMesh.Wedge(18f, 4f, 1.6f), new Vector3(x, 0f, 36f), 0f, Palette.ConcreteDark);
            }
            QuarterWithCoping("Intermission_QP", new Vector3(0f, 0f, 38f), 8f, 2.4f, 1.6f);

            // Parking rows: grindable curbs with slanted speaker posts to pole jam, and cars to bonk.
            foreach (float z in Rows)
            {
                Curb("Row_" + z + "_W", new Vector3(-31f, 0f, z), new Vector3(-AisleHalfWidth - 0.5f, 0f, z));
                Curb("Row_" + z + "_E", new Vector3(AisleHalfWidth + 0.5f, 0f, z), new Vector3(31f, 0f, z));
                for (int i = 0; i < PostX.Length; i++)
                    SlantedPost("Speaker", new Vector3(PostX[i], 0f, z - 0.6f), Vector3.forward, 1.25f, 22f, Palette.Metal, Palette.Ink);
            }
            var cars = new (float x, float z, Color color)[]
            {
                (-24f, 2f, Palette.ContainerRed), (-12f, 2f, Palette.Teal), (18f, 2f, Palette.ContainerMustard),
                (-18f, 14f, Palette.NeonViolet), (12f, 14f, Palette.Coral), (24f, 14f, Palette.ContainerTeal),
                (-9f + 3f, 26f, Palette.Cream), (21f, 26f, Palette.ContainerRed),
            };
            int n = 0;
            foreach (var (x, z, color) in cars)
                ParkedCar("Car_" + n++, new Vector3(x, 0f, z + 2.8f), 0f, color);

            // The Car Hop: a kicker in the centre aisle and a car parked across it. Clear it, or bonk it.
            Kicker("CarHop_Kicker", new Vector3(0f, 0f, -9f), 0f, 3f, 2.5f, 0.8f);
            ParkedCar("CarHop_Car", new Vector3(0f, 0f, CarHopZ), 90f, Palette.TapeYellow);
            Neon("CarHop_Line", new Vector3(0f, 0.02f, -6.2f), new Vector3(6f, 0.04f, 0.3f), Palette.TapeYellow);

            // South end: the snack bar with picnic benches, a funbox, a mini ramp and some cones to bonk.
            Building("Snack_Bar", new Vector3(0f, 0f, -36f), new Vector3(14f, 4f, 8f), Palette.Brick, false);
            Neon("Snack_Sign", new Vector3(0f, 4.6f, -31.9f), new Vector3(10f, 0.6f, 0.15f), Palette.NeonPink);
            Neon("Snack_Stripe", new Vector3(0f, 2.6f, -31.95f), new Vector3(12f, 0.2f, 0.1f), Palette.NeonCyan);
            Ledge("Snack_Counter", new Vector3(-5f, 0f, -31.2f), new Vector3(5f, 0f, -31.2f), 0.9f, 0.6f, Palette.Concrete);
            Bench("Picnic_1", new Vector3(-10f, 0f, -26f), 90f);
            Bench("Picnic_2", new Vector3(-10f, 0f, -22f), 90f);
            Bench("Picnic_3", new Vector3(10f, 0f, -26f), 90f);
            Bench("Picnic_4", new Vector3(10f, 0f, -22f), 90f);
            Funbox("Funbox", new Vector3(-24f, 0f, -30f), 3f, 2.5f, 0.9f, Palette.Concrete, true);
            MiniRamp("MiniRamp", new Vector3(26f, 0f, -32f), 90f, 6f, 2f, 3f, 1.2f);
            var rail = new List<Vector3> { new Vector3(-30f, 0.5f, -14f), new Vector3(-30f, 0.5f, -4f) };
            Rail("Flat_Rail", rail, GrindSurface.Rail, false, _root);
            Bar(rail[0], rail[1], 0.05f, Palette.Metal, true);
            foreach (var p in new[] { new Vector3(-6f, 0f, -15f), new Vector3(-4.6f, 0f, -16.4f), new Vector3(6f, 0f, -15f), new Vector3(4.6f, 0f, -16.4f), new Vector3(-36f, 0f, -18f), new Vector3(-34f, 0f, -20f) })
                Cone("Cone", p);
            Hydrant("Hydrant", new Vector3(9f, 0f, -30f));
            Hydrant("Hydrant", new Vector3(-36f, 0f, 8f));

            // The marquee at the gate: a tall sign of plain neon bars (no text) and a scatter of stars.
            Cylinder("Marquee_Pole", new Vector3(38f, 0f, -44f), 0.25f, 9f, Palette.Metal, false);
            Box("Marquee", new Vector3(38f, 10f, -44f), new Vector3(8f, 3f, 0.6f), Palette.Ink);
            for (int i = 0; i < 3; i++)
                Neon("Marquee_Bar", new Vector3(38f, 9f + i * 0.9f, -44.35f), new Vector3(7f - i, 0.3f, 0.1f), i % 2 == 0 ? Palette.NeonPink : Palette.TapeYellow);
            var rng = new System.Random(18);
            for (int i = 0; i < 40; i++)
            {
                float x = (float)(rng.NextDouble() * 220.0 - 110.0), z = (float)(rng.NextDouble() * 120.0 + 60.0), y = (float)(rng.NextDouble() * 60.0 + 30.0);
                Neon("Star", new Vector3(x, y, z), Vector3.one * 0.5f, Palette.Cream);
            }

            level.gaps.Add(Gap(ParkCatalog.CarHop, "Car Hop", new Vector3(0f, 2.2f, CarHopZ), new Vector3(4.6f, 1.4f, 2.6f), 500));
            level.gaps.Add(Gap(ParkCatalog.IntermissionAir, "Intermission Air", new Vector3(0f, 4.6f, 40.4f), new Vector3(7f, 2f, 2f), 450));
            level.gaps.Add(Gap(ParkCatalog.SnackBarHop, "Snack Bar Hop", new Vector3(-24f, 2.1f, -30f), new Vector3(3f, 1.2f, 8f), 350));

            KillPlane(-6f);
            level.playableBounds = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(100f, 40f, 110f));
            Spawn(level, new Vector3(0f, 0.05f, -20f), Vector3.forward); // facing the Car Hop and the screens
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
    }
}
