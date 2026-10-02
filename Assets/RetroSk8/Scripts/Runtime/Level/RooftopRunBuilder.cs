using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Rooftop Run: an original two-roof park above an invented skyline.
    /// Roof A (y = 0) has a drainage channel you can ride through or jump across, air vents and a vent duct,
    /// parapet ledges, and two kickers aimed at the rooftop gap. Roof B (y = -1.5, across a 6 m drop) has
    /// water towers with grindable bases, a plywood construction quarter pipe and a scaffold bar.
    /// Falling between the roofs sends you back to safety.
    /// </summary>
    public sealed class RooftopRunBuilder : ParkBuilder
    {
        public const string GeneratedRootName = "RooftopRun_Generated";
        protected override string RootName => GeneratedRootName;
        public override string LocationId => ParkCatalog.RooftopRun;

        private const float RoofBY = -1.5f;
        private const float ChannelY = -1.2f;

        protected override void BuildPark(LevelInfo level)
        {
            BuildRoofA(level);
            BuildRoofB();
            BuildSkyline();

            level.gaps.Add(Gap(ParkCatalog.RooftopGap, "Rooftop Gap", new Vector3(13f, 2f, 0f), new Vector3(6f, 8f, 40f), 1200));
            level.gaps.Add(Gap(ParkCatalog.ChannelGap, "Channel Gap", new Vector3(-10f, 2f, 0f), new Vector3(38f, 2f, 3.2f), 500));

            KillPlane(-12f);
            level.killHeight = -6f;
            level.playableBounds = new Bounds(new Vector3(5f, 5f, 0f), new Vector3(100f, 60f, 70f));
            // Spawn faces west along a clear lane; turn around to line up the kickers and the rooftop gap.
            Spawn(level, new Vector3(4f, 0.05f, 13f), Vector3.left);
        }

        private void BuildRoofA(LevelInfo level)
        {
            // Two slabs either side of the drainage channel (x -30..10).
            Box("RoofA_North", new Vector3(-10f, -0.5f, 12.5f), new Vector3(40f, 1f, 19f), Palette.RoofTar);
            Box("RoofA_South", new Vector3(-10f, -0.5f, -12.5f), new Vector3(40f, 1f, 19f), Palette.RoofTar);

            // Drainage channel: sloped sides down to a floor 1.2 m below the roof.
            Box("Channel_Floor", new Vector3(-10f, ChannelY - 0.5f, 0f), new Vector3(40f, 1f, 2f), Palette.ConcreteDark);
            MeshObject("Channel_BankNorth", ProcMesh.Wedge(40f, 2f, -ChannelY), new Vector3(-10f, ChannelY, 1f), 0f, Palette.Concrete);
            MeshObject("Channel_BankSouth", ProcMesh.Wedge(40f, 2f, -ChannelY), new Vector3(-10f, ChannelY, -1f), 180f, Palette.Concrete);
            Box("Channel_EndWest", new Vector3(-30.25f, -0.15f, 0f), new Vector3(0.5f, 2.1f, 6f), Palette.Parapet);
            Box("Channel_EndEast", new Vector3(10.25f, -0.15f, 0f), new Vector3(0.5f, 2.1f, 6f), Palette.Parapet);

            // Parapets with grindable tops (the east edge stays open toward the gap).
            Parapet("Parapet_North", new Vector3(-30f, 0f, 21.75f), new Vector3(10f, 0f, 21.75f));
            Parapet("Parapet_South", new Vector3(-30f, 0f, -21.75f), new Vector3(10f, 0f, -21.75f));
            Parapet("Parapet_WestN", new Vector3(-29.75f, 0f, 3f), new Vector3(-29.75f, 0f, 21.5f));
            Parapet("Parapet_WestS", new Vector3(-29.75f, 0f, -21.5f), new Vector3(-29.75f, 0f, -3f));

            // Kickers aimed across the rooftop gap.
            MeshObject("Gap_KickerNorth", ProcMesh.Wedge(6f, 3.5f, 1f), new Vector3(6f, 0f, 8f), 90f, Palette.Plywood);
            MeshObject("Gap_KickerSouth", ProcMesh.Wedge(6f, 3.5f, 1f), new Vector3(6f, 0f, -10f), 90f, Palette.Plywood);

            // Air vents and a long vent duct with grindable edges.
            foreach (var p in new[] { new Vector3(-20f, 0.5f, -14f), new Vector3(-12f, 0.5f, -17f), new Vector3(-2f, 0.5f, -13f) })
            {
                Box("AirVent", p, new Vector3(1.4f, 1f, 1.4f), Palette.Metal);
                Box("AirVentCap", p + new Vector3(0f, 0.6f, 0f), new Vector3(1.7f, 0.2f, 1.7f), Palette.ConcreteDark);
            }
            Box("VentDuct", new Vector3(-21f, 0.45f, -8f), new Vector3(10f, 0.9f, 1.2f), Palette.Metal);
            Rail("VentDuct_EdgeN", new List<Vector3> { new Vector3(-25.7f, 0.92f, -7.4f), new Vector3(-16.3f, 0.92f, -7.4f) }, GrindSurface.Ledge, false, _root);
            Rail("VentDuct_EdgeS", new List<Vector3> { new Vector3(-25.7f, 0.92f, -8.6f), new Vector3(-16.3f, 0.92f, -8.6f) }, GrindSurface.Ledge, false, _root);
            MeshObject("Vent_Bank", ProcMesh.Wedge(3f, 2.5f, 0.9f), new Vector3(-28.5f, 0f, -14f), 90f, Palette.Concrete);

            var a = new Vector3(-20f, 0.55f, 16f);
            var b = new Vector3(-4f, 0.55f, 16f);
            Rail("RoofA_FlatBar", new List<Vector3> { a, b }, GrindSurface.Rail, false, _root);
            Bar(a, b, 0.06f, Palette.Metal, true);

            // Skylight: a low glass box you ollie onto (manual pad).
            Box("Skylight", new Vector3(-8f, 0.2f, -3f - 3.5f), new Vector3(5f, 0.4f, 2.5f), new Color(0.55f, 0.75f, 0.85f));
        }

        private void BuildRoofB()
        {
            Box("RoofB", new Vector3(28f, RoofBY - 0.5f, 0f), new Vector3(24f, 1f, 36f), Palette.RoofTar);
            Parapet("RoofB_ParapetN", new Vector3(16f, RoofBY, 17.75f), new Vector3(40f, RoofBY, 17.75f));
            Parapet("RoofB_ParapetS", new Vector3(16f, RoofBY, -17.75f), new Vector3(40f, RoofBY, -17.75f));
            Box("RoofB_ParapetE", new Vector3(39.75f, RoofBY + 0.45f, 0f), new Vector3(0.5f, 0.9f, 36f), Palette.Parapet);

            // Water towers: grindable round bases, tanks on legs above.
            foreach (float z in new[] { 10f, -10f })
            {
                var basePos = new Vector3(22f, RoofBY, z);
                Cylinder("TowerBase", basePos, 1.8f, 1f, Palette.ConcreteDark);
                var ring = new List<Vector3>();
                const int segs = 16;
                for (int i = 0; i < segs; i++)
                {
                    float ang = Mathf.PI * 2f * i / segs;
                    ring.Add(basePos + new Vector3(Mathf.Cos(ang) * 1.72f, 1.02f, Mathf.Sin(ang) * 1.72f));
                }
                Rail("TowerBase_Rim", ring, GrindSurface.Ledge, true, _root);
                foreach (var leg in new[] { new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, -1f) })
                    Cylinder("TowerLeg", basePos + Vector3.up + leg, 0.08f, 2.5f, Palette.Ink, false);
                Cylinder("TowerTank", basePos + Vector3.up * 3.5f, 1.6f, 3f, Palette.Wood);
                Cylinder("TowerRoof", basePos + Vector3.up * 6.5f, 1.7f, 0.3f, Palette.Ink, false);
            }

            // Construction corner: plywood quarter pipe on the east edge, scaffold bar and a launch ramp.
            const float radius = 2.5f, deck = 1.2f, width = 14f;
            var toe = new Vector3(33.4f, RoofBY, 0f);
            var qp = MeshObject("Plywood_QP", ProcMesh.QuarterPipe(width, radius, deck), toe, 90f, Palette.Plywood);
            float lipAlong = radius * Mathf.Sin(88f * Mathf.Deg2Rad);
            float lipY = radius - radius * Mathf.Cos(88f * Mathf.Deg2Rad);
            var rot = Quaternion.Euler(0f, 90f, 0f);
            var ca = toe + rot * new Vector3(-width * 0.5f + 0.3f, lipY + 0.03f, lipAlong);
            var cb = toe + rot * new Vector3(width * 0.5f - 0.3f, lipY + 0.03f, lipAlong);
            Rail("Plywood_Coping", new List<Vector3> { ca, cb }, GrindSurface.Coping, false, qp.transform);
            Bar(ca, cb, 0.07f, Palette.Coping, false);

            var sa = new Vector3(27f, RoofBY + 0.55f, -14f);
            var sb = new Vector3(27f, RoofBY + 0.55f, 6f);
            Rail("Scaffold_Bar", new List<Vector3> { sa, sb }, GrindSurface.Rail, false, _root);
            Bar(sa, sb, 0.06f, Palette.TapeYellow, true, RoofBY);

            MeshObject("Construction_Ramp", ProcMesh.Wedge(3f, 3f, 1f), new Vector3(31f, RoofBY, 15f), 180f, Palette.Plywood);
        }

        /// <summary>An invented skyline: plain towers with a few lit window bands. Visual only.</summary>
        private void BuildSkyline()
        {
            RemoveCollider(Box("Street", new Vector3(5f, -60f, 0f), new Vector3(600f, 1f, 600f), Palette.Ink));
            var rng = new System.Random(4242); // fixed seed so the skyline never changes between runs
            for (int i = 0; i < 36; i++)
            {
                float ang = i / 36f * Mathf.PI * 2f;
                float dist = 85f + (float)rng.NextDouble() * 60f;
                float top = -12f + (float)rng.NextDouble() * 55f;
                float w = 10f + (float)rng.NextDouble() * 14f;
                var center = new Vector3(5f + Mathf.Cos(ang) * dist, (top - 60f) * 0.5f, Mathf.Sin(ang) * dist);
                var tower = Box("SkylineTower", center, new Vector3(w, top + 60f, w), Color.Lerp(Palette.Skyline, Palette.Ink, (float)rng.NextDouble() * 0.5f));
                RemoveCollider(tower);
                if (i % 3 == 0)
                {
                    var toward = new Vector3(5f, 0f, 0f) - new Vector3(center.x, 0f, center.z);
                    var face = new Vector3(center.x, 0f, center.z) + toward.normalized * (w * 0.5f + 0.05f);
                    for (int band = 0; band < 3; band++)
                        Neon("WindowBand", new Vector3(face.x, top - 4f - band * 5f, face.z), new Vector3(w * 0.7f, 0.5f, 0.1f), Palette.Cream,
                            Quaternion.LookRotation(toward).eulerAngles.y);
                }
            }
        }

        private void Parapet(string name, Vector3 a, Vector3 b)
        {
            const float h = 0.9f, t = 0.5f;
            Vector3 mid = (a + b) * 0.5f;
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            float len = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
            Box(name, mid + Vector3.up * (h * 0.5f), alongX ? new Vector3(len, h, t) : new Vector3(t, h, len), Palette.Parapet);
            Vector3 inset = alongX ? new Vector3(0.3f, 0f, 0f) : new Vector3(0f, 0f, 0.3f);
            Vector3 lo = alongX ? (a.x < b.x ? a : b) : (a.z < b.z ? a : b);
            Vector3 hi = alongX ? (a.x < b.x ? b : a) : (a.z < b.z ? b : a);
            Rail(name + "_Top", new List<Vector3> { lo + inset + Vector3.up * (h + 0.02f), hi - inset + Vector3.up * (h + 0.02f) }, GrindSurface.Ledge, false, _root);
        }
    }
}
