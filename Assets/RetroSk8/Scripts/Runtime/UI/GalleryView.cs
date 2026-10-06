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
    /// CODES → GALLERY (Phase 16): the newest parks and ghosts other players posted. GET adds a park to Create-a-Park or
    /// races a ghost; each post has REPORT, HIDE and BLOCK. POST shares one of your parks or your last run's ghost.
    /// Player-typed names are filtered, and nothing you hid or blocked comes back.
    /// </summary>
    public sealed class GalleryView : MonoBehaviour
    {
        private const int Rows = 8;

        private ContentRegistry _content;
        private GalleryKind _kind = GalleryKind.Park;
        private List<GalleryEntry> _entries = new List<GalleryEntry>();
        private int _page;
        private Action _pending;            // what to do when the current request finishes
        private Action<string> _failed;
        private GalleryService.Status _last = GalleryService.Status.Idle;

        private (Image bg, Text label) _parksTab, _ghostsTab;
        private Text _status, _pageLabel, _postLabel;
        private readonly List<(GameObject root, Text title, Text sub, Button get, Text getLabel, Button report, Button hide, Button block)> _rows =
            new List<(GameObject, Text, Text, Button, Text, Button, Button, Button)>();
        private GameObject _reportPanel;
        private GalleryEntry _reporting;
        private int _postSlot;

        public void Build(RectTransform root, ContentRegistry content, Action onClose)
        {
            _content = content;
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.98f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, GalleryService.IsTest ? "GALLERY (TEST)" : "GALLERY", 60, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -24f), new Vector2(520f, 96f));

            var parks = UIFactory.MakeButton("Parks", root, "PARKS", new Vector2(220f, 76f), Theme.InkSoft, () => SetKind(GalleryKind.Park), 32);
            UIFactory.Place((RectTransform)parks.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(620f, -34f), new Vector2(220f, 76f));
            _parksTab = (parks.GetComponent<Image>(), parks.GetComponentInChildren<Text>());
            var ghosts = UIFactory.MakeButton("Ghosts", root, "GHOSTS", new Vector2(220f, 76f), Theme.InkSoft, () => SetKind(GalleryKind.Ghost), 32);
            UIFactory.Place((RectTransform)ghosts.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(860f, -34f), new Vector2(220f, 76f));
            _ghostsTab = (ghosts.GetComponent<Image>(), ghosts.GetComponentInChildren<Text>());
            var refresh = UIFactory.MakeButton("Refresh", root, "REFRESH", new Vector2(220f, 76f), Theme.Cream, Load, 30);
            UIFactory.Place((RectTransform)refresh.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1100f, -34f), new Vector2(220f, 76f));

            // Post (top right): cycles through what you can post, then posts it.
            var post = UIFactory.MakeButton("Post", root, "", new Vector2(640f, 76f), Theme.Tape, Post, 28);
            UIFactory.Place((RectTransform)post.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-200f, -34f), new Vector2(640f, 76f));
            _postLabel = post.GetComponentInChildren<Text>();
            var next = UIFactory.MakeButton("PostNext", root, ">", new Vector2(120f, 76f), Theme.Cream, () => { _postSlot++; RefreshPost(); }, 40);
            UIFactory.Place((RectTransform)next.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -34f), new Vector2(120f, 76f));

            // Rows.
            for (int i = 0; i < Rows; i++)
            {
                float y = -150f - i * 98f;
                var bg = UIFactory.Panel("Row" + i, root, new Color(1f, 1f, 1f, i % 2 == 0 ? 0.06f : 0.03f));
                UIFactory.Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(2100f, 90f));
                var t = UIFactory.Label("Title", bg.transform, "", 34, Theme.Cream, TextAnchor.MiddleLeft);
                UIFactory.Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 12f), new Vector2(900f, 46f));
                var sub = UIFactory.Label("Sub", bg.transform, "", 22, Theme.Teal, TextAnchor.MiddleLeft, false);
                UIFactory.Place(sub.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, -24f), new Vector2(1100f, 30f));
                int row = i;
                var get = UIFactory.MakeButton("Get", bg.transform, "GET", new Vector2(300f, 70f), Theme.Tape, () => Get(row), 30);
                UIFactory.Place((RectTransform)get.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-520f, 0f), new Vector2(300f, 70f));
                var rep = UIFactory.MakeButton("Report", bg.transform, "REPORT", new Vector2(160f, 70f), Theme.Coral, () => AskReport(row), 24);
                UIFactory.Place((RectTransform)rep.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-340f, 0f), new Vector2(160f, 70f));
                var hide = UIFactory.MakeButton("Hide", bg.transform, "HIDE", new Vector2(140f, 70f), Theme.Cream, () => HideRow(row), 24);
                UIFactory.Place((RectTransform)hide.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-180f, 0f), new Vector2(140f, 70f));
                var block = UIFactory.MakeButton("Block", bg.transform, "BLOCK", new Vector2(140f, 70f), Theme.Cream, () => BlockRow(row), 24);
                UIFactory.Place((RectTransform)block.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(140f, 70f));
                _rows.Add((bg.gameObject, t, sub, get, get.GetComponentInChildren<Text>(), rep, hide, block));
            }

            _status = UIFactory.Label("Status", root, "", 30, Theme.Cream, TextAnchor.MiddleCenter);
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1600f, 60f));
            var prev = UIFactory.MakeButton("Prev", root, "<", new Vector2(110f, 80f), Theme.Cream, () => Flip(-1), 44);
            UIFactory.Place((RectTransform)prev.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-180f, 40f), new Vector2(110f, 80f));
            var nextPage = UIFactory.MakeButton("Next", root, ">", new Vector2(110f, 80f), Theme.Cream, () => Flip(1), 44);
            UIFactory.Place((RectTransform)nextPage.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(180f, 40f), new Vector2(110f, 80f));
            _pageLabel = UIFactory.Label("Page", root, "", 28, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Place(_pageLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(240f, 80f));

            var rules = UIFactory.Label("Rules", root, "BE COOL: REPORT ANYTHING NASTY · HIDE OR BLOCK WHAT YOU DON'T WANT TO SEE · POSTS SHOW YOUR CODES NAME", 22, Theme.Cream, TextAnchor.MiddleRight);
            UIFactory.Place(rules.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 60f), new Vector2(1000f, 40f));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => onClose?.Invoke(), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 30f), new Vector2(300f, 90f));

            BuildReportPanel(root);
        }

        private void BuildReportPanel(RectTransform root)
        {
            var panel = UIFactory.Panel("ReportPanel", root, new Color(0f, 0f, 0f, 0.8f), true);
            UIFactory.Stretch(panel.rectTransform);
            _reportPanel = panel.gameObject;
            var card = UIFactory.Panel("Card", panel.transform, Theme.Ink, true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 640f));
            var head = UIFactory.Label("Head", card.transform, "WHAT'S WRONG WITH IT?", 40, Theme.Tape, TextAnchor.UpperCenter);
            UIFactory.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(860f, 60f));
            for (int i = 0; i < Gallery.ReportReasons.Length; i++)
            {
                string reason = Gallery.ReportReasons[i];
                var b = UIFactory.MakeButton("Reason" + i, card.transform, reason, new Vector2(640f, 84f), Theme.Cream, () => SendReport(reason), 32);
                UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f - i * 100f), new Vector2(640f, 84f));
            }
            var cancel = UIFactory.MakeButton("Cancel", card.transform, "CANCEL", new Vector2(300f, 80f), Theme.Coral, () => _reportPanel.SetActive(false), 32);
            UIFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 80f));
            _reportPanel.SetActive(false);
        }

        private void OnEnable()
        {
            if (_status == null) return;
            _postSlot = 0;
            RefreshPost();
            Load();
        }

        private void SetKind(GalleryKind kind)
        {
            _kind = kind;
            _page = 0;
            _postSlot = 0;
            RefreshPost();
            Load();
        }

        // ---------------------------------------------------------------- requests

        private bool Busy => _pending != null;

        private void Start(Action begin, Action done, Action<string> failed)
        {
            if (Busy) return;
            if (!GalleryService.IsAvailable)
            {
                _status.text = "THE ONLINE GALLERY ISN'T IN THIS BUILD YET.";
                return;
            }
            _pending = done;
            _failed = failed;
            _last = GalleryService.Status.Busy;
            begin();
            Update(); // the editor's test gallery answers at once
        }

        private void Update()
        {
            if (_pending == null) return;
            var state = GalleryService.State;
            if (state == _last && state == GalleryService.Status.Busy) return;
            _last = state;
            if (state == GalleryService.Status.Done)
            {
                var done = _pending;
                _pending = null;
                done();
            }
            else if (state == GalleryService.Status.Failed)
            {
                var failed = _failed;
                _pending = null;
                failed?.Invoke(GalleryService.Result);
            }
        }

        private void Load()
        {
            Tint(_parksTab, _kind == GalleryKind.Park);
            Tint(_ghostsTab, _kind == GalleryKind.Ghost);
            _status.text = "LOADING...";
            ShowRows();
            Start(() => GalleryService.Query(_kind), () =>
            {
                _entries = Gallery.Visible(Gallery.Parse(GalleryService.Result), GalleryService.Saved);
                _status.text = _entries.Count == 0 ? (_kind == GalleryKind.Park ? "NO PARKS YET. POST YOURS!" : "NO GHOSTS YET. POST YOUR BEST RUN!") : "";
                ShowRows();
            }, error => { _status.text = error; _entries.Clear(); ShowRows(); });
        }

        private void ShowRows()
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(_entries.Count / (float)Rows));
            _page = Mathf.Clamp(_page, 0, pages - 1);
            _pageLabel.text = $"{_page + 1} / {pages}";
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            for (int i = 0; i < Rows; i++)
            {
                var r = _rows[i];
                int index = _page * Rows + i;
                bool show = index < _entries.Count;
                r.root.SetActive(show);
                if (!show) continue;
                var e = _entries[index];
                bool mine = GalleryService.IsMine(e);
                r.title.text = Gallery.DisplayName(e) + (mine ? "  (YOURS)" : "");
                string what = e.Kind == GalleryKind.Park
                    ? $"{e.Detail} PIECES"
                    : $"{e.Detail:N0} AT {WhereName(e)}";
                r.sub.text = $"BY {Gallery.DisplayAuthor(e)} · {what} · {Gallery.Age(e.Created, now)}";
                r.getLabel.text = e.Kind == GalleryKind.Park ? "ADD TO MY PARKS" : "RACE IT";
                r.report.gameObject.SetActive(!mine);
                r.block.gameObject.SetActive(!mine);
                r.hide.GetComponentInChildren<Text>().text = mine ? "DELETE" : "HIDE";
            }
        }

        private string WhereName(GalleryEntry e)
        {
            if (string.IsNullOrEmpty(e.Location)) return "THEIR PARK";
            var loc = _content.FindLocation(e.Location);
            return loc != null ? loc.displayName.ToUpperInvariant() : "A PARK";
        }

        private GalleryEntry Row(int row)
        {
            int index = _page * Rows + row;
            return index < _entries.Count ? _entries[index] : null;
        }

        private void Flip(int dir)
        {
            _page += dir;
            ShowRows();
        }

        // ---------------------------------------------------------------- actions

        private void Get(int row)
        {
            var e = Row(row);
            if (e == null) return;
            _status.text = "DOWNLOADING...";
            Start(() => GalleryService.FetchCode(e.Id), () =>
            {
                string code = GalleryService.Result;
                if (e.Kind == GalleryKind.Park)
                {
                    if (!ShareCodes.TryDecodePark(code, out var park, out var error)) { _status.text = error; return; }
                    park.name = Gallery.SafeName(park.name, CustomPark.MaxNameLength, "GALLERY PARK");
                    string id = ShareService.ImportPark(park);
                    _status.text = id == null ? "ALL SIX PARK SLOTS ARE FULL. DELETE ONE IN CREATE-A-PARK FIRST."
                        : $"ADDED TO CREATE-A-PARK SLOT {id.Substring(CustomParkIds.Prefix.Length)}.";
                }
                else
                {
                    if (!GhostCodes.TryDecode(code, out var c, out var error)) { _status.text = error; return; }
                    c.From = Gallery.SafeName(c.From, ShareCodes.MaxFromLength, "SKATER");
                    // Phase 19: a downloaded ghost is someone else's claim; keep its target believable and its park name clean.
                    c.Target = Math.Min(c.Target, ScoreLimits.MaxRunScore);
                    if (c.Park != null) c.Park.name = Gallery.SafeName(c.Park.name, CustomPark.MaxNameLength, "GALLERY PARK");
                    ShareService.StartChallenge(c, _content);
                }
            }, error => _status.text = error);
        }

        private void AskReport(int row)
        {
            _reporting = Row(row);
            if (_reporting != null) _reportPanel.SetActive(true);
        }

        private void SendReport(string reason)
        {
            _reportPanel.SetActive(false);
            var e = _reporting;
            if (e == null) return;
            Start(() => GalleryService.Report(e, reason), () =>
            {
                _status.text = "THANKS. WE'LL TAKE A LOOK, AND IT'S HIDDEN FOR YOU.";
                _entries.Remove(e);
                ShowRows();
            }, error => { _status.text = "HIDDEN FOR YOU (THE REPORT DIDN'T SEND: " + error + ")"; _entries.Remove(e); ShowRows(); });
        }

        private void HideRow(int row)
        {
            var e = Row(row);
            if (e == null) return;
            if (GalleryService.IsMine(e))
            {
                Start(() => GalleryService.Delete(e.Id), () => { _status.text = "DELETED"; _entries.Remove(e); ShowRows(); }, error => _status.text = error);
                return;
            }
            GalleryService.Hide(e);
            _entries.Remove(e);
            _status.text = "HIDDEN";
            ShowRows();
        }

        private void BlockRow(int row)
        {
            var e = Row(row);
            if (e == null) return;
            GalleryService.BlockAuthor(e);
            _entries = Gallery.Visible(_entries, GalleryService.Saved);
            _status.text = $"YOU WON'T SEE POSTS BY {Gallery.DisplayAuthor(e)} AGAIN";
            ShowRows();
        }

        // ---------------------------------------------------------------- posting

        private List<CustomPark> MyParks()
        {
            var list = new List<CustomPark>();
            foreach (var p in SaveManager.Data.customParks)
                if (p != null && p.id != ShareService.SharedParkId && p.pieces.Count > 0) list.Add(p);
            return list;
        }

        private void RefreshPost()
        {
            if (_postLabel == null) return;
            if (_kind == GalleryKind.Park)
            {
                var parks = MyParks();
                if (parks.Count == 0) { _postLabel.text = "BUILD A PARK TO POST IT"; return; }
                var p = parks[((_postSlot % parks.Count) + parks.Count) % parks.Count];
                _postLabel.text = $"POST: {p.name}";
            }
            else
            {
                var r = GameSession.LastResult;
                bool ghost = r != null && GameSession.LastRunTrack != null && GameSession.LastRunTrack.LocationId == r.locationId;
                _postLabel.text = ghost ? $"POST LAST RUN: {r.score:N0}" : "SKATE A TWO-MINUTE RUN FIRST";
            }
        }

        private void Post()
        {
            string error;
            if (_kind == GalleryKind.Park)
            {
                var parks = MyParks();
                if (parks.Count == 0) { _status.text = "BUILD A PARK IN CREATE-A-PARK FIRST"; return; }
                var p = parks[((_postSlot % parks.Count) + parks.Count) % parks.Count];
                error = Upload(GalleryKind.Park, p.name, p.pieces.Count, "", ShareCodes.EncodePark(p));
            }
            else
            {
                var r = GameSession.LastResult;
                string code = r != null ? ShareService.GhostCode(r.locationId, r.score) : null;
                if (code == null) { _status.text = "NO GHOST FROM YOUR LAST RUN. SKATE A TWO-MINUTE RUN FIRST."; return; }
                var loc = _content.FindLocation(r.locationId);
                string where = CustomParkIds.IsCustom(r.locationId) ? "" : r.locationId;
                string name = CustomParkIds.IsCustom(r.locationId) ? (SaveManager.FindCustomPark(r.locationId)?.name ?? "MY PARK") : (loc != null ? loc.displayName : "A RUN");
                error = Upload(GalleryKind.Ghost, name, r.score, where, code);
            }
            if (error != null) _status.text = error;
        }

        private string Upload(GalleryKind kind, string name, long detail, string location, string code)
        {
            string error = GalleryService.CheckUpload(name, code);
            if (error != null) return error;
            _status.text = "POSTING...";
            Start(() => GalleryService.Upload(kind, name, detail, location, code), () =>
            {
                GalleryService.UploadDone(GalleryService.Result, code);
                _status.text = "POSTED! IT'S AT THE TOP OF THE LIST.";
                Load();
            }, e => _status.text = e);
            return null;
        }

        private static void Tint((Image bg, Text label) tab, bool on)
        {
            tab.bg.color = on ? Theme.Tape : Theme.InkSoft;
            tab.label.color = on ? Theme.Ink : Theme.Cream;
        }
    }
}
