using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Neon Warehouse: an original indoor park. Quarter pipes on the east and west walls, a low spine in the middle,
    /// a raised conveyor line with a gap (the belts carry you toward it), a broken loading dock along the south wall,
    /// a maintenance walkway with a down-rail in the north-west corner, and abstract neon strips on every wall.
    /// Floor is y = 0; interior spans x -35..35, z -25..25.
    /// </summary>
    public sealed class NeonWarehouseBuilder : ParkBuilder
    {
        public const string GeneratedRootName = "NeonWarehouse_Generated";
        protected override string RootName => GeneratedRootName;
        public override string LocationId => ParkCatalog.NeonWarehouse;

        private const float WallHeight = 12f;

        protected override void BuildPark(LevelInfo level)
        {
            BuildShell(level);
            BuildWallQuarterPipes();
            BuildSpine(level);
            BuildConveyorLine(level);
            BuildLoadingDock(level);
            BuildMaintenanceWalkway();
            BuildFloorFeatures();
            BuildNeon();
            Spawn(level, new Vector3(0f, 0.05f, -18f), Vector3.forward);
        }

        private void BuildShell(LevelInfo level)
        {
            Box("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(74f, 1f, 54f), Palette.WarehouseFloor);
            // Painted floor lanes (visual) to read speed indoors.
            for (int i = -3; i <= 3; i++)
                RemoveCollider(Box("FloorLane", new Vector3(i * 9f, 0.005f, 0f), new Vector3(0.2f, 0.01f, 50f), Palette.ConcreteDark));

            Box("Wall_North", new Vector3(0f, WallHeight * 0.5f, 25.5f), new Vector3(72f, WallHeight, 1f), Palette.WarehouseWall);
            Box("Wall_South", new Vector3(0f, WallHeight * 0.5f, -25.5f), new Vector3(72f, WallHeight, 1f), Palette.WarehouseWall);
            Box("Wall_West", new Vector3(-35.5f, WallHeight * 0.5f, 0f), new Vector3(1f, WallHeight, 52f), Palette.WarehouseWall);
            Box("Wall_East", new Vector3(35.5f, WallHeight * 0.5f, 0f), new Vector3(1f, WallHeight, 52f), Palette.WarehouseWall);

            // Roof trusses: visual only, open between them so the chase camera never clips a ceiling.
            for (float x = -32f; x <= 32f; x += 8f)
                RemoveCollider(Box("Truss", new Vector3(x, WallHeight + 0.4f, 0f), new Vector3(0.4f, 0.8f, 52f), Palette.Ink));

            KillPlane(-6f);
            level.killHeight = -3.5f;
            level.playableBounds = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(80f, 40f, 60f));
        }

        private void BuildWallQuarterPipes()
        {
            const float radius = 3f, deck = 1.6f, width = 20f;
            // West wall: rises toward -x. East wall: rises toward +x.
            QuarterPipeWithCoping("QP_West", new Vector3(-30.4f, 0f, 0f), -90f, width, radius, deck, 88f);
            QuarterPipeWithCoping("QP_East", new Vector3(30.4f, 0f, 0f), 90f, width, radius, deck, 88f);
        }

        /// <summary>
        /// A low spine: two 60° transitions back to back. 60° keeps the lip below the vert-assist angle,
        /// so riders carry forward speed over the top (the spine transfer).
        /// </summary>
        private void BuildSpine(LevelInfo level)
        {
            const float radius = 3f, deck = 0.3f, width = 16f, angle = 60f;
            float lipAlong = radius * Mathf.Sin(angle * Mathf.Deg2Rad);   // 2.6
            float lipY = radius - radius * Mathf.Cos(angle * Mathf.Deg2Rad); // 1.5
            float toeA = 5f;
            float back = toeA + lipAlong + deck;                          // shared back edge
            float toeB = back + deck + lipAlong;

            QuarterPipeWithCoping("Spine_South", new Vector3(0f, 0f, toeA), 0f, width, radius, deck, angle);
            QuarterPipeWithCoping("Spine_North", new Vector3(0f, 0f, toeB), 180f, width, radius, deck, angle);

            level.gaps.Add(Gap(ParkCatalog.SpineTransfer, "Spine Transfer", new Vector3(0f, lipY + 1.7f, back), new Vector3(width, 2.4f, 0.9f), 600));
        }

        private void BuildConveyorLine(LevelInfo level)
        {
            const float h = 1.2f, w = 3f, x = 18f;
            var beltTexture = StripeTexture(Palette.Rubber, Palette.TapeYellow);
            foreach (var (z0, z1) in new[] { (-20f, -6f), (0f, 14f) })
            {
                float len = z1 - z0;
                var body = Box("Conveyor", new Vector3(x, h * 0.5f, (z0 + z1) * 0.5f), new Vector3(w, h, len), Palette.Metal);
                var belt = PrimitiveMeshes.CreateVisual("Belt", PrimitiveType.Cube, _root, new Vector3(x, h + 0.005f, (z0 + z1) * 0.5f), new Vector3(w - 0.3f, 0.01f, len), Color.white);
                var beltRenderer = belt.GetComponent<MeshRenderer>();
                beltRenderer.sharedMaterial = PlaceholderMaterials.GetTextured(beltTexture);
                var conveyor = body.AddComponent<ConveyorSurface>();
                conveyor.direction = Vector3.forward;
                conveyor.speed = 3f;
                conveyor.beltRenderer = beltRenderer;

                Rail("Conveyor_EdgeWest", new List<Vector3> { new Vector3(x - w * 0.5f, h + 0.02f, z0 + 0.3f), new Vector3(x - w * 0.5f, h + 0.02f, z1 - 0.3f) }, GrindSurface.Ledge, false, _root);
                Rail("Conveyor_EdgeEast", new List<Vector3> { new Vector3(x + w * 0.5f, h + 0.02f, z0 + 0.3f), new Vector3(x + w * 0.5f, h + 0.02f, z1 - 0.3f) }, GrindSurface.Ledge, false, _root);
            }
            MeshObject("Conveyor_AccessBank", ProcMesh.Wedge(w, 4f, h), new Vector3(x, 0f, -24f), 0f, Palette.Concrete);
            MeshObject("Conveyor_RunoutBank", ProcMesh.Wedge(w, 4f, h), new Vector3(x, 0f, 18f), 180f, Palette.Concrete);

            level.gaps.Add(Gap(ParkCatalog.ConveyorGap, "Conveyor Gap", new Vector3(x, h + 1.8f, -3f), new Vector3(w, 3.6f, 5.2f), 900));
        }

        private void BuildLoadingDock(LevelInfo level)
        {
            const float h = 1.2f, zFront = -21f, zBack = -25f;
            // Two dock sections with the collapsed middle between x -12 and -6.
            foreach (var (x0, x1) in new[] { (-30f, -12f), (-6f, 10f) })
            {
                Box("LoadingDock", new Vector3((x0 + x1) * 0.5f, h * 0.5f, (zFront + zBack) * 0.5f), new Vector3(x1 - x0, h, zFront - zBack), Palette.Concrete);
                Rail("Dock_Edge", new List<Vector3> { new Vector3(x0 + 0.3f, h + 0.02f, zFront), new Vector3(x1 - 0.3f, h + 0.02f, zFront) }, GrindSurface.Ledge, false, _root);
                Box("Dock_Bumper", new Vector3((x0 + x1) * 0.5f, h * 0.5f, zFront + 0.12f), new Vector3(x1 - x0 - 1f, 0.3f, 0.25f), Palette.Rubber);
            }
            MeshObject("Dock_BankA", ProcMesh.Wedge(6f, 4f, h), new Vector3(-20f, 0f, -17f), 180f, Palette.Concrete);
            MeshObject("Dock_BankB", ProcMesh.Wedge(6f, 4f, h), new Vector3(4f, 0f, -17f), 180f, Palette.Concrete);

            // Rubble in the broken section: visual only, so it can't trip a clean landing.
            for (int i = 0; i < 4; i++)
            {
                var chunk = Box("DockRubble", new Vector3(-11f + i * 1.4f, 0.2f, -23f + (i % 2) * 1.1f), new Vector3(1f, 0.4f, 0.8f), Palette.ConcreteDark);
                chunk.transform.rotation = Quaternion.Euler(8f * i, 23f * i, -6f * i);
                RemoveCollider(chunk);
            }

            level.gaps.Add(Gap(ParkCatalog.DockGap, "Dock Gap", new Vector3(-9f, h + 1.6f, -23f), new Vector3(6f, 3.2f, 4f), 800));
        }

        private void BuildMaintenanceWalkway()
        {
            const float h = 2f;
            Box("Walkway", new Vector3(-22f, h * 0.5f, 21.5f), new Vector3(16f, h, 7f), Palette.Metal);
            MeshObject("Walkway_Bank", ProcMesh.Wedge(8f, 5f, h), new Vector3(-22f, 0f, 13f), 0f, Palette.Concrete);
            Rail("Walkway_Ledge", new List<Vector3> { new Vector3(-29.7f, h + 0.02f, 18f), new Vector3(-26.3f, h + 0.02f, 18f) }, GrindSurface.Ledge, false, _root);
            Rail("Walkway_LedgeEast", new List<Vector3> { new Vector3(-17.7f, h + 0.02f, 18f), new Vector3(-14.3f, h + 0.02f, 18f) }, GrindSurface.Ledge, false, _root);

            // Down-rail beside the bank: from the walkway edge down to the floor.
            var top = new Vector3(-27f, h + 0.55f, 17.8f);
            var bottom = new Vector3(-27f, 0.6f, 12.5f);
            Rail("Maintenance_DownRail", new List<Vector3> { top, bottom }, GrindSurface.Rail, false, _root);
            Bar(top, bottom, 0.06f, Palette.TapeYellow, true);
        }

        private void BuildFloorFeatures()
        {
            var a = new Vector3(-24f, 0.55f, -8f);
            var b = new Vector3(-8f, 0.55f, -8f);
            Rail("Catwalk_FlatBar", new List<Vector3> { a, b }, GrindSurface.Rail, false, _root);
            Bar(a, b, 0.06f, Palette.Metal, true);

            Box("ManualPad", new Vector3(-18f, 0.18f, 2f), new Vector3(6f, 0.36f, 3f), Palette.Concrete);
            MeshObject("Kicker", ProcMesh.Wedge(3f, 3f, 0.9f), new Vector3(8f, 0f, -14f), 0f, Palette.TapeYellow);

            // Pallet stack: a waist-high box with grindable edges.
            Box("PalletStack", new Vector3(-4f, 0.45f, 17f), new Vector3(5f, 0.9f, 1.4f), Palette.Wood);
            Rail("Pallet_Edge", new List<Vector3> { new Vector3(-6.3f, 0.92f, 16.3f), new Vector3(-1.7f, 0.92f, 16.3f) }, GrindSurface.Ledge, false, _root);
        }

        /// <summary>Abstract neon: stripes, chevrons and light bars. Deliberately no text, logos or recognizable marks.</summary>
        private void BuildNeon()
        {
            Color[] colors = { Palette.NeonPink, Palette.NeonCyan, Palette.NeonViolet, Palette.NeonLime };
            // Long horizontal strips on north and south walls.
            for (int i = 0; i < 3; i++)
            {
                Neon("NeonStripe", new Vector3(0f, 4f + i * 2.2f, 24.9f), new Vector3(60f - i * 12f, 0.12f, 0.1f), colors[i]);
                Neon("NeonStripe", new Vector3(0f, 5f + i * 2.2f, -24.9f), new Vector3(50f - i * 10f, 0.12f, 0.1f), colors[(i + 1) % colors.Length]);
            }
            // Chevrons on the side walls, pointing toward the quarter pipes.
            for (int i = 0; i < 4; i++)
            {
                float z = -15f + i * 10f;
                var c = colors[i % colors.Length];
                Neon("NeonChevronA", new Vector3(-34.9f, 7f, z - 0.6f), new Vector3(0.1f, 0.14f, 2.4f), c, 0f).transform.rotation = Quaternion.Euler(35f, 0f, 0f);
                Neon("NeonChevronB", new Vector3(-34.9f, 7f, z + 0.6f), new Vector3(0.1f, 0.14f, 2.4f), c, 0f).transform.rotation = Quaternion.Euler(-35f, 0f, 0f);
                Neon("NeonChevronA", new Vector3(34.9f, 7f, z - 0.6f), new Vector3(0.1f, 0.14f, 2.4f), c, 0f).transform.rotation = Quaternion.Euler(35f, 0f, 0f);
                Neon("NeonChevronB", new Vector3(34.9f, 7f, z + 0.6f), new Vector3(0.1f, 0.14f, 2.4f), c, 0f).transform.rotation = Quaternion.Euler(-35f, 0f, 0f);
            }
            // Light bars hanging from the trusses.
            for (float x = -24f; x <= 24f; x += 16f)
                Neon("LightBar", new Vector3(x, 9.5f, 0f), new Vector3(0.25f, 0.1f, 30f), Palette.Cream);
            // Underglow along the conveyor and spine copings.
            Neon("ConveyorGlow", new Vector3(18f, 0.05f, -3f), new Vector3(3.4f, 0.05f, 40f), Palette.NeonCyan);
            Neon("SpineGlow", new Vector3(0f, 0.03f, 7.9f), new Vector3(16.5f, 0.05f, 0.3f), Palette.NeonPink);
        }

        // ---------------------------------------------------------------- helpers

        private void QuarterPipeWithCoping(string name, Vector3 toe, float yaw, float width, float radius, float deck, float angle)
        {
            var qp = MeshObject(name, ProcMesh.QuarterPipe(width, radius, deck, 10, angle), toe, yaw, Palette.Concrete);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            float lipAlong = radius * Mathf.Sin(angle * Mathf.Deg2Rad);
            float lipY = radius - radius * Mathf.Cos(angle * Mathf.Deg2Rad);
            Vector3 a = toe + rot * new Vector3(-width * 0.5f + 0.3f, lipY + 0.03f, lipAlong);
            Vector3 b = toe + rot * new Vector3(width * 0.5f - 0.3f, lipY + 0.03f, lipAlong);
            Rail(name + "_Coping", new List<Vector3> { a, b }, GrindSurface.Coping, false, qp.transform);
            Bar(a, b, 0.07f, Palette.Coping, false);
        }

        private static Texture2D StripeTexture(Color a, Color b)
        {
            var tex = new Texture2D(4, 16, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                name = "ConveyorStripes",
            };
            var px = new Color[4 * 16];
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 4; x++)
                px[y * 4 + x] = (y / 2) % 4 == 0 ? b : a;
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
