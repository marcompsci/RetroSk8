using System.Collections.Generic;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Builds a Create-a-Park layout (<see cref="CustomPark"/>): an 80 m walled lot, a start pad, and every placed
    /// obstacle from the shared spot kit. Each piece lives under its own object so the editor can rebuild just
    /// that piece when it is moved, turned or resized.
    /// </summary>
    public sealed class CustomParkBuilder : ParkBuilder
    {
        public const string GeneratedRootName = "CustomPark_Generated";
        protected override string RootName => GeneratedRootName;
        public override string LocationId => Park != null ? Park.id : CustomParkIds.ForSlot(1);

        /// <summary>The layout to build. Set before Build (the installer does this).</summary>
        public CustomPark Park;

        private Transform _parkRoot;
        private readonly List<Transform> _pieces = new List<Transform>();
        public IReadOnlyList<Transform> PieceRoots => _pieces;

        public static readonly float[] LedgeHeights = { 0.35f, 0.5f, 0.7f };
        public static readonly float[] RailHeights = { 0.3f, 0.45f, 0.65f };
        public static readonly float[] KickerHeights = { 0.5f, 0.8f, 1.1f };
        public static readonly float[] QuarterRadii = { 1.4f, 2f, 2.6f };
        public static readonly float[] BankHeights = { 0.8f, 1.2f, 1.6f };
        public static readonly float[] FunboxHeights = { 0.6f, 0.9f, 1.2f };
        public static readonly float[] MiniRadii = { 1.2f, 1.6f, 2f };
        public static readonly float[] BowlDepths = { 1.3f, 1.8f, 2.4f };
        public static readonly float[] BowlTransitions = { 1.8f, 2.4f, 3f };
        public static readonly int[] StairCounts = { 3, 5, 7 };
        public static readonly float[] WallHeights = { 2f, 3f, 4f };

        public static Vector3 SpawnPosition =>
            new Vector3((CustomPark.SpawnX + CustomPark.SpawnW * 0.5f) * CustomPark.CellSize - CustomPark.HalfSize, 0.05f,
                (CustomPark.SpawnZ + CustomPark.SpawnD * 0.5f) * CustomPark.CellSize - CustomPark.HalfSize);

        protected override void BuildPark(LevelInfo level)
        {
            if (Park == null) Park = CustomPark.Starter(CustomParkIds.ForSlot(1), "MY PARK");
            _parkRoot = _root;
            float half = CustomPark.HalfSize;
            var theme = Park.Theme;

            Color ground = theme == ParkTheme.NeonNight ? Palette.WarehouseFloor : theme == ParkTheme.Sunset ? new Color(0.7f, 0.6f, 0.52f) : Palette.Paving;
            Color wall = theme == ParkTheme.NeonNight ? Palette.WarehouseWall : theme == ParkTheme.Sunset ? Palette.Coral : Palette.Brick;
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(half * 2f + 4f, 1f, half * 2f + 4f), ground);
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, 1.5f, half + 0.5f), new Vector3(half * 2f + 2f, 3f, 1f)), (new Vector3(0f, 1.5f, -half - 0.5f), new Vector3(half * 2f + 2f, 3f, 1f)),
                (new Vector3(half + 0.5f, 1.5f, 0f), new Vector3(1f, 3f, half * 2f)), (new Vector3(-half - 0.5f, 1.5f, 0f), new Vector3(1f, 3f, half * 2f)),
            })
                Box("Wall", pos, size, wall);

            // Faint grid lines so the layout reads like the editor map (visual only).
            Color line = theme == ParkTheme.NeonNight ? new Color(0.35f, 0.3f, 0.45f) : new Color(0.55f, 0.52f, 0.5f);
            for (int i = 0; i <= CustomPark.GridCells; i += 5)
            {
                float p = i * CustomPark.CellSize - half;
                PrimitiveMeshes.CreateVisual("GridX", PrimitiveType.Cube, _root, new Vector3(0f, 0.005f, p), new Vector3(half * 2f, 0.01f, 0.06f), line);
                PrimitiveMeshes.CreateVisual("GridZ", PrimitiveType.Cube, _root, new Vector3(p, 0.005f, 0f), new Vector3(0.06f, 0.01f, half * 2f), line);
            }
            // Start pad.
            var pad = SpawnPosition;
            Neon("StartPad", new Vector3(pad.x, 0.01f, pad.z), new Vector3(CustomPark.SpawnW * CustomPark.CellSize - 0.4f, 0.02f, CustomPark.SpawnD * CustomPark.CellSize - 0.4f),
                theme == ParkTheme.NeonNight ? Palette.NeonCyan : Palette.TapeYellow);
            if (theme == ParkTheme.NeonNight)
            {
                Neon("Strip", new Vector3(0f, 2.6f, half - 0.05f), new Vector3(half * 1.6f, 0.15f, 0.1f), Palette.NeonPink);
                Neon("Strip", new Vector3(0f, 2.6f, -half + 0.05f), new Vector3(half * 1.6f, 0.15f, 0.1f), Palette.NeonCyan);
            }
            else if (theme == ParkTheme.Sunset)
            {
                Neon("SunsetStripe", new Vector3(0f, 2.6f, half - 0.05f), new Vector3(half * 1.6f, 0.15f, 0.1f), Palette.NeonPink);
                Neon("SunsetStripe", new Vector3(0f, 2.2f, half - 0.05f), new Vector3(half * 1.4f, 0.15f, 0.1f), Palette.TapeYellow);
            }

            RebuildAll();

            KillPlane(-6f);
            level.killHeight = -3.5f;
            level.playableBounds = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(half * 2f + 10f, 40f, half * 2f + 10f));
            Spawn(level, pad, Vector3.forward);
        }

        /// <summary>Rebuilds every piece (after loading or undoing).</summary>
        public void RebuildAll()
        {
            foreach (var t in _pieces) Discard(t);
            _pieces.Clear();
            for (int i = 0; i < Park.pieces.Count; i++) _pieces.Add(BuildPiece(Park.pieces[i], i));
        }

        /// <summary>Rebuilds one piece in place (after a move, turn or resize).</summary>
        public void RebuildPiece(int index)
        {
            if (index < 0 || index >= Park.pieces.Count) return;
            while (_pieces.Count < Park.pieces.Count) _pieces.Add(null);
            Discard(_pieces[index]);
            _pieces[index] = BuildPiece(Park.pieces[index], index);
        }

        /// <summary>A piece was added at the end of the list.</summary>
        public void PieceAdded() => _pieces.Add(BuildPiece(Park.pieces[Park.pieces.Count - 1], Park.pieces.Count - 1));

        public void PieceRemoved(int index)
        {
            if (index < 0 || index >= _pieces.Count) return;
            Discard(_pieces[index]);
            _pieces.RemoveAt(index);
        }

        private static void Discard(Transform t)
        {
            if (t == null) return;
            t.gameObject.SetActive(false); // gone from physics and rendering this frame
            Destroy(t.gameObject);
        }

        private Transform BuildPiece(ParkPiece p, int index)
        {
            var holder = new GameObject($"Piece_{index}_{p.Kind}").transform;
            holder.SetParent(_parkRoot, false);
            var saved = _root;
            _root = holder;
            try
            {
                var (cx, cz) = CustomPark.WorldCenter(p);
                BuildKind(p, new Vector3(cx, 0f, cz), p.rot * 90f);
            }
            finally
            {
                _root = saved;
            }
            return holder;
        }

        private void BuildKind(ParkPiece p, Vector3 c, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 dir = rot * Vector3.forward;
            Vector3 L(float x, float z, float y = 0f) => c + rot * new Vector3(x, y, z);
            int s = Mathf.Clamp(p.size, 0, 2);

            switch (p.Kind)
            {
                case PieceKind.Ledge:
                    Ledge("Ledge", L(0f, -2.7f), L(0f, 2.7f), LedgeHeights[s], 0.8f, Palette.Concrete);
                    break;

                case PieceKind.FlatRail:
                {
                    float h = RailHeights[s];
                    Vector3 a = L(0f, -2.7f, h), b = L(0f, 2.7f, h);
                    Rail("FlatRail", new List<Vector3> { a, b }, GrindSurface.Rail, false, _root);
                    Bar(a, b, 0.05f, Palette.Metal, true);
                    break;
                }

                case PieceKind.Kicker:
                    Kicker("Kicker", L(0f, -1.25f), yaw, 3f, 2.5f, KickerHeights[s]);
                    break;

                case PieceKind.QuarterPipe:
                {
                    float radius = QuarterRadii[s], deck = 3.9f - radius, width = 7.6f;
                    Vector3 toe = L(0f, -2f);
                    var qp = MeshObject("QuarterPipe", ProcMesh.QuarterPipe(width, radius, deck), toe, yaw, Palette.Plywood);
                    float lipAlong = radius * Mathf.Sin(88f * Mathf.Deg2Rad), lipY = radius - radius * Mathf.Cos(88f * Mathf.Deg2Rad);
                    Vector3 a = toe + rot * new Vector3(-width * 0.5f + 0.3f, lipY + 0.03f, lipAlong);
                    Vector3 b = toe + rot * new Vector3(width * 0.5f - 0.3f, lipY + 0.03f, lipAlong);
                    Rail("QP_Coping", new List<Vector3> { a, b }, GrindSurface.Coping, false, qp.transform);
                    Bar(a, b, 0.07f, Palette.Coping, false);
                    break;
                }

                case PieceKind.Bank:
                    MeshObject("Bank", ProcMesh.Wedge(7.6f, 3.6f, BankHeights[s]), L(0f, -1.9f), yaw, Palette.Concrete);
                    break;

                case PieceKind.Funbox:
                    Funbox("Funbox", c, 2f, 1.9f, FunboxHeights[s], Palette.Concrete, true);
                    break;

                case PieceKind.MiniRamp:
                {
                    float r = MiniRadii[s];
                    MiniRamp("MiniRamp", c, yaw, 7.6f, r, 4f, Mathf.Min(1.4f, 5.9f - 2f - r));
                    break;
                }

                case PieceKind.Bowl:
                {
                    float depth = BowlDepths[s];
                    BowlWithDeck("Bowl", c, 1f, BowlTransitions[s], depth, 5f, new Color(0.45f, 0.7f, 0.82f));
                    // Banks up to the deck on two sides.
                    MeshObject("Bowl_BankS", ProcMesh.Wedge(6f, 2.95f, depth), L(0f, -7.95f), yaw, Palette.Concrete);
                    MeshObject("Bowl_BankN", ProcMesh.Wedge(6f, 2.95f, depth), L(0f, 7.95f), yaw + 180f, Palette.Concrete);
                    break;
                }

                case PieceKind.Stairs:
                {
                    int steps = StairCounts[s];
                    float h = steps * StepRise, run = steps * StepRun;
                    Vector3 down = -dir;
                    Vector3 top = Stairs("Stairs", L(0f, -4.9f), down, steps, 3.4f, Palette.Concrete);
                    float platStart = -4.9f + run, platEnd = 2f;
                    var plat = Box("Platform", L(0f, (platStart + platEnd) * 0.5f, h * 0.5f), new Vector3(5.8f, h, platEnd - platStart), Palette.ConcreteDark);
                    plat.transform.rotation = rot;
                    // The platform is beside and behind the steps; the steps' own ramp collider handles the stairs.
                    Handrail("Stairs_Rail", top, down, steps, 2.3f);
                    Hubba("Stairs_Hubba", top, down, steps, -2.3f, Palette.Concrete);
                    MeshObject("Stairs_BackBank", ProcMesh.Wedge(5.8f, 2.95f, h), L(0f, 4.95f), yaw + 180f, Palette.Concrete);
                    break;
                }

                case PieceKind.ManualPad:
                    ManualPad("ManualPad", c, new Vector2(3f, 5f), yaw, Palette.ConcreteDark);
                    break;

                case PieceKind.Bench:
                    Bench("Bench", c, yaw, 3.2f);
                    break;

                case PieceKind.Wall:
                {
                    float h = WallHeights[s];
                    var w = Box("Wall", c + Vector3.up * (h * 0.5f), new Vector3(0.6f, h, 7.6f), Palette.Brick);
                    w.transform.rotation = rot;
                    break;
                }
            }
        }
    }
}
