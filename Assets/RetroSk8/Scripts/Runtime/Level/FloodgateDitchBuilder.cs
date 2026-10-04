using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Floodgate Ditch: an original concrete drainage channel at dusk. You drop in from the top of the dam down a
    /// steep spillway into a long ditch with banked walls (coping on both lips, made for bank-to-bank transfers),
    /// launch off a kicker over the outlet wall, and session the service yards either side: a stair set with a
    /// handrail and a culvert mini ramp to the east, sluice ledges, a hubba and a flat bar to the west.
    /// Sized like a real flood-control channel: 4 m floor, 1.8 m banks, 4.4 m dam.
    /// </summary>
    public sealed class FloodgateDitchBuilder : ParkBuilder
    {
        public const string GeneratedRootName = "FloodgateDitch_Generated";
        protected override string RootName => GeneratedRootName;
        public override string LocationId => ParkCatalog.FloodgateDitch;

        private const float BankHeight = 8 * StepRise; // 1.76 m: an 8-stair drop to the yards
        private static readonly Color Dam = new Color(0.58f, 0.57f, 0.56f);
        private static readonly Color Ditch = new Color(0.66f, 0.64f, 0.6f);
        private static readonly Color Moss = new Color(0.32f, 0.42f, 0.3f);

        protected override void BuildPark(LevelInfo level)
        {
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(96f, 1f, 86f), Palette.Paving);
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, 1.5f, 43.5f), new Vector3(96f, 3f, 1f)), (new Vector3(0f, 1.5f, -43.5f), new Vector3(96f, 3f, 1f)),
                (new Vector3(48.5f, 1.5f, 0f), new Vector3(1f, 3f, 86f)), (new Vector3(-48.5f, 1.5f, 0f), new Vector3(1f, 3f, 86f)),
            })
                Box("Fence", pos, size, Palette.ConcreteDark);

            // The ditch: 4 m floor, banked walls up to decks on both sides (z -24 .. 23).
            const float zStart = -24f, zEnd = 23f;
            float len = zEnd - zStart, zMid = (zStart + zEnd) * 0.5f;
            MeshObject("Bank_E", ProcMesh.Wedge(len, 4f, BankHeight), new Vector3(2f, 0f, zMid), 90f, Ditch);
            MeshObject("Bank_W", ProcMesh.Wedge(len, 4f, BankHeight), new Vector3(-2f, 0f, zMid), -90f, Ditch);
            Box("Deck_E", new Vector3(13f, BankHeight * 0.5f, 2.5f), new Vector3(14f, BankHeight, 53f), Dam);
            Box("Deck_W", new Vector3(-13f, BankHeight * 0.5f, 2.5f), new Vector3(14f, BankHeight, 53f), Dam);
            foreach (float x in new[] { 6.05f, -6.05f })
            {
                var a = new Vector3(x, BankHeight + 0.03f, zStart + 0.6f);
                var b = new Vector3(x, BankHeight + 0.03f, zEnd - 0.6f);
                Rail(x > 0 ? "Coping_E" : "Coping_W", new List<Vector3> { a, b }, GrindSurface.Coping, false, _root);
                Bar(a, b, 0.07f, Palette.Coping, false);
            }
            // A trickle of water down the middle and moss along the floor edges (visual only).
            PrimitiveMeshes.CreateVisual("Trickle", PrimitiveType.Cube, _root, new Vector3(0f, 0.012f, zMid), new Vector3(0.9f, 0.02f, len), Palette.Water).isStatic = true;
            foreach (float x in new[] { 1.8f, -1.8f })
                PrimitiveMeshes.CreateVisual("Moss", PrimitiveType.Cube, _root, new Vector3(x, 0.01f, zMid), new Vector3(0.4f, 0.02f, len), Moss).isStatic = true;

            // The dam and its spillway: the drop-in.
            const float damHeight = 4.4f;
            Box("Dam", new Vector3(0f, damHeight * 0.5f, 33f), new Vector3(40f, damHeight, 8f), Dam);
            MeshObject("Spillway", ProcMesh.Wedge(12f, 6f, damHeight), new Vector3(0f, 0f, zEnd), 0f, Ditch);
            var lipA = new Vector3(-5.5f, damHeight + 0.03f, 29.05f);
            var lipB = new Vector3(5.5f, damHeight + 0.03f, 29.05f);
            Rail("Spillway_Lip", new List<Vector3> { lipA, lipB }, GrindSurface.Coping, false, _root);
            Bar(lipA, lipB, 0.07f, Palette.Coping, false);
            Ledge("Dam_Ledge", new Vector3(-16f, damHeight, 34f), new Vector3(-8f, damHeight, 34f), 0.45f, 0.7f, Palette.ConcreteDark);
            // Hazard stripes and gate lamps (abstract shapes, no text).
            Neon("Hazard", new Vector3(0f, damHeight - 0.4f, 28.95f), new Vector3(30f, 0.18f, 0.1f), Palette.TapeYellow);
            Neon("Hazard", new Vector3(0f, damHeight - 0.8f, 28.95f), new Vector3(26f, 0.18f, 0.1f), Palette.TapeYellow);
            foreach (float x in new[] { -18f, 18f })
            {
                Cylinder("GatePost", new Vector3(x, damHeight, 30f), 0.3f, 3f, Palette.Metal, false);
                Neon("GateLamp", new Vector3(x, damHeight + 3.1f, 30f), new Vector3(0.5f, 0.3f, 0.5f), Palette.NeonCyan);
            }
            // Stairs from both decks up to the dam top (12 steps = the 2.64 m from deck to dam).
            foreach (float x in new[] { 13f, -13f })
                Stairs(x > 0 ? "DamStairs_E" : "DamStairs_W", new Vector3(x, BankHeight, 29f - 12 * StepRun), Vector3.back, 12, 3f, Palette.ConcreteDark);

            // The outlet: a kicker in the channel launches over the outlet wall.
            Kicker("Outlet_Kicker", new Vector3(0f, 0f, -21f), 180f, 3f, 2.5f, 0.8f);
            Box("Outlet_Wall", new Vector3(0f, 0.5f, -27f), new Vector3(14f, 1f, 1f), Palette.ConcreteDark);
            PrimitiveMeshes.CreateVisual("Outlet_Pool", PrimitiveType.Cube, _root, new Vector3(0f, 0.012f, -33f), new Vector3(14f, 0.02f, 10f), Palette.Water).isStatic = true;

            // East yard: an 8-stair with a handrail and a culvert mini ramp.
            var stairBottom = new Vector3(20f + 8 * StepRun, 0f, -14f);
            Stairs("Stairs_E", stairBottom, Vector3.right, 8, 4f, Palette.ConcreteDark);
            Handrail("Stairs_E_Rail", new Vector3(20f, BankHeight, -14f), Vector3.right, 8, 2.4f);
            MiniRamp("Culvert", new Vector3(36f, 0f, 8f), 0f, 8f, 1.8f, 5f, 1.2f);
            ManualPad("Pad_E", new Vector3(36f, 0f, -26f), new Vector2(6f, 2.4f), 0f, Palette.Concrete);

            // West yard: sluice ledges, a hubba down from the deck and a flat bar.
            Ledge("Sluice_A", new Vector3(-26f, 0f, -20f), new Vector3(-26f, 0f, -4f), 0.5f, 0.7f, Palette.Concrete);
            Ledge("Sluice_B", new Vector3(-34f, 0f, -14f), new Vector3(-34f, 0f, 2f), 0.4f, 0.7f, Palette.Concrete);
            Stairs("Stairs_W", new Vector3(-20f - 8 * StepRun, 0f, 14f), Vector3.left, 8, 3f, Palette.ConcreteDark);
            Hubba("Hubba_W", new Vector3(-20f, BankHeight, 14f), Vector3.left, 8, -2f, Palette.Concrete);
            var ra = new Vector3(-40f, 0.45f, 10f);
            var rb = new Vector3(-40f, 0.45f, 26f);
            Rail("FlatBar_W", new List<Vector3> { ra, rb }, GrindSurface.Rail, false, _root);
            Bar(ra, rb, 0.05f, Palette.Metal, true);

            level.gaps.Add(Gap(ParkCatalog.DitchTransfer, "Ditch Transfer", new Vector3(0f, BankHeight + 1.1f, zMid), new Vector3(8f, 1.6f, len - 4f), 650));
            level.gaps.Add(Gap(ParkCatalog.OutletGap, "Outlet Gap", new Vector3(0f, 2f, -27f), new Vector3(6f, 2.6f, 3f), 500));
            level.gaps.Add(Gap(ParkCatalog.SpillwayDrop, "Spillway Drop", new Vector3(0f, damHeight + 1.2f, 27.5f), new Vector3(10f, 2f, 3f), 400));

            KillPlane(-6f);
            level.killHeight = -3.5f;
            level.playableBounds = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(100f, 40f, 90f));
            Spawn(level, new Vector3(0f, damHeight + 0.05f, 33f), Vector3.back); // drop in from the dam
        }
    }
}
