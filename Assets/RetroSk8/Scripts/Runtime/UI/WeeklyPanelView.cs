using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Main menu → THIS WEEK: the weekly event, its twist, three goals with progress, and the leaderboards.</summary>
    public sealed class WeeklyPanelView : MonoBehaviour
    {
        private Text _name, _desc, _meta, _best;
        private readonly List<(Text text, Image fill, Image check)> _goals = new List<(Text, Image, Image)>();
        private Button _boards;

        public void Build(RectTransform root, Action onClose)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.96f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "THIS WEEK", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(420f, 100f));

            var card = UIFactory.Panel("Card", root, Theme.InkSoft);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1600f, 720f));
            _name = UIFactory.Label("Name", card.transform, "", 70, Theme.Coral, TextAnchor.UpperLeft);
            UIFactory.Place(_name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -26f), new Vector2(1500f, 90f));
            _desc = UIFactory.Label("Desc", card.transform, "", 32, Theme.Cream, TextAnchor.UpperLeft, false);
            _desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -120f), new Vector2(1500f, 50f));
            _meta = UIFactory.Label("Meta", card.transform, "", 28, Theme.Teal, TextAnchor.UpperLeft);
            UIFactory.Place(_meta.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -176f), new Vector2(1500f, 40f));

            for (int i = 0; i < 3; i++)
            {
                float y = -250f - i * 110f;
                var check = UIFactory.Panel("Check" + i, card.transform, Theme.Teal);
                UIFactory.Place(check.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y - 4f), new Vector2(44f, 44f));
                var text = UIFactory.Label("Goal" + i, card.transform, "", 34, Theme.Cream, TextAnchor.UpperLeft);
                UIFactory.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, y), new Vector2(1400f, 46f));
                var bar = UIFactory.Panel("Bar" + i, card.transform, new Color(1f, 1f, 1f, 0.12f));
                UIFactory.Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, y - 56f), new Vector2(1100f, 16f));
                var fill = UIFactory.Panel("Fill", bar.transform, Theme.Tape);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                _goals.Add((text, fill, check));
            }
            _best = UIFactory.Label("Best", card.transform, "", 30, Theme.Tape, TextAnchor.LowerLeft);
            UIFactory.Place(_best.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(1000f, 50f));
            _boards = UIFactory.MakeButton("Boards", card.transform, "LEADERBOARDS", new Vector2(420f, 96f), Theme.Teal, GameCenter.ShowDashboard, 36);
            UIFactory.Place((RectTransform)_boards.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(420f, 96f));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 30f), new Vector2(300f, 90f));
            Refresh();
        }

        private void OnEnable()
        {
            if (_name != null) Refresh();
        }

        private void Refresh()
        {
            var ev = WeeklyService.Current;
            var s = WeeklyService.State;
            _name.text = ev.Name;
            _desc.text = ev.Description;
            int days = WeeklyEvents.DaysLeft(DateTime.Now);
            _meta.text = $"{days} DAY{(days == 1 ? "" : "S")} LEFT · EACH GOAL +{WeeklyEvents.GoalTokens} TOKENS · ALL THREE +{WeeklyEvents.AllGoalsBonus} MORE";
            for (int i = 0; i < _goals.Count; i++)
            {
                var g = ev.Goals[i];
                float p = s.Progress(g);
                var (text, fill, check) = _goals[i];
                long have = Math.Min(s.Get(g.Counter), g.Target);
                text.text = $"{g.Text.ToUpperInvariant()}   {have:N0}/{g.Target:N0}";
                text.color = p >= 1f ? Theme.Teal : Theme.Cream;
                fill.rectTransform.anchorMax = new Vector2(p, 1f);
                check.color = p >= 1f ? Theme.Teal : new Color(1f, 1f, 1f, 0.15f);
            }
            _best.text = $"YOUR BEST RUN THIS WEEK: {s.Get(WeeklyCounters.BestScore):N0}";
            _boards.gameObject.SetActive(GameCenter.IsAvailable);
        }
    }
}
