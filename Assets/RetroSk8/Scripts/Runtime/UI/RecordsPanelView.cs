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
        private Text _parks;
        private Text _achLeft;
        private Text _achRight;
        private Text _gcLabel;
        private GameObject _friends;

        public void Build(RectTransform root, ContentRegistry content, Action onClose)
        {
            _content = content;
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.94f), true);
            UIFactory.Stretch(dim.rectTransform);

            var title = UIFactory.TapeLabel("Title", root, "RECORDS", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(460f, 100f));

            _parks = UIFactory.Label("Parks", root, "", 32, Theme.Cream, TextAnchor.UpperLeft, false);
            _parks.lineSpacing = 1.2f;
            UIFactory.Place(_parks.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1800f, 290f));

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

        public void Refresh()
        {
            var sb = new StringBuilder();
            foreach (var loc in _content.PlayableLocations())
            {
                var rec = SaveManager.Data.Record(loc.id);
                var contract = _content.FindContract(loc.id);
                sb.Append($"{loc.displayName.ToUpperInvariant(),-16}  BEST {rec.bestScore,9:N0}");
                if (rec.bestCombo > 0) sb.Append($"   COMBO {rec.bestCombo:N0}  {rec.bestComboLabel}");
                if (contract != null) sb.Append($"   CONTRACT {SaveManager.ContractStars(loc.id)}/{contract.goals.Count}");
                sb.AppendLine();
            }
            var city = SaveManager.Data.city;
            int golds = 0, medals = 0;
            foreach (var e in city.challenges) { if (e.medal > 0) medals++; if (e.medal == (int)Medal.Gold) golds++; }
            foreach (var e in city.races) { if (e.medal > 0) medals++; if (e.medal == (int)Medal.Gold) golds++; }
            int events = RetroCityLayout.Spots.Count + RetroCityLayout.Races.Count;
            sb.AppendLine($"RETRO CITY  SPOTS {city.spots.Count}/{RetroCityLayout.Spots.Count}   TAPES {city.tapes.Count}/{RetroCityLayout.Tapes.Count}   MEDALS {medals}/{events}   GOLD {golds}/{events}");
            sb.Append($"DAILY LINES CLEARED {SaveManager.Data.daily.clears}   ·   GAPS FOUND {SaveManager.Data.stats.gapIds.Count}   ·   TAPE TOKENS {SaveManager.Data.tapeTokens}");
            _parks.text = sb.ToString();

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
