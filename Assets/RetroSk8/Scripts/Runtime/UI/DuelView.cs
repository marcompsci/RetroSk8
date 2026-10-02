using RetroSk8.Core;
using RetroSk8.Duel;
using RetroSk8.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>S.K.A.T.E. screen: both skaters' letters, whose turn it is, the target, the countdown, and the final result.</summary>
    public sealed class DuelView : MonoBehaviour
    {
        private DuelController _duel;
        private Text _banner;
        private Text _left, _right;
        private Text _big;
        private Text _timer;
        private GameObject _final;
        private Text _finalTitle, _finalBody;
        private Button _rematch;
        private Text _rematchLabel;

        public void Build(RectTransform safe, DuelController duel)
        {
            _duel = duel;
            var bar = UIFactory.Panel("DuelBar", safe, new Color(0.07f, 0.075f, 0.09f, 0.85f));
            UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1400f, 70f));
            _banner = UIFactory.Label("Banner", bar.transform, "", 36, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_banner.rectTransform);

            _left = Letters(safe, new Vector2(0f, 1f), new Vector2(40f, -240f), TextAnchor.UpperLeft);
            _right = Letters(safe, new Vector2(1f, 1f), new Vector2(-40f, -240f), TextAnchor.UpperRight);

            _big = UIFactory.Label("Big", safe, "", 180, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Place(_big.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 220f));
            _timer = UIFactory.Label("Timer", safe, "", 44, Theme.Cream, TextAnchor.UpperCenter);
            UIFactory.Place(_timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(600f, 60f));

            _final = UIFactory.Panel("Final", safe, new Color(0.07f, 0.075f, 0.09f, 0.93f), true).gameObject;
            UIFactory.Stretch((RectTransform)_final.transform);
            _finalTitle = UIFactory.Label("Title", _final.transform, "", 90, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Place(_finalTitle.rectTransform, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800f, 140f));
            _finalBody = UIFactory.Label("Body", _final.transform, "", 40, Theme.Cream, TextAnchor.UpperCenter, false);
            _finalBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_finalBody.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1600f, 240f));
            _rematch = UIFactory.MakeButton("Rematch", _final.transform, "REMATCH", new Vector2(460f, 120f), Theme.Tape, () => DuelSession.Current?.RequestRematch(), 50);
            UIFactory.Place((RectTransform)_rematch.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 60f), new Vector2(460f, 120f));
            _rematchLabel = _rematch.GetComponentInChildren<Text>();
            var home = UIFactory.MakeButton("Home", _final.transform, "MENU", new Vector2(460f, 120f), Theme.Cream, () =>
            {
                DuelSession.End();
                if (Application.CanStreamedLevelBeLoaded(SceneNames.MainMenu)) SceneManager.LoadScene(SceneNames.MainMenu);
            }, 50);
            UIFactory.Place((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(20f, 60f), new Vector2(460f, 120f));
            _final.SetActive(false);

            duel.Changed += Refresh;
            Refresh();
        }

        private static Text Letters(RectTransform safe, Vector2 anchor, Vector2 pos, TextAnchor align)
        {
            var t = UIFactory.Label("Letters", safe, "", 40, Theme.Cream, align);
            UIFactory.Place(t.rectTransform, anchor, anchor, pos, new Vector2(620f, 120f));
            t.lineSpacing = 1.1f;
            return t;
        }

        private static string Spell(int letters)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < SkateDuel.Word.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(i < letters ? SkateDuel.Word[i] : '_');
            }
            return sb.ToString();
        }

        private void Refresh()
        {
            var s = DuelSession.Current;
            if (s == null || s.Duel == null)
            {
                _banner.text = "CONNECTION LOST";
                _final.SetActive(true);
                _finalTitle.text = "MATCH OVER";
                _finalBody.text = "The connection to the other skater was lost.";
                _rematch.gameObject.SetActive(false);
                return;
            }
            var d = s.Duel;
            _left.text = $"{s.MyName} (YOU)\n{Spell(d.Letters(s.Me))}";
            _right.text = $"{s.TheirName}\n{Spell(d.Letters(s.Opponent))}";
            _left.color = d.Actor == s.Me ? Theme.Tape : Theme.Cream;
            _right.color = d.Actor == s.Opponent ? Theme.Tape : Theme.Cream;

            bool over = d.Phase == DuelPhase.Finished || s.State == DuelSession.Stage.Ended;
            _final.SetActive(over);
            if (over)
            {
                bool won = d.Winner == s.Me;
                _finalTitle.text = won ? "YOU WIN!" : "YOU LOST";
                _finalTitle.color = won ? Theme.Teal : Theme.Coral;
                _finalBody.text = $"{d.LastMessage}\n\n{s.MyName}: {Spell(d.Letters(s.Me))}     {s.TheirName}: {Spell(d.Letters(s.Opponent))}";
                bool canRematch = !(s.IsOnline && s.Transport.State == LinkState.Disconnected) && string.IsNullOrEmpty(s.Status);
                _rematch.gameObject.SetActive(canRematch);
                _rematchLabel.text = s.IWantRematch ? "WAITING..." : s.OpponentWantsRematch ? "REMATCH? YES!" : "REMATCH";
                return;
            }

            string need = d.Phase == DuelPhase.Matching ? $"   ·   NEED {d.Target:N0}" : "";
            _banner.text = d.Actor == s.Me
                ? (d.Phase == DuelPhase.Setting ? "YOUR SET: BANK ANY LINE" : $"MATCH IT: BANK {d.Target:N0} OR TAKE A LETTER")
                : $"{s.TheirName} IS {(d.Phase == DuelPhase.Setting ? "SETTING" : "MATCHING")}{need}";
            if (!string.IsNullOrEmpty(d.LastMessage) && _duel.State != DuelController.LocalState.Attempting)
                _banner.text = d.LastMessage + "   ·   " + _banner.text;
        }

        private void Update()
        {
            if (_duel == null) return;
            switch (_duel.State)
            {
                case DuelController.LocalState.Countdown:
                    _big.text = Mathf.CeilToInt(_duel.Countdown).ToString();
                    _timer.text = "";
                    break;
                case DuelController.LocalState.Attempting:
                    _big.text = "";
                    _timer.text = $"{Mathf.CeilToInt(_duel.AttemptLeft)}s" + (_duel.AttemptBest > 0 ? $"   BANKED {_duel.AttemptBest:N0}" : "");
                    break;
                case DuelController.LocalState.Watching:
                    _big.text = "";
                    var s = DuelSession.Current;
                    _timer.text = s == null ? "" : _duel.WatchingLive ? "WATCHING " + s.TheirName : s.IsOnline ? "WAITING FOR " + s.TheirName + "..." : s.TheirName + " IS SKATING...";
                    break;
                default:
                    _big.text = "";
                    _timer.text = "";
                    break;
            }
        }
    }
}
