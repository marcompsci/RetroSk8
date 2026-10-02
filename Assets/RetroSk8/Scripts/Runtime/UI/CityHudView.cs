using RetroSk8.Core;
using RetroSk8.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Retro City HUD: district name, toasts, and a banner + gate arrow while a challenge or race runs.</summary>
    public sealed class CityHudView : MonoBehaviour
    {
        private CityController _city;
        private Image _banner;
        private Text _bannerText;
        private Text _district;
        private Text _toast;
        private float _toastTimer;
        private RectTransform _arrow;
        private Text _arrowDistance;
        private Text _counts;

        public void Build(RectTransform safe, CityController city)
        {
            _city = city;

            _banner = UIFactory.Panel("CityBanner", safe, Theme.InkSoft);
            UIFactory.Place(_banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1100f, 90f));
            _bannerText = UIFactory.Label("Text", _banner.transform, "", 40, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Stretch(_bannerText.rectTransform, 8f);

            _district = UIFactory.Label("District", safe, "", 30, Theme.Cream, TextAnchor.UpperRight);
            UIFactory.Place(_district.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -165f), new Vector2(800f, 44f));

            _counts = UIFactory.Label("Counts", safe, "", 26, Theme.Tape, TextAnchor.UpperRight);
            UIFactory.Place(_counts.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -210f), new Vector2(800f, 36f));

            _toast = UIFactory.Label("CityToast", safe, "", 44, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Place(_toast.rectTransform, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800f, 70f));

            // Arrow towards the next race gate: a needle with a head, rotated around the screen's upper centre.
            _arrow = UIFactory.Rect("GateArrow", safe);
            UIFactory.Place(_arrow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(120f, 120f));
            var needle = UIFactory.Panel("Needle", _arrow, ArrowColor);
            UIFactory.Place(needle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, -30f), new Vector2(16f, 70f));
            var head = UIFactory.Panel("Head", _arrow, ArrowColor);
            UIFactory.Place(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 42f), new Vector2(36f, 36f));
            head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _arrowDistance = UIFactory.Label("Distance", safe, "", 28, Theme.Cream, TextAnchor.UpperCenter);
            UIFactory.Place(_arrowDistance.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(300f, 40f));

            city.Toast += (text, color) =>
            {
                _toast.text = text;
                _toast.color = color;
                _toastTimer = 2.6f;
            };
            city.Changed += RefreshCounts;
            RefreshCounts();
            _toast.text = "";
        }

        private static readonly Color ArrowColor = new Color(0.1f, 0.9f, 1f);

        private void RefreshCounts()
        {
            var p = _city.Progress;
            _counts.text = $"SPOTS {p.spots.Count}/{RetroCityLayout.Spots.Count}   TAPES {p.tapes.Count}/{RetroCityLayout.Tapes.Count}";
            var spot = _city.CurrentSpot;
            _district.text = spot != null ? spot.Name.ToUpperInvariant() : "RETRO CITY STREETS";
        }

        private void Update()
        {
            if (_city == null) return;

            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                var c = _toast.color;
                c.a = Mathf.Clamp01(_toastTimer * 1.5f);
                _toast.color = c;
            }

            if (_city.Challenge != null)
            {
                var s = _city.Challenge;
                long best = _city.ChallengeBest;
                var medal = MedalRules.ForScore(best, s.Bronze, s.Silver, s.Gold);
                long next = medal == Medal.None ? s.Bronze : medal == Medal.Bronze ? s.Silver : medal == Medal.Silver ? s.Gold : 0;
                string goal = next > 0 ? $"NEXT {(medal == Medal.None ? "BRONZE" : medal == Medal.Bronze ? "SILVER" : "GOLD")} {next:N0}" : "GOLD!";
                SetBanner($"{s.Name.ToUpperInvariant()}   BEST {best:N0}   {goal}   {Mathf.CeilToInt(_city.ChallengeTimeLeft)}s");
            }
            else if (_city.Race != null)
            {
                var r = _city.Race;
                SetBanner($"{r.Race.Name.ToUpperInvariant()}   GATE {Mathf.Min(r.NextGate + 1, r.Race.GateCount)}/{r.Race.GateCount}   {CityController.FormatTime(r.Elapsed)}");
            }
            else SetBanner(null);

            var gate = _city.NextGatePosition;
            _arrow.gameObject.SetActive(gate.HasValue);
            _arrowDistance.gameObject.SetActive(gate.HasValue);
            if (gate.HasValue)
            {
                var cam = Camera.main;
                var from = _city.PlayerPosition;
                var to = gate.Value - from;
                to.y = 0f;
                var fwd = cam != null ? cam.transform.forward : _city.PlayerHeading;
                fwd.y = 0f;
                float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
                _arrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
                _arrowDistance.text = $"{to.magnitude:0} m";
            }
        }

        private void SetBanner(string text)
        {
            bool on = !string.IsNullOrEmpty(text);
            if (_banner.gameObject.activeSelf != on) _banner.gameObject.SetActive(on);
            if (on) _bannerText.text = text;
        }
    }
}
