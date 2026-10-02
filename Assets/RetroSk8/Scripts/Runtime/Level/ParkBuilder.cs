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
    }
}
