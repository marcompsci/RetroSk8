using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>Obstacles you can place in Create-a-Park. Values are saved, so never renumber them.</summary>
    public enum PieceKind
    {
        Ledge = 0,
        FlatRail = 1,
        Kicker = 2,
        QuarterPipe = 3,
        Bank = 4,
        Funbox = 5,
        MiniRamp = 6,
        Bowl = 7,
        Stairs = 8,
        ManualPad = 9,
        Bench = 10,
        Wall = 11,
    }

    /// <summary>One placed obstacle. Position is the min corner of its (rotated) footprint in grid cells.</summary>
    [Serializable]
    public sealed class ParkPiece
    {
        public int kind;
        public int x, z;
        /// <summary>Quarter turns clockwise (0-3). 0 faces +z (north on the editor map).</summary>
        public int rot;
        /// <summary>Size variant 0-2 (heights and depths; the footprint stays the same).</summary>
        public int size = 1;
        /// <summary>Phase 16: extra length in 2-cell steps (0-3) for ledges, rails, quarters, banks, walls and pads.</summary>
        public int length;
        /// <summary>Phase 16: rail curve, -2..2 (0 = straight). A bent rail takes three columns.</summary>
        public int bend;

        public PieceKind Kind => (PieceKind)kind;
        public ParkPiece Clone() => (ParkPiece)MemberwiseClone();
    }

    /// <summary>Visual theme for a custom park (sky, light, ground colour).</summary>
    public enum ParkTheme
    {
        Daylight = 0,
        NeonNight = 1,
        Sunset = 2,
    }

    /// <summary>
    /// A player-made park: obstacles on an 80 x 80 m grid of 2 m cells. Pure rules (placement, moving with the
    /// arrow buttons, rotating, overlap checks) so the editor, the builder and the tests agree. JsonUtility-friendly.
    /// </summary>
    [Serializable]
    public sealed class CustomPark
    {
        public const float CellSize = 2f;
        public const int GridCells = 40;
        public const int MaxPieces = 60;
        public const int MaxNameLength = 18;
        public const int MaxLength = 3;
        public const int MaxBend = 2;
        /// <summary>The start pad at the south edge is kept clear so you always spawn safely.</summary>
        public const int SpawnX = 18, SpawnZ = 0, SpawnW = 4, SpawnD = 3;

        public string id;
        public string name;
        public int theme;
        public List<ParkPiece> pieces = new List<ParkPiece>();

        public ParkTheme Theme => (ParkTheme)theme;
        public static float HalfSize => GridCells * CellSize * 0.5f;

        public static CustomPark Create(string id, string name) => new CustomPark { id = id, name = name };

        // ---------------------------------------------------------------- footprints

        /// <summary>Unrotated footprint in cells (width along x, depth along z).</summary>
        public static (int w, int d) BaseFootprint(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Ledge: return (1, 3);
                case PieceKind.FlatRail: return (1, 3);
                case PieceKind.Kicker: return (2, 2);
                case PieceKind.QuarterPipe: return (4, 2);
                case PieceKind.Bank: return (4, 2);
                case PieceKind.Funbox: return (3, 3);
                case PieceKind.MiniRamp: return (4, 6);
                case PieceKind.Bowl: return (8, 8);
                case PieceKind.Stairs: return (3, 5);
                case PieceKind.ManualPad: return (2, 3);
                case PieceKind.Bench: return (1, 2);
                case PieceKind.Wall: return (1, 4);
                default: return (1, 1);
            }
        }

        public static (int w, int d) Footprint(PieceKind kind, int rot) => Footprint(kind, rot, 0, 0);

        /// <summary>Footprint with Phase 16 stretch and bend: length adds 2 cells along the piece's long side.</summary>
        public static (int w, int d) Footprint(PieceKind kind, int rot, int length, int bend)
        {
            var (w, d) = BaseFootprint(kind);
            int extra = 2 * Clamp(length, 0, MaxLength) * (Stretches(kind) ? 1 : 0);
            if (StretchesAcross(kind)) w += extra; else d += extra;
            if (kind == PieceKind.FlatRail && bend != 0) w = 3;
            return (rot & 1) == 0 ? (w, d) : (d, w);
        }

        public static (int w, int d) Footprint(ParkPiece p) => Footprint(p.Kind, p.rot, p.length, p.bend);

        /// <summary>Pieces that can be made longer (Phase 16).</summary>
        public static bool Stretches(PieceKind kind) =>
            kind == PieceKind.Ledge || kind == PieceKind.FlatRail || kind == PieceKind.QuarterPipe || kind == PieceKind.Bank
            || kind == PieceKind.Wall || kind == PieceKind.ManualPad;

        /// <summary>Quarters and banks get wider (along x before rotating); the rest get longer (along z).</summary>
        public static bool StretchesAcross(PieceKind kind) => kind == PieceKind.QuarterPipe || kind == PieceKind.Bank;

        public static bool Bends(PieceKind kind) => kind == PieceKind.FlatRail;

        public static string DisplayName(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.FlatRail: return "Flat Rail";
                case PieceKind.QuarterPipe: return "Quarter Pipe";
                case PieceKind.MiniRamp: return "Mini Ramp";
                case PieceKind.ManualPad: return "Manual Pad";
                default: return kind.ToString();
            }
        }

        /// <summary>World-space centre (x, z) of a piece's footprint, in metres (grid centred on the origin).</summary>
        public static (float x, float z) WorldCenter(ParkPiece p)
        {
            var (w, d) = Footprint(p);
            return ((p.x + w * 0.5f) * CellSize - HalfSize, (p.z + d * 0.5f) * CellSize - HalfSize);
        }

        /// <summary>Grid cell containing a world point, or (-1, -1) outside the grid.</summary>
        public static (int x, int z) CellAt(float worldX, float worldZ)
        {
            int x = (int)Math.Floor((worldX + HalfSize) / CellSize);
            int z = (int)Math.Floor((worldZ + HalfSize) / CellSize);
            return x < 0 || z < 0 || x >= GridCells || z >= GridCells ? (-1, -1) : (x, z);
        }

        // ---------------------------------------------------------------- rules

        public static bool InsideGrid(int x, int z, int w, int d) => x >= 0 && z >= 0 && x + w <= GridCells && z + d <= GridCells;

        private static bool Overlap(int ax, int az, int aw, int ad, int bx, int bz, int bw, int bd) =>
            ax < bx + bw && bx < ax + aw && az < bz + bd && bz < az + ad;

        /// <summary>True when a footprint is on the grid, off the start pad and clear of every other piece.</summary>
        public bool IsFree(int x, int z, int w, int d, int ignoreIndex = -1)
        {
            if (!InsideGrid(x, z, w, d)) return false;
            if (Overlap(x, z, w, d, SpawnX, SpawnZ, SpawnW, SpawnD)) return false;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (i == ignoreIndex) continue;
                var p = pieces[i];
                var (pw, pd) = Footprint(p);
                if (Overlap(x, z, w, d, p.x, p.z, pw, pd)) return false;
            }
            return true;
        }

        /// <summary>Index of the piece covering a cell, or -1.</summary>
        public int PieceAt(int cellX, int cellZ)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var p = pieces[i];
                var (w, d) = Footprint(p);
                if (cellX >= p.x && cellX < p.x + w && cellZ >= p.z && cellZ < p.z + d) return i;
            }
            return -1;
        }

        /// <summary>
        /// Adds a piece at the nearest free spot to (nearX, nearZ). Returns its index, or -1 when the park is full
        /// or nothing fits.
        /// </summary>
        public int Add(PieceKind kind, int nearX, int nearZ, int rot = 0, int size = 1, int length = 0, int bend = 0)
        {
            if (pieces.Count >= MaxPieces) return -1;
            rot = ((rot % 4) + 4) % 4;
            length = Stretches(kind) ? Clamp(length, 0, MaxLength) : 0;
            bend = Bends(kind) ? Clamp(bend, -MaxBend, MaxBend) : 0;
            var (w, d) = Footprint(kind, rot, length, bend);
            // Spiral outwards (ring by ring) from the requested cell.
            for (int r = 0; r < GridCells * 2; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                    int x = nearX - w / 2 + dx, z = nearZ - d / 2 + dz;
                    if (!IsFree(x, z, w, d)) continue;
                    pieces.Add(new ParkPiece { kind = (int)kind, x = x, z = z, rot = rot, size = Clamp(size, 0, 2), length = length, bend = bend });
                    return pieces.Count - 1;
                }
            }
            return -1;
        }

        /// <summary>Arrow-button move: one cell in a direction. Blocked by the grid edge, the start pad or another piece.</summary>
        public bool TryMove(int index, int dx, int dz)
        {
            if (!Valid(index)) return false;
            var p = pieces[index];
            var (w, d) = Footprint(p);
            if (!IsFree(p.x + dx, p.z + dz, w, d, index)) return false;
            p.x += dx;
            p.z += dz;
            return true;
        }

        /// <summary>Rotates a quarter turn clockwise about the footprint centre (nudged to fit when it can).</summary>
        public bool TryRotate(int index)
        {
            if (!Valid(index)) return false;
            var p = pieces[index];
            int rot = (p.rot + 1) % 4;
            var (ow, od) = Footprint(p);
            var (w, d) = Footprint(p.Kind, rot, p.length, p.bend);
            // Keep the centre where it was.
            int cx2 = p.x * 2 + ow, cz2 = p.z * 2 + od;
            int bx = (cx2 - w) / 2, bz = (cz2 - d) / 2;
            foreach (var (ox, oz) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1) })
            {
                if (!IsFree(bx + ox, bz + oz, w, d, index)) continue;
                p.rot = rot;
                p.x = bx + ox;
                p.z = bz + oz;
                return true;
            }
            return false;
        }

        /// <summary>Makes a piece longer (+1) or shorter (-1) in 2-cell steps. Grows away from its min corner, then nudges to fit.</summary>
        public bool TryStretch(int index, int delta)
        {
            if (!Valid(index)) return false;
            var p = pieces[index];
            if (!Stretches(p.Kind)) return false;
            int length = p.length + Math.Sign(delta);
            if (length < 0 || length > MaxLength) return false;
            return TryReshape(index, length, p.bend);
        }

        /// <summary>Cycles a rail's curve: straight → right → more right → more left → left → straight.</summary>
        public bool TryBend(int index)
        {
            if (!Valid(index)) return false;
            var p = pieces[index];
            if (!Bends(p.Kind)) return false;
            int next = p.bend == 0 ? 1 : p.bend == 1 ? 2 : p.bend == 2 ? -2 : p.bend == -2 ? -1 : 0;
            return TryReshape(index, p.length, next);
        }

        /// <summary>Applies a new length/bend keeping the piece centred where it can, or fails with nothing changed.</summary>
        private bool TryReshape(int index, int length, int bend)
        {
            var p = pieces[index];
            var (ow, od) = Footprint(p);
            var (w, d) = Footprint(p.Kind, p.rot, length, bend);
            int cx2 = p.x * 2 + ow, cz2 = p.z * 2 + od;
            int bx = (cx2 - w) / 2, bz = (cz2 - d) / 2;
            foreach (var (ox, oz) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1) })
            {
                if (!IsFree(bx + ox, bz + oz, w, d, index)) continue;
                p.length = length;
                p.bend = bend;
                p.x = bx + ox;
                p.z = bz + oz;
                return true;
            }
            return false;
        }

        public bool CycleSize(int index)
        {
            if (!Valid(index)) return false;
            pieces[index].size = (pieces[index].size + 1) % 3;
            return true;
        }

        public bool Remove(int index)
        {
            if (!Valid(index)) return false;
            pieces.RemoveAt(index);
            return true;
        }

        /// <summary>Copy of a piece placed next to it (for quickly building lines of ledges or rails).</summary>
        public int Duplicate(int index)
        {
            if (!Valid(index)) return -1;
            var p = pieces[index];
            var (w, d) = Footprint(p);
            int i = Add(p.Kind, p.x + w / 2 + w + 1, p.z + d / 2, p.rot, p.size, p.length, p.bend);
            return i;
        }

        public bool Valid(int index) => index >= 0 && index < pieces.Count;

        /// <summary>Repairs a loaded park: drops unknown or overlapping pieces, clamps fields, trims the name.</summary>
        public void Sanitize()
        {
            if (pieces == null) pieces = new List<ParkPiece>();
            if (string.IsNullOrWhiteSpace(name)) name = "MY PARK";
            name = name.Trim();
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
            theme = Clamp(theme, 0, 2);
            var kept = new List<ParkPiece>();
            var original = pieces;
            pieces = kept;
            foreach (var p in original)
            {
                if (p == null || !Enum.IsDefined(typeof(PieceKind), p.kind) || kept.Count >= MaxPieces) continue;
                p.rot = ((p.rot % 4) + 4) % 4;
                p.size = Clamp(p.size, 0, 2);
                p.length = Stretches(p.Kind) ? Clamp(p.length, 0, MaxLength) : 0;
                p.bend = Bends(p.Kind) ? Clamp(p.bend, -MaxBend, MaxBend) : 0;
                var (w, d) = Footprint(p);
                if (IsFree(p.x, p.z, w, d)) kept.Add(p);
            }
        }

        public CustomPark Clone()
        {
            var c = (CustomPark)MemberwiseClone();
            c.pieces = new List<ParkPiece>();
            foreach (var p in pieces) c.pieces.Add(p.Clone());
            return c;
        }

        /// <summary>A playable starter layout so a brand-new park is never empty.</summary>
        public static CustomPark Starter(string id, string name)
        {
            var park = Create(id, name);
            park.Add(PieceKind.Ledge, 14, 10);
            park.Add(PieceKind.FlatRail, 26, 10);
            park.Add(PieceKind.Funbox, 20, 16);
            park.Add(PieceKind.QuarterPipe, 20, 36, 2);
            park.Add(PieceKind.Kicker, 20, 8);
            return park;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }

    /// <summary>
    /// Undo/redo for Create-a-Park (Phase 16): snapshots of the whole layout, newest last. Every edit pushes the
    /// state from before it; undo swaps back and keeps the undone state for redo.
    /// </summary>
    public sealed class ParkHistory
    {
        public const int Limit = 40;
        private readonly List<CustomPark> _undo = new List<CustomPark>();
        private readonly List<CustomPark> _redo = new List<CustomPark>();

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        /// <summary>Call before an edit with the park as it is now.</summary>
        public void Record(CustomPark before)
        {
            if (before == null) return;
            _undo.Add(before.Clone());
            if (_undo.Count > Limit) _undo.RemoveAt(0);
            _redo.Clear();
        }

        /// <summary>Returns the layout to go back to (null when there's nothing to undo).</summary>
        public CustomPark Undo(CustomPark current)
        {
            if (_undo.Count == 0) return null;
            var prev = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            if (current != null) _redo.Add(current.Clone());
            return prev;
        }

        public CustomPark Redo(CustomPark current)
        {
            if (_redo.Count == 0) return null;
            var next = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            if (current != null) _undo.Add(current.Clone());
            return next;
        }

        public void Clear() { _undo.Clear(); _redo.Clear(); }
    }

    /// <summary>Ids for saved custom parks ("custom_1" ... ).</summary>
    public static class CustomParkIds
    {
        public const string Prefix = "custom_";
        public const int MaxSlots = 6;
        public static bool IsCustom(string locationId) => locationId != null && locationId.StartsWith(Prefix, StringComparison.Ordinal);
        public static string ForSlot(int slot) => Prefix + slot;
    }
}
