using System.Collections.Generic;
using RetroSk8.Audio;
using RetroSk8.Core;
using RetroSk8.Feedback;
using RetroSk8.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Goal checklist shown during Spot Contracts and the Daily Line, with a toast when a goal completes.</summary>
    public sealed class GoalsHudView : MonoBehaviour
    {
        private GoalManager _goals;
        private HudView _hud;
        private readonly List<Text> _lines = new List<Text>();

        public void Build(RectTransform safe, GoalManager goals, HudView hud, string title)
        {
            _goals = goals;
            _hud = hud;

            var panel = UIFactory.Panel("Goals", safe, Theme.InkSoft);
            int count = goals.Tracker.Total;
            UIFactory.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -170f),
                new Vector2(640f, 70f + 52f * count));

            var header = UIFactory.TapeLabel("Header", panel.transform, title, 28, Theme.Tape, -2f);
            UIFactory.Place(header.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(300f, 44f));

            for (int i = 0; i < count; i++)
            {
                var line = UIFactory.Label("Goal" + i, panel.transform, "", 30, Theme.Cream, TextAnchor.MiddleLeft, false);
                UIFactory.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -40f - 52f * i), new Vector2(610f, 48f));
                _lines.Add(line);
            }

            goals.GoalCompleted += state =>
            {
                _hud.ShowToast("GOAL!  " + state.Goal.description.ToUpperInvariant(), Theme.Teal, 2.2f);
                AudioManager.Instance?.PlaySfx(SfxId.GoalComplete, 0.8f);
                HapticsManager.Play(HapticKind.Success);
            };
        }

        private void Update()
        {
            if (_goals == null) return;
            var states = _goals.Tracker.Goals;
            if (_shown.Length < _lines.Count) _shown = new int[_lines.Count];
            for (int i = 0; i < _lines.Count && i < states.Count; i++)
            {
                var s = states[i];
                // Phase 18: rebuild a line only when its state or progress count changes (+1 so 0 isn't "unset").
                int key = s.Completed ? -2 : (int)s.Progress + 1;
                if (_shown[i] == key) continue;
                _shown[i] = key;
                string progress = s.Completed ? "" : ProgressText(s);
                _lines[i].text = (s.Completed ? "■ " : "□ ") + s.Goal.description + progress;
                _lines[i].color = s.Completed ? Theme.Teal : Theme.Cream;
            }
        }

        private int[] _shown = new int[0];

        private static string ProgressText(GoalTracker.GoalState s)
        {
            switch (s.Goal.type)
            {
                case GoalType.DistinctRails:
                case GoalType.DistinctTricks:
                    return $"  {(int)s.Progress}/{(int)s.Goal.target}";
                default:
                    return "";
            }
        }
    }
}
