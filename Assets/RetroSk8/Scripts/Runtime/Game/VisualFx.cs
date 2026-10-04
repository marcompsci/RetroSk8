using RetroSk8.Data;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.Rendering;
#if RETROSK8_URP
using UnityEngine.Rendering.Universal;
#endif

namespace RetroSk8.Game
{
    /// <summary>
    /// Visual juice for a run: landing dust (tinted by surface), sparks on metal grinds and wallrides, and a
    /// post-processing look per park (bloom, gentle vignette, a little contrast). Everything here is skipped when
    /// "Visual effects: Low" is on. Particles use the opaque placeholder material and shrink out instead of fading,
    /// so no extra shaders need to ship.
    /// </summary>
    public sealed class VisualFx : MonoBehaviour
    {
        private PlayerController _player;
        private GrindController _grind;
        private WallController _wall;
        private ParticleSystem _dust;
        private ParticleSystem _sparks;
        private float _sparkCarry;

        public ParticleSystem Dust => _dust;
        public ParticleSystem Sparks => _sparks;
        public bool PostProcessing { get; private set; }

        public static VisualFx Create(PlayerController player, LocationDefinition location, Camera cam)
        {
            var fx = new GameObject("VisualFx").AddComponent<VisualFx>();
            fx.Init(player, location, cam);
            return fx;
        }

        private void Init(PlayerController player, LocationDefinition location, Camera cam)
        {
            _player = player;
            _grind = player.GetComponent<GrindController>();
            _wall = player.GetComponent<WallController>();
            if (SaveManager.Data.settings.lowEffects) return;

            _dust = MakeSystem("LandingDust", 0.45f, 2.5f, 0.14f, 0.35f, 48);
            _sparks = MakeSystem("GrindSparks", 0.35f, 4.5f, 0.05f, 1.2f, 96);
            _sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(new Color(1f, 0.7f, 0.25f), 3f);
            player.Landed += OnLanded;
            SetUpPost(location, cam);
        }

        private ParticleSystem[] _fireworks;

        /// <summary>A burst of coloured sparks over a big banked line (Phase 13 juice). Skipped on Low effects.</summary>
        public void Fireworks(Vector3 position, int count)
        {
            if (count <= 0 || SaveManager.Data.settings.lowEffects) return;
            if (_fireworks == null)
            {
                Color[] colors = { Palette.TapeYellow, Palette.Coral, Palette.Teal };
                _fireworks = new ParticleSystem[colors.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    var ps = MakeSystem("Fireworks", 1.3f, 9f, 0.2f, 0.55f, 160);
                    var shape = ps.shape;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.4f;
                    ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(colors[i], 3f);
                    _fireworks[i] = ps;
                }
            }
            foreach (var ps in _fireworks)
            {
                ps.transform.position = position;
                ps.Emit(Mathf.Max(1, count / _fireworks.Length));
            }
        }

        private ParticleSystem MakeSystem(string name, float lifetime, float speed, float size, float gravity, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.25f;
            var sizeOver = ps.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f)); // shrink out
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = PlaceholderMaterials.Get(Palette.Concrete);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private void OnLanded(Core.LandingVerdict verdict)
        {
            if (_dust == null) return;
            var surface = SurfaceLookup.FromCollider(_player.GroundCollider);
            Color c = surface == SurfaceKind.Wood ? Palette.Plywood
                    : surface == SurfaceKind.Metal ? Palette.Metal
                    : surface == SurfaceKind.Rubber ? Palette.Rubber
                    : Palette.Concrete;
            _dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = PlaceholderMaterials.Get(c);
            _dust.transform.position = _player.transform.position + Vector3.up * 0.05f;
            _dust.Emit(Mathf.Clamp(6 + Mathf.RoundToInt(_player.LastImpactSpeed * 2f), 6, 30));
        }

        private void Update()
        {
            if (_sparks == null || _player == null) return;
            bool metalGrind = _player.State == SkaterState.Grinding && _grind != null && _grind.CurrentRail != null
                              && _grind.CurrentRail.surface != GrindSurface.Ledge;
            bool wallride = _player.State == SkaterState.Wallride && _wall != null && _wall.CurrentMove == Core.WallMove.Wallride;
            if (!metalGrind && !wallride) { _sparkCarry = 0f; return; }

            _sparkCarry += Time.deltaTime * (metalGrind ? 45f : 20f);
            int n = Mathf.FloorToInt(_sparkCarry);
            if (n <= 0) return;
            _sparkCarry -= n;
            _sparks.transform.SetPositionAndRotation(_player.transform.position + Vector3.up * 0.1f, Quaternion.LookRotation(-_player.Heading + Vector3.up * 0.6f));
            _sparks.Emit(n);
        }

        private void SetUpPost(LocationDefinition location, Camera cam)
        {
#if RETROSK8_URP
            if (cam == null || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderPostProcessing = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            bool neon = location != null && location.ambience == AmbienceKind.Warehouse;
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(neon ? 1.1f : 0.45f);
            bloom.threshold.Override(neon ? 0.9f : 1.1f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(false);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(12f);
            color.contrast.Override(6f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            PostProcessing = true;
#endif
        }

        private void OnDestroy()
        {
            if (_player != null) _player.Landed -= OnLanded;
        }
    }
}
