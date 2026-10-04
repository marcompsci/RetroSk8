using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Level;
using RetroSk8.Replay;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Main menu → REPLAYS: your saved runs (newest first, starred ones kept forever) to watch, star or delete.</summary>
    public sealed class ReplaysPanelView : MonoBehaviour
    {
        private const int PerPage = 6;
        private RectTransform _list;
        private Text _page;
        private Text _empty;
        private int _pageIndex;

        public void Build(RectTransform root, Action onClose)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.96f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "REPLAYS", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(380f, 100f));
            var sub = UIFactory.Label("Sub", root, $"FINISHED RUNS SAVE HERE AUTOMATICALLY (NEWEST {ReplayIndex.MaxRecent} KEPT) · STAR ONE TO KEEP IT · PAUSE → SAVE REPLAY IN FREE SKATE", 24, Theme.Cream, TextAnchor.UpperCenter);
            UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(1900f, 40f));

            _list = UIFactory.Rect("List", root);
            UIFactory.Place(_list, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1800f, 700f));
            var v = _list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 12f;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = v.childControlHeight = false;
            _empty = UIFactory.Label("Empty", root, "NO REPLAYS YET. FINISH A TWO-MINUTE RUN AND IT SHOWS UP HERE.", 34, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Place(_empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 60f));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 30f), new Vector2(300f, 90f));
            var prev = UIFactory.MakeButton("Prev", root, "<", new Vector2(110f, 90f), Theme.Cream, () => { _pageIndex--; Refresh(); }, 44);
            UIFactory.Place((RectTransform)prev.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, 30f), new Vector2(110f, 90f));
            _page = UIFactory.Label("Page", root, "", 30, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Place(_page.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-180f, 30f), new Vector2(140f, 90f));
            var next = UIFactory.MakeButton("Next", root, ">", new Vector2(110f, 90f), Theme.Cream, () => { _pageIndex++; Refresh(); }, 44);
            UIFactory.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 30f), new Vector2(110f, 90f));
            Refresh();
        }

        private void OnEnable()
        {
            if (_list != null) Refresh();
        }

        private void Refresh()
        {
            foreach (Transform child in _list) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var entries = ReplayLibrary.Index.entries;
            int pages = Mathf.Max(1, (entries.Count + PerPage - 1) / PerPage);
            _pageIndex = Mathf.Clamp(_pageIndex, 0, pages - 1);
            _page.text = $"{_pageIndex + 1}/{pages}";
            _empty.gameObject.SetActive(entries.Count == 0);
            for (int i = _pageIndex * PerPage; i < Mathf.Min(entries.Count, (_pageIndex + 1) * PerPage); i++) Row(entries[i]);
        }

        private void Row(ReplayEntry e)
        {
            var row = UIFactory.Panel("Row_" + e.id, _list, Theme.InkSoft);
            row.rectTransform.sizeDelta = new Vector2(1800f, 104f);
            var when = new DateTime(e.savedTicks);
            var best = e.BestMoment();
            var title = UIFactory.Label("Title", row.transform, $"{(e.favorite ? "* " : "")}{(e.parkName ?? e.locationId).ToUpperInvariant()} · {e.mode} · {e.score:N0}", 34, Theme.Tape, TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 16f), new Vector2(1000f, 46f));
            var info = UIFactory.Label("Info", row.transform, $"{when:MMM d, HH:mm} · {e.duration:0}s" + (best != null ? $" · BEST LINE {best.points:N0}" : ""), 24, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(info.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, -26f), new Vector2(1000f, 34f));

            var buttons = UIFactory.Rect("Buttons", row.transform);
            UIFactory.Place(buttons, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(720f, 84f));
            var h = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleRight;
            h.childControlWidth = h.childControlHeight = false;
            string id = e.id, park = e.locationId;
            UIFactory.MakeButton("Watch", buttons, "WATCH", new Vector2(240f, 80f), Theme.Tape, () => Watch(id, park), 34);
            UIFactory.MakeButton("Star", buttons, e.favorite ? "UNSTAR" : "STAR", new Vector2(200f, 80f), Theme.Teal, () => { ReplayLibrary.ToggleFavorite(id); Refresh(); }, 30);
            UIFactory.MakeButton("Delete", buttons, "DELETE", new Vector2(200f, 80f), Theme.Coral, () => { ReplayLibrary.Delete(id); Refresh(); }, 30);
        }

        /// <summary>Opens a replay in its park's scene.</summary>
        public static void Watch(string replayId, string locationId)
        {
            GameSession.Mode = RunMode.Replay;
            GameSession.ReplayId = replayId;
            GameSession.EditPark = false;
            GameSession.Challenge = null;
            SceneRouter.LoadPark(locationId, ParkCatalog.SceneFor(locationId));
        }
    }
}
