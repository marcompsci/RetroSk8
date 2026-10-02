using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Every scene opens from black with a short fade, which hides the first frame of procedural park building
    /// and makes menu ↔ park ↔ results changes feel smooth. Never blocks taps.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        public const float FadeSeconds = 0.35f;
        private static ScreenFader s_instance;
        private Image _black;
        private float _t = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_instance != null) return;
            var go = new GameObject("ScreenFader");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<ScreenFader>();
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var img = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(go.transform, false);
            UIFactory.Stretch(img.rectTransform);
            img.color = Theme.Ink;
            img.raycastTarget = false;
            s_instance._black = img;
            SceneManager.sceneLoaded += (_, __) => s_instance.Restart();
            s_instance.Restart();
        }

        private void Restart()
        {
            _t = 0f;
            Apply();
        }

        private void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / FadeSeconds);
            Apply();
        }

        private void Apply()
        {
            if (_black == null) return;
            var c = _black.color;
            c.a = 1f - _t * _t; // ease out
            _black.color = c;
            _black.enabled = _t < 1f;
        }
    }
}
