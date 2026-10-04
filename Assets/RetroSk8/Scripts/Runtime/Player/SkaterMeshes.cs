using System.Collections.Generic;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>Builds (once) the smooth lathe meshes for the Phase 17 skater from <see cref="SkaterShapes"/>.</summary>
    public static class SkaterMeshes
    {
        public const int Segments = 14;
        private static readonly Dictionary<string, Mesh> s_cache = new Dictionary<string, Mesh>();

        public static Mesh Get(SkaterShapes.Profile p)
        {
            if (s_cache.TryGetValue(p.Name, out var m) && m != null) return m;
            m = Lathe(p, Segments);
            s_cache[p.Name] = m;
            return m;
        }

        private static Mesh Lathe(SkaterShapes.Profile p, int segments)
        {
            var verts = new List<Vector3>(SkaterShapes.VertexCount(p, segments));
            var tris = new List<int>();
            var ringStart = new int[p.Y.Length];
            for (int i = 0; i < p.Y.Length; i++)
            {
                ringStart[i] = verts.Count;
                if (p.R[i] <= 0f) { verts.Add(new Vector3(0f, p.Y[i], 0f)); continue; }
                for (int s = 0; s < segments; s++)
                {
                    float a = s * Mathf.PI * 2f / segments;
                    verts.Add(new Vector3(Mathf.Cos(a) * p.R[i], p.Y[i], Mathf.Sin(a) * p.R[i]));
                }
            }
            for (int i = 0; i + 1 < p.Y.Length; i++)
            {
                bool topPole = p.R[i] <= 0f, bottomPole = p.R[i + 1] <= 0f;
                int a0 = ringStart[i], b0 = ringStart[i + 1];
                for (int s = 0; s < segments; s++)
                {
                    int s1 = (s + 1) % segments;
                    if (topPole && bottomPole) continue;
                    if (topPole) { tris.Add(a0); tris.Add(b0 + s1); tris.Add(b0 + s); continue; }
                    if (bottomPole) { tris.Add(a0 + s); tris.Add(a0 + s1); tris.Add(b0); continue; }
                    tris.Add(a0 + s); tris.Add(a0 + s1); tris.Add(b0 + s);
                    tris.Add(a0 + s1); tris.Add(b0 + s1); tris.Add(b0 + s);
                }
            }
            var mesh = new Mesh { name = "Skater_" + p.Name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
