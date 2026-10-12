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
    /// Local records: best score and best combo per park, contract stars, and every achievement with progress.
    /// Works offline and on every platform; the GAME CENTER button appears only in builds with Game Center on.
    /// </summary>
    public sealed class RecordsPanelView : MonoBehaviour
    {
        private ContentRegistry _content;
        private Text _parks, _parks2, _cityLine;
        private Text _achLeft;
        private Text _achRight;
        private Text _gcLabel;
        private GameObject _friends;
        private GameObject _ratingPanel;

        public void Build(RectTransform root, ContentRegistry content, Action onClose)
        {
            _content = content;
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.94f), true);
            UIFactory.Stretch(dim.rectTransform);

            var title = UIFactory.TapeLabel("Title", root, "RECORDS", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(460f, 100f));

            // Phase 27: parks in two columns (best · contract stars · best combo); ten parks and the city no longer fit
            // one list above the achievements.
            _parks = UIFactory.Label("Parks", root, "", 26, Theme.Cream, TextAnchor.UpperLeft, false);
            _parks.lineSpacing = 1.1f;
            UIFactory.Place(_parks.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -150f), new Vector2(890f, 210f));
            _parks2 = UIFactory.Label("Parks2", root, "", 26, Theme.Cream, TextAnchor.UpperLeft, false);
            _parks2.lineSpacing = 1.1f;
            UIFactory.Place(_parks2.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(10f, -150f), new Vector2(890f, 210f));
            _cityLine = UIFactory.Label("CityLine", root, "", 28, Theme.Cream, TextAnchor.UpperLeft, false);
            _cityLine.lineSpacing = 1.1f;
            UIFactory.Place(_cityLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -364f), new Vector2(1800f, 80f));

            var achTitle = UIFactory.Label("AchTitle", root, "ACHIEVEMENTS", 40, Theme.Teal, TextAnchor.UpperLeft);
            UIFactory.Place(achTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -450f), new Vector2(1800f, 56f));
            _achLeft = UIFactory.Label("AchLeft", root, "", 30, Theme.Cream, TextAnchor.UpperLeft, false);
            _achLeft.lineSpacing = 1.15f;
            UIFactory.Place(_achLeft.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -506f), new Vector2(890f, 380f));
            _achRight = UIFactory.Label("AchRight", root, "", 30, Theme.Cream, TextAnchor.UpperLeft, false);
            _achRight.lineSpacing = 1.15f;
            UIFactory.Place(_achRight.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(10f, -506f), new Vector2(890f, 380f));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(300f, 90f));

            // Phase 30: the SK8 RATING (always there; its board is in Leaderboards → EVENTS).
            var ratingButton = UIFactory.MakeButton("Rating", root, "", new Vector2(420f, 90f), Theme.Tape, () => { _ratingPanel.SetActive(true); _ratingPanel.transform.SetAsLastSibling(); }, 34);
            UIFactory.Place((RectTransform)ratingButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(390f, 40f), new Vector2(420f, 90f));
            _ratingLabel = ratingButton.GetComponentInChildren<Text>();
            var ratingRoot = UIFactory.Rect("RatingPanel", root);
            UIFactory.Stretch(ratingRoot);
            ratingRoot.gameObject.AddComponent<RatingPanelView>().Build(ratingRoot, content, () => _ratingPanel.SetActive(false));
            _ratingPanel = ratingRoot.gameObject;
            _ratingPanel.SetActive(false);

            if (GameCenter.IsAvailable)
            {
                var gc = UIFactory.MakeButton("GameCenter", root, "", new Vector2(460f, 90f), Theme.Teal, OnGameCenter, 38);
                UIFactory.Place((RectTransform)gc.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(460f, 90f));
                _gcLabel = gc.GetComponentInChildren<Text>();

                var boards = UIFactory.MakeButton("Leaderboards", root, "LEADERBOARDS", new Vector2(400f, 90f), Theme.Tape, () => _friends.SetActive(true), 36);
                UIFactory.Place((RectTransform)boards.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-540f, 40f), new Vector2(400f, 90f));
                // Phase 14: the Leaderboards hub replaces the friends-only park board.
                var board = UIFactory.Rect("LeaderboardsHub", root);
                UIFactory.Stretch(board);
                board.gameObject.AddComponent<LeaderboardsView>().Build(board, content, () => _friends.SetActive(false));
                _friends = board.gameObject;
                _friends.SetActive(false);
            }
            Refresh();
        }

        private void OnEnable()
        {
            if (_content != null) Refresh();
        }

        private void Update()
        {
            if (_gcLabel != null) _gcLabel.text = GameCenter.IsAuthenticated ? "GAME CENTER" : "SIGN IN TO GAME CENTER";
        }

        private void OnGameCenter()
        {
            if (GameCenter.IsAuthenticated)
            {
                ProgressService.SyncGameCenter(_content);
                GameCenter.ShowDashboard();
            }
            else GameCenter.Authenticate();
        }

        private Text _ratingLabel;

        public void Refresh()
        {
            if (_ratingLabel != null) _ratingLabel.text = $"SK8 RATING {SaveManager.Data.ratingBest:N0}";
            var sb = new StringBuilder();
            var sb2 = new StringBuilder();
            var playable = new System.Collections.Generic.List<RetroSk8.Data.LocationDefinition>(_content.PlayableLocations());
            int firstColumn = (playable.Count + 1) / 2;
            for (int i = 0; i < playable.Count; i++)
            {
                var loc = playable[i];
                var rec = SaveManager.Data.Record(loc.id);
                var contract = _content.FindContract(loc.id);
                var col = i < firstColumn ? sb : sb2;
                col.Append($"{loc.displayName.ToUpperInvariant(),-20} {rec.bestScore,9:N0}");
                if (contract != null) col.Append($" · {SaveManager.ContractStars(loc.id)}/{contract.goals.Count}");
                if (rec.bestCombo > 0) col.Append($" · COMBO {rec.bestCombo:N0}");
                col.AppendLine();
            }
            _parks.text = sb.ToString().TrimEnd();
            _parks2.text = sb2.ToString().TrimEnd();
            sb.Clear();
            var city = SaveManager.Data.city;
            int golds = 0, medals = 0;
            foreach (var e in city.challenges) { if (e.medal > 0) medals++; if (e.medal == (int)Medal.Gold) golds++; }
            foreach (var e in city.races) { if (e.medal > 0) medals++; if (e.medal == (int)Medal.Gold) golds++; }
            int events = RetroCityLayout.Spots.Count + RetroCityLayout.Races.Count;
            sb.AppendLine($"RETRO CITY  SPOTS {city.spots.Count}/{RetroCityLayout.Spots.Count}   TAPES {city.tapes.Count}/{RetroCityLayout.Tapes.Count}   MEDALS {medals}/{events}   GOLD {golds}/{events}");
            sb.Append($"DAILY LINES {SaveManager.Data.daily.clears}   ·   NIGHT LINES {SaveManager.Data.nightLines.Count}/{RetroSk8.Core.NightSessions.ParkCount}   ·   GAPS FOUND {SaveManager.Data.stats.gapIds.Count}   ·   TAPE TOKENS {SaveManager.Data.tapeTokens}");
            _cityLine.text = sb.ToString();

            var progress = ProgressService.Snapshot(_content);
            var left = new StringBuilder();
            var right = new StringBuilder();
            int half = (Achievements.All.Count + 1) / 2;
            for (int i = 0; i < Achievements.All.Count; i++)
            {
                var a = Achievements.All[i];
                bool done = SaveManager.Data.achievements.Contains(a.Id);
                string mark = done ? "■" : $"{Mathf.FloorToInt(a.Progress(progress) * 100f),3}%";
                string line = $"{mark}  {a.Title.ToUpperInvariant()} — {a.Description}";
                (i < half ? left : right).AppendLine(line);
            }
            _achLeft.text = left.ToString();
            _achRight.text = right.ToString();
        }
    }
}
