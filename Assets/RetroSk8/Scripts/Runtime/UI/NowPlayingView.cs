using RetroSk8.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>A small tape card under the score that slides in when a radio song starts, then fades away.</summary>
    public sealed class NowPlayingView : MonoBehaviour
    {
        private const float ShowSeconds = 4f;
        private CanvasGroup _group;
        private Text _label;
        private float _until;
        private AudioManager _audio;

        public void Build(RectTransform safe, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            var bg = UIFactory.Panel("NowPlaying", safe, Theme.InkSoft);
            UIFactory.Place(bg.rectTransform, anchor, pivot, position, new Vector2(760f, 48f));
            _group = bg.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            var note = UIFactory.Panel("Note", bg.transform, Theme.Tape);
            UIFactory.Place(note.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(10f, 30f));
            _label = UIFactory.Label("Text", bg.transform, "", 26, Theme.Cream, TextAnchor.MiddleLeft, false);
            UIFactory.Stretch(_label.rectTransform);
            _label.rectTransform.offsetMin = new Vector2(32f, 0f);

            _audio = AudioManager.Ensure();
            _audio.NowPlaying += Show;
            if (!string.IsNullOrEmpty(_audio.NowPlayingText)) Show(_audio.NowPlayingText); // a song already on air
        }

        private void OnDestroy()
        {
            if (_audio != null) _audio.NowPlaying -= Show;
        }

        private void Show(string text)
        {
            if (string.IsNullOrEmpty(text) || _label == null) return;
            _label.text = "NOW PLAYING  ·  " + text;
            _until = Time.unscaledTime + ShowSeconds;
        }

        private void Update()
        {
            if (_group == null) return;
            float target = Time.unscaledTime < _until ? 1f : 0f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime * 3f);
        }
    }
}
