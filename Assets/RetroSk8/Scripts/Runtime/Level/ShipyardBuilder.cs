using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Shipyard (Phase 24): an original working dockyard at dawn.
    /// - North: a raised quay with mooring bollards to bonk. You reach it up a long slipway, and come back down a
    ///   gangway stair set with handrails, or straight off the quay edge.
    /// - Centre: two rows of stacked shipping containers with a gap between them (Container Canyon), with ramps up
    ///   and down each end.
    /// - South: a gantry crane over grindable crane tracks, with its cable hook hanging low enough to pole jam.
    /// Everything here is invented.
    /// </summary>
    public sealed class ShipyardBuilder : ParkBuilder
    {
        public const string GeneratedRootName = "Shipyard_Generated";
        protected override string RootName => GeneratedRootName;
        public override string LocationId => ParkCatalog.Shipyard;

        public const float QuayHeight = 2.42f;   // 11 stair risers
        public const float QuayEdgeZ = 24f;
        public const int GangwaySteps = 11;
        public const float GangwayX = 25f;
        public const float ContainerHeight = 2.6f;
        /// <summary>Container Canyon: the open gap between the two rows (x).</summary>
        public const float CanyonFromX = -4f, CanyonToX = 1f;
        public const float CraneX = 30f, CraneZ = -18f;

        private static readonly Color Quay = new Color(0.5f, 0.52f, 0.55f);
        private static readonly Color Yard = new Color(0.36f, 0.37f, 0.4f);
        private static readonly Color CraneYellow = new Color(0.95f, 0.7f, 0.12f);
        private static readonly Color Bollard = new Color(0.16f, 0.17f, 0.2f);

        protected override void BuildPark(LevelInfo level)
        {
            // Yard floor, harbour walls all round, and water beyond the north wall behind the quay (the kill plane catches falls).
            Box("Yard", new Vector3(0f, -0.5f, 0f), new Vector3(92f, 1f, 100f), Yard);
            Box("Wall_W", new Vector3(-46.5f, 1.5f, 0f), new Vector3(1f, 3f, 100f), Palette.ConcreteDark);
            Box("Wall_E", new Vector3(46.5f, 1.5f, 0f), new Vector3(1f, 3f, 100f), Palette.ConcreteDark);
            Box("Wall_S", new Vector3(0f, 1.5f, -50.5f), new Vector3(94f, 3f, 1f), Palette.ConcreteDark);
            Box("Wall_N", new Vector3(0f, 1.5f, 50.5f), new Vector3(94f, 3f, 1f), Palette.ConcreteDark);
            var water = Box("Harbour", new Vector3(0f, -0.6f, 80f), new Vector3(200f, 0.2f, 60f), Palette.Water);
            RemoveCollider(water);

            // ---- The quay (north): a raised deck reached by the slipway, with bollards and a gangway down.
            float quayDepth = 50f - QuayEdgeZ;
            Box("Quay", new Vector3(0f, QuayHeight * 0.5f, QuayEdgeZ + quayDepth * 0.5f), new Vector3(92f, QuayHeight, quayDepth), Quay);
            Neon("Quay_Edge", new Vector3(0f, QuayHeight + 0.02f, QuayEdgeZ + 0.25f), new Vector3(92f, 0.04f, 0.3f), Palette.TapeYellow);
            // Slipway: a long bank from the yard up to the quay (rises toward +z).
            MeshObject("Slipway", ProcMesh.Wedge(12f, 12f, QuayHeight), new Vector3(-22f, 0f, QuayEdgeZ - 12f), 0f, Palette.Concrete);
            // Gangway: stairs down from the quay edge toward the yard, a handrail each side.
            Vector3 gangwayBottom = new Vector3(GangwayX, 0f, QuayEdgeZ - GangwaySteps * StepRun);
            Vector3 gangwayTop = Stairs("Gangway", gangwayBottom, Vector3.back, GangwaySteps, 6f, Palette.Metal);
            Handrail("Gangway_Rail_L", gangwayTop, Vector3.back, GangwaySteps, -2.6f);
            Handrail("Gangway_Rail_R", gangwayTop, Vector3.back, GangwaySteps, 2.6f);
            // Mooring bollards along the quay (bonkable) and a long grindable kerb at the back of the quay.
            for (int i = 0; i < 6; i++)
            {
                float x = -36f + i * 12f;
                if (Mathf.Abs(x - GangwayX) < 5f || Mathf.Abs(x + 22f) < 7f) continue; // keep the stairs and the slipway top clear
                var b = Cylinder("Bollard", new Vector3(x, QuayHeight, QuayEdgeZ + 3f), 0.32f, 0.75f, Bollard);
                Bonkable(b);
            }
            Ledge("Quay_Kerb", new Vector3(-30f, QuayHeight, 44f), new Vector3(30f, QuayHeight, 44f), 0.45f, 0.6f, Palette.Concrete);
            Building("Dock_Office", new Vector3(36f, QuayHeight, 40f), new Vector3(10f, 4f, 8f), Palette.Brick, false);
            Neon("Dock_Office_Sign", new Vector3(36f, QuayHeight + 3.2f, 35.95f), new Vector3(6f, 0.4f, 0.1f), Palette.NeonCyan);

            // ---- Container Canyon (centre): two container rows with a 5 m gap; ramps up the west end and down the east.
            ContainerRow("Containers_W", -14f, CanyonFromX, new[] { Palette.ContainerRed, Palette.ContainerTeal });
            ContainerRow("Containers_E", CanyonToX, 11f, new[] { Palette.ContainerMustard, Palette.ContainerRed });
            MeshObject("Canyon_RampUp", ProcMesh.Wedge(5.2f, 6f, ContainerHeight), new Vector3(-20f, 0f, 0f), 90f, Palette.Plywood);
            MeshObject("Canyon_RampDown", ProcMesh.Wedge(5.2f, 6f, ContainerHeight), new Vector3(17f, 0f, 0f), -90f, Palette.Plywood);

            // ---- The gantry crane (south) over its tracks; the hook hangs low enough to pole jam.
            foreach (float dx in new[] { -6f, 6f })
                foreach (float dz in new[] { -4f, 4f })
                    Box("Crane_Leg", new Vector3(CraneX + dx, 6f, CraneZ + dz), new Vector3(0.8f, 12f, 0.8f), CraneYellow);
            var beam = Box("Crane_Beam", new Vector3(CraneX, 12.4f, CraneZ), new Vector3(14f, 0.8f, 9.6f), CraneYellow);
            RemoveCollider(beam);
            var cable = Box("Crane_Cable", new Vector3(CraneX, 6.7f, CraneZ), new Vector3(0.06f, 10.6f, 0.06f), Palette.Metal); // down to the hook
            RemoveCollider(cable);
            SlantedPost("Crane_Hook", new Vector3(CraneX, 0f, CraneZ), Vector3.forward, 1.4f, 18f, Palette.Metal, CraneYellow);
            foreach (float dx in new[] { -6f, 6f })
                Curb("Crane_Track", new Vector3(CraneX + dx * 0.5f, 0f, -44f), new Vector3(CraneX + dx * 0.5f, 0f, -6f)); // between the legs

            // ---- Odds and ends: a flat bar, a funbox, cones, and lamp posts with neon heads.
            var rail = new List<Vector3> { new Vector3(-30f, 0.5f, -30f), new Vector3(-30f, 0.5f, -16f) };
            Rail("Flat_Bar", rail, GrindSurface.Rail, false, _root);
            Bar(rail[0], rail[1], 0.05f, Palette.Metal, true);
            Funbox("Funbox", new Vector3(-12f, 0f, -34f), 3f, 2.5f, 0.9f, Palette.Concrete, true);
            foreach (var p in new[] { new Vector3(10f, 0f, -12f), new Vector3(12f, 0f, -13.5f), new Vector3(-36f, 0f, 8f), new Vector3(38f, 0f, 8f) })
                Cone("Cone", p);
            foreach (float x in new[] { -40f, -20f, 0f, 20f, 40f })
            {
                Cylinder("Lamp", new Vector3(x, 0f, -48f), 0.12f, 6f, Palette.Metal, false);
                Neon("Lamp_Head", new Vector3(x, 6.1f, -47.6f), new Vector3(1.2f, 0.2f, 0.6f), Palette.Cream);
            }

            level.gaps.Add(Gap(ParkCatalog.ContainerCanyon, "Container Canyon", new Vector3((CanyonFromX + CanyonToX) * 0.5f, ContainerHeight + 2.0f, 0f), new Vector3(CanyonToX - CanyonFromX - 0.4f, 1.6f, 5f), 500)); // above any ollie from the canyon floor
            level.gaps.Add(Gap(ParkCatalog.QuayDrop, "Quay Drop", new Vector3(8f, 4.2f, QuayEdgeZ - 0.8f), new Vector3(20f, 0.8f, 1.2f), 300)); // only reachable rolling off the quay
            level.gaps.Add(Gap(ParkCatalog.GangwaySet, "Gangway Set", new Vector3(GangwayX, QuayHeight + 3.1f, QuayEdgeZ - GangwaySteps * StepRun * 0.5f), new Vector3(5f, 1.2f, GangwaySteps * StepRun * 0.6f), 450)); // above an ollie on the steps

            KillPlane(-6f);
            level.playableBounds = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(100f, 40f, 104f));
            Spawn(level, new Vector3(0f, 0.05f, -24f), Vector3.forward); // in the yard, facing Container Canyon and the quay
        }

        /// <summary>A double-wide row of stacked containers from x0 to x1 along z = 0 (a flat top you can ride).</summary>
        private void ContainerRow(string name, float x0, float x1, Color[] colors)
        {
            float len = x1 - x0, cx = (x0 + x1) * 0.5f;
            for (int i = 0; i < 2; i++)
            {
                float z = i == 0 ? -1.3f : 1.3f;
                Box(name + "_" + i, new Vector3(cx, ContainerHeight * 0.5f, z), new Vector3(len, ContainerHeight, 2.6f), colors[i % colors.Length]);
                // Corrugation stripes (no colliders) so it reads as a container.
                for (float x = x0 + 0.6f; x < x1 - 0.3f; x += 1.2f)
                {
                    var rib = Box(name + "_Rib", new Vector3(x, ContainerHeight * 0.5f, z + (i == 0 ? -1.31f : 1.31f)), new Vector3(0.12f, ContainerHeight - 0.2f, 0.02f), Color.Lerp(colors[i % colors.Length], Palette.Ink, 0.3f));
                    RemoveCollider(rib);
                }
            }
        }
    }
}
