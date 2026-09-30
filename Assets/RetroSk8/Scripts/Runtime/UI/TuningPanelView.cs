using System.Collections.Generic;
using RetroSk8.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Live feel-tuning panel (Debug → TUNING). Sliders are grouped into tabs so each page fits a phone screen.
    /// SAVE writes a preset that is re-applied on the next launch; COPY puts the JSON on the clipboard.
    /// </summary>
    public sealed class TuningPanelView : MonoBehaviour
    {
        private TuningSession _session;
        private RectTransform _rows;
        private Text _status;
        private readonly List<(Tunable tunable, Slider slider, Text label)> _live = new List<(Tunable, Slider, Text)>();
        private string _group;

        public void Build(RectTransform root, TuningSession session)
        {
            _session = session;

            var panel = UIFactory.Panel("TuningPanel", root, new Color(0f, 0f, 0f, 0.85f), true);
            UIFactory.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(1000f, 900f));

            var title = UIFactory.Label("Title", panel.transform, "FEEL TUNING", 44, Theme.Tape, TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(600f, 60f));

            // Tabs
            var tabs = UIFactory.Rect("Tabs", panel.transform);
            UIFactory.Place(tabs, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(940f, 70f));
            var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 10f;
            tabLayout.childControlWidth = tabLayout.childControlHeight = false;
            var groups = new List<string>();
            foreach (var t in session.Tunables) if (!groups.Contains(t.Group)) groups.Add(t.Group);
            foreach (var g in groups)
            {
                string group = g;
                UIFactory.MakeButton("Tab_" + g, tabs, g.ToUpperInvariant(), new Vector2(178f, 60f), Theme.Cream, () => ShowGroup(group), 26);
            }

            _rows = UIFactory.Rect("Rows", panel.transform);
            UIFactory.Place(_rows, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(940f, 560f));
            var rowLayout = _rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 12f;
            rowLayout.childControlWidth = rowLayout.childControlHeight = false;
            rowLayout.childAlignment = TextAnchor.UpperLeft;

            // Actions
            var actions = UIFactory.Rect("Actions", panel.transform);
            UIFactory.Place(actions, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(940f, 70f));
            var actionLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 14f;
            actionLayout.childControlWidth = actionLayout.childControlHeight = false;
            UIFactory.MakeButton("Save", actions, "SAVE", new Vector2(220f, 64f), Theme.Tape, Save, 30);
            UIFactory.MakeButton("Copy", actions, "COPY JSON", new Vector2(240f, 64f), Theme.Cream, CopyJson, 30);
            UIFactory.MakeButton("Reset", actions, "RESET", new Vector2(220f, 64f), Theme.Coral, ResetAll, 30);
            UIFactory.MakeButton("Close", actions, "CLOSE", new Vector2(200f, 64f), Theme.Cream, () => gameObject.SetActive(false), 30);

            _status = UIFactory.Label("Status", panel.transform, "", 26, Theme.Teal, TextAnchor.MiddleLeft, false);
            UIFactory.Place(_status.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 18f), new Vector2(900f, 40f));

            if (groups.Count > 0) ShowGroup(groups[0]);
        }

        private void ShowGroup(string group)
        {
            _group = group;
            foreach (Transform child in _rows)
            {
                child.gameObject.SetActive(false); // hide now; Destroy completes at end of frame
                Destroy(child.gameObject);
            }
            _live.Clear();
            foreach (var t in _session.Tunables)
            {
                if (t.Group != group) continue;
                AddRow(t);
            }
        }

        private void AddRow(Tunable t)
        {
            var row = UIFactory.Rect("Row_" + t.Key, _rows);
            row.sizeDelta = new Vector2(940f, 76f);

            var label = UIFactory.Label("Label", row, "", 28, Theme.Cream, TextAnchor.MiddleLeft, false);
            UIFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(430f, 60f));

            var sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderGo.transform.SetParent(row, false);
            var rt = (RectTransform)sliderGo.transform;
            UIFactory.Place(rt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(460f, 40f));
            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = t.Min;
            slider.maxValue = t.Max;
            slider.SetValueWithoutNotify(t.Get());
            TintSlider(sliderGo);
            slider.onValueChanged.AddListener(v =>
            {
                t.Set(v);
                label.text = Format(t);
            });
            label.text = Format(t);
            _live.Add((t, slider, label));
        }

        private static void TintSlider(GameObject sliderGo)
        {
            foreach (var img in sliderGo.GetComponentsInChildren<Image>())
            {
                switch (img.gameObject.name)
                {
                    case "Background": img.color = new Color(1f, 1f, 1f, 0.2f); break;
                    case "Fill": img.color = Theme.Teal; break;
                    case "Handle": img.color = Theme.Tape; break;
                }
            }
        }

        private static string Format(Tunable t)
        {
            float v = t.Get();
            string num = Mathf.Abs(t.Max - t.Min) <= 1.01f ? v.ToString("0.000") : v.ToString("0.0");
            bool changed = Mathf.Abs(v - t.Default) > 1e-4f;
            return $"{t.Label}  {num}{(changed ? " *" : "")}";
        }

        private void Refresh()
        {
            foreach (var (tunable, slider, label) in _live)
            {
                slider.SetValueWithoutNotify(tunable.Get());
                label.text = Format(tunable);
            }
        }

        private void Save() => _status.text = _session.Save() ? "Saved. Applied automatically next launch." : "Save failed (see console).";

        private void CopyJson()
        {
            GUIUtility.systemCopyBuffer = _session.ToJson();
            _status.text = "Tuning JSON copied to the clipboard.";
        }

        private void ResetAll()
        {
            _session.ResetToDefaults();
            TuningSession.DeleteSaved();
            Refresh();
            _status.text = "Defaults restored and saved preset removed.";
        }

        private void OnEnable()
        {
            if (_session != null) Refresh();
        }
    }
}
