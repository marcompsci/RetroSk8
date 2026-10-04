using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Save;
using RetroSk8.Scoring;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// One-time hints for the first sessions after the lesson: shows a short tip card when it's useful (no ollie yet,
    /// airs without tricks, a sketchy landing, no grinds, special ready, a lost combo, no gaps). Each tip shows once,
    /// ever, and never two within a few seconds (<see cref="TipState"/>).
    /// </summary>
    public sealed class TipCoachView : MonoBehaviour
    {
        private const float ShowSeconds = 5.5f;

        private PlayerController _player;
        private ComboManager _combo;
        private RunController _run;
        private CanvasGroup _group;
        private Text _text;
        private float _until;
        private float _elapsed;
        private float _lastTip = -999f;
        private bool _popped, _trickThisAir, _grinded, _gapped;
        private int _airsWithoutTricks;
        private bool _touch;

        private static TipState Tips => SaveManager.Data.tips;

        public void Build(RectTransform safe, PlayerController player, ComboManager combo, RunController run)
        {
            _player = player;
            _combo = combo;
            _run = run;
            _touch = UIManager.IsTouchDevice;

            var card = UIFactory.Panel("TipCard", safe, new Color(0.07f, 0.075f, 0.09f, 0.9f));
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(1180f, 72f));
            _group = card.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            var tag = UIFactory.TapeLabel("Tag", card.transform, "TIP", 26, Theme.Tape, -3f);
            UIFactory.Place(tag.transform.parent as RectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(90f, 44f));
            _text = UIFactory.Label("Text", card.transform, "", 27, Theme.Cream, TextAnchor.MiddleLeft, false);
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Stretch(_text.rectTransform);
            _text.rectTransform.offsetMin = new Vector2(120f, 4f);
            _text.rectTransform.offsetMax = new Vector2(-16f, -4f);

            player.Popped += _ => { _popped = true; _trickThisAir = false; };
            player.Landed += OnLanded;
            var tricks = player.GetComponent<TrickController>();
            if (tricks != null) tricks.TrickStarted += _ => _trickThisAir = true;
            var grind = player.GetComponent<GrindController>();
            if (grind != null) grind.GrindStarted += _ => _grinded = true;
            var gaps = player.GetComponent<GapTracker>();
            if (gaps != null) gaps.GapCleared += _ => _gapped = true;
            combo.Bailed += (lost, reason) => { if (lost > 0) Show(TipId.Bail); };
        }

        private void OnLanded(LandingVerdict v)
        {
            if (v.Quality == LandingQuality.Sketchy) Show(TipId.CleanLanding);
            if (!_trickThisAir && ++_airsWithoutTricks >= 2) Show(TipId.AirTrick);
            if (_trickThisAir) _airsWithoutTricks = 0;
            _trickThisAir = false;
        }

        private void Show(TipId id)
        {
            if (!Tips.TryShow(id, _elapsed - _lastTip)) return;
            _lastTip = _elapsed;
            _text.text = TipState.Text(id, _touch);
            _until = Time.unscaledTime + ShowSeconds;
            SaveManager.Save();
        }

        private void Update()
        {
            if (_group == null) return;
            if (_run == null || !_run.IsPaused) _elapsed += Time.deltaTime;
            if (_elapsed > 6f && !_popped) Show(TipId.Ollie);
            if (_elapsed > 40f && !_grinded) Show(TipId.Grind);
            if (_elapsed > 70f && !_gapped) Show(TipId.Gap);
            if (_combo != null && _combo.Special.IsReady) Show(TipId.Special);
            float target = Time.unscaledTime < _until ? 1f : 0f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime * 4f);
        }
    }
}
