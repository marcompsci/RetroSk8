using System;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Duel;
using RetroSk8.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Main menu → S.K.A.T.E.: play a live opponent through Game Center, or the CPU on a park you pick.</summary>
    public sealed class DuelPanelView : MonoBehaviour
    {
        private ContentRegistry _content;
        private Text _onlineInfo;
        private Button _online;
        private Text _onlineLabel;
        private Text _park;
        private int _parkIndex;
        private DuelBot.Level _level = DuelBot.Level.Medium;
        private readonly Image[] _levels = new Image[3];
        private bool _searching;

        public void Build(RectTransform root, ContentRegistry content, Action onClose)
        {
            _content = content;
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.96f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "GAME OF S.K.A.T.E.", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(760f, 100f));
            var rules = UIFactory.Label("Rules", root,
                "TAKE TURNS. THE SETTER BANKS ANY LINE; THE OTHER SKATER MUST BANK 80% OF IT OR TAKE A LETTER.\nMISS YOUR SET AND THE SET PASSES OVER. SPELL S-K-A-T-E AND YOU LOSE.",
                28, Theme.Cream, TextAnchor.UpperCenter, false);
            UIFactory.Place(rules.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1900f, 90f));

            // Online card.
            var online = Card(root, "Online", new Vector2(-480f, -260f), "LIVE ONLINE");
            _onlineInfo = UIFactory.Label("Info", online, "", 28, Theme.Cream, TextAnchor.UpperLeft, false);
            _onlineInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_onlineInfo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -90f), new Vector2(820f, 300f));
            _online = UIFactory.MakeButton("Find", online, "", new Vector2(420f, 110f), Theme.Tape, ToggleSearch, 40);
            UIFactory.Place((RectTransform)_online.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(420f, 110f));
            _onlineLabel = _online.GetComponentInChildren<Text>();

            // CPU card.
            var cpu = Card(root, "Cpu", new Vector2(480f, -260f), "VS CPU");
            var levels = UIFactory.Rect("Levels", cpu);
            UIFactory.Place(levels, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(820f, 90f));
            var h = levels.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            for (int i = 0; i < 3; i++)
            {
                var lvl = (DuelBot.Level)i;
                _levels[i] = UIFactory.MakeButton("Level" + i, levels, lvl.ToString().ToUpperInvariant(), new Vector2(250f, 84f), Theme.Cream, () => { _level = lvl; Refresh(); }, 34).GetComponent<Image>();
            }
            var parkRow = UIFactory.Rect("Park", cpu);
            UIFactory.Place(parkRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(820f, 90f));
            var prev = UIFactory.MakeButton("Prev", parkRow, "<", new Vector2(100f, 84f), Theme.Cream, () => StepPark(-1), 44);
            UIFactory.Place((RectTransform)prev.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(100f, 84f));
            _park = UIFactory.Label("ParkName", parkRow, "", 34, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Place(_park.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 84f));
            var next = UIFactory.MakeButton("Next", parkRow, ">", new Vector2(100f, 84f), Theme.Cream, () => StepPark(1), 44);
            UIFactory.Place((RectTransform)next.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(100f, 84f));
            var go = UIFactory.MakeButton("StartCpu", cpu, "PLAY CPU", new Vector2(420f, 110f), Theme.Tape, StartCpu, 40);
            UIFactory.Place((RectTransform)go.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(420f, 110f));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () =>
            {
                CancelSearch();
                onClose?.Invoke();
            }, 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(300f, 90f));
            Refresh();
        }

        private static RectTransform Card(RectTransform root, string name, Vector2 pos, string title)
        {
            var card = UIFactory.Panel(name, root, Theme.InkSoft);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(880f, 560f));
            var t = UIFactory.Label("Title", card.transform, title, 44, Theme.Teal, TextAnchor.UpperCenter);
            UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(820f, 60f));
            return card.rectTransform;
        }

        private void OnEnable()
        {
            if (_park != null) Refresh();
        }

        private void OnDisable() => CancelSearch();

        private void StepPark(int dir)
        {
            int n = DuelSession.Parks.Length;
            _parkIndex = ((_parkIndex + dir) % n + n) % n;
            Refresh();
        }

        private void StartCpu()
        {
            CancelSearch();
            var s = DuelSession.StartCpu(_level, _parkIndex, Environment.TickCount);
            s.Go();
        }

        private void ToggleSearch()
        {
            if (_searching) { CancelSearch(); Refresh(); return; }
            if (!GameCenterDuelTransport.Supported) { Refresh(); return; }
            DuelSession.StartOnline();
            _searching = true;
            Refresh();
        }

        private void CancelSearch()
        {
            if (!_searching) return;
            _searching = false;
            // A match that already started loads the park; only an unfinished search is cancelled here.
            if (DuelSession.Current != null && DuelSession.Current.State != DuelSession.Stage.Playing) DuelSession.End();
        }

        private void Update()
        {
            if (!_searching) return;
            var s = DuelSession.Current;
            if (s == null || s.State == DuelSession.Stage.Ended)
            {
                _searching = false;
                _onlineInfo.text = s != null && !string.IsNullOrEmpty(s.Status) ? s.Status : "SEARCH ENDED";
                if (s != null) DuelSession.End();
                _onlineLabel.text = "FIND A SKATER";
                return;
            }
            _onlineInfo.text = s.Status;
        }

        private void Refresh()
        {
            for (int i = 0; i < _levels.Length; i++) _levels[i].color = (int)_level == i ? Theme.Tape : Theme.Cream;
            var loc = _content.FindLocation(DuelSession.Parks[_parkIndex]);
            _park.text = loc != null ? loc.displayName.ToUpperInvariant() : DuelSession.Parks[_parkIndex];

            bool supported = GameCenterDuelTransport.Supported;
            _online.interactable = supported || _searching;
            _onlineLabel.text = _searching ? "CANCEL" : "FIND A SKATER";
            if (!_searching)
                _onlineInfo.text = supported
                    ? "PLAY SOMEONE LIVE THROUGH GAME CENTER: INVITE A FRIEND OR GET MATCHED. YOU'LL WATCH THEIR TRIES AS A GLOWING GHOST."
                    : "LIVE PLAY USES GAME CENTER ON IPHONE. IT NEEDS A BUILD WITH GAME CENTER ON (RETRO SK8 > BUILD IOS > ENABLE GAME CENTER), A PAID APPLE DEVELOPER ACCOUNT, AND YOU SIGNED IN. UNTIL THEN, PLAY THE CPU.";
        }
    }
}
