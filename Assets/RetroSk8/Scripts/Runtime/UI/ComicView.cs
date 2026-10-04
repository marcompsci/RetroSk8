using System;
using RetroSk8.Audio;
using RetroSk8.Core;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Comic-book cutscenes for the story and the welcome. Each panel slams in over a spinning sunburst in the speaker's
    /// colour: a thick-inked page with halftone dots, the speaker rendered as a real 3D skater in a portrait window
    /// (<see cref="PortraitStudio"/>), a speech bubble whose words type out, and a slapped-on name sticker (rivals get a
    /// VS! starburst). Narration gets a big caption box instead. Tap anywhere (or NEXT) to finish the line, tap again for
    /// the next panel; SKIP jumps ahead. Reduced Motion turns the animation off.
    /// </summary>
    public sealed class ComicView : MonoBehaviour
    {
        private const float CharsPerSecond = 55f;
        private const float EnterSeconds = 0.38f;

        private StoryPanel[] _panels;
        private int _index;
        private Action _done;

        private RawImage _burst;
        private RectTransform _panel;
        private Image _border, _page;
        private RawImage _dots;
        private RectTransform _portraitFrame;
        private RawImage _portrait;
        private Image _portraitBg;
        private RectTransform _bubble;
        private Image _bubbleTail;
        private Text _line;
        private RectTransform _nameSticker;
        private Text _name;
        private Image _vs;
        private RectTransform _caption;
        private Text _captionText;
        private Text _header;
        private RectTransform _dotsRow;
        private Text _nextLabel;
        private Text _hint;
        private PortraitStudio _studio;

        private string _fullText = "";
        private float _typed;
        private float _anim;
        private float _side = 1f;
        private int _tickChars;
        private bool _reduced;

        public void Build(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0.03f, 0.03f, 0.05f, 0.94f), true);
            UIFactory.Stretch(dim.rectTransform);

            // Spinning sunburst behind everything, tinted per speaker.
            _burst = UIFactory.Rect("Sunburst", root).gameObject.AddComponent<RawImage>();
            _burst.texture = ComicArt.Sunburst;
            _burst.raycastTarget = false;
            UIFactory.Place(_burst.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2600f, 2600f));

            // Tap anywhere to advance.
            var catcher = UIFactory.Panel("TapCatcher", root, new Color(0f, 0f, 0f, 0f), true);
            UIFactory.Stretch(catcher.rectTransform);
            catcher.gameObject.AddComponent<ComicTap>().Tapped = Advance;

            // The panel: ink border, coloured page, halftone, portrait window, bubble or caption.
            _panel = UIFactory.Rect("Panel", root);
            UIFactory.Place(_panel, new Vector2(0.5f, 0.53f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640f, 660f));
            var shadow = UIFactory.Panel("Shadow", _panel, new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22f, -22f), new Vector2(1640f, 660f));
            _border = UIFactory.Panel("Border", _panel, Theme.Ink);
            UIFactory.Stretch(_border.rectTransform);
            _page = UIFactory.Panel("Page", _panel, Theme.Cream);
            UIFactory.Stretch(_page.rectTransform, 12f);
            _dots = UIFactory.Rect("Halftone", _page.transform).gameObject.AddComponent<RawImage>();
            _dots.texture = ComicArt.Halftone;
            _dots.raycastTarget = false;
            UIFactory.Stretch(_dots.rectTransform);
            _dots.uvRect = new Rect(0f, 0f, 1616f / 24f, 636f / 24f);

            // Portrait window on the left (a real 3D render of whoever's talking).
            _portraitFrame = UIFactory.Panel("PortraitFrame", _page.transform, Theme.Ink).rectTransform;
            UIFactory.Place(_portraitFrame, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(520f, 560f));
            _portraitBg = UIFactory.Panel("PortraitBg", _portraitFrame, Theme.Teal);
            UIFactory.Stretch(_portraitBg.rectTransform, 10f);
            var burstSmall = UIFactory.Rect("PortraitBurst", _portraitBg.transform).gameObject.AddComponent<RawImage>();
            burstSmall.texture = ComicArt.Sunburst;
            burstSmall.color = new Color(1f, 1f, 1f, 0.18f);
            burstSmall.raycastTarget = false;
            UIFactory.Place(burstSmall.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 900f));
            _portrait = UIFactory.Rect("Portrait", _portraitBg.transform).gameObject.AddComponent<RawImage>();
            _portrait.raycastTarget = false;
            UIFactory.Stretch(_portrait.rectTransform);
            _portraitBg.gameObject.AddComponent<RectMask2D>();

            // Speech bubble with a tail pointing at the portrait.
            var bubbleBorder = UIFactory.Panel("Bubble", _page.transform, Theme.Ink);
            _bubble = bubbleBorder.rectTransform;
            UIFactory.Place(_bubble, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(600f, -20f), new Vector2(980f, 430f));
            var bubbleFill = UIFactory.Panel("Fill", _bubble, Color.white);
            UIFactory.Stretch(bubbleFill.rectTransform, 8f);
            _bubbleTail = UIFactory.Panel("Tail", _bubble, Theme.Ink);
            _bubbleTail.sprite = ComicArt.Tail;
            UIFactory.Place(_bubbleTail.rectTransform, new Vector2(0f, 0.35f), new Vector2(0f, 0.5f), new Vector2(-70f, 0f), new Vector2(80f, 80f));
            var tailFill = UIFactory.Panel("TailFill", _bubbleTail.transform, Color.white);
            tailFill.sprite = ComicArt.Tail;
            UIFactory.Place(tailFill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(62f, 62f));
            _line = UIFactory.Label("Line", bubbleFill.transform, "", 46, Theme.Ink, TextAnchor.MiddleLeft, false);
            _line.horizontalOverflow = HorizontalWrapMode.Wrap;
            _line.lineSpacing = 1.12f;
            UIFactory.Stretch(_line.rectTransform, 34f);

            // Name sticker slapped on the bubble's corner (rivals get a VS! starburst).
            _vs = UIFactory.Panel("VS", _page.transform, Theme.Coral);
            _vs.sprite = ComicArt.Star;
            UIFactory.Place(_vs.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-70f, -50f), new Vector2(190f, 190f));
            var vsText = UIFactory.Label("Text", _vs.transform, "VS!", 52, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Stretch(vsText.rectTransform);
            var name = UIFactory.TapeLabel("Name", _page.transform, "", 44, Theme.Tape, -5f);
            _name = name;
            _nameSticker = name.transform.parent as RectTransform;
            UIFactory.Place(_nameSticker, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(580f, -46f), new Vector2(420f, 78f));

            // Narration caption box.
            var captionBorder = UIFactory.Panel("Caption", _page.transform, Theme.Ink);
            _caption = captionBorder.rectTransform;
            UIFactory.Place(_caption, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1380f, 420f));
            var captionFill = UIFactory.Panel("Fill", _caption, Theme.Tape);
            UIFactory.Stretch(captionFill.rectTransform, 8f);
            _captionText = UIFactory.Label("Text", captionFill.transform, "", 56, Theme.Ink, TextAnchor.MiddleCenter, false);
            _captionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _captionText.lineSpacing = 1.1f;
            UIFactory.Stretch(_captionText.rectTransform, 40f);

            // Header sticker, progress dots, buttons.
            var header = UIFactory.TapeLabel("Header", root, "", 40, Theme.Tape, -3f);
            _header = header;
            UIFactory.Place(header.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -26f), new Vector2(760f, 76f));
            _dotsRow = UIFactory.Rect("Dots", root);
            UIFactory.Place(_dotsRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(600f, 30f));
            var dl = _dotsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            dl.spacing = 14f;
            dl.childAlignment = TextAnchor.MiddleCenter;
            dl.childControlWidth = dl.childControlHeight = false;
            _hint = UIFactory.Label("Hint", root, "TAP ANYWHERE", 24, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Place(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(500f, 34f));

            var skip = UIFactory.MakeButton("Close", root, "SKIP", new Vector2(200f, 80f), Theme.Cream, Finish, 32);
            UIFactory.Place((RectTransform)skip.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-400f, 34f), new Vector2(200f, 80f));
            var next = UIFactory.MakeButton("Resume", root, "NEXT  >", new Vector2(320f, 100f), Theme.Tape, Advance, 44);
            UIFactory.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 26f), new Vector2(320f, 100f));
            _nextLabel = next.GetComponentInChildren<Text>();

            _studio = PortraitStudio.Create(transform);
            _portrait.texture = _studio.Texture;
        }

        /// <summary>Shows <paramref name="panels"/> in order, then calls <paramref name="done"/> (may be null).</summary>
        public void Play(StoryPanel[] panels, string header, Action done)
        {
            _panels = panels ?? new StoryPanel[0];
            _done = done;
            _index = 0;
            _header.text = string.IsNullOrEmpty(header) ? "RETRO SK8" : header;
            _reduced = SaveManager.Data.settings.reducedMotion;
            gameObject.SetActive(true);
            BuildDots();
            if (_panels.Length == 0) { Finish(); return; }
            Show();
        }

        private void BuildDots()
        {
            foreach (Transform c in _dotsRow) Destroy(c.gameObject);
            for (int i = 0; i < _panels.Length; i++)
            {
                var d = UIFactory.Panel("Dot", _dotsRow, Theme.Cream);
                d.sprite = UIFactory.Circle;
                d.rectTransform.sizeDelta = new Vector2(22f, 22f);
            }
        }

        /// <summary>First tap finishes the typing; the next one moves on.</summary>
        private void Advance()
        {
            if (_panels == null) return;
            if (_typed < _fullText.Length) { _typed = _fullText.Length; return; }
            _index++;
            if (_index >= _panels.Length) Finish();
            else Show();
        }

        private void Finish()
        {
            if (_studio != null) _studio.Hide();
            gameObject.SetActive(false);
            var done = _done;
            _done = null;
            done?.Invoke();
        }

        private static Color MoodColor(StoryMood m) =>
            m == StoryMood.Rival ? Theme.Coral : m == StoryMood.Crew ? Theme.Teal : m == StoryMood.You ? Theme.Tape : new Color(0.62f, 0.5f, 0.95f);

        private void Show()
        {
            var p = _panels[_index];
            bool narration = p.Mood == StoryMood.Narration || string.IsNullOrEmpty(p.Speaker);
            Color mood = MoodColor(p.Mood);

            _burst.color = new Color(mood.r, mood.g, mood.b, 0.22f);
            _page.color = Color.Lerp(Theme.Cream, mood, narration ? 0.15f : 0.35f);
            _dots.color = new Color(mood.r * 0.6f, mood.g * 0.6f, mood.b * 0.6f, 0.22f);

            bool portrait = !narration && _studio.Show(p.Speaker, p.Mood, Color.Lerp(Theme.Ink, mood, 0.55f));
            _portraitFrame.gameObject.SetActive(portrait);
            _portraitBg.color = Color.Lerp(Theme.Ink, mood, 0.75f);
            _bubble.gameObject.SetActive(!narration);
            _nameSticker.gameObject.SetActive(!narration);
            _vs.gameObject.SetActive(p.Mood == StoryMood.Rival);
            _caption.gameObject.SetActive(narration);
            _bubbleTail.gameObject.SetActive(portrait);

            // Dialogue sits beside the portrait; with no portrait the bubble takes the page.
            _bubble.anchoredPosition = new Vector2(portrait ? 600f : 60f, -20f);
            _bubble.sizeDelta = new Vector2(portrait ? 980f : 1500f, 430f);
            _nameSticker.anchoredPosition = new Vector2(portrait ? 580f : 40f, -46f);
            _name.text = p.Speaker;
            _nameSticker.GetComponent<Image>().color = p.Mood == StoryMood.Rival ? Theme.Coral : p.Mood == StoryMood.Crew ? Theme.Teal : Theme.Tape;

            _fullText = (p.Line ?? "").ToUpperInvariant();
            _typed = _reduced ? _fullText.Length : 0f;
            _tickChars = 0;
            ApplyText();

            int i = 0;
            foreach (Transform d in _dotsRow)
            {
                var img = d.GetComponent<Image>();
                img.color = i < _index ? Theme.Cream : i == _index ? Theme.Tape : new Color(1f, 1f, 1f, 0.25f);
                d.localScale = Vector3.one * (i == _index ? 1.35f : 1f);
                i++;
            }
            bool last = _index == _panels.Length - 1;
            _nextLabel.text = last ? (_done != null ? "LET'S GO!" : "DONE") : "NEXT  >";

            _side = -_side;
            _anim = _reduced ? 1f : 0f;
            AudioManager.Instance?.PlaySfx(SfxId.TrickWhoosh, 0.55f, 1.2f);
            if (p.Mood == StoryMood.Rival) AudioManager.Instance?.PlaySfx(SfxId.Land, 0.5f, 0.8f);
            Animate();
        }

        private void ApplyText()
        {
            string shown = _fullText.Substring(0, Mathf.Clamp((int)_typed, 0, _fullText.Length));
            _line.text = shown;
            _captionText.text = shown;
        }

        private void Update()
        {
            if (_panels == null) return;
            float dt = Time.unscaledDeltaTime;
            if (!_reduced) _burst.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 8f);

            if (_typed < _fullText.Length && _anim > 0.6f)
            {
                _typed = Mathf.Min(_fullText.Length, _typed + CharsPerSecond * dt);
                ApplyText();
                int chars = (int)_typed;
                if (chars - _tickChars >= 3)
                {
                    _tickChars = chars;
                    AudioManager.Instance?.PlaySfx(SfxId.UiClick, 0.12f, 1.6f + 0.2f * Mathf.Sin(chars));
                }
            }
            if (_anim < 1f)
            {
                _anim = Mathf.Min(1f, _anim + dt / EnterSeconds);
                Animate();
            }
            _hint.color = new Color(1f, 1f, 1f, _reduced ? 0.5f : 0.35f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f));
        }

        private void Animate()
        {
            float k = Juice.EaseOutBack(_anim);
            float tilt = (_index % 2 == 0 ? -1.4f : 1.2f);
            _panel.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, k);
            _panel.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(9f * _side, tilt, k));
            _panel.anchoredPosition = new Vector2(Mathf.Lerp(380f * _side, 0f, Mathf.Clamp01(k)), 0f);
            // The name sticker slams down a beat after the panel lands.
            float slam = Mathf.Clamp01((_anim - 0.45f) / 0.55f);
            _nameSticker.localScale = Vector3.one * Mathf.Lerp(1.7f, 1f, Juice.EaseOutBack(slam));
            _vs.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, Juice.EaseOutBack(slam));
            _vs.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f + Time.unscaledTime * (_reduced ? 0f : 20f));
        }

        private void OnDisable()
        {
            if (_studio != null) _studio.Hide();
        }
    }

    /// <summary>Invisible full-screen catcher so a tap anywhere advances the comic (not a Selectable, so controllers skip it).</summary>
    public sealed class ComicTap : MonoBehaviour, IPointerClickHandler
    {
        public Action Tapped;
        public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke();
    }
}
