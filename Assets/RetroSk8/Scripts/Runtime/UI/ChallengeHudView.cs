using RetroSk8.Core;
using RetroSk8.Replay;
using RetroSk8.Scoring;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// "BEAT 25,430 · FROM OMARI" under the score during a challenge run; turns teal once beaten.
    /// In a ghost race it also tracks the friend's score as their ghost skates: "OMARI 12,300 · YOU +450".
    /// </summary>
    public sealed class ChallengeHudView : MonoBehaviour
    {
        private ScoreManager _score;
        private ScoreChallenge _challenge;
        private Text _label;
        private Text _race;
        private bool _beaten;
        private float _nextRace;

        public void Build(RectTransform safe, ScoreManager score, ScoreChallenge challenge)
        {
            _score = score;
            _challenge = challenge;
            var bg = UIFactory.Panel("Challenge", safe, Theme.InkSoft);
            UIFactory.Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(760f, 64f));
            _label = UIFactory.Label("Text", bg.transform, "", 32, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_label.rectTransform, 6f);

            if (challenge.Ghost != null)
            {
                // Ghost race: one line with the friend's running score and your lead, on their ghost's pink.
                bg.color = new Color(0.55f, 0.08f, 0.33f, 0.88f);
                bg.rectTransform.sizeDelta = new Vector2(900f, 64f);
                _race = _label;
            }
            Refresh();
        }

        private void Update()
        {
            if (_score == null) return;
            if (!_beaten && _score.Ledger.Total > _challenge.Target) { _beaten = true; Refresh(); }
            if (_race != null && Time.unscaledTime >= _nextRace)
            {
                _nextRace = Time.unscaledTime + 0.1f;
                UpdateRace();
            }
        }

        private void UpdateRace()
        {
            var ghost = GhostPlayer.Rival;
            float t = ghost != null ? ghost.PlaybackTime : 0f;
            long theirs = RivalTimeline.ScoreAt(_challenge.GhostBanks, t);
            long mine = _score.Ledger.Total;
            long diff = mine - theirs;
            string who = string.IsNullOrEmpty(_challenge.From) ? "GHOST" : _challenge.From;
            string lead = diff > 0 ? $"YOU +{diff:N0}" : diff < 0 ? $"YOU -{-diff:N0}" : "LEVEL";
            _race.text = $"{who} {theirs:N0}  ·  {lead}  ·  " + (_beaten ? "BEATEN!" : $"BEAT {_challenge.Target:N0}");
            _race.color = _beaten ? Theme.Teal : Theme.Cream;
        }

        private void Refresh()
        {
            if (_race != null) { UpdateRace(); return; }
            string from = string.IsNullOrEmpty(_challenge.From) ? "" : " · FROM " + _challenge.From;
            if (_challenge.Ghost != null)
                from = string.IsNullOrEmpty(_challenge.From) ? " · GHOST RACE" : $" · {_challenge.From}'S GHOST";
            _label.text = _beaten ? $"CHALLENGE BEATEN! ({_challenge.Target:N0}{from})" : $"BEAT {_challenge.Target:N0}{from}";
            _label.color = _beaten ? Theme.Teal : Theme.Tape;
        }
    }
}
