using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Main menu → DAILY TRICK (Phase 24): today's trick task and your progress, the reward with its streak bonus, and
    /// a 14-day calendar of finished days (DONE also has a text mark, so it doesn't rely on colour alone).
    /// </summary>
    public sealed class DailyTrickPanelView : MonoBehaviour
    {
        private Text _task, _progress, _reward, _streak;
        private readonly List<(Image cell, Text label)> _days = new List<(Image, Text)>();

        public void Build(RectTransform root, Action onClose)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.96f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "DAILY TRICK", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(460f, 100f));

            var card = UIFactory.Panel("Card", root, Theme.InkSoft);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1600f, 700f));
            _task = UIFactory.Label("Task", card.transform, "", 56, Theme.Coral, TextAnchor.UpperCenter, false);
            _task.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_task.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1500f, 150f));
            _progress = UIFactory.Label("Progress", card.transform, "", 40, Theme.Cream, TextAnchor.UpperCenter);
            UIFactory.Place(_progress.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1500f, 56f));
            _reward = UIFactory.Label("Reward", card.transform, "", 34, Theme.Tape, TextAnchor.UpperCenter);
            UIFactory.Place(_reward.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -256f), new Vector2(1500f, 48f));
            _streak = UIFactory.Label("Streak", card.transform, "", 30, Theme.Teal, TextAnchor.UpperCenter);
            UIFactory.Place(_streak.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -310f), new Vector2(1500f, 44f));

            // 14-day calendar, oldest on the left, today on the right.
            const float cellW = 96f, gap = 8f;
            float startX = -(DailyTricks.CalendarDays * cellW + (DailyTricks.CalendarDays - 1) * gap) * 0.5f + cellW * 0.5f;
            for (int i = 0; i < DailyTricks.CalendarDays; i++)
            {
                var cell = UIFactory.Panel("Day" + i, card.transform, new Color(1f, 1f, 1f, 0.08f));
                UIFactory.Place(cell.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(startX + i * (cellW + gap), -460f), new Vector2(cellW, 120f));
                var label = UIFactory.Label("Label", cell.transform, "", 24, Theme.Cream, TextAnchor.MiddleCenter, false);
                UIFactory.Stretch(label.rectTransform);
                _days.Add((cell, label));
            }
            var hint = UIFactory.Label("Hint", card.transform, "Any banked line in any mode counts. A new trick every day at midnight.", 28, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperCenter, false);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1500f, 40f));

            var back = UIFactory.MakeButton("Back", root, "DONE", new Vector2(360f, 100f), Theme.Tape, onClose, 48);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(360f, 100f));
            Refresh();
        }

        private void OnEnable() { if (_task != null) Refresh(); }

        private void Refresh()
        {
            var c = DailyTrickService.Today;
            int need = DailyTricks.Needed(c), have = Math.Min(DailyTrickService.Progress, need);
            bool done = DailyTrickService.DoneToday;
            _task.text = c.Text;
            _progress.text = done ? "DONE FOR TODAY!" : need > 1 ? $"{have} / {need}" : "NOT YET";
            int today = DailyTrickService.TodayNumber;
            int streak = DailyTricks.StreakEnding(DailyTrickService.State, today);
            int next = done ? streak : streak + 1;
            _reward.text = done ? $"PAID: {DailyTricks.TokensFor(streak)} TAPE TOKENS" : $"REWARD: {DailyTricks.TokensFor(next)} TAPE TOKENS";
            _streak.text = streak > 0 ? $"{streak} DAY{(streak == 1 ? "" : "S")} IN A ROW · EACH DAY IN A ROW ADDS +{DailyTricks.StreakBonusPerDay} (UP TO +{DailyTricks.StreakBonusPerDay * DailyTricks.MaxStreakBonusDays})"
                                       : $"FINISH DAYS IN A ROW FOR UP TO +{DailyTricks.StreakBonusPerDay * DailyTricks.MaxStreakBonusDays} BONUS TOKENS";

            var cal = DailyTricks.Calendar(DailyTrickService.State, today);
            var date = DateTime.Now.Date.AddDays(-(cal.Count - 1));
            for (int i = 0; i < _days.Count && i < cal.Count; i++)
            {
                bool isToday = i == cal.Count - 1;
                _days[i].cell.color = cal[i].done ? Theme.Teal : isToday ? new Color(Theme.Tape.r, Theme.Tape.g, Theme.Tape.b, 0.35f) : new Color(1f, 1f, 1f, 0.08f);
                string dow = date.AddDays(i).ToString("ddd", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();
                string day = dow.Substring(0, Math.Min(2, dow.Length)) + "\n" + date.AddDays(i).Day;
                _days[i].label.text = (cal[i].done ? "■\n" : "\n") + day;
                _days[i].label.color = cal[i].done ? Theme.Ink : Theme.Cream;
            }
        }
    }
}
