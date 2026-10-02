using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Level;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Main menu → CAREER: chapter list, the selected chapter's story, crew/sponsor, goals with progress, and GO.</summary>
    public sealed class CareerPanelView : MonoBehaviour
    {
        private ContentRegistry _content;
        private Action _openCreatePark;
        private readonly List<(Button button, Text label, Image bg)> _chapters = new List<(Button, Text, Image)>();
        private Text _title, _meta, _story, _reward, _rank, _messages;
        private readonly List<(Text text, Image fill, Image check)> _goals = new List<(Text, Image, Image)>();
        private Button _go;
        private Text _goLabel;
        private int _selected = 1;

        public void Build(RectTransform root, ContentRegistry content, Action onClose, Action openCreatePark)
        {
            _content = content;
            _openCreatePark = openCreatePark;
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.96f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "CAREER", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -24f), new Vector2(360f, 100f));
            _rank = UIFactory.Label("Rank", root, "", 32, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(_rank.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(460f, -46f), new Vector2(900f, 56f));

            // Chapter list.
            var list = UIFactory.Rect("Chapters", root);
            UIFactory.Place(list, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -150f), new Vector2(620f, 800f));
            var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10f;
            v.childControlWidth = v.childControlHeight = false;
            foreach (var c in Career.Chapters)
            {
                int n = c.Number;
                var b = UIFactory.MakeButton("Chapter" + n, list, "", new Vector2(620f, 84f), Theme.Cream, () => Select(n), 30);
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(24f, 0f);
                _chapters.Add((b, label, b.GetComponent<Image>()));
            }

            // Detail card.
            var card = UIFactory.Panel("Detail", root, Theme.InkSoft);
            UIFactory.Place(card.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -150f), new Vector2(1360f, 800f));
            _title = UIFactory.Label("ChapterTitle", card.transform, "", 56, Theme.Tape, TextAnchor.UpperLeft);
            UIFactory.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -24f), new Vector2(1280f, 70f));
            _meta = UIFactory.Label("Meta", card.transform, "", 28, Theme.Teal, TextAnchor.UpperLeft);
            UIFactory.Place(_meta.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -100f), new Vector2(1280f, 40f));
            _story = UIFactory.Label("Story", card.transform, "", 30, Theme.Cream, TextAnchor.UpperLeft, false);
            _story.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_story.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -150f), new Vector2(1280f, 130f));

            for (int i = 0; i < 3; i++)
            {
                float y = -300f - i * 100f;
                var check = UIFactory.Panel("Check" + i, card.transform, Theme.Teal);
                UIFactory.Place(check.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, y - 6f), new Vector2(40f, 40f));
                var text = UIFactory.Label("Goal" + i, card.transform, "", 30, Theme.Cream, TextAnchor.UpperLeft);
                UIFactory.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, y), new Vector2(1180f, 40f));
                var bar = UIFactory.Panel("Bar" + i, card.transform, new Color(1f, 1f, 1f, 0.12f));
                UIFactory.Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, y - 48f), new Vector2(900f, 14f));
                var fill = UIFactory.Panel("Fill", bar.transform, Theme.Tape);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(0f, 1f);
                fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                _goals.Add((text, fill, check));
            }

            _reward = UIFactory.Label("Reward", card.transform, "", 30, Theme.Tape, TextAnchor.LowerLeft);
            UIFactory.Place(_reward.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 130f), new Vector2(1000f, 44f));
            _messages = UIFactory.Label("Messages", card.transform, "", 26, Theme.Teal, TextAnchor.LowerLeft, false);
            _messages.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_messages.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 30f), new Vector2(880f, 96f));
            _go = UIFactory.MakeButton("Go", card.transform, "", new Vector2(380f, 110f), Theme.Tape, Go, 44);
            UIFactory.Place((RectTransform)_go.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 30f), new Vector2(380f, 110f));
            _goLabel = _go.GetComponentInChildren<Text>();

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 30f), new Vector2(300f, 90f));
        }

        private void OnEnable()
        {
            if (_title == null) return;
            CareerService.Check();
            _selected = Career.Current(CareerService.Facts).Number;
            Refresh();
            var msgs = CareerService.TakePending();
            _messages.text = msgs.Count > 0 ? string.Join("\n", msgs.ToArray()) : "";
        }

        private void Select(int number)
        {
            _selected = number;
            _messages.text = "";
            Refresh();
        }

        private void Refresh()
        {
            var facts = CareerService.Facts;
            var state = CareerService.State;
            int done = 0;
            foreach (var c in Career.Chapters) if (Career.IsComplete(c, facts)) done++;
            _rank.text = (string.IsNullOrEmpty(state.title) ? "ROOKIE" : state.title) + $"   ·   {done}/{Career.Chapters.Length} CHAPTERS";

            for (int i = 0; i < _chapters.Count; i++)
            {
                var c = Career.Chapters[i];
                bool open = Career.IsUnlocked(c, facts);
                bool complete = Career.IsComplete(c, facts);
                var (button, label, bg) = _chapters[i];
                label.text = $"{c.Number}. {c.Title.ToUpperInvariant()}" + (complete ? "   ■" : open ? "" : "   (LOCKED)");
                bg.color = c.Number == _selected ? Theme.Tape : complete ? Theme.Teal : open ? Theme.Cream : new Color(0.45f, 0.45f, 0.48f);
            }

            var ch = Career.Find(_selected);
            bool unlocked = Career.IsUnlocked(ch, facts);
            _title.text = $"CHAPTER {ch.Number}: {ch.Title.ToUpperInvariant()}";
            _meta.text = "CREW: " + ch.Crew.ToUpperInvariant() + (string.IsNullOrEmpty(ch.Sponsor) ? "" : "   ·   SPONSOR: " + ch.Sponsor.ToUpperInvariant());
            _story.text = unlocked ? ch.Story : "Finish the chapter before this one to unlock it.";
            for (int i = 0; i < _goals.Count; i++)
            {
                var g = ch.Goals[i];
                float p = Career.Progress(g, facts);
                var (text, fill, check) = _goals[i];
                text.text = g.Text.ToUpperInvariant();
                text.color = p >= 1f ? Theme.Teal : Theme.Cream;
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
                check.color = p >= 1f ? Theme.Teal : new Color(1f, 1f, 1f, 0.15f);
            }
            var unlocks = new List<string>();
            foreach (var s in Stickers.All) if (s.UnlockChapter == ch.Number) unlocks.Add(s.Name + " STICKER");
            _reward.text = $"REWARD: {ch.RewardTokens} TAPE TOKENS · TITLE \"{ch.RewardTitle}\"" + (unlocks.Count > 0 ? " · " + string.Join(", ", unlocks.ToArray()) : "");
            _go.interactable = unlocked;
            _goLabel.text = ch.Action == CareerAction.ExploreCity ? "EXPLORE" : ch.Action == CareerAction.CreatePark ? "BUILD" : "SKATE";
        }

        private void Go()
        {
            var ch = Career.Find(_selected);
            switch (ch.Action)
            {
                case CareerAction.ExploreCity:
                    GameSession.Mode = RunMode.FreeSkate;
                    SceneRouter.LoadPark(ParkCatalog.RetroCity, ParkCatalog.SceneFor(ParkCatalog.RetroCity));
                    break;
                case CareerAction.CreatePark:
                    _openCreatePark?.Invoke();
                    break;
                default:
                {
                    var loc = _content.FindLocation(ch.LocationId);
                    GameSession.Mode = RunMode.TwoMinuteRun;
                    SceneRouter.LoadPark(loc.id, loc.sceneName);
                    break;
                }
            }
        }
    }
}
