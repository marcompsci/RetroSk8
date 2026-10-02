using RetroSk8.Core;
using RetroSk8.Scoring;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>"BEAT 25,430 · FROM OMARI" under the score during a challenge run; turns teal once beaten.</summary>
    public sealed class ChallengeHudView : MonoBehaviour
    {
        private ScoreManager _score;
        private ScoreChallenge _challenge;
        private Text _label;
        private bool _beaten;

        public void Build(RectTransform safe, ScoreManager score, ScoreChallenge challenge)
        {
            _score = score;
            _challenge = challenge;
            var bg = UIFactory.Panel("Challenge", safe, Theme.InkSoft);
            UIFactory.Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(760f, 64f));
            _label = UIFactory.Label("Text", bg.transform, "", 32, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_label.rectTransform, 6f);
            Refresh();
        }

        private void Update()
        {
            if (!_beaten && _score != null && _score.Ledger.Total > _challenge.Target) { _beaten = true; Refresh(); }
        }

        private void Refresh()
        {
            string from = string.IsNullOrEmpty(_challenge.From) ? "" : " · FROM " + _challenge.From;
            _label.text = _beaten ? $"CHALLENGE BEATEN! ({_challenge.Target:N0}{from})" : $"BEAT {_challenge.Target:N0}{from}";
            _label.color = _beaten ? Theme.Teal : Theme.Tape;
        }
    }
}
