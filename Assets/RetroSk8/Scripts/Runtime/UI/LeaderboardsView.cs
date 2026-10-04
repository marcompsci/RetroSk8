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
    /// Records → LEADERBOARDS (Phase 14): every Game Center board in one screen. Pick a group (parks, races, spot
    /// challenges, events), flip through its boards, switch between everyone and just your friends, and see who to
    /// beat next. Only built when Game Center is in the build; works signed out too (shows your local bests).
    /// </summary>
    public sealed class LeaderboardsView : MonoBehaviour
    {
        private const int RowSlots = 14;

        private List<BoardInfo> _boards;
        private BoardGroup _group;
        private int _index;
        private bool _friendsOnly;
        private int _lastState = -1;

        private readonly List<(Image bg, Text label)> _groupTabs = new List<(Image, Text)>();
        private (Image bg, Text label) _globalTab, _friendsTab;
        private Text _boardTitle, _boardCount, _status, _target, _localBest;
        private readonly List<(Image bg, Text rank, Text name, Text score)> _rows = new List<(Image, Text, Text, Text)>();

        public void Build(RectTransform root, ContentRegistry content, Action onClose)
        {
            var parks = new List<(string, string)>();
            foreach (var loc in content.PlayableLocations()) parks.Add((loc.id, loc.displayName));
            _boards = LeaderboardHub.Boards(parks);

            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.97f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "LEADERBOARDS", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -24f), new Vector2(520f, 100f));

            // Group tabs (top) and scope toggle (top right).
            var tabs = UIFactory.Rect("Groups", root);
            UIFactory.Place(tabs, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(640f, -36f), new Vector2(1100f, 76f));
            var tl = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tl.spacing = 10f;
            tl.childControlWidth = tl.childControlHeight = false;
            foreach (BoardGroup g in Enum.GetValues(typeof(BoardGroup)))
            {
                var group = g;
                var b = UIFactory.MakeButton("Group_" + g, tabs, LeaderboardHub.GroupName(g), new Vector2(g == BoardGroup.Challenges ? 330f : 230f, 72f), Theme.InkSoft, () => SelectGroup(group), 28);
                _groupTabs.Add((b.GetComponent<Image>(), b.GetComponentInChildren<Text>()));
            }
            var global = UIFactory.MakeButton("Global", root, "EVERYONE", new Vector2(230f, 72f), Theme.InkSoft, () => SetScope(false), 28);
            UIFactory.Place((RectTransform)global.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-300f, -36f), new Vector2(230f, 72f));
            _globalTab = (global.GetComponent<Image>(), global.GetComponentInChildren<Text>());
            var friends = UIFactory.MakeButton("Friends", root, "FRIENDS", new Vector2(230f, 72f), Theme.InkSoft, () => SetScope(true), 28);
            UIFactory.Place((RectTransform)friends.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -36f), new Vector2(230f, 72f));
            _friendsTab = (friends.GetComponent<Image>(), friends.GetComponentInChildren<Text>());

            // Board flipper.
            var prev = UIFactory.MakeButton("Prev", root, "<", new Vector2(110f, 90f), Theme.Cream, () => Flip(-1), 54);
            UIFactory.Place((RectTransform)prev.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-600f, -150f), new Vector2(110f, 90f));
            var next = UIFactory.MakeButton("Next", root, ">", new Vector2(110f, 90f), Theme.Cream, () => Flip(1), 54);
            UIFactory.Place((RectTransform)next.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(600f, -150f), new Vector2(110f, 90f));
            _boardTitle = UIFactory.Label("Board", root, "", 46, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Place(_boardTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1000f, 64f));
            _boardCount = UIFactory.Label("Count", root, "", 24, Theme.Cream, TextAnchor.MiddleCenter, false);
            UIFactory.Place(_boardCount.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1000f, 36f));

            // Rows.
            var list = UIFactory.Rect("Rows", root);
            UIFactory.Place(list, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -256f), new Vector2(1300f, RowSlots * 44f));
            for (int i = 0; i < RowSlots; i++)
            {
                var bg = UIFactory.Panel("Row" + i, list, new Color(1f, 1f, 1f, i % 2 == 0 ? 0.05f : 0.02f));
                UIFactory.Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -i * 44f), new Vector2(1300f, 42f));
                var rank = UIFactory.Label("Rank", bg.transform, "", 28, Theme.Cream, TextAnchor.MiddleRight, false);
                UIFactory.Place(rank.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(120f, 42f));
                var name = UIFactory.Label("Name", bg.transform, "", 28, Theme.Cream, TextAnchor.MiddleLeft, false);
                UIFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(170f, 0f), new Vector2(760f, 42f));
                var score = UIFactory.Label("Score", bg.transform, "", 28, Theme.Cream, TextAnchor.MiddleRight, false);
                UIFactory.Place(score.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(360f, 42f));
                _rows.Add((bg, rank, name, score));
            }
            _status = UIFactory.Label("Status", list, "", 32, Theme.Cream, TextAnchor.UpperCenter);
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1100f, 200f));

            // Target strip and your local best.
            _target = UIFactory.TapeLabel("Target", root, "", 34, Theme.Coral, 1.5f);
            UIFactory.Place(_target.transform.parent as RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(980f, 76f));
            _localBest = UIFactory.Label("Local", root, "", 26, Theme.Cream, TextAnchor.MiddleRight);
            UIFactory.Place(_localBest.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 150f), new Vector2(600f, 40f));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(300f, 90f));
            var gc = UIFactory.MakeButton("Dashboard", root, "GAME CENTER", new Vector2(340f, 90f), Theme.Teal, GameCenter.ShowDashboard, 34);
            UIFactory.Place((RectTransform)gc.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(340f, 90f));

            _group = BoardGroup.Parks;
            _friendsOnly = true; // friends first: the closest rivalries
        }

        private void OnEnable()
        {
            if (_boards != null) Load();
        }

        private List<BoardInfo> GroupBoards() => _boards.FindAll(b => b.Group == _group);

        private BoardInfo Current
        {
            get
            {
                var list = GroupBoards();
                if (list.Count == 0) return null;
                _index = (_index % list.Count + list.Count) % list.Count;
                return list[_index];
            }
        }

        private void SelectGroup(BoardGroup g)
        {
            _group = g;
            _index = 0;
            Load();
        }

        private void SetScope(bool friendsOnly)
        {
            _friendsOnly = friendsOnly;
            Load();
        }

        private void Flip(int dir)
        {
            _index += dir;
            Load();
        }

        private void Load()
        {
            for (int i = 0; i < _groupTabs.Count; i++) Tint(_groupTabs[i], i == (int)_group);
            Tint(_globalTab, !_friendsOnly);
            Tint(_friendsTab, _friendsOnly);
            var board = Current;
            var count = GroupBoards().Count;
            _boardTitle.text = board != null ? board.Title : "";
            _boardCount.text = board != null ? $"{LeaderboardHub.GroupName(_group)} · {_index + 1} OF {count} · {(_friendsOnly ? "FRIENDS" : "EVERYONE")}" : "";
            long local = LocalBest(board);
            _localBest.text = local > 0 ? "YOUR BEST HERE: " + LeaderboardHub.Format(local, board.Format) : "";
            ShowRows(null, board);
            _lastState = -1;
            if (board == null) return;
            if (!GameCenter.IsAuthenticated)
            {
                _status.text = "SIGN IN TO GAME CENTER TO SEE THE BOARDS.";
                _target.text = LeaderboardHub.TargetText(null, board, local);
                return;
            }
            _status.text = "LOADING...";
            _target.text = "...";
            GameCenter.LoadScores(board.Id, _friendsOnly, LeaderboardHub.PageSize);
        }

        private void Update()
        {
            if (_boards == null || !GameCenter.IsAuthenticated) return;
            int state = GameCenter.FriendScoresState;
            if (state == _lastState) return;
            _lastState = state;
            var board = Current;
            if (state == 3)
            {
                _status.text = "COULDN'T LOAD THIS BOARD. CHECK YOUR CONNECTION AND TRY AGAIN.";
                _target.text = LeaderboardHub.TargetText(null, board, LocalBest(board));
            }
            else if (state == 2)
            {
                var page = GameCenter.ScoresPage();
                ShowRows(page, board);
                _status.text = page.Rows.Count == 0
                    ? (_friendsOnly ? "NO FRIENDS ON THIS BOARD YET. SEND THEM A GHOST FROM RESULTS." : "NOBODY ON THIS BOARD YET. BE THE FIRST.")
                    : "";
                if (page.Total > 0) _boardCount.text += $" · {page.Total:N0} PLAYERS";
                _target.text = LeaderboardHub.TargetText(page, board, LocalBest(board));
            }
        }

        private void ShowRows(BoardPage page, BoardInfo board)
        {
            int slot = 0;
            var target = LeaderboardHub.NextTarget(page);
            if (page != null && board != null)
            {
                for (int i = 0; i < page.Rows.Count && slot < RowSlots; i++)
                {
                    if (LeaderboardHub.GapBefore(page, i) && slot < RowSlots - 1) SetRow(slot++, "", "· · ·", "", false, false);
                    var r = page.Rows[i];
                    SetRow(slot++, "#" + r.Rank, r.Name.Length > 24 ? r.Name.Substring(0, 24) : r.Name, LeaderboardHub.Format(r.Score, board.Format), r.IsYou, r == target);
                }
            }
            for (; slot < RowSlots; slot++) _rows[slot].bg.gameObject.SetActive(false);
        }

        private void SetRow(int slot, string rank, string name, string score, bool you, bool target)
        {
            var (bg, rankText, nameText, scoreText) = _rows[slot];
            bg.gameObject.SetActive(true);
            bg.color = you ? Theme.Tape : target ? new Color(Theme.Coral.r, Theme.Coral.g, Theme.Coral.b, 0.45f) : new Color(1f, 1f, 1f, slot % 2 == 0 ? 0.05f : 0.02f);
            var text = you ? Theme.Ink : Theme.Cream;
            rankText.text = rank;
            nameText.text = you ? name + "  (YOU)" : target ? name + "  · NEXT UP" : name;
            scoreText.text = score;
            rankText.color = nameText.color = scoreText.color = text;
        }

        private static long LocalBest(BoardInfo board)
        {
            if (board == null) return 0;
            var d = SaveManager.Data;
            switch (board.Group)
            {
                case BoardGroup.Parks: return d.Record(board.SourceId).bestScore;
                case BoardGroup.Races:
                    float t = d.city.RaceBest(board.SourceId);
                    return t > 0f ? Leaderboards.RaceScore(t) : 0;
                case BoardGroup.Challenges: return d.city.ChallengeBest(board.SourceId);
            }
            return board.Id == Leaderboards.SkateWins ? d.skateWins : d.weekly.Get(WeeklyCounters.BestScore);
        }

        private static void Tint((Image bg, Text label) tab, bool on)
        {
            tab.bg.color = on ? Theme.Tape : Theme.InkSoft;
            tab.label.color = on ? Theme.Ink : Theme.Cream;
        }
    }
}
