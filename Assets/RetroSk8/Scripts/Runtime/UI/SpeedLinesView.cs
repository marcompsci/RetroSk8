using RetroSk8.Player;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Radial speed streaks at the screen edges when you're flying. Off with reduced motion.</summary>
    public sealed class SpeedLinesView : MonoBehaviour
    {
        private const float StartSpeed = 15.5f;
        private const float FullSpeed = 22f;
        private const float MaxAlpha = 0.32f;

        private static Texture2D s_texture;
        private PlayerController _player;
        private RawImage _image;

        public void Build(RectTransform parent, PlayerController player)
        {
            _player = player;
            var rt = UIFactory.Rect("SpeedLines", parent);
            UIFactory.Stretch(rt);
            rt.SetAsFirstSibling(); // behind every other HUD element
            _image = rt.gameObject.AddComponent<RawImage>();
            _image.texture = Texture();
            _image.raycastTarget = false;
            _image.color = new Color(1f, 1f, 1f, 0f);
        }

        private void Update()
        {
            if (_player == null) return;
            bool calm = SaveManager.Data.settings.reducedMotion;
            float k = calm ? 0f : Mathf.InverseLerp(StartSpeed, FullSpeed, _player.Speed);
            var c = _image.color;
            c.a = Mathf.MoveTowards(c.a, k * MaxAlpha, Time.deltaTime * 1.5f);
            _image.color = c;
        }

        /// <summary>Thin radial streaks that are clear in the centre and strongest at the edges.</summary>
        private static Texture2D Texture()
        {
            if (s_texture != null) return s_texture;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SpeedLines" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f;
                int bucket = Mathf.FloorToInt(ang * 90f);
                uint h = (uint)bucket * 2654435761u;
                bool streak = (h >> 28) < 5;                      // ~1 in 3 angle slices carries a streak
                float within = Mathf.Abs(ang * 90f - bucket - 0.5f); // thin line in the middle of its slice
                float a = streak && within < 0.12f ? Mathf.Clamp01((r - 0.55f) / 0.5f) : 0f;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            s_texture = tex;
            return tex;
        }
    }
}
