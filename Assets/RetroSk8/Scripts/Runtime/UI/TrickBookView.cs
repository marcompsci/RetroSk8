using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Main menu → TRICKS (Phase 14): every trick in the game, sorted into tabs. Each card shows how often you've
    /// landed it and its four challenge stars; tap one for how to do it, your stats and the challenge progress.
    /// The two-minute lesson lives here too.
    /// </summary>
    public sealed class TrickBookView : MonoBehaviour
    {
        private const int Slots = 12;

        private readonly List<(Image tabBg, Text tabLabel)> _tabs = new List<(Image, Text)>();
        private readonly List<(Button button, Image bg, Text name, Text count, Image[] stars)> _cards = new List<(Button, Image, Text, Text, Image[])>();
        private GridLayoutGroup _grid;
        private Text _completion;
        private Text _detailName, _detailMeta, _detailHow, _detailStats;
        private readonly List<(Text label, Image fill, Text value)> _goalRows = new List<(Text, Image, Text)>();
        private TrickBookTab _tab;
        private string _selected;
        private List<TrickInfo> _shown = new List<TrickInfo>();

        public void Build(RectTransform root, Action onLesson, Action onClose)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.97f), true);
            UIFactory.Stretch(dim.rectTransform);
            var halftone = new GameObject("Halftone").AddComponent<RawImage>();
            halftone.transform.SetParent(root, false);
            halftone.texture = ComicArt.Halftone;
            halftone.color = new Color(1f, 1f, 1f, 0.035f);
            halftone.uvRect = new Rect(0f, 0f, 70f, 34f);
            halftone.raycastTarget = false;
            UIFactory.Stretch(halftone.rectTransform);

            var title = UIFactory.TapeLabel("Title", root, "TRICK BOOK", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -24f), new Vector2(420f, 100f));
            _completion = UIFactory.Label("Completion", root, "", 30, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(_completion.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(520f, -40f), new Vector2(1700f, 70f));

            // Tabs.
            var tabRow = UIFactory.Rect("Tabs", root);
            UIFactory.Place(tabRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -140f), new Vector2(2200f, 70f));
            var tl = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tl.spacing = 10f;
            tl.childControlWidth = tl.childControlHeight = false;
            foreach (TrickBookTab tab in Enum.GetValues(typeof(TrickBookTab)))
            {
                var t = tab;
                var b = UIFactory.MakeButton("Tab_" + t, tabRow, TrickCatalog.TabName(t), new Vector2(262f, 66f), Theme.InkSoft, () => SelectTab(t), 28);
                _tabs.Add((b.GetComponent<Image>(), b.GetComponentInChildren<Text>()));
            }

            // Card grid (left).
            var gridRect = UIFactory.Rect("Grid", root);
            UIFactory.Place(gridRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -236f), new Vector2(1320f, 640f));
            _grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
            _grid.spacing = new Vector2(16f, 16f);
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            for (int i = 0; i < Slots; i++) _cards.Add(Card(gridRect, i));

            BuildDetail(root, onLesson);

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 30f), new Vector2(300f, 90f));
            var hint = UIFactory.Label("Hint", root, "EVERY TRICK HAS 4 CHALLENGES · EACH ONE PAYS TAPE TOKENS · BANK A LINE TO COUNT IT", 24, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(400f, 50f), new Vector2(1300f, 60f));

            SelectTab(TrickBookTab.Flips);
        }

        private (Button, Image, Text, Text, Image[]) Card(RectTransform grid, int index)
        {
            var bg = UIFactory.Panel("Card" + index, grid, Theme.InkSoft, true);
            var shadow = bg.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(6f, -6f);
            var button = bg.gameObject.AddComponent<Button>();
            int i = index;
            button.onClick.AddListener(() =>
            {
                Audio.AudioManager.Instance?.PlaySfx(Audio.SfxId.UiClick);
                if (i < _shown.Count) Select(_shown[i].Id);
            });
            var name = UIFactory.Label("Name", bg.transform, "", 34, Theme.Cream, TextAnchor.UpperLeft);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(360f, 90f));
            var count = UIFactory.Label("Count", bg.transform, "", 24, Theme.Teal, TextAnchor.LowerLeft, false);
            UIFactory.Place(count.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 14f), new Vector2(220f, 36f));
            var stars = new Image[4];
            for (int s = 0; s < 4; s++)
            {
                var star = UIFactory.Panel("Star" + s, bg.transform, new Color(1f, 1f, 1f, 0.15f));
                star.sprite = ComicArt.Star;
                UIFactory.Place(star.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f - (3 - s) * 40f, 12f), new Vector2(40f, 40f));
                stars[s] = star;
            }
            return (button, bg, name, count, stars);
        }

        private void BuildDetail(RectTransform root, Action onLesson)
        {
            var page = UIFactory.Panel("Detail", root, Theme.Cream);
            UIFactory.Place(page.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -236f), new Vector2(780f, 760f));
            var shadow = page.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Theme.Ink;
            shadow.effectDistance = new Vector2(10f, -10f);
            page.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 0.8f);
            var dots = new GameObject("Halftone").AddComponent<RawImage>();
            dots.transform.SetParent(page.transform, false);
            dots.texture = ComicArt.Halftone;
            dots.color = new Color(0f, 0f, 0f, 0.05f);
            dots.uvRect = new Rect(0f, 0f, 24f, 24f);
            dots.raycastTarget = false;
            UIFactory.Stretch(dots.rectTransform);

            _detailName = UIFactory.Label("Name", page.transform, "", 54, Theme.Ink, TextAnchor.UpperLeft, false);
            _detailName.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_detailName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -26f), new Vector2(712f, 70f));
            _detailMeta = UIFactory.Label("Meta", page.transform, "", 24, Theme.Coral, TextAnchor.UpperLeft, false);
            UIFactory.Place(_detailMeta.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -100f), new Vector2(712f, 36f));

            var howTag = UIFactory.TapeLabel("HowTag", page.transform, "HOW TO", 24, Theme.Tape, -3f);
            UIFactory.Place(howTag.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -146f), new Vector2(150f, 42f));
            _detailHow = UIFactory.Label("How", page.transform, "", 26, Theme.Ink, TextAnchor.UpperLeft, false);
            _detailHow.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_detailHow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -198f), new Vector2(712f, 120f));
            _detailStats = UIFactory.Label("Stats", page.transform, "", 24, new Color(0.18f, 0.18f, 0.2f), TextAnchor.UpperLeft, false);
            _detailStats.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_detailStats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -320f), new Vector2(712f, 70f));

            var goalsTag = UIFactory.TapeLabel("GoalsTag", page.transform, "CHALLENGES", 24, Theme.Teal, 2f);
            UIFactory.Place(goalsTag.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -398f), new Vector2(210f, 42f));
            for (int g = 0; g < 4; g++)
            {
                float y = -452f - g * 58f;
                var label = UIFactory.Label("Goal" + g, page.transform, "", 22, Theme.Ink, TextAnchor.MiddleLeft, false);
                UIFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, y), new Vector2(430f, 44f));
                var bar = UIFactory.Panel("Bar" + g, page.transform, new Color(0f, 0f, 0f, 0.12f));
                UIFactory.Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(470f, y - 12f), new Vector2(170f, 20f));
                var fill = UIFactory.Panel("Fill", bar.transform, Theme.Teal);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.pivot = new Vector2(0f, 0.5f);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                var value = UIFactory.Label("Value", page.transform, "", 22, Theme.Ink, TextAnchor.MiddleRight, false);
                UIFactory.Place(value.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(646f, y), new Vector2(100f, 44f));
                _goalRows.Add((label, fill, value));
            }

            var lesson = UIFactory.MakeButton("Lesson", page.transform, "PLAY THE LESSON", new Vector2(420f, 76f), Theme.Tape, () => onLesson?.Invoke(), 32);
            UIFactory.Place((RectTransform)lesson.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(420f, 76f));
        }

        private void OnEnable()
        {
            if (_grid != null) SelectTab(_tab);
        }

        private void SelectTab(TrickBookTab tab)
        {
            _tab = tab;
            _shown = new List<TrickInfo>();
            foreach (var t in TrickBookService.Catalog) if (t.Tab == tab) _shown.Add(t);
            for (int i = 0; i < _tabs.Count; i++)
            {
                bool on = i == (int)tab;
                _tabs[i].tabBg.color = on ? Theme.Tape : Theme.InkSoft;
                _tabs[i].tabLabel.color = on ? Theme.Ink : Theme.Cream;
            }
            // Up to 9 cards fit three across; more go four across.
            int cols = _shown.Count > 9 ? 4 : 3;
            int rows = Mathf.Max(1, Mathf.CeilToInt(_shown.Count / (float)cols));
            float w = (1320f - (cols - 1) * 16f) / cols;
            float h = Mathf.Min(200f, (640f - (rows - 1) * 16f) / rows);
            _grid.constraintCount = cols;
            _grid.cellSize = new Vector2(w, h);
            if (_shown.Count > 0 && (_selected == null || !_shown.Exists(t => t.Id == _selected))) _selected = _shown[0].Id;
            Refresh();
        }

        private void Select(string id)
        {
            _selected = id;
            Refresh();
        }

        private void Refresh()
        {
            var state = TrickBookService.State;
            var (landed, total) = TrickBookService.Completion();
            int stars = 0;
            foreach (var t in TrickBookService.Catalog) stars += state.GoalsDone(t.Id);
            _completion.text = $"{landed} / {total} TRICKS LANDED   ·   {stars} / {total * 4} CHALLENGE STARS";

            for (int i = 0; i < _cards.Count; i++)
            {
                var (button, bg, name, count, starImgs) = _cards[i];
                bool used = i < _shown.Count;
                button.gameObject.SetActive(used);
                if (!used) continue;
                var info = _shown[i];
                var rec = state.Find(info.Id);
                int n = rec != null ? rec.landed : 0;
                bool sel = info.Id == _selected;
                bg.color = sel ? new Color(0.12f, 0.32f, 0.3f, 0.97f) : Theme.InkSoft;
                name.text = info.Name.ToUpperInvariant();
                name.color = n > 0 ? Theme.Tape : new Color(1f, 1f, 1f, 0.55f);
                count.text = n > 0 ? $"LANDED ×{n:N0}" : "NOT LANDED YET";
                count.color = n > 0 ? Theme.Teal : new Color(1f, 1f, 1f, 0.4f);
                for (int s = 0; s < 4; s++)
                    starImgs[s].color = rec != null && (rec.claimed & (1 << s)) != 0 ? Theme.Tape : new Color(1f, 1f, 1f, 0.15f);
            }
            RefreshDetail();
        }

        private void RefreshDetail()
        {
            var info = TrickBookService.Find(_selected);
            if (info == null)
            {
                _detailName.text = "";
                _detailMeta.text = _detailHow.text = _detailStats.text = "";
                foreach (var (label, fill, value) in _goalRows) { label.text = value.text = ""; fill.rectTransform.anchorMax = new Vector2(0f, 1f); }
                return;
            }
            var rec = TrickBookService.State.Find(info.Id);
            _detailName.text = info.Name.ToUpperInvariant();
            _detailMeta.text = TrickCatalog.TabName(info.Tab) + (info.Points > 0 ? $"  ·  {info.Points:N0} PTS" + (IsHeld(info) ? " TO START" : "") : "");
            _detailHow.text = info.HowTo;
            _detailStats.text = rec == null || rec.landed == 0
                ? "YOU HAVEN'T LANDED THIS ONE YET."
                : $"LANDED {rec.landed:N0}×  ·  BEST LINE {rec.bestLine:N0}  ·  LONGEST LINE {rec.longestLine} TRICKS  ·  {rec.parks.Count} PARK{(rec.parks.Count == 1 ? "" : "S")}";
            for (int g = 0; g < 4; g++)
            {
                var goal = (TrickGoal)g;
                var (label, fill, value) = _goalRows[g];
                bool done = rec != null && (rec.claimed & (1 << g)) != 0;
                var (cur, target) = TrickBookState.Progress(rec, goal);
                label.text = TrickBookState.GoalText(goal) + $"  +{TrickBookState.Tokens(goal)}";
                label.color = done ? new Color(0.1f, 0.45f, 0.4f) : Theme.Ink;
                fill.rectTransform.anchorMax = new Vector2(target > 0 ? Mathf.Clamp01(cur / (float)target) : 0f, 1f);
                fill.color = done ? Theme.Tape : Theme.Teal;
                value.text = done ? "DONE" : goal == TrickGoal.BigLine ? $"{cur / 1000f:0.#}K" : $"{cur}/{target}";
            }
        }

        /// <summary>Held tricks start at their points and build while you hold them.</summary>
        private static bool IsHeld(TrickInfo t) => t.Category == TrickCategory.Grind || t.Category == TrickCategory.Manual || t.Category == TrickCategory.Lip || t.Id == "wallride";
    }
}
