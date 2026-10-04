using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.UI
{
    /// <summary>
    /// A tiny off-screen photo studio for the comic panels: one real 3D skater (built like the in-game one), a key and a
    /// rim light and a camera framing head and shoulders, rendering into a texture the UI shows. Lives far below the
    /// world on its own layer, renders only while a comic is open, and gives the character a little idle sway and a
    /// pose per mood (rivals lean in, crew relax).
    /// </summary>
    public sealed class PortraitStudio : MonoBehaviour
    {
        private const int Layer = 31;            // unused by the game; the studio camera sees only this
        private static readonly Vector3 Origin = new Vector3(0f, -600f, 0f);

        private Camera _cam;
        private SkaterVisual _skater;
        private Transform _rig;
        private RenderTexture _rt;
        private string _who;
        private StoryMood _mood;
        private float _t;
        private float _turn;

        public RenderTexture Texture => _rt;

        public static PortraitStudio Create(Transform owner)
        {
            var go = new GameObject("PortraitStudio");
            go.transform.SetParent(owner, false);
            var studio = go.AddComponent<PortraitStudio>();
            studio.Build();
            return studio;
        }

        private void Build()
        {
            _rt = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32) { name = "PortraitRT", antiAliasing = 2 };
            var root = new GameObject("StudioRoot").transform; // not parented to the UI: world space, far away
            root.position = Origin;
            _rig = root;

            var camGo = new GameObject("PortraitCamera");
            camGo.transform.SetParent(root, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.black;
            _cam.allowHDR = false;
            _cam.cullingMask = 1 << Layer;
            _cam.fieldOfView = 26f;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 20f;
            _cam.targetTexture = _rt;
            _cam.enabled = false;

            foreach (var (euler, intensity, color) in new[]
            {
                (new Vector3(25f, -35f, 0f), 1.3f, new Color(1f, 0.96f, 0.9f)),   // key
                (new Vector3(10f, 160f, 0f), 0.9f, new Color(0.6f, 0.85f, 1f)),   // rim from behind
            })
            {
                var l = new GameObject("StudioLight").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.type = LightType.Directional;
                l.transform.rotation = Quaternion.Euler(euler);
                l.intensity = intensity;
                l.color = color;
                l.cullingMask = 1 << Layer;
                l.shadows = LightShadows.None;
            }
        }

        /// <summary>Shows <paramref name="speaker"/> (null or unknown = nobody). Returns false when there's no one to show.</summary>
        public bool Show(string speaker, StoryMood mood, Color background)
        {
            if (_cam != null) _cam.backgroundColor = background;
            var look = StoryCast.LookFor(speaker, SaveManager.Data.look);
            _mood = mood;
            if (look == null)
            {
                _cam.enabled = false;
                if (_skater != null) _skater.gameObject.SetActive(false);
                _who = null;
                return false;
            }
            if (_who != speaker || _skater == null)
            {
                if (_skater != null) Destroy(_skater.gameObject);
                var go = new GameObject("PortraitSkater");
                go.transform.SetParent(_rig, false);
                _skater = go.AddComponent<SkaterVisual>();
                _skater.Build();
                if (speaker == StoryCast.You)
                    _skater.ApplyLoadout(CosmeticsService.CurrentLoadout(ContentRegistry.WithDefaults(null)));
                _skater.ApplyLook(look);
                SetLayer(go.transform);
                _who = speaker;
                _t = 0f;
            }
            _skater.gameObject.SetActive(true);
            _cam.enabled = true;
            Frame();
            return true;
        }

        public void Hide()
        {
            if (_cam != null) _cam.enabled = false;
            if (_skater != null) _skater.gameObject.SetActive(false);
            _who = null;
        }

        private void Frame()
        {
            // The skater's body is turned 70° into a skate stance; turning the model 110° points the face at the camera.
            // A little off-centre reads as a 3/4 comic portrait: rivals square up, crew lean back a touch.
            _turn = _mood == StoryMood.Rival ? 100f : _mood == StoryMood.You ? 118f : 126f;
            _skater.transform.localPosition = Vector3.zero;
            _skater.transform.localRotation = Quaternion.Euler(0f, _turn, 0f);
            var target = Origin + Vector3.up * 1.6f;
            _cam.transform.position = target + new Vector3(0f, 0.05f, -2.2f);
            _cam.transform.LookAt(target);
        }

        private void Update()
        {
            if (_skater == null || !_cam.enabled) return;
            _t += Time.unscaledDeltaTime;
            bool reduced = SaveManager.Data.settings.reducedMotion;
            float sway = reduced ? 0f : Mathf.Sin(_t * 1.6f) * 4f;
            float lean = _mood == StoryMood.Rival ? 6f : 0f; // rivals lean in toward you
            _skater.transform.localRotation = Quaternion.Euler(lean, _turn + sway, 0f);
            _skater.SetCrouch(_mood == StoryMood.Rival ? 0.25f : reduced ? 0f : 0.08f + 0.06f * Mathf.Sin(_t * 2.1f));
        }

        private static void SetLayer(Transform t)
        {
            t.gameObject.layer = Layer;
            foreach (Transform c in t) SetLayer(c);
            var r = t.GetComponent<Renderer>();
            if (r != null)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private void OnDestroy()
        {
            if (_rig != null) Destroy(_rig.gameObject);
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }
    }
}
