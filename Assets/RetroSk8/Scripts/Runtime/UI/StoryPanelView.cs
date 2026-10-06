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
            // Phase 26: buttons shrink to fit every chapter in the column (nine already ran off the bottom at 104 px).
            int n = Story.Chapters.Length;
            float rowH = Mathf.Min(104f, (760f - v.spacing * (n - 1)) / n);
            foreach (var c in Story.Chapters)
            {
                var chapter = c;
                var b = UIFactory.MakeButton("Chapter_" + c.Id, list, "", new Vector2(620f, rowH), Theme.Cream, () => Select(chapter), 30);
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(24f, 0f);
                _chapters.Add((c, b.GetComponent<Image>(), label));
            }

            _chapterTitle = UIFactory.Label("ChapterTitle", root, "", 46, Theme.Tape, TextAnchor.UpperLeft);
            UIFactory.Place(_chapterTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(740f, -150f), new Vector2(1300f, 70f));
            _steps = UIFactory.Rect("Steps", root);
            UIFactory.Place(_steps, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(740f, -240f), new Vector2(1300f, StepsHeight));
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
            // Phase 26: the finale's outro runs straight into the epilogue.
            string caption = step.Id == Story.FinalStepId ? $"{Story.Title} · THE END" : $"{Story.ChapterOf(stepId).Title} · CLEARED";
            _comic.Play(Story.ClearPanels(step), caption, null);
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

        /// <summary>Phase 26: room for the step cards (four-step chapters need the full height down to the bottom).</summary>
        private const float StepsHeight = 800f;

        private void StepCard(StoryStep step, StoryState state)
        {
            bool cleared = state.IsCleared(step.Id);
            bool unlocked = Story.IsUnlocked(state, step.Id);
            int count = Story.ChapterOf(step.Id).Steps.Length;
            float cardH = Mathf.Min(280f, (StepsHeight - 20f * (count - 1)) / count);
            bool compact = cardH < 240f;
            var card = UIFactory.Panel("Step_" + step.Id, _steps, Theme.InkSoft);
            card.rectTransform.sizeDelta = new Vector2(1300f, cardH);
            var name = UIFactory.Label("Name", card.transform, step.Title.ToUpperInvariant() + (cleared ? "   ■ CLEARED" : ""), 38, cleared ? Theme.Teal : Theme.Tape, TextAnchor.UpperLeft);
            UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, compact ? -12f : -20f), new Vector2(1200f, 56f));
            var park = _content.FindLocationExact(step.LocationId);
            string parkName = park != null ? park.displayName.ToUpperInvariant() : step.LocationId;
            var info = UIFactory.Label("Info", card.transform,
                (unlocked ? step.ObjectiveText(parkName).ToUpperInvariant() : "CLEAR THE STEP BEFORE THIS ONE") + $"\nREWARD +{step.Tokens} TAPE TOKENS",
                28, Theme.Cream, TextAnchor.UpperLeft, false);
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, compact ? -64f : -86f), new Vector2(900f, compact ? cardH - 72f : 110f));
            var play = UIFactory.MakeButton("Play", card.transform, cleared ? "REPLAY" : "PLAY", new Vector2(300f, 96f), cleared ? Theme.Cream : Theme.Tape,
                () => _comic.Play(step.Intro, $"CHAPTER {Story.ChapterOf(step.Id).Number} · {step.Title.ToUpperInvariant()}", () => StoryService.Begin(step, _content)), 42);
            UIFactory.Place((RectTransform)play.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(300f, 96f));
            play.interactable = unlocked;
        }
    }
}
