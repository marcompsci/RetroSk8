using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>Flat-shaded procedural meshes for placeholder skate geometry (banks, quarter pipes, ring banks).</summary>
    public static class ProcMesh
    {
        private sealed class Builder
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<int> _t = new List<int>();

            // Unity treats a triangle as front-facing when Cross(b - a, c - a) points at the viewer.
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { var tmp = b; b = c; c = tmp; }
                int i = _v.Count;
                _v.Add(a); _v.Add(b); _v.Add(c);
                _t.Add(i); _t.Add(i + 1); _t.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                Tri(a, b, c, outward);
                Tri(a, c, d, outward);
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(_v);
                m.SetTriangles(_t, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>Ramp prism: 0 height at z = 0 rising to <paramref name="height"/> at z = length. Pivot at the low edge centre.</summary>
        public static Mesh Wedge(float width, float length, float height)
        {
            var b = new Builder();
            float hw = width * 0.5f;
            Vector3 l0 = new Vector3(-hw, 0, 0), r0 = new Vector3(hw, 0, 0);
            Vector3 lB = new Vector3(-hw, 0, length), rB = new Vector3(hw, 0, length);
            Vector3 lT = new Vector3(-hw, height, length), rT = new Vector3(hw, height, length);
            Vector3 slopeN = new Vector3(0, length, -height).normalized;

            b.Quad(l0, lT, rT, r0, slopeN);                 // riding surface
            b.Quad(lB, rB, rT, lT, Vector3.forward);        // back wall
            b.Tri(l0, lB, lT, Vector3.left);                // sides
            b.Tri(r0, rT, rB, Vector3.right);
            b.Quad(l0, r0, rB, lB, Vector3.down);           // bottom (keeps collider closed)
            return b.Build("Wedge");
        }

        /// <summary>
        /// Quarter pipe facing -z: curved transition of <paramref name="radius"/> from z = 0 up to the lip at (z = radius, y = radius),
        /// then a flat deck of <paramref name="deckDepth"/>. Pivot at the toe (bottom-front) centre.
        /// </summary>
        public static Mesh QuarterPipe(float width, float radius, float deckDepth, int segments = 10, float maxAngleDeg = 88f)
        {
            var b = new Builder();
            float hw = width * 0.5f;
            var profile = new List<Vector2>();
            float maxRad = maxAngleDeg * Mathf.Deg2Rad;
            for (int i = 0; i <= segments; i++)
            {
                float a = maxRad * i / segments;
                profile.Add(new Vector2(radius * Mathf.Sin(a), radius - radius * Mathf.Cos(a))); // (z, y)
            }
            Vector2 lip = profile[profile.Count - 1];
            Vector2 deckEnd = new Vector2(lip.x + deckDepth, lip.y);
            Vector2 backBottom = new Vector2(deckEnd.x, 0f);

            // Curved surface.
            for (int i = 0; i < segments; i++)
            {
                Vector2 p0 = profile[i], p1 = profile[i + 1];
                Vector2 tangent = (p1 - p0).normalized;
                Vector3 n = new Vector3(0, tangent.x, -tangent.y); // perpendicular, pointing into the open side
                b.Quad(V(-hw, p0), V(hw, p0), V(hw, p1), V(-hw, p1), n);
            }
            // Deck, back, bottom.
            b.Quad(V(-hw, lip), V(hw, lip), V(hw, deckEnd), V(-hw, deckEnd), Vector3.up);
            b.Quad(V(-hw, deckEnd), V(hw, deckEnd), V(hw, backBottom), V(-hw, backBottom), Vector3.forward);
            b.Quad(V(-hw, profile[0]), V(-hw, backBottom), V(hw, backBottom), V(hw, profile[0]), Vector3.down);

            // Side caps: fan from the back-bottom corner (valid because the transition curve is convex).
            var outline = new List<Vector2>(profile) { deckEnd };
            for (int i = 0; i < outline.Count - 1; i++)
            {
                b.Tri(V(-hw, backBottom), V(-hw, outline[i]), V(-hw, outline[i + 1]), Vector3.left);
                b.Tri(V(hw, backBottom), V(hw, outline[i]), V(hw, outline[i + 1]), Vector3.right);
            }
            return b.Build("QuarterPipe");
        }

        /// <summary>Annulus whose height blends from innerHeight to outerHeight. Used for the fountain's banks and rim.</summary>
        public static Mesh RingBank(float innerRadius, float outerRadius, float innerHeight, float outerHeight, int segments)
        {
            var b = new Builder();
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments;
                float a1 = Mathf.PI * 2f * (i + 1) / segments;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 i0 = d0 * innerRadius + Vector3.up * innerHeight;
                Vector3 i1 = d1 * innerRadius + Vector3.up * innerHeight;
                Vector3 o0 = d0 * outerRadius + Vector3.up * outerHeight;
                Vector3 o1 = d1 * outerRadius + Vector3.up * outerHeight;
                Vector3 mid = (d0 + d1).normalized;

                b.Quad(i0, i1, o1, o0, Vector3.up);
                if (outerHeight > 0.001f)
                    b.Quad(o0, o1, new Vector3(o1.x, 0, o1.z), new Vector3(o0.x, 0, o0.z), mid);
                if (innerHeight > 0.001f)
                    b.Quad(i0, new Vector3(i0.x, 0, i0.z), new Vector3(i1.x, 0, i1.z), i1, -mid);
            }
            return b.Build("RingBank");
        }

        /// <summary>
        /// Round skate bowl with a curved transition, floor at y = 0 and coping at the top:
        /// flat floor of <paramref name="floorRadius"/>, then a quarter-circle transition of <paramref name="transitionRadius"/>
        /// up to <paramref name="depth"/> (must be below the transition radius). Pivot at the floor centre.
        /// Pair it with <see cref="DeckWithHole"/> (hole radius = <see cref="BowlTopRadius"/>) for the surrounding deck.
        /// </summary>
        public static Mesh Bowl(float floorRadius, float transitionRadius, float depth, int segments = 32, int rings = 9)
        {
            var b = new Builder();
            depth = Mathf.Min(depth, transitionRadius * 0.995f);
            float maxA = Mathf.Acos((transitionRadius - depth) / transitionRadius);
            var profile = new List<Vector2>(); // (radius, height)
            for (int i = 0; i <= rings; i++)
            {
                float a = maxA * i / rings;
                profile.Add(new Vector2(floorRadius + transitionRadius * Mathf.Sin(a), transitionRadius - transitionRadius * Mathf.Cos(a)));
            }
            Vector3 above = Vector3.up * (depth * 2f + 1f);
            for (int s = 0; s < segments; s++)
            {
                float a0 = Mathf.PI * 2f * s / segments, a1 = Mathf.PI * 2f * (s + 1) / segments;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                b.Tri(Vector3.zero, d0 * profile[0].x, d1 * profile[0].x, Vector3.up); // floor
                for (int i = 0; i < rings; i++)
                {
                    Vector3 p00 = d0 * profile[i].x + Vector3.up * profile[i].y;
                    Vector3 p01 = d1 * profile[i].x + Vector3.up * profile[i].y;
                    Vector3 p10 = d0 * profile[i + 1].x + Vector3.up * profile[i + 1].y;
                    Vector3 p11 = d1 * profile[i + 1].x + Vector3.up * profile[i + 1].y;
                    Vector3 mid = (p00 + p11) * 0.5f;
                    b.Quad(p00, p01, p11, p10, above - mid); // faces into the bowl
                }
            }
            return b.Build("Bowl");
        }

        public static float BowlTopRadius(float floorRadius, float transitionRadius, float depth)
        {
            depth = Mathf.Min(depth, transitionRadius * 0.995f);
            return floorRadius + transitionRadius * Mathf.Sin(Mathf.Acos((transitionRadius - depth) / transitionRadius));
        }

        /// <summary>
        /// Square deck of half-size <paramref name="halfSize"/> and <paramref name="height"/> with a round hole of
        /// <paramref name="holeRadius"/> in the middle (the bowl drops in there). Outer walls are closed so the deck is solid.
        /// Segments should be a multiple of 8 so the square's corners land on a segment edge.
        /// </summary>
        public static Mesh DeckWithHole(float halfSize, float holeRadius, float height, int segments = 32)
        {
            var b = new Builder();
            for (int s = 0; s < segments; s++)
            {
                float a0 = Mathf.PI * 2f * s / segments, a1 = Mathf.PI * 2f * (s + 1) / segments;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 c0 = d0 * holeRadius + Vector3.up * height, c1 = d1 * holeRadius + Vector3.up * height;
                Vector3 s0 = d0 * (halfSize / Mathf.Max(Mathf.Abs(d0.x), Mathf.Abs(d0.z))) + Vector3.up * height;
                Vector3 s1 = d1 * (halfSize / Mathf.Max(Mathf.Abs(d1.x), Mathf.Abs(d1.z))) + Vector3.up * height;
                b.Quad(c0, c1, s1, s0, Vector3.up);
            }
            float h = halfSize;
            Vector3 T(float x, float z) => new Vector3(x, height, z);
            Vector3 B(float x, float z) => new Vector3(x, 0f, z);
            b.Quad(B(-h, h), B(h, h), T(h, h), T(-h, h), Vector3.forward);
            b.Quad(B(h, -h), B(-h, -h), T(-h, -h), T(h, -h), Vector3.back);
            b.Quad(B(h, h), B(h, -h), T(h, -h), T(h, h), Vector3.right);
            b.Quad(B(-h, -h), B(-h, h), T(-h, h), T(-h, -h), Vector3.left);
            return b.Build("DeckWithHole");
        }

        /// <summary>Four-sided pyramid funbox: flat top of <paramref name="topSize"/>, ramps of <paramref name="rampLength"/> on every side.</summary>
        public static Mesh Pyramid(float topSize, float rampLength, float height)
        {
            var b = new Builder();
            float t = topSize * 0.5f, o = t + rampLength;
            Vector3 T(float x, float z) => new Vector3(x, height, z);
            Vector3 G(float x, float z) => new Vector3(x, 0f, z);
            b.Quad(T(-t, -t), T(-t, t), T(t, t), T(t, -t), Vector3.up);
            b.Quad(G(-o, o), G(o, o), T(t, t), T(-t, t), new Vector3(0, rampLength, height));     // north ramp
            b.Quad(G(o, -o), G(-o, -o), T(-t, -t), T(t, -t), new Vector3(0, rampLength, -height)); // south
            b.Quad(G(o, o), G(o, -o), T(t, -t), T(t, t), new Vector3(height, rampLength, 0));      // east
            b.Quad(G(-o, -o), G(-o, o), T(-t, t), T(-t, -t), new Vector3(-height, rampLength, 0)); // west
            b.Quad(G(-o, -o), G(o, -o), G(o, o), G(-o, o), Vector3.down);
            return b.Build("Pyramid");
        }

        private static Vector3 V(float x, Vector2 zy) => new Vector3(x, zy.y, zy.x);
    }

    /// <summary>Collider-free copies of Unity's built-in primitive meshes, for visuals parented under physics bodies.</summary>
    public static class PrimitiveMeshes
    {
        private static readonly Dictionary<PrimitiveType, Mesh> s_meshes = new Dictionary<PrimitiveType, Mesh>();

        public static Mesh Get(PrimitiveType type)
        {
            if (s_meshes.TryGetValue(type, out var m) && m != null) return m;
            var go = GameObject.CreatePrimitive(type);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            s_meshes[type] = m;
            return m;
        }

        public static GameObject CreateVisual(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = Get(type);
            go.AddComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.Get(color);
            return go;
        }
    }
}
