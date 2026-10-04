using System;
using System.Collections.Generic;
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
    /// Records → FRIENDS: your Game Center friends' best Two-Minute Run on each park (friends-only leaderboard scope),
    /// with your own rank. Flip parks with the arrows. Only built when Game Center is in the build.
    /// </summary>
    public sealed class FriendsBoardView : MonoBehaviour
    {
        private readonly List<LocationDefinition> _parks = new List<LocationDefinition>();
        private int _index;
        private Text _park;
        private Text _list;
        private int _lastState = -1;

        public void Build(RectTransform root, ContentRegistry content, Action onClose)
        {
            foreach (var loc in content.PlayableLocations()) _parks.Add(loc);
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.97f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "FRIENDS", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(420f, 100f));

            var prev = UIFactory.MakeButton("Prev", root, "<", new Vector2(110f, 90f), Theme.Cream, () => Flip(-1), 54);
            UIFactory.Place((RectTransform)prev.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-520f, -160f), new Vector2(110f, 90f));
            var next = UIFactory.MakeButton("Next", root, ">", new Vector2(110f, 90f), Theme.Cream, () => Flip(1), 54);
            UIFactory.Place((RectTransform)next.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(520f, -160f), new Vector2(110f, 90f));
            _park = UIFactory.Label("Park", root, "", 44, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Place(_park.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(900f, 90f));

            _list = UIFactory.Label("List", root, "", 34, Theme.Cream, TextAnchor.UpperLeft, false);
            _list.lineSpacing = 1.15f;
            UIFactory.Place(_list.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(1100f, 640f));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(300f, 90f));
        }

        private void OnEnable()
        {
            if (_park != null) Load();
        }

        private void Flip(int dir)
        {
            if (_parks.Count == 0) return;
            _index = (_index + dir + _parks.Count) % _parks.Count;
            Load();
        }

        private void Load()
        {
            if (_parks.Count == 0) return;
            var loc = _parks[_index];
            _park.text = loc.displayName.ToUpperInvariant();
            _lastState = -1;
            if (!GameCenter.IsAuthenticated)
            {
                _list.text = "SIGN IN TO GAME CENTER TO SEE YOUR FRIENDS' SCORES.";
                return;
            }
            _list.text = "LOADING...";
            GameCenter.LoadFriendScores(Achievements.LeaderboardId(loc.id));
        }

        private void Update()
        {
            if (!GameCenter.IsAuthenticated) return;
            int state = GameCenter.FriendScoresState;
            if (state == _lastState) return;
            _lastState = state;
            if (state == 3) _list.text = "COULDN'T LOAD SCORES. CHECK YOUR CONNECTION AND TRY AGAIN.";
            else if (state == 2) Show(GameCenter.FriendScores());
        }

        private void Show(List<FriendScore> rows)
        {
            var mine = _parks.Count > 0 ? SaveManager.Data.Record(_parks[_index].id).bestScore : 0;
            if (rows.Count == 0)
            {
                _list.text = $"NO FRIENDS' SCORES HERE YET.\nYOUR BEST: {mine:N0}\n\nSEND A GHOST FROM RESULTS TO GET THEM STARTED.";
                return;
            }
            var sb = new StringBuilder();
            foreach (var r in rows)
                sb.Append(r.IsYou ? "> " : "   ").Append(r.Rank.ToString().PadLeft(2)).Append(".  ")
                  .Append(r.Name.Length > 18 ? r.Name.Substring(0, 18) : r.Name).Append("   ")
                  .Append(r.Score.ToString("N0")).Append('\n');
            _list.text = sb.ToString().TrimEnd();
        }
    }
}
