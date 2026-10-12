using System;
using System.Text;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Records → SK8 RATING (Phase 30): your rating and rank, where every point comes from (with a bar per part), your
    /// grade at each park, and the three steps worth the most points next. Works offline; the rating also has its own
    /// Game Center board.
    /// </summary>
    public sealed class RatingPanelView : MonoBehaviour
    {
        private ContentRegistry _content;
        private Text _rating, _rank, _next, _tips, _grades, _grades2, _extras;
        private readonly Text[] _partNames = new Text[8];
        private readonly Text[] _partPoints = new Text[8];
        private readonly RectTransform[] _partFills = new RectTransform[8];

        public void Build(RectTransform root, ContentRegistry content, Action onClose)
        {
            _content = content;
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.98f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "SK8 RATING", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -24f), new Vector2(520f, 100f));

            _rating = UIFactory.Label("Rating", root, "0", 140, Theme.Tape, TextAnchor.MiddleLeft, false);
            UIFactory.Place(_rating.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -140f), new Vector2(700f, 160f));
            _rank = UIFactory.Label("Rank", root, "", 52, Theme.Cream, TextAnchor.MiddleLeft, false);
            UIFactory.Place(_rank.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(76f, -296f), new Vector2(760f, 64f));
            _next = UIFactory.Label("Next", root, "", 30, Theme.Teal, TextAnchor.UpperLeft, false);
            _next.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_next.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(76f, -366f), new Vector2(760f, 80f));

            var tipsTitle = UIFactory.Label("TipsTitle", root, "WORTH THE MOST NEXT", 34, Theme.Coral, TextAnchor.UpperLeft, false);
            UIFactory.Place(tipsTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(76f, -470f), new Vector2(760f, 48f));
            _tips = UIFactory.Label("Tips", root, "", 30, Theme.Cream, TextAnchor.UpperLeft, false);
            _tips.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tips.lineSpacing = 1.2f;
            UIFactory.Place(_tips.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(76f, -524f), new Vector2(800f, 260f));

            // Breakdown: name, bar, points / max.
            for (int i = 0; i < 8; i++)
            {
                float y = -150f - i * 66f;
                _partNames[i] = UIFactory.Label("Part" + i, root, "", 30, Theme.Cream, TextAnchor.MiddleLeft, false);
                UIFactory.Place(_partNames[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-180f, y), new Vector2(330f, 56f));
                var bar = UIFactory.Panel("Bar" + i, root, new Color(1f, 1f, 1f, 0.08f));
                UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(160f, y - 12f), new Vector2(560f, 32f));
                var fill = UIFactory.Panel("Fill", bar.transform, Theme.Teal);
                _partFills[i] = fill.rectTransform;
                _partFills[i].anchorMin = Vector2.zero;
                _partFills[i].anchorMax = new Vector2(0f, 1f);
                _partFills[i].offsetMin = _partFills[i].offsetMax = Vector2.zero;
                _partPoints[i] = UIFactory.Label("Points" + i, root, "", 28, Theme.Cream, TextAnchor.MiddleRight, false);
                UIFactory.Place(_partPoints[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(730f, y), new Vector2(300f, 56f));
            }

            // Park grades in two columns of five (one long line ran off the screen).
            var gradesTitle = UIFactory.Label("GradesTitle", root, "PARK GRADES", 28, Theme.Teal, TextAnchor.UpperLeft, false);
            UIFactory.Place(gradesTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-180f, -686f), new Vector2(600f, 40f));
            _grades = UIFactory.Label("Grades", root, "", 26, Theme.Cream, TextAnchor.UpperLeft, false);
            _grades.lineSpacing = 1.05f;
            UIFactory.Place(_grades.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-180f, -728f), new Vector2(590f, 170f));
            _grades2 = UIFactory.Label("Grades2", root, "", 26, Theme.Cream, TextAnchor.UpperLeft, false);
            _grades2.lineSpacing = 1.05f;
            UIFactory.Place(_grades2.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(430f, -728f), new Vector2(600f, 170f));
            _extras = UIFactory.Label("Extras", root, "", 26, Theme.Teal, TextAnchor.UpperLeft, false);
            UIFactory.Place(_extras.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-180f, -906f), new Vector2(1210f, 40f));

            var how = UIFactory.Label("How", root,
                "EVERY PARK COUNTS THE SAME: EACH BEST IS SCORED AGAINST THAT PARK'S PAR, SO BREADTH BEATS FARMING ONE SPOT. ONLY YOUR BESTS COUNT, SO IT NEVER GOES DOWN. ASSISTED BESTS DON'T COUNT.",
                24, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperLeft, false);
            how.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(how.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(400f, 40f), new Vector2(1500f, 90f));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(300f, 90f));
            Refresh();
        }

        private void OnEnable()
        {
            if (_content != null) Refresh();
        }

        public void Refresh()
        {
            int rating = RatingService.Refresh();
            var facts = RatingService.Facts();
            _rating.text = rating.ToString("N0");
            _rank.text = SkaterRating.RankName(rating);
            int toNext = SkaterRating.ToNextRank(rating);
            int idx = SkaterRating.RankIndex(rating);
            _next.text = toNext > 0
                ? $"{toNext:N0} TO {SkaterRating.RankNames[idx + 1]}"
                : "TOP RANK. KEEP CLIMBING THE BOARD.";

            var lines = SkaterRating.Breakdown(facts);
            for (int i = 0; i < _partNames.Length; i++)
            {
                bool has = i < lines.Count;
                _partNames[i].text = has ? SkaterRating.PartName(lines[i].Part) : "";
                _partPoints[i].text = has ? $"{lines[i].Points:N0} / {lines[i].Max:N0}" : "";
                float t = has && lines[i].Max > 0 ? Mathf.Clamp01((float)lines[i].Points / lines[i].Max) : 0f;
                _partFills[i].anchorMax = new Vector2(t, 1f);
            }

            var tips = SkaterRating.Tips(facts, RatingService.ParkNames(_content));
            var sb = new StringBuilder();
            foreach (var tip in tips) sb.AppendLine($"+{tip.Gain}  {tip.Text}");
            _tips.text = tips.Count > 0 ? sb.ToString().TrimEnd() : "NOTHING LEFT BUT RECORDS. GO BREAK YOURS.";

            var items = new System.Collections.Generic.List<string>();
            var names = RatingService.ParkNames(_content);
            foreach (var id in SkaterRating.Parks)
            {
                facts.ParkBests.TryGetValue(id, out long best);
                items.Add(names(id) + " " + SkaterRating.GradeLabel(SkaterRating.GradeFor(id, best)));
            }
            int half = (items.Count + 1) / 2;
            _grades.text = string.Join("\n", items.GetRange(0, half));
            _grades2.text = string.Join("\n", items.GetRange(half, items.Count - half));

            var d = SaveManager.Data;
            int podiums = 0, wins = 0;
            foreach (var c in d.contests) { if (c.bestPlace > 0) podiums++; wins += c.wins; }
            _extras.text = $"CONTEST PODIUMS {podiums}/{NightSessions.ParkCount} · WINS {wins}"
                           + (d.slamBest > 0 ? $"   ·   BIGGEST SLAM {d.slamBestName} {d.slamBest:N0}" : "");
        }
    }
}
