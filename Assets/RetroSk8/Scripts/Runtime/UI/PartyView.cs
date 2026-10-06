using System.Text;
using RetroSk8.Core;
using RetroSk8.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Pass-and-play HUD: whose turn, what to beat, letters and time, plus the hand-off and final screens.</summary>
    public sealed class PartyView : MonoBehaviour
    {
        private PartyController _party;
        private Text _banner;
        private Text _standings;
        private GameObject _handoff;
        private Text _handoffTitle;
        private Text _handoffBody;
        private GameObject _final;
        private Text _finalTitle;
        private Text _finalBody;

        public void Build(RectTransform safe, PartyController party)
        {
            _party = party;

            var bar = UIFactory.Panel("PartyBar", safe, new Color(0.07f, 0.075f, 0.09f, 0.82f));
            UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1300f, 70f));
            _banner = UIFactory.Label("Banner", bar.transform, "", 38, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_banner.rectTransform);

            _standings = UIFactory.Label("Standings", safe, "", 32, Theme.Cream, TextAnchor.UpperRight, true);
            UIFactory.Place(_standings.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -170f), new Vector2(560f, 240f));

            _handoff = Overlay(safe, "Handoff", out _handoffTitle, out _handoffBody);
            var ready = UIFactory.MakeButton("Ready", _handoff.transform, "READY", new Vector2(460f, 130f), Theme.Tape, party.Ready, 60);
            UIFactory.Place((RectTransform)ready.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(460f, 130f));

            _final = Overlay(safe, "Final", out _finalTitle, out _finalBody);
            var again = UIFactory.MakeButton("Rematch", _final.transform, "REMATCH", new Vector2(420f, 120f), Theme.Tape,
                () => SceneManager.LoadScene(SceneManager.GetActiveScene().name), 52);
            UIFactory.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 60f), new Vector2(420f, 120f));
            var home = UIFactory.MakeButton("Home", _final.transform, "HOME", new Vector2(420f, 120f), Theme.Cream, () =>
            {
                if (Application.CanStreamedLevelBeLoaded(SceneNames.MainMenu)) SceneManager.LoadScene(SceneNames.MainMenu);
            }, 52);
            UIFactory.Place((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(20f, 60f), new Vector2(420f, 120f));

            party.Changed += Refresh;
            Refresh();
        }

        private static GameObject Overlay(RectTransform safe, string name, out Text title, out Text body)
        {
            var root = UIFactory.Panel(name, safe, new Color(0.07f, 0.075f, 0.09f, 0.92f), true);
            UIFactory.Stretch(root.rectTransform);
            var t = UIFactory.TapeLabel("Title", root.transform, "", 72, Theme.Tape, -2f);
            UIFactory.Place(t.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1200f, 120f));
            title = t;
            body = UIFactory.Label("Body", root.transform, "", 40, Theme.Cream, TextAnchor.UpperCenter, false);
            body.lineSpacing = 1.2f;
            UIFactory.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(1700f, 500f));
            return root.gameObject;
        }

        private void Update()
        {
            if (_party?.Rules == null || _party.Rules.Phase != PartyPhase.Playing) return;
            _banner.text = BannerText();
        }

        private string BannerText()
        {
            var r = _party.Rules;
            string who = r.Current.Name;
            string task = r.Game == PartyGame.ScoreTurns ? $"SCORE {_party.AttemptTotal:N0}"
                        : r.Game == PartyGame.TrickBattle ? (r.IsSetting ? "CALL A TRICK: LAND IT IN A LINE" : $"LAND A {r.TargetTrickName}")
                        : r.IsSetting ? "SET A LINE: BANK ANY COMBO"
                        : $"MATCH: BANK {r.Target:N0}";
            return $"{who}  ·  {task}  ·  {Mathf.CeilToInt(_party.TimeLeft)}s";
        }

        private void Refresh()
        {
            var r = _party.Rules;
            var sb = new StringBuilder();
            foreach (var p in r.Players)
            {
                string mark = p == r.Current && r.Phase != PartyPhase.Finished ? "▶ " : "";
                if (r.Game != PartyGame.ScoreTurns)
                    sb.AppendLine($"{mark}{p.Name}  {(p.Out ? "OUT" : Pad(p.LetterText))}");
                else
                    sb.AppendLine($"{mark}{p.Name}  {p.Score:N0}");
            }
            _standings.text = sb.ToString();

            _handoff.SetActive(r.Phase == PartyPhase.Handoff);
            _final.SetActive(r.Phase == PartyPhase.Finished);
            _banner.text = r.Phase == PartyPhase.Playing ? BannerText() : "";

            if (r.Phase == PartyPhase.Handoff)
            {
                _handoffTitle.text = $"PASS TO {r.Current.Name}";
                string job = r.Game == PartyGame.ScoreTurns ? $"Score as much as you can in {PartyRules.ScoreTurnSeconds:0} seconds."
                           : r.Game == PartyGame.TrickBattle
                           ? (r.IsSetting ? "Call a trick: land a line. Its best trick is what everyone else has to land."
                                          : $"Land a {r.TargetTrickName} in a line.\nMiss it and you take a letter.")
                           : r.IsSetting ? "Set a line: bank any combo. Everyone else has to match it."
                           : $"Match the line: bank a combo worth at least {r.Target:N0}.\nMiss it and you take a letter.";
                _handoffBody.text = (string.IsNullOrEmpty(r.LastMessage) ? "" : r.LastMessage + "\n\n") + job + "\n\n" + _standings.text;
            }
            if (r.Phase == PartyPhase.Finished)
            {
                var winners = r.Winners();
                var names = new StringBuilder();
                foreach (var w in winners) names.Append(names.Length > 0 ? " & " : "").Append(w.Name);
                _finalTitle.text = winners.Count > 1 ? $"{names} TIE" : $"{names} WINS";
                _finalBody.text = (string.IsNullOrEmpty(r.LastMessage) ? "" : r.LastMessage + "\n\n") + _standings.text;
            }
        }

        private static string Pad(string letters)
        {
            var sb = new StringBuilder(letters);
            for (int i = letters.Length; i < PartyRules.Word.Length; i++) sb.Append('·');
            return sb.ToString();
        }
    }
}
