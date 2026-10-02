using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Shared scaffolding for procedurally built parks: root handling, spawn, kill plane and the
    /// geometry helpers (boxes, ramps, rails, bars, gaps, neon strips). Each park only describes its layout.
    /// </summary>
    public abstract class ParkBuilder : MonoBehaviour
    {
        [Tooltip("Build automatically when the scene starts (skipped if a generated root already exists).")]
        public bool buildOnAwake = true;

        protected Transform _root;

        /// <summary>Name of the generated child object (unique per park).</summary>
        protected abstract string RootName { get; }

        /// <summary>The LocationDefinition id this builder makes (lets a borrowed scene swap to the right park).</summary>
        public abstract string LocationId { get; }

        /// <summary>Describe the park. Must set level.spawnPoint (use <see cref="Spawn"/>), bounds and kill height.</summary>
        protected abstract void BuildPark(LevelInfo level);

        private void Awake()
        {
            if (buildOnAwake) Build();
        }

        [ContextMenu("Rebuild Now")]
        private void RebuildFromMenu() => Build(true);

        /// <summary>Builds the park, or returns the existing one unless <paramref name="force"/> is set.</summary>
        public LevelInfo Build(bool force = false)
        {
            var existing = transform.Find(RootName);
            if (existing != null)
            {
                var info = existing.GetComponent<LevelInfo>();
                if (info != null && !force) return info;
                existing.name = RootName + "_Old";
                if (Application.isPlaying) Destroy(existing.gameObject); else DestroyImmediate(existing.gameObject);
            }

            _root = new GameObject(RootName).transform;
            _root.SetParent(transform, false);
            var level = _root.gameObject.AddComponent<LevelInfo>();
            BuildPark(level);
            // Hundreds of small static meshes share a handful of materials: batching them cuts draw calls
            // sharply on phones. Only at runtime, so edit-mode rebuilds stay editable.
            if (Application.isPlaying) StaticBatchingUtility.Combine(_root.gameObject);
            return level;
        }

        protected Transform Spawn(LevelInfo level, Vector3 position, Vector3 heading)
        {
            var spawn = new GameObject("SpawnPoint").transform;
            spawn.SetParent(_root, false);
            spawn.position = position;
            spawn.rotation = Quaternion.LookRotation(heading);
            level.spawnPoint = spawn;
            return spawn;
        }

        /// <summary>Trigger volume below the park that sends a falling skater back to safety.</summary>
        protected void KillPlane(float y, float size = 400f)
        {
            var kill = new GameObject("KillVolume");
            kill.transform.SetParent(_root, false);
            kill.transform.position = new Vector3(0f, y, 0f);
            var kc = kill.AddComponent<BoxCollider>();
            kc.isTrigger = true;
            kc.size = new Vector3(size, 4f, size);
            kill.AddComponent<KillVolume>();
        }

        /// <summary>Glowing, collider-free strip (neon signage, light bars). Abstract shapes only, no text or logos.</summary>
        protected GameObject Neon(string name, Vector3 center, Vector3 size, Color color, float yaw = 0f)
        {
            var go = PrimitiveMeshes.CreateVisual(name, PrimitiveType.Cube, _root, center, size, color);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(color);
            return go;
        }

        protected GameObject Cylinder(string name, Vector3 baseCenter, float radius, float height, Color color, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Place(go, name, baseCenter + Vector3.up * (height * 0.5f), new Vector3(radius * 2f, height * 0.5f, radius * 2f), color);
            if (!solid) RemoveCollider(go);
            return go;
        }

        // ---------------------------------------------------------------- helpers

        protected GapZone Gap(string id, string displayName, Vector3 center, Vector3 size, int points)
        {
            var go = new GameObject("Gap_" + id);
            go.transform.SetParent(_root, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var zone = go.AddComponent<GapZone>();
            zone.gapId = id;
            zone.displayName = displayName;
            zone.points = points;
            return zone;
        }

        protected static void RemoveCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            // DestroyImmediate so the collider never participates in physics, and so edit-mode rebuilds work.
            DestroyImmediate(c);
        }

        protected GameObject Box(string name, Vector3 center, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Place(go, name, center, size, color);
            return go;
        }

        protected void Place(GameObject go, string name, Vector3 pos, Vector3 scale, Color color)
        {
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.isStatic = true;
            go.GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.Get(color);
        }

        protected GameObject MeshObject(string name, Mesh mesh, Vector3 pos, float yaw, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.Get(color);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        protected GrindRail Rail(string name, List<Vector3> worldPoints, GrindSurface surface, bool loop, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var rail = go.AddComponent<GrindRail>();
            rail.localPoints = new List<Vector3>();
            foreach (var p in worldPoints) rail.localPoints.Add(go.transform.InverseTransformPoint(p));
            rail.loop = loop;
            rail.surface = surface;
            rail.Rebuild();
            return rail;
        }

        /// <summary>Visual (and optionally solid) bar between two points, with support posts when solid.</summary>
        protected void Bar(Vector3 a, Vector3 b, float radius, Color color, bool withPosts, float groundY = 0f)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Vector3 mid = (a + b) * 0.5f;
            float len = Vector3.Distance(a, b);
            Place(bar, "Bar", mid, new Vector3(radius * 2f, len * 0.5f, radius * 2f), color);
            bar.transform.rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);

            if (!withPosts) { RemoveCollider(bar); return; }
            bar.AddComponent<NonGroundSurface>();
            int posts = Mathf.Max(2, Mathf.CeilToInt(len / 4f) + 1);
            for (int i = 0; i < posts; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)(posts - 1));
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                float h = Mathf.Max(0.05f, p.y - groundY);
                Place(post, "BarPost", new Vector3(p.x, groundY + h * 0.5f, p.z), new Vector3(0.08f, h * 0.5f, 0.08f), Palette.Coping);
                post.AddComponent<NonGroundSurface>();
            }
        }

        // ================================================================ spot kit (Phase 8)
        // Dimensions follow public skatepark design guidelines, scaled up slightly for arcade play:
        // ledges 0.35-0.5 m, hubbas ~0.45 m above the stair line, rails ~0.85 m above the step nosings,
        // risers ~0.2 m, mini ramps ~1.2 m, bowls 2-3 m deep, banks around 30-35 degrees.

        public const float StepRise = 0.22f;
        public const float StepRun = 0.42f;

        protected GameObject ColliderOnly(string name, Mesh mesh, Vector3 pos, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.isStatic = true;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>
        /// A stair set descending toward <paramref name="downDir"/> from a raised platform. The riding surface is a smooth
        /// invisible ramp (no wheel-catching risers); the steps are visual. Returns the top-edge centre.
        /// </summary>
        protected Vector3 Stairs(string name, Vector3 bottomCenter, Vector3 downDir, int steps, float width, Color color)
        {
            downDir = Vector3.ProjectOnPlane(downDir, Vector3.up).normalized;
            float h = steps * StepRise, run = steps * StepRun;
            float yaw = Quaternion.LookRotation(-downDir).eulerAngles.y; // wedge rises along its +z = up the stairs
            ColliderOnly(name + "_Ramp", ProcMesh.Wedge(width, run, h), bottomCenter, yaw); // bottomCenter.y = the landing level
            for (int i = 0; i < steps; i++)
            {
                float stepTop = (i + 1) * StepRise;
                Vector3 c = bottomCenter - downDir * (StepRun * (i + 0.5f)) + Vector3.up * (stepTop * 0.5f);
                var step = Box(name + "_Step", c, new Vector3(width, stepTop, StepRun), color);
                step.transform.rotation = Quaternion.LookRotation(-downDir);
                RemoveCollider(step);
            }
            return bottomCenter + Vector3.up * h - downDir * run;
        }

        /// <summary>Handrail running down a stair set (grindable), offset sideways by <paramref name="side"/> metres.</summary>
        protected GrindRail Handrail(string name, Vector3 topEdge, Vector3 downDir, int steps, float side)
        {
            downDir = Vector3.ProjectOnPlane(downDir, Vector3.up).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, downDir).normalized * side;
            Vector3 a = topEdge + across + Vector3.up * 0.85f + downDir * -0.6f;
            Vector3 b = topEdge + across + downDir * (steps * StepRun + 0.4f);
            b.y = topEdge.y - steps * StepRise + 0.85f;
            var rail = Rail(name, new List<Vector3> { a, b }, GrindSurface.Rail, false, _root);
            Bar(a, b, 0.05f, Palette.Metal, true);
            return rail;
        }

        /// <summary>Hubba: a sloped ledge alongside a stair set, grindable along its top edge.</summary>
        protected void Hubba(string name, Vector3 topEdge, Vector3 downDir, int steps, float side, Color color)
        {
            downDir = Vector3.ProjectOnPlane(downDir, Vector3.up).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, downDir).normalized * side;
            float run = steps * StepRun, h = steps * StepRise;
            Vector3 top = topEdge + across + Vector3.up * 0.45f;
            Vector3 bottom = topEdge + across + downDir * run;
            bottom.y = topEdge.y - h + 0.45f;
            Vector3 mid = (top + bottom) * 0.5f - Vector3.up * 0.3f;
            float len = Vector3.Distance(top, bottom);
            var block = Box(name, mid, new Vector3(0.6f, 0.6f, len), color);
            block.transform.rotation = Quaternion.LookRotation(bottom - top);
            Vector3 inner = -across.normalized * 0.3f;
            Rail(name + "_Edge", new List<Vector3> { top + inner * 0.9f, bottom + inner * 0.9f }, GrindSurface.Ledge, false, _root);
        }

        /// <summary>Ledge from <paramref name="a"/> to <paramref name="b"/> (ground points) with both top edges grindable.</summary>
        protected void Ledge(string name, Vector3 a, Vector3 b, float height, float depth, Color color)
        {
            Vector3 dir = (b - a);
            dir.y = 0f;
            float len = dir.magnitude;
            dir /= len;
            Vector3 across = Vector3.Cross(Vector3.up, dir) * (depth * 0.5f);
            var block = Box(name, (a + b) * 0.5f + Vector3.up * (height * 0.5f), new Vector3(depth, height, len), color);
            block.transform.rotation = Quaternion.LookRotation(dir);
            Vector3 up = Vector3.up * (height + 0.02f);
            Vector3 inset = dir * 0.25f;
            Rail(name + "_EdgeA", new List<Vector3> { a + across + up + inset, b + across + up - inset }, GrindSurface.Ledge, false, _root);
            Rail(name + "_EdgeB", new List<Vector3> { a - across + up + inset, b - across + up - inset }, GrindSurface.Ledge, false, _root);
        }

        protected void ManualPad(string name, Vector3 center, Vector2 size, float yaw, Color color)
        {
            var pad = Box(name, center + Vector3.up * 0.15f, new Vector3(size.x, 0.3f, size.y), color);
            pad.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Bench: a seat ledge on legs (grindable seat edge).</summary>
        protected void Bench(string name, Vector3 center, float yaw, float length = 3f)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 dir = rot * Vector3.forward;
            Vector3 across = rot * Vector3.right;
            var seat = Box(name, center + Vector3.up * 0.45f, new Vector3(0.6f, 0.1f, length), Palette.Wood);
            seat.transform.rotation = rot;
            foreach (float k in new[] { -0.4f, 0.4f })
            {
                var leg = Box(name + "_Leg", center + dir * (length * k) + Vector3.up * 0.2f, new Vector3(0.5f, 0.4f, 0.12f), Palette.Coping);
                leg.transform.rotation = rot;
            }
            Vector3 top = Vector3.up * 0.51f;
            Rail(name + "_Edge", new List<Vector3> { center - dir * (length * 0.5f - 0.2f) + across * 0.3f + top, center + dir * (length * 0.5f - 0.2f) + across * 0.3f + top },
                GrindSurface.Ledge, false, _root);
        }

        protected void Planter(string name, Vector3 center, Vector2 size, Color color)
        {
            Ledge(name, center - Vector3.forward * (size.y * 0.5f), center + Vector3.forward * (size.y * 0.5f), 0.5f, size.x, color);
            var soil = Box(name + "_Green", center + Vector3.up * 0.52f, new Vector3(size.x - 0.4f, 0.06f, size.y - 0.4f), new Color(0.25f, 0.45f, 0.25f));
            RemoveCollider(soil);
            var tree = PrimitiveMeshes.CreateVisual(name + "_Tree", PrimitiveType.Sphere, _root, center + Vector3.up * 2.6f, new Vector3(1.8f, 2f, 1.8f), new Color(0.2f, 0.5f, 0.3f));
            PrimitiveMeshes.CreateVisual(name + "_Trunk", PrimitiveType.Cylinder, _root, center + Vector3.up * 1.3f, new Vector3(0.18f, 0.8f, 0.18f), Palette.Wood);
            tree.isStatic = true;
        }

        /// <summary>Round bowl sunk into a raised deck (floor at the ground), with a grindable coping ring. Returns the coping radius.</summary>
        protected float BowlWithDeck(string name, Vector3 center, float floorRadius, float transitionRadius, float depth, float deckHalfSize, Color color)
        {
            float top = ProcMesh.BowlTopRadius(floorRadius, transitionRadius, depth);
            float d = Mathf.Min(depth, transitionRadius * 0.995f);
            MeshObject(name, ProcMesh.Bowl(floorRadius, transitionRadius, d), center, 0f, color);
            MeshObject(name + "_Deck", ProcMesh.DeckWithHole(deckHalfSize, top, d), center, 0f, Palette.ConcreteDark);
            var ring = new List<Vector3>();
            const int segs = 32;
            for (int i = 0; i < segs; i++)
            {
                float a = Mathf.PI * 2f * (i + 0.5f) / segs;
                ring.Add(center + new Vector3(Mathf.Cos(a) * (top + 0.03f), d + 0.03f, Mathf.Sin(a) * (top + 0.03f)));
            }
            Rail(name + "_Coping", ring, GrindSurface.Coping, true, _root);
            return top;
        }

        /// <summary>Mini ramp: two quarter pipes facing each other over a flat bottom, both copings grindable.</summary>
        protected void MiniRamp(string name, Vector3 center, float yaw, float width, float radius, float flat, float deck)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 dir = rot * Vector3.forward;
            foreach (float s in new[] { 1f, -1f })
            {
                Vector3 toe = center + dir * (flat * 0.5f * s);
                float qYaw = yaw + (s > 0f ? 0f : 180f);
                var qp = MeshObject(name + "_QP", ProcMesh.QuarterPipe(width, radius, deck), toe, qYaw, Palette.Plywood);
                var r = Quaternion.Euler(0f, qYaw, 0f);
                float lipAlong = radius * Mathf.Sin(88f * Mathf.Deg2Rad), lipY = radius - radius * Mathf.Cos(88f * Mathf.Deg2Rad);
                Vector3 a = toe + r * new Vector3(-width * 0.5f + 0.3f, lipY + 0.03f, lipAlong);
                Vector3 b = toe + r * new Vector3(width * 0.5f - 0.3f, lipY + 0.03f, lipAlong);
                Rail(name + "_Coping", new List<Vector3> { a, b }, GrindSurface.Coping, false, qp.transform);
                Bar(a, b, 0.07f, Palette.Coping, false);
            }
        }

        protected void Funbox(string name, Vector3 center, float topSize, float rampLength, float height, Color color, bool hubbaLedge)
        {
            MeshObject(name, ProcMesh.Pyramid(topSize, rampLength, height), center, 0f, color);
            if (!hubbaLedge) return;
            float t = topSize * 0.5f;
            Rail(name + "_TopEdgeN", new List<Vector3> { center + new Vector3(-t + 0.2f, height + 0.02f, t), center + new Vector3(t - 0.2f, height + 0.02f, t) }, GrindSurface.Ledge, false, _root);
            Rail(name + "_TopEdgeS", new List<Vector3> { center + new Vector3(-t + 0.2f, height + 0.02f, -t), center + new Vector3(t - 0.2f, height + 0.02f, -t) }, GrindSurface.Ledge, false, _root);
        }

        /// <summary>A city building: a solid block with lit window bands (good for wallrides).</summary>
        protected void Building(string name, Vector3 center, Vector3 size, Color color, bool neonBands)
        {
            Box(name, center + Vector3.up * (size.y * 0.5f), size, color);
            if (!neonBands) return;
            for (float y = 4f; y < size.y - 2f; y += 4f)
            {
                foreach (var (offset, bandSize) in new[]
                {
                    (new Vector3(0f, 0f, size.z * 0.5f + 0.05f), new Vector3(size.x * 0.8f, 0.5f, 0.1f)),
                    (new Vector3(0f, 0f, -size.z * 0.5f - 0.05f), new Vector3(size.x * 0.8f, 0.5f, 0.1f)),
                    (new Vector3(size.x * 0.5f + 0.05f, 0f, 0f), new Vector3(0.1f, 0.5f, size.z * 0.8f)),
                    (new Vector3(-size.x * 0.5f - 0.05f, 0f, 0f), new Vector3(0.1f, 0.5f, size.z * 0.8f)),
                })
                    Neon(name + "_Windows", center + offset + Vector3.up * y, bandSize, Palette.Cream);
            }
        }

        /// <summary>Street curb: visual strip with a grindable edge, no collider (so wheels never catch on it).</summary>
        protected void Curb(string name, Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            var strip = Box(name, (a + b) * 0.5f + Vector3.up * 0.06f, new Vector3(0.3f, 0.12f, len), Palette.Concrete);
            strip.transform.rotation = Quaternion.LookRotation(d);
            RemoveCollider(strip);
            Rail(name + "_Edge", new List<Vector3> { a + Vector3.up * 0.13f, b + Vector3.up * 0.13f }, GrindSurface.Ledge, false, _root);
        }

        protected void Kicker(string name, Vector3 toe, float yaw, float width = 3f, float length = 2.5f, float height = 0.8f)
        {
            MeshObject(name, ProcMesh.Wedge(width, length, height), toe, yaw, Palette.TapeYellow);
        }
    }
}
