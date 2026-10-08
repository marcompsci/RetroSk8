using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Night sessions (Phase 28): turns a built park to dusk or night after its daytime look is applied. The sun becomes
    /// a low gold sun or the moon, ambient and fog darken, neon glows harder, lamp heads light up, and soft pools of
    /// light are laid on the ground under every lamp and sign (cheap additive quads, no real lights). At night a dim
    /// pool follows the skater so you can always read the landing. Shared neon materials are put back when the scene
    /// unloads, so the next daytime park looks the way it should.
    /// </summary>
    public sealed class ParkNight : MonoBehaviour
    {
        public const int MaxPools = 36;

        private static readonly Color NightSky = new Color(0.05f, 0.06f, 0.14f);
        private static readonly Color NightAmbient = new Color(0.2f, 0.21f, 0.33f);
        private static readonly Color NightFog = new Color(0.07f, 0.08f, 0.16f);
        private static readonly Color Moon = new Color(0.55f, 0.62f, 0.95f);
        private static readonly Color GoldenLight = new Color(1f, 0.62f, 0.36f);
        private static readonly Color DuskSky = new Color(0.98f, 0.5f, 0.38f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static Texture2D s_glow;

        // Daytime glow of every shared neon material, kept across instances: a restart can build the next night park
        // before the last one is destroyed, and it must not take the boosted glow as its baseline.
        private static readonly Dictionary<Material, Color> s_daytime = new Dictionary<Material, Color>();
        private static int s_alive;
        /// <summary>Raycasts ignore the Ignore Raycast layer (the skater lives there).</summary>
        private const int GroundMask = ~(1 << 2);
        private readonly List<Transform> _pools = new List<Transform>();
        private ParkTime _time;
        private Material _glow;
        private Transform _player, _playerPool;
        private MaterialPropertyBlock _mpb;

        public ParkTime CurrentTime => _time;
        public int PoolCount => _pools.Count;

        /// <summary>Applies <paramref name="time"/> to the current scene. Does nothing (and adds nothing) for daytime.</summary>
        public static ParkNight Apply(ParkTime time, Transform levelRoot, Material glowMaterial, Transform player)
        {
            if (time == ParkTime.Day) return null;
            var go = new GameObject("ParkNight");
            var night = go.AddComponent<ParkNight>();
            night.Init(time, levelRoot, glowMaterial, player);
            return night;
        }

        private void Init(ParkTime time, Transform levelRoot, Material glowMaterial, Transform player)
        {
            s_alive++;
            _time = time;
            _glow = glowMaterial;
            _player = player;
            _mpb = new MaterialPropertyBlock();
            var (golden, night) = NightSessions.Blend(time);
            ApplySky(golden, night);
            if (levelRoot != null) DressLights(levelRoot, night);
            if (time == ParkTime.Night && _glow != null && player != null)
                _playerPool = MakePool(player.position, Vector3.up, 2.4f, new Color(0.75f, 0.85f, 1f), 0.35f, "SkaterGlow");
        }

        private void ApplySky(float golden, float night)
        {
            Light sun = RenderSettings.sun;
            if (sun == null)
                foreach (var l in FindObjectsByType<Light>())
                    if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null)
            {
                var e = sun.transform.rotation.eulerAngles;
                float elevation = night >= 1f ? 38f : Mathf.Lerp(e.x, 9f, golden);
                sun.transform.rotation = Quaternion.Euler(elevation, e.y, 0f);
                sun.color = Color.Lerp(Color.Lerp(sun.color, GoldenLight, golden), Moon, night);
                sun.intensity = Mathf.Lerp(Mathf.Lerp(sun.intensity, 0.95f, golden), 0.4f, night);
            }
            RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(RenderSettings.ambientSkyColor, GoldenLight * 0.7f, golden * 0.4f), NightAmbient * 1.2f, night);
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(RenderSettings.ambientEquatorColor, GoldenLight * 0.6f, golden * 0.5f), NightAmbient, night);
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, NightAmbient * 0.6f, night);
            RenderSettings.ambientLight = RenderSettings.ambientEquatorColor;
            RenderSettings.fogColor = Color.Lerp(Color.Lerp(RenderSettings.fogColor, GoldenLight * 0.8f, golden * 0.45f), NightFog, night);
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = Color.Lerp(Color.Lerp(cam.backgroundColor, DuskSky, golden * 0.6f), NightSky, night);
        }

        /// <summary>Brighter neon, glowing lamp heads, and a light pool under each light source.</summary>
        private void DressLights(Transform root, float night)
        {
            float glow = 1f + 0.9f * night;
            // The park was built this frame: move its colliders to where they were placed before raycasting.
            Physics.SyncTransforms();
            var lamps = new List<(Vector3 pos, Color color, float size)>();
            var boosted = new HashSet<Material>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null) continue;
                bool neon = m.name.StartsWith("Neon_");
                string n = r.gameObject.name;
                bool lamp = n.Contains("LampHead") || n.Contains("Bulb") || n.Contains("Lamp_Head");
                if (neon && m.HasProperty("_EmissionColor") && boosted.Add(m))
                {
                    if (!s_daytime.ContainsKey(m)) s_daytime[m] = m.GetColor("_EmissionColor");
                    m.SetColor("_EmissionColor", s_daytime[m] * glow);
                }
                if (lamp && !neon && night > 0.5f)
                {
                    Color c = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
                    r.sharedMaterial = PlaceholderMaterials.GetEmissive(Color.Lerp(c, new Color(1f, 0.92f, 0.7f), 0.6f), 2.2f);
                    neon = true;
                    m = r.sharedMaterial;
                }
                if (neon || lamp)
                {
                    var b = r.bounds;
                    Color c = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.white;
                    float size = Mathf.Clamp(1.8f + Mathf.Max(b.size.x, b.size.z) * 0.5f, 2.2f, 7f);
                    lamps.Add((b.center, Normalise(c), size));
                }
            }
            if (_glow == null || night < 0.3f) return;

            // Light pools on whatever is underneath (not under strips painted on the floor itself).
            var placed = new List<Vector3>();
            foreach (var (pos, color, size) in lamps)
            {
                if (_pools.Count >= MaxPools) break;
                if (!Physics.Raycast(pos, Vector3.down, out var hit, 16f, GroundMask, QueryTriggerInteraction.Ignore)) continue;
                float height = pos.y - hit.point.y;
                if (height < 0.6f) continue;
                bool near = false;
                foreach (var p in placed) if ((p - hit.point).sqrMagnitude < 2.5f * 2.5f) { near = true; break; }
                if (near) continue;
                placed.Add(hit.point);
                float spread = Mathf.Clamp(size + height * 0.45f, 2.5f, 8f);
                _pools.Add(MakePool(hit.point, hit.normal, spread, color, 0.55f * night, "LightPool"));
            }
        }

        private static Color Normalise(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return max > 1e-4f ? new Color(c.r / max, c.g / max, c.b / max, 1f) : Color.white;
        }

        private Transform MakePool(Vector3 point, Vector3 normal, float size, Color color, float strength, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = point + normal * 0.03f;
            go.transform.rotation = Quaternion.LookRotation(-normal);
            go.transform.localScale = new Vector3(size, size, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _glow;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var c = new Color(color.r * strength, color.g * strength, color.b * strength, strength);
            mr.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId, c);
            _mpb.SetTexture("_BaseMap", GlowTexture);
            _mpb.SetTexture("_MainTex", GlowTexture);
            mr.SetPropertyBlock(_mpb);
            return go.transform;
        }

        private void LateUpdate()
        {
            if (_playerPool == null || _player == null) return;
            var from = _player.position + Vector3.up * 0.6f;
            if (Physics.Raycast(from, Vector3.down, out var hit, 8f, GroundMask, QueryTriggerInteraction.Ignore))
            {
                _playerPool.gameObject.SetActive(true);
                _playerPool.position = hit.point + hit.normal * 0.035f;
                _playerPool.rotation = Quaternion.LookRotation(-hit.normal);
            }
            else _playerPool.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            s_alive = Mathf.Max(0, s_alive - 1);
            if (s_alive > 0) return; // the next night park took over the glow
            RestoreNeon();
        }

        /// <summary>Puts every shared neon material back to its daytime glow.</summary>
        public static void RestoreNeon()
        {
            foreach (var kv in s_daytime) if (kv.Key != null) kv.Key.SetColor("_EmissionColor", kv.Value);
            s_daytime.Clear();
        }

        // ---------------------------------------------------------------- shared assets

        private static Mesh s_quad;

        /// <summary>A 1 x 1 quad facing +Z, centred.</summary>
        private static Mesh QuadMesh
        {
            get
            {
                if (s_quad != null) return s_quad;
                s_quad = new Mesh { name = "GlowQuad" };
                s_quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
                s_quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                s_quad.colors = new[] { Color.white, Color.white, Color.white, Color.white }; // particle shaders multiply by vertex colour
                s_quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                s_quad.RecalculateNormals();
                s_quad.RecalculateBounds();
                return s_quad;
            }
        }

        /// <summary>Soft round falloff (white, alpha fades to the edge).</summary>
        public static Texture2D GlowTexture
        {
            get
            {
                if (s_glow != null) return s_glow;
                const int n = 64;
                s_glow = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "GlowPool", wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = (1f - d) * (1f - d);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
                s_glow.SetPixels(px);
                s_glow.Apply();
                return s_glow;
            }
        }
    }
}
