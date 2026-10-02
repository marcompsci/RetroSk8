using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Builds the Harbor Plaza greybox from primitives and procedural meshes. Original layout:
    /// north quarter pipes, drained fountain bowl at the centre, shipping-container line to the east,
    /// planter ledges and bars to the west, and a sea-wall ledge along the southern waterfront.
    /// Units are metres; the plaza floor is y = 0, the water is south of z = -35.
    /// </summary>
    public sealed class HarborPlazaBuilder : ParkBuilder
    {
        public const string GeneratedRootName = "HarborPlaza_Generated";
        protected override string RootName => GeneratedRootName;
        public override string LocationId => ParkCatalog.HarborPlaza;

        protected override void BuildPark(LevelInfo level)
        {
            BuildGroundAndBounds(level);
            BuildNorthQuarterPipes();
            BuildFountain();
            BuildContainerLine();
            BuildWestSection();
            BuildWaterfront();
            BuildProps();

            level.gaps.Add(Gap(ParkCatalog.FountainGap, "Fountain Gap", new Vector3(0f, 3.5f, 0f), new Vector3(4.4f, 3f, 4.4f), 750));
            level.gaps.Add(Gap(ParkCatalog.ContainerGap, "Container Gap", new Vector3(30f, 4.5f, 3f), new Vector3(4.8f, 3.4f, 4.8f), 1000));
            Spawn(level, new Vector3(0f, 0.05f, 25f), Vector3.back);
        }

        // ---------------------------------------------------------------- sections

        private void BuildGroundAndBounds(LevelInfo level)
        {
            Box("Ground", new Vector3(0f, -0.5f, 0.5f), new Vector3(92f, 1f, 71f), Palette.Paving);
            // Paving stripes: purely visual inlays to help read speed and direction.
            for (int i = -4; i <= 4; i++)
            {
                var stripe = Box("PavingStripe", new Vector3(i * 10f, 0.005f, 0.5f), new Vector3(0.25f, 0.01f, 71f), Palette.ConcreteDark);
                RemoveCollider(stripe);
            }

            Box("Wall_North", new Vector3(0f, 4f, 36.5f), new Vector3(92f, 8f, 1f), Palette.Brick);
            Box("Wall_West", new Vector3(-46.5f, 4f, 0.5f), new Vector3(1f, 8f, 71f), Palette.Brick);
            Box("Wall_East", new Vector3(46.5f, 4f, 0.5f), new Vector3(1f, 8f, 71f), Palette.Brick);

            var water = Box("Water", new Vector3(0f, -1.6f, -80f), new Vector3(400f, 0.2f, 90f), Palette.Water);
            RemoveCollider(water);

            KillPlane(-6f);

            level.killHeight = -3.5f;
            level.playableBounds = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(100f, 40f, 90f));
        }

        private void BuildNorthQuarterPipes()
        {
            const float radius = 3f, deck = 1.6f, toeZ = 31f, width = 18f;
            foreach (float x in new[] { -14f, 14f })
            {
                var qp = MeshObject("QuarterPipe", ProcMesh.QuarterPipe(width, radius, deck), new Vector3(x, 0f, toeZ), 0f, Palette.Concrete);
                float lipZ = toeZ + radius * Mathf.Sin(88f * Mathf.Deg2Rad);
                float lipY = radius - radius * Mathf.Cos(88f * Mathf.Deg2Rad);
                var a = new Vector3(x - width * 0.5f + 0.3f, lipY + 0.03f, lipZ);
                var b = new Vector3(x + width * 0.5f - 0.3f, lipY + 0.03f, lipZ);
                Rail("Coping", new List<Vector3> { a, b }, GrindSurface.Coping, false, qp.transform);
                Bar(a, b, 0.07f, Palette.Coping, false);
            }
        }

        private void BuildFountain()
        {
            const int segs = 24;
            const float rimH = 1.1f;
            MeshObject("Fountain_OuterBank", ProcMesh.RingBank(4.4f, 6.4f, rimH, 0f, segs), Vector3.zero, 0f, Palette.Concrete);
            MeshObject("Fountain_Rim", ProcMesh.RingBank(4.0f, 4.4f, rimH, rimH, segs), Vector3.zero, 0f, Palette.Coping);
            MeshObject("Fountain_InnerBank", ProcMesh.RingBank(2.2f, 4.0f, 0f, rimH, segs), Vector3.zero, 0f, Palette.ConcreteDark);

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Place(pedestal, "Fountain_Pedestal", new Vector3(0f, 0.7f, 0f), new Vector3(1.6f, 0.7f, 1.6f), Palette.Concrete);
            var tier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Place(tier, "Fountain_Tier", new Vector3(0f, 1.6f, 0f), new Vector3(0.7f, 0.2f, 0.7f), Palette.Teal);

            // The rim is one continuous grindable loop; the segment count matches the mesh so the rail sits on the flats.
            var ring = new List<Vector3>();
            for (int i = 0; i < segs; i++)
            {
                float a = Mathf.PI * 2f * (i + 0.5f) / segs;
                ring.Add(new Vector3(Mathf.Cos(a) * 4.2f, rimH + 0.02f, Mathf.Sin(a) * 4.2f));
            }
            Rail("Fountain_RimRail", ring, GrindSurface.Ledge, true, _root);
        }

        private void BuildContainerLine()
        {
            const float h = 2.6f, w = 2.4f;
            Color[] colors = { Palette.ContainerRed, Palette.ContainerTeal, Palette.ContainerMustard, Palette.ContainerTeal };
            int c = 0;
            foreach (float zc in new[] { 12f, -6f })
            {
                foreach (float xc in new[] { 28.8f, 31.2f })
                {
                    Box("ShippingContainer", new Vector3(xc, h * 0.5f, zc), new Vector3(w, h, 12f), colors[c++ % colors.Length]);
                }
                float z0 = zc - 6f, z1 = zc + 6f;
                Rail("Container_EdgeWest", new List<Vector3> { new Vector3(27.6f, h + 0.02f, z0 + 0.3f), new Vector3(27.6f, h + 0.02f, z1 - 0.3f) }, GrindSurface.Ledge, false, _root);
                Rail("Container_EdgeEast", new List<Vector3> { new Vector3(32.4f, h + 0.02f, z0 + 0.3f), new Vector3(32.4f, h + 0.02f, z1 - 0.3f) }, GrindSurface.Ledge, false, _root);
            }
            // Access bank from the north, run-out bank to the south; the 6 m gap between the stacks is the container gap.
            MeshObject("Container_AccessBank", ProcMesh.Wedge(4.8f, 6f, h), new Vector3(30f, 0f, 24f), 180f, Palette.Concrete);
            MeshObject("Container_RunoutBank", ProcMesh.Wedge(4.8f, 6f, h), new Vector3(30f, 0f, -18f), 0f, Palette.Concrete);
        }

        private void BuildWestSection()
        {
            // Long concrete bank against the west wall.
            MeshObject("West_Bank", ProcMesh.Wedge(40f, 5f, 2f), new Vector3(-40.5f, 0f, 0f), -90f, Palette.Concrete);

            // Planter ledge with grindable top edges.
            Box("Planter_Ledge", new Vector3(-30f, 0.25f, 12f), new Vector3(1.2f, 0.5f, 14f), Palette.ConcreteDark);
            Rail("Planter_EdgeA", new List<Vector3> { new Vector3(-30.6f, 0.52f, 5.3f), new Vector3(-30.6f, 0.52f, 18.7f) }, GrindSurface.Ledge, false, _root);
            Rail("Planter_EdgeB", new List<Vector3> { new Vector3(-29.4f, 0.52f, 5.3f), new Vector3(-29.4f, 0.52f, 18.7f) }, GrindSurface.Ledge, false, _root);

            // Flat bar.
            var a = new Vector3(-20f, 0.55f, -14f);
            var b = new Vector3(-20f, 0.55f, 6f);
            Rail("FlatBar_West", new List<Vector3> { a, b }, GrindSurface.Rail, false, _root);
            Bar(a, b, 0.06f, Palette.Metal, true);

            // Kicker for air practice, aimed south on a clear lane.
            MeshObject("Kicker_North", ProcMesh.Wedge(3f, 3f, 0.9f), new Vector3(-10f, 0f, 17f), 180f, Palette.TapeYellow);

            // Manual pad: low block you ollie onto.
            Box("ManualPad", new Vector3(-8f, 0.18f, -12f), new Vector3(4f, 0.36f, 8f), Palette.Concrete);
        }

        private void BuildWaterfront()
        {
            Box("SeaWall", new Vector3(0f, 0.35f, -31f), new Vector3(84f, 0.7f, 0.8f), Palette.ConcreteDark);
            Rail("SeaWall_Ledge", new List<Vector3> { new Vector3(-41.6f, 0.72f, -31f), new Vector3(41.6f, 0.72f, -31f) }, GrindSurface.Ledge, false, _root);

            // Bank-to-sea-wall: ride up onto the wall and drop to the promenade.
            MeshObject("SeaWall_Bank", ProcMesh.Wedge(8f, 4.2f, 0.7f), new Vector3(20f, 0f, -26.4f), 180f, Palette.Concrete);

            // Promenade bar running along the waterfront.
            var a = new Vector3(-16f, 0.55f, -24f);
            var b = new Vector3(6f, 0.55f, -24f);
            Rail("Promenade_Bar", new List<Vector3> { a, b }, GrindSurface.Rail, false, _root);
            Bar(a, b, 0.06f, Palette.Metal, true);

            // Mooring bollards at the water edge.
            for (float x = -40f; x <= 40f; x += 8f)
            {
                var bollard = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Place(bollard, "Bollard", new Vector3(x, 0.4f, -34.5f), new Vector3(0.5f, 0.4f, 0.5f), Palette.Ink);
            }
        }

        private void BuildProps()
        {
            // Facade blocks along the north wall (warehouse fronts) for silhouette and colour.
            Color[] facade = { Palette.Coral, Palette.TapeYellow, Palette.Teal, Palette.Cream };
            for (int i = 0; i < 8; i++)
            {
                float x = -40f + i * 11.4f;
                var f = Box("Facade", new Vector3(x, 9f, 37.5f), new Vector3(10f, 18f, 1f), Color.Lerp(facade[i % facade.Length], Color.black, 0.15f));
                RemoveCollider(f);
            }
            // Lamp posts: visual only (no collider) so they never cause unfair bails.
            foreach (var p in new[] { new Vector3(-9f, 0f, 3f), new Vector3(9f, 0f, 3f), new Vector3(-9f, 0f, -9f), new Vector3(9f, 0f, -9f) })
            {
                var post = PrimitiveMeshes.CreateVisual("LampPost", PrimitiveType.Cylinder, _root, p + Vector3.up * 2f, new Vector3(0.15f, 2f, 0.15f), Palette.Ink);
                PrimitiveMeshes.CreateVisual("LampHead", PrimitiveType.Sphere, post.transform.parent, p + Vector3.up * 4.1f, Vector3.one * 0.5f, Palette.Cream);
            }
        }

    }
}
