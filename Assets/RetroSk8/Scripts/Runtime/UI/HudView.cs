using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Player;
using RetroSk8.Scoring;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>In-run HUD: score, timer, combo + multiplier + Line Flow, balance meter, special meter, popups.</summary>
    public sealed class HudView : MonoBehaviour
    {
        private PlayerController _player;
        private ComboManager _combo;
        private ScoreManager _score;
        private RunController _run;
        private GrindController _grind;
        private ManualController _manual;

        private Text _scoreValue;
        private Text _timer;
        private RectTransform _specialFill;
        private Text _specialLabel;
        private GameObject _comboRoot;
        private Text _comboValue;
        private Text _multiplier;
        private Text _trickLine;
        private Text _flow;
        private GameObject _balanceRoot;
        private RectTransform _needle;
        private Image _needleImage;
        private Text _popup;
        private float _popupTimer;
        private Text _banner;
        private long _shownScore;

        public void Build(RectTransform safe, PlayerController player, ComboManager combo, ScoreManager score, RunController run)
        {
            _player = player;
            _combo = combo;
            _score = score;
            _run = run;
            _grind = player.GetComponent<GrindController>();
            _manual = player.GetComponent<ManualController>();

            // Score (top-left)
            var scoreRoot = UIFactory.Rect("Score", safe);
            UIFactory.Place(scoreRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -28f), new Vector2(560f, 190f));
            var tag = UIFactory.TapeLabel("ScoreTag", scoreRoot, "SCORE", 36, Theme.Tape, -3f);
            UIFactory.Place(tag.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(190f, 54f));
            _scoreValue = UIFactory.Label("Value", scoreRoot, "0", 88, Theme.White, TextAnchor.UpperLeft);
            UIFactory.Place(_scoreValue.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -62f), new Vector2(560f, 100f));

            // Special meter under the score
            var specialBg = UIFactory.Panel("SpecialMeter", scoreRoot, Theme.InkSoft);
            UIFactory.Place(specialBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -168f), new Vector2(380f, 20f));
            var fill = UIFactory.Panel("Fill", specialBg.transform, Theme.Teal);
            _specialFill = fill.rectTransform;
            _specialFill.anchorMin = Vector2.zero;
            _specialFill.anchorMax = new Vector2(0f, 1f);
            _specialFill.offsetMin = _specialFill.offsetMax = Vector2.zero;
            _specialLabel = UIFactory.Label("SpecialLabel", specialBg.transform, "SPECIAL", 26, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(_specialLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(300f, 40f));

            // Timer (top-centre) with the one CRT scanline accent in the HUD
            var timerBg = UIFactory.Panel("Timer", safe, Theme.Ink);
            UIFactory.Place(timerBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(300f, 116f));
            _timer = UIFactory.Label("Value", timerBg.transform, "2:00", 84, Theme.Cream, TextAnchor.MiddleCenter, false);
            UIFactory.Stretch(_timer.rectTransform);
            UIFactory.Scanlines(timerBg.transform, 0.22f);

            // Combo (upper-middle)
            _comboRoot = UIFactory.Rect("Combo", safe).gameObject;
            var comboRt = (RectTransform)_comboRoot.transform;
            UIFactory.Place(comboRt, new Vector2(0.5f, 0.74f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 260f));
            _comboValue = UIFactory.Label("Value", comboRt, "0", 104, Theme.Tape, TextAnchor.MiddleRight);
            UIFactory.Place(_comboValue.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-10f, 0f), new Vector2(700f, 120f));
            _multiplier = UIFactory.Label("Multiplier", comboRt, "x1", 80, Theme.Coral, TextAnchor.MiddleLeft);
            UIFactory.Place(_multiplier.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(18f, -8f), new Vector2(400f, 110f));
            _trickLine = UIFactory.Label("Tricks", comboRt, "", 40, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Place(_trickLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(1400f, 56f));
            _flow = UIFactory.Label("Flow", comboRt, "", 34, Theme.Teal, TextAnchor.MiddleCenter);
            UIFactory.Place(_flow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(900f, 48f));
            _comboRoot.SetActive(false);

            // Balance meter (centre, below the skater's head)
            var balanceBg = UIFactory.Panel("Balance", safe, Theme.InkSoft);
            _balanceRoot = balanceBg.gameObject;
            UIFactory.Place(balanceBg.rectTransform, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 30f));
            var centre = UIFactory.Panel("Centre", balanceBg.transform, Theme.Cream);
            UIFactory.Place(centre.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 44f));
            _needleImage = UIFactory.Panel("Needle", balanceBg.transform, Theme.Teal);
            _needle = _needleImage.rectTransform;
            UIFactory.Place(_needle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 64f));
            _balanceRoot.SetActive(false);

            // Popup and banner
            _popup = UIFactory.Label("Popup", safe, "", 64, Theme.Cream);
            UIFactory.Place(_popup.rectTransform, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 90f));
            _banner = UIFactory.TapeLabel("Banner", safe, "TIME!", 110, Theme.Coral, 4f);
            UIFactory.Place(_banner.transform.parent as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 170f));
            _banner.transform.parent.gameObject.SetActive(false);

            combo.Banked += OnBanked;
            combo.Bailed += OnBailed;
            run.TimeUp += () => ShowPopup("TIME! LAND IT", Theme.Coral, 2f);
            run.Finished += _ => _banner.transform.parent.gameObject.SetActive(true);
            player.Landed += v => { if (v.Quality == LandingQuality.Sketchy) ShowPopup("SKETCHY", Theme.Tape, 0.8f); };
        }

        private int _shownSeconds = int.MinValue;
        private long _shownBase = -1;
        private int _shownMult = -1, _shownEntries = -1, _shownFlow = -1;
        private float _baseRefreshAt;
        private string _shownLast, _shownPrev;

        private void Update()
        {
            if (_player == null) return;

            // Score counts up quickly for readability.
            long total = _score.Ledger.Total;
            if (_shownScore != total)
            {
                long step = System.Math.Max(1, (total - _shownScore) / 6);
                _shownScore = System.Math.Min(total, _shownScore + step);
                if (total < _shownScore) _shownScore = total;
                _scoreValue.text = _shownScore.ToString("N0");
            }

            var timer = _run.Timer;
            if (timer != null)
            {
                // Phase 17 perf: only format the clock when the shown second changes (no string garbage every frame).
                int shown = !timer.IsTimed || GameSession.DebugInfiniteTime ? -1 : Mathf.CeilToInt(Mathf.Max(0f, timer.Remaining));
                if (shown != _shownSeconds)
                {
                    _shownSeconds = shown;
                    _timer.text = shown < 0 ? "FREE" : RunTimer.Format(timer.Remaining);
                    _timer.color = shown >= 0 && timer.Remaining <= 10f ? Theme.Coral : Theme.Cream;
                }
            }

            var tracker = _combo.Tracker;
            bool comboOn = tracker.IsActive;
            if (_comboRoot.activeSelf != comboOn) _comboRoot.SetActive(comboOn);
            if (comboOn)
            {
                // Phase 17 perf: rebuild each label only when its number actually changes.
                long basePts = (long)tracker.BasePoints;
                // Phase 18: continuous tricks raise the points every frame, so redraw them at most ~12 times a second.
                if (basePts != _shownBase && (Time.unscaledTime >= _baseRefreshAt || tracker.Entries.Count != _shownEntries))
                {
                    _shownBase = basePts;
                    _baseRefreshAt = Time.unscaledTime + 0.08f;
                    _comboValue.text = basePts.ToString("N0");
                }
                int mult = Mathf.RoundToInt(tracker.Multiplier);
                if (mult != _shownMult) { _shownMult = mult; _multiplier.text = "x" + mult; }
                int entries = tracker.Entries.Count;
                // Phase 18: names can change in place (stall swaps), so compare the shown names by reference.
                string last = entries > 0 ? tracker.Entries[entries - 1].DisplayName : null;
                string prev = entries > 1 ? tracker.Entries[entries - 2].DisplayName : null;
                if (entries != _shownEntries || !ReferenceEquals(last, _shownLast) || !ReferenceEquals(prev, _shownPrev))
                {
                    _shownEntries = entries;
                    _shownLast = last;
                    _shownPrev = prev;
                    _trickLine.text = _combo.BuildLabel(3);
                }
                int flowPct = Mathf.RoundToInt((tracker.FlowMultiplier - 1f) * 100f);
                if (flowPct != _shownFlow) { _shownFlow = flowPct; _flow.text = flowPct > 0 ? "LINE FLOW +" + flowPct + "%" : string.Empty; }
            }
            else
            {
                _shownBase = -1; _shownMult = -1; _shownEntries = -1; _shownFlow = -1;
            }

            BalanceMeter meter = null;
            if (_grind.IsGrinding) meter = _grind.Balance;
            else if (_manual.IsActive) meter = _manual.Balance;
            bool showBalance = meter != null && meter.IsActive;
            if (_balanceRoot.activeSelf != showBalance) _balanceRoot.SetActive(showBalance);
            if (showBalance)
            {
                float lean = meter.Lean;
                _needle.anchoredPosition = new Vector2(lean * 300f, 0f);
                _needleImage.color = Color.Lerp(Theme.Teal, Theme.Coral, Mathf.Abs(lean));
                // Phase 20: not colour alone: the needle also grows as you lose balance.
                float grow = 1f + Mathf.Clamp01(Mathf.Abs(lean) - 0.4f) * 1.2f;
                _needle.localScale = new Vector3(grow, grow, 1f);
            }

            float special = _combo.Special.Value;
            _specialFill.anchorMax = new Vector2(special, 1f);
            bool ready = _combo.Special.IsReady;
            _specialLabel.text = ready ? "SPECIAL READY" : "SPECIAL";
            bool still = RetroSk8.Save.SaveManager.Data.settings.reducedMotion; // Phase 20: no pulsing with Reduce Motion
            _specialLabel.color = ready ? (still ? Theme.Tape : Color.Lerp(Theme.Tape, Theme.Coral, Mathf.PingPong(Time.time * 3f, 1f))) : Theme.Cream;

            if (_popupTimer > 0f)
            {
                _popupTimer -= Time.unscaledDeltaTime;
                var c = _popup.color;
                c.a = Mathf.Clamp01(_popupTimer * 2f);
                _popup.color = c;
                _popup.rectTransform.anchoredPosition = new Vector2(0f, still ? 0f : (1f - Mathf.Clamp01(_popupTimer)) * 40f);
            }
        }

        private void OnBanked(ComboResult result, string label, LandingQuality quality)
        {
            if (result.Points <= 0) return;
            string q = quality == LandingQuality.Sketchy ? "SKETCHY " : string.Empty;
            ShowPopup($"{q}+{result.Points:N0}", quality == LandingQuality.Sketchy ? Theme.Tape : Theme.Teal, 1.4f);
        }

        private void OnBailed(long lost, BailReason reason)
        {
            ShowPopup(lost > 0 ? $"BAIL  -{lost:N0}" : "BAIL", Theme.Coral, 1.4f);
        }

        /// <summary>Short centre-screen message (goals, milestones).</summary>
        public void ShowToast(string text, Color color, float seconds) => ShowPopup(text, color, seconds);

        private void ShowPopup(string text, Color color, float seconds)
        {
            _popup.text = text;
            _popup.color = color;
            _popupTimer = seconds;
        }
    }
}
