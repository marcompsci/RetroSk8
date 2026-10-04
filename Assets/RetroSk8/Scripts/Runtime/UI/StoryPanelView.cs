using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Main menu → STORY: the six chapters of "The Last Spot", each step's objective, and PLAY for the next one.
    /// Steps open with comic panels (<see cref="ComicView"/>); cleared steps play their closing panels back at the menu.
    /// </summary>
    public sealed class StoryPanelView : MonoBehaviour
    {
        private ContentRegistry _content;
        private readonly List<(StoryChapter chapter, Image bg, Text label)> _chapters = new List<(StoryChapter, Image, Text)>();
        private StoryChapter _selected;
        private Text _chapterTitle;
        private RectTransform _steps;
        private ComicView _comic;
        private Text _progress;

        public void Build(RectTransform root, ContentRegistry content, Action onClose)
        {
            _content = content;
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.97f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "STORY: " + Story.Title, 60, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -24f), new Vector2(720f, 100f));
            _progress = UIFactory.Label("Progress", root, "", 30, Theme.Cream, TextAnchor.MiddleRight);
            UIFactory.Place(_progress.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -40f), new Vector2(900f, 60f));

            var list = UIFactory.Rect("Chapters", root);
            UIFactory.Place(list, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -150f), new Vector2(620f, 760f));
            var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 12f;
            v.childControlWidth = v.childControlHeight = false;
            foreach (var c in Story.Chapters)
            {
                var chapter = c;
                var b = UIFactory.MakeButton("Chapter_" + c.Id, list, "", new Vector2(620f, 104f), Theme.Cream, () => Select(chapter), 30);
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(24f, 0f);
                _chapters.Add((c, b.GetComponent<Image>(), label));
            }

            _chapterTitle = UIFactory.Label("ChapterTitle", root, "", 46, Theme.Tape, TextAnchor.UpperLeft);
            UIFactory.Place(_chapterTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(740f, -150f), new Vector2(1300f, 70f));
            _steps = UIFactory.Rect("Steps", root);
            UIFactory.Place(_steps, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(740f, -240f), new Vector2(1300f, 640f));
            var sv = _steps.gameObject.AddComponent<VerticalLayoutGroup>();
            sv.spacing = 20f;
            sv.childControlWidth = sv.childControlHeight = false;

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 30f), new Vector2(300f, 90f));

            var comicRoot = UIFactory.Rect("Comic", root);
            UIFactory.Stretch(comicRoot);
            _comic = comicRoot.gameObject.AddComponent<ComicView>();
            _comic.Build(comicRoot);
            comicRoot.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_content == null) return;
            var next = Story.NextStep(StoryService.State);
            Select(next != null ? Story.ChapterOf(next.Id) : Story.Chapters[Story.Chapters.Length - 1]);
        }

        /// <summary>Plays the closing panels of a step just cleared (the menu calls this on return).</summary>
        public void PlayOutro(string stepId)
        {
            var step = Story.FindStep(stepId);
            if (step == null) return;
            Select(Story.ChapterOf(stepId));
            RetroSk8.Audio.AudioManager.Instance?.PlaySfx(RetroSk8.Audio.SfxId.Fanfare, 0.8f);
            _comic.Play(step.Outro, $"{Story.ChapterOf(stepId).Title} · CLEARED", null);
        }

        private void Select(StoryChapter chapter)
        {
            _selected = chapter;
            var state = StoryService.State;
            int done = 0, total = 0;
            foreach (var s in Story.AllSteps()) { total++; if (state.IsCleared(s.Id)) done++; }
            _progress.text = state.Finished ? $"STORY COMPLETE · {done}/{total} STEPS" : $"{done}/{total} STEPS CLEARED";

            foreach (var (c, bg, label) in _chapters)
            {
                bool unlocked = Story.IsUnlocked(state, c.Steps[0].Id);
                int cleared = 0;
                foreach (var s in c.Steps) if (state.IsCleared(s.Id)) cleared++;
                string status = cleared == c.Steps.Length ? "■ DONE" : unlocked ? "OPEN" : "LOCKED";
                label.text = $"{c.Number}. {c.Title}   {status}";
                bg.color = c == chapter ? Theme.Tape : unlocked ? Theme.Cream : new Color(0.4f, 0.4f, 0.43f);
            }

            _chapterTitle.text = $"CHAPTER {chapter.Number}: {chapter.Title}";
            foreach (Transform child in _steps) Destroy(child.gameObject);
            foreach (var s in chapter.Steps) StepCard(s, state);
        }

        private void StepCard(StoryStep step, StoryState state)
        {
            bool cleared = state.IsCleared(step.Id);
            bool unlocked = Story.IsUnlocked(state, step.Id);
            var card = UIFactory.Panel("Step_" + step.Id, _steps, Theme.InkSoft);
            card.rectTransform.sizeDelta = new Vector2(1300f, 280f);
            var name = UIFactory.Label("Name", card.transform, step.Title.ToUpperInvariant() + (cleared ? "   ■ CLEARED" : ""), 38, cleared ? Theme.Teal : Theme.Tape, TextAnchor.UpperLeft);
            UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(1200f, 56f));
            var park = _content.FindLocationExact(step.LocationId);
            string parkName = park != null ? park.displayName.ToUpperInvariant() : step.LocationId;
            var info = UIFactory.Label("Info", card.transform,
                (unlocked ? step.ObjectiveText(parkName).ToUpperInvariant() : "CLEAR THE STEP BEFORE THIS ONE") + $"\nREWARD +{step.Tokens} TAPE TOKENS",
                28, Theme.Cream, TextAnchor.UpperLeft, false);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -86f), new Vector2(900f, 110f));
            var play = UIFactory.MakeButton("Play", card.transform, cleared ? "REPLAY" : "PLAY", new Vector2(300f, 96f), cleared ? Theme.Cream : Theme.Tape,
                () => _comic.Play(step.Intro, $"CHAPTER {Story.ChapterOf(step.Id).Number} · {step.Title.ToUpperInvariant()}", () => StoryService.Begin(step, _content)), 42);
            UIFactory.Place((RectTransform)play.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(300f, 96f));
            play.interactable = unlocked;
        }
    }

    /// <summary>
    /// Comic-panel cutscenes: one panel at a time with the speaker on a tape strip, tilted like a page, coloured by
    /// who's talking (crew teal, rival coral, narration cream, you tape-yellow). Tap NEXT to read on, SKIP to jump.
    /// </summary>
    public sealed class ComicView : MonoBehaviour
    {
        private StoryPanel[] _panels;
        private int _index;
        private Action _done;
        private Image _page;
        private Text _speaker;
        private RectTransform _speakerTape;
        private Text _line;
        private Text _header;
        private Text _count;
        private Image _head, _body;
        private RectTransform _figure;
        private Text _nextLabel;

        public void Build(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0.04f, 0.04f, 0.06f, 0.96f), true);
            UIFactory.Stretch(dim.rectTransform);
            _header = UIFactory.Label("Header", root, "", 30, Theme.Tape, TextAnchor.UpperLeft);
            UIFactory.Place(_header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -40f), new Vector2(1400f, 50f));

            _page = UIFactory.Panel("Page", root, Theme.Cream);
            UIFactory.Place(_page.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1700f, 640f));
            var shadow = _page.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(16f, -16f);
            UIFactory.Scanlines(_page.transform, 0.06f);

            // A simple figure for whoever's talking (head and shoulders).
            _figure = UIFactory.Rect("Figure", _page.transform);
            UIFactory.Place(_figure, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(70f, 0f), new Vector2(360f, 520f));
            _body = UIFactory.Panel("Body", _figure, Theme.Ink);
            _body.sprite = UIFactory.Circle;
            UIFactory.Place(_body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -120f), new Vector2(340f, 380f));
            _head = UIFactory.Panel("Head", _figure, Theme.Ink);
            _head.sprite = UIFactory.Circle;
            UIFactory.Place(_head.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(170f, 190f));

            var tape = UIFactory.TapeLabel("Speaker", _page.transform, "", 40, Theme.Tape, -3f);
            _speaker = tape;
            _speakerTape = tape.transform.parent as RectTransform;
            UIFactory.Place(_speakerTape, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(480f, -10f), new Vector2(420f, 72f));
            _line = UIFactory.Label("Line", _page.transform, "", 52, Theme.Ink, TextAnchor.MiddleLeft, false);
            _line.horizontalOverflow = HorizontalWrapMode.Wrap;
            _line.lineSpacing = 1.1f;
            UIFactory.Place(_line.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(480f, -10f), new Vector2(1160f, 460f));

            _count = UIFactory.Label("Count", root, "", 26, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(_count.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 60f), new Vector2(300f, 50f));
            var skip = UIFactory.MakeButton("Close", root, "SKIP", new Vector2(240f, 90f), Theme.Cream, Finish, 36);
            UIFactory.Place((RectTransform)skip.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-400f, 36f), new Vector2(240f, 90f));
            var next = UIFactory.MakeButton("Resume", root, "NEXT", new Vector2(320f, 100f), Theme.Tape, Next, 44);
            UIFactory.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 30f), new Vector2(320f, 100f));
            _nextLabel = next.GetComponentInChildren<Text>();
        }

        /// <summary>Shows <paramref name="panels"/> in order, then calls <paramref name="done"/> (may be null).</summary>
        public void Play(StoryPanel[] panels, string header, Action done)
        {
            _panels = panels ?? new StoryPanel[0];
            _done = done;
            _index = 0;
            _header.text = header ?? "";
            gameObject.SetActive(true);
            if (_panels.Length == 0) { Finish(); return; }
            Show();
        }

        private void Next()
        {
            _index++;
            if (_index >= _panels.Length) Finish();
            else Show();
        }

        private void Finish()
        {
            gameObject.SetActive(false);
            var done = _done;
            _done = null;
            done?.Invoke();
        }

        private void Show()
        {
            var p = _panels[_index];
            RetroSk8.Audio.AudioManager.Instance?.PlaySfx(RetroSk8.Audio.SfxId.UiClick, 0.6f, 0.8f);
            Color pageColor, figure;
            switch (p.Mood)
            {
                case StoryMood.Rival: pageColor = new Color(1f, 0.82f, 0.78f); figure = Theme.Coral; break;
                case StoryMood.Crew: pageColor = new Color(0.8f, 0.96f, 0.93f); figure = Theme.Teal; break;
                case StoryMood.You: pageColor = new Color(1f, 0.93f, 0.7f); figure = Theme.Tape; break;
                default: pageColor = Theme.Cream; figure = new Color(0f, 0f, 0f, 0f); break;
            }
            _page.color = pageColor;
            _page.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (_index % 2 == 0 ? -1f : 1f) * 1.2f);
            bool narration = p.Mood == StoryMood.Narration;
            _figure.gameObject.SetActive(!narration);
            _head.color = figure;
            _body.color = figure * 0.8f;
            _speakerTape.gameObject.SetActive(!narration);
            _speaker.text = p.Speaker;
            _speakerTape.GetComponent<Image>().color = p.Mood == StoryMood.Rival ? Theme.Coral : p.Mood == StoryMood.Crew ? Theme.Teal : Theme.Tape;
            // Narration spans the page; dialogue sits beside the speaker.
            _line.rectTransform.anchoredPosition = new Vector2(narration ? 100f : 480f, -10f);
            _line.rectTransform.sizeDelta = new Vector2(narration ? 1500f : 1160f, 460f);
            _line.fontStyle = narration ? FontStyle.Italic : FontStyle.Bold;
            _line.text = narration ? p.Line : "“" + p.Line + "”";
            _count.text = $"{_index + 1} / {_panels.Length}";
            _nextLabel.text = _index == _panels.Length - 1 ? (_done != null ? "LET'S GO" : "DONE") : "NEXT";
        }
    }
}
