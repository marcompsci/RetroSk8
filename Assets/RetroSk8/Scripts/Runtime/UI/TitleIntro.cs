using RetroSk8.Core;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.UI
{
    /// <summary>
    /// The title screen's entrance: the tape bands slide in from the sides, the RETRO SK8 logo pops in with a little
    /// overshoot and the tagline drops onto it; afterwards the logo sways gently. Skipped with Reduced Motion on.
    /// </summary>
    public sealed class TitleIntro : MonoBehaviour
    {
        private const float Duration = 1.1f;

        private RectTransform _title, _tag, _bandA, _bandB;
        private Vector2 _bandAPos, _bandBPos, _tagPos;
        private float _t;
        private bool _reduced;

        public void Init(RectTransform title, RectTransform tag, RectTransform bandA, RectTransform bandB)
        {
            _title = title;
            _tag = tag;
            _bandA = bandA;
            _bandB = bandB;
            _bandAPos = bandA.anchoredPosition;
            _bandBPos = bandB.anchoredPosition;
            _tagPos = tag != null ? tag.anchoredPosition : Vector2.zero;
            _reduced = SaveManager.Data.settings.reducedMotion;
            if (!_reduced) Apply(0f);
        }

        private void Update()
        {
            if (_reduced || _title == null) return;
            _t += Time.unscaledDeltaTime;
            if (_t <= Duration + 0.4f) Apply(_t);
            else _title.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_t * 1.3f) * 0.6f); // idle sway
        }

        private void Apply(float t)
        {
            float bands = Mathf.Clamp01(t / (Duration * 0.6f));
            float slide = 1f - Mathf.Pow(1f - bands, 3f);
            _bandA.anchoredPosition = _bandAPos + new Vector2((1f - slide) * -3000f, 0f);
            _bandB.anchoredPosition = _bandBPos + new Vector2((1f - slide) * 3000f, 0f);

            float pop = Juice.EaseOutBack(Mathf.Clamp01((t - 0.2f) / 0.6f));
            _title.localScale = Vector3.one * Mathf.Max(0.001f, pop);

            if (_tag != null)
            {
                float drop = Mathf.Clamp01((t - 0.65f) / 0.35f);
                _tag.anchoredPosition = _tagPos + new Vector2(0f, (1f - Juice.EaseOutBack(drop)) * 220f);
                _tag.localScale = Vector3.one * (drop > 0f ? 1f : 0.001f);
            }
            if (t >= 0.25f && t - Time.unscaledDeltaTime < 0.25f)
                RetroSk8.Audio.AudioManager.Instance?.PlaySfx(RetroSk8.Audio.SfxId.TrickWhoosh, 0.6f, 0.9f);
        }
    }
}
