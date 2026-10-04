using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Replay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Replay editor controls: transport, speed, scrub bar with in/out marks and moments, camera, export, hide UI.</summary>
    public sealed class ReplayTheaterView : MonoBehaviour
    {
        private ReplayTheater _theater;
        private GameObject _ui;
        private Text _play, _speed, _camera, _time, _moment, _exportLabel;
        private RectTransform _fill, _inMark, _outMark, _bar;
        private Button _share;
        private readonly List<RectTransform> _blocking = new List<RectTransform>();
        private float _showUiAt = -1f;

        public void Build(RectTransform safe, ReplayTheater theater)
        {
            _theater = theater;
            theater.IsOverUi = p =>
            {
                foreach (var r in _blocking) if (r != null && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, p, null)) return true;
                return false;
            };
            _ui = UIFactory.Rect("ReplayUi", safe).gameObject;
            UIFactory.Stretch((RectTransform)_ui.transform);
            var ui = (RectTransform)_ui.transform;

            if (!theater.Loaded)
            {
                var missing = UIFactory.Label("Missing", ui, "THAT REPLAY COULDN'T BE LOADED", 50, Theme.Coral, TextAnchor.MiddleCenter);
                UIFactory.Stretch(missing.rectTransform);
            }

            // Top bar.
            var back = UIFactory.MakeButton("Back", ui, "BACK", new Vector2(220f, 84f), Theme.Coral, Back, 36);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(220f, 84f));
            _blocking.Add((RectTransform)back.transform);
            var e = theater.Entry;
            var title = UIFactory.Label("Title", ui, e == null ? "REPLAY" : $"{(e.parkName ?? "").ToUpperInvariant()} · {e.mode} · {e.score:N0}", 34, Theme.Tape, TextAnchor.UpperCenter);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(1200f, 50f));
            var cam = UIFactory.MakeButton("Camera", ui, "", new Vector2(300f, 84f), Theme.Teal, () => { _theater.CycleCamera(); Refresh(); }, 32);
            UIFactory.Place((RectTransform)cam.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-260f, -24f), new Vector2(300f, 84f));
            _camera = cam.GetComponentInChildren<Text>();
            _blocking.Add((RectTransform)cam.transform);
            var hide = UIFactory.MakeButton("Hide", ui, "HIDE", new Vector2(200f, 84f), Theme.Cream, () => { _ui.SetActive(false); _showUiAt = Time.unscaledTime + 4f; }, 32);
            UIFactory.Place((RectTransform)hide.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(200f, 84f));
            _blocking.Add((RectTransform)hide.transform);

            _moment = UIFactory.Label("Moment", ui, "", 44, Theme.Tape, TextAnchor.MiddleCenter);
            UIFactory.Place(_moment.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1800f, 70f));

            // Bottom panel: scrub bar + transport.
            var panel = UIFactory.Panel("Transport", ui, Theme.InkSoft, true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(2000f, 230f));
            _blocking.Add(panel.rectTransform);

            var bar = UIFactory.Panel("Bar", panel.transform, new Color(1f, 1f, 1f, 0.15f), true);
            UIFactory.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1900f, 44f));
            _bar = bar.rectTransform;
            var fill = UIFactory.Panel("Fill", bar.transform, Theme.Tape);
            _fill = fill.rectTransform;
            _fill.anchorMin = Vector2.zero; _fill.anchorMax = new Vector2(0f, 1f); _fill.pivot = new Vector2(0f, 0.5f);
            _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            _inMark = Marker(bar.transform, Theme.Teal);
            _outMark = Marker(bar.transform, Theme.Coral);
            if (theater.Entry != null && theater.Clock.Duration > 0f)
                foreach (var m in theater.Entry.moments)
                {
                    var tick = UIFactory.Panel("Moment", bar.transform, Theme.Cream);
                    float x = Mathf.Clamp01(m.time / theater.Clock.Duration);
                    tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(x, 1f);
                    tick.rectTransform.pivot = new Vector2(0.5f, 1f);
                    tick.rectTransform.sizeDelta = new Vector2(6f, 16f);
                    tick.rectTransform.anchoredPosition = Vector2.zero;
                }
            bar.gameObject.AddComponent<ScrubBar>().OnScrub = f => { _theater.SeekFraction(f); Refresh(); };

            _time = UIFactory.Label("Time", panel.transform, "", 28, Theme.Cream, TextAnchor.MiddleLeft);
            UIFactory.Place(_time.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -78f), new Vector2(500f, 40f));

            var row = UIFactory.Rect("Row", panel.transform);
            UIFactory.Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1960f, 96f));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            Btn(row, "-5s", 150, Theme.Cream, () => _theater.Skip(-5f));
            _play = Btn(row, "", 200, Theme.Tape, () => _theater.TogglePlay());
            Btn(row, "+5s", 150, Theme.Cream, () => _theater.Skip(5f));
            _speed = Btn(row, "", 170, Theme.Teal, () => _theater.CycleSpeed());
            Btn(row, "SET IN", 190, Theme.Teal, () => _theater.MarkIn());
            Btn(row, "SET OUT", 200, Theme.Coral, () => _theater.MarkOut());
            Btn(row, "CLEAR", 170, Theme.Cream, () => _theater.ClearMarks());
            _exportLabel = Btn(row, ReplayTheater.CanExport ? "EXPORT CLIP" : "TAKE A SCREEN RECORDING", ReplayTheater.CanExport ? 280 : 420, Theme.Tape, () => _theater.Export());
            _share = UIFactory.MakeButton("Share", row, "SHARE CLIP", new Vector2(240f, 90f), Theme.Teal, ClipRecorder.Share, 30);

            theater.ExportingChanged += exporting =>
            {
                _ui.SetActive(!exporting);
                Refresh();
            };
            Refresh();
        }

        private static RectTransform Marker(Transform bar, Color c)
        {
            var m = UIFactory.Panel("Mark", bar, c);
            m.rectTransform.anchorMin = m.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            m.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            m.rectTransform.sizeDelta = new Vector2(8f, 60f);
            return m.rectTransform;
        }

        private Text Btn(RectTransform row, string label, float width, Color color, Action click)
        {
            var b = UIFactory.MakeButton(label, row, label, new Vector2(width, 90f), color, () => { click(); Refresh(); }, 30);
            return b.GetComponentInChildren<Text>();
        }

        private static void Back()
        {
            GameSession.Mode = RunMode.FreeSkate;
            if (Application.CanStreamedLevelBeLoaded(SceneNames.MainMenu)) SceneManager.LoadScene(SceneNames.MainMenu);
        }

        private void Refresh()
        {
            var c = _theater.Clock;
            if (c == null) return;
            _play.text = c.Playing ? "PAUSE" : "PLAY";
            _speed.text = c.Speed >= 1f ? "1X" : c.Speed >= 0.5f ? "0.5X" : "0.25X";
            _camera.text = "CAM: " + ReplayTheater.CameraName(_theater.CameraMode);
            _share.gameObject.SetActive(ClipRecorder.State == ClipState.Ready || ClipRecorder.State == ClipState.Saving);
            _share.interactable = ClipRecorder.State == ClipState.Ready;
            if (!ReplayTheater.CanExport) _exportLabel.text = "USE IOS SCREEN RECORDING";
        }

        private void Update()
        {
            if (!_ui.activeSelf && _showUiAt > 0f && Time.unscaledTime >= _showUiAt && !_theater.Exporting) { _ui.SetActive(true); _showUiAt = -1f; }
            var c = _theater.Clock;
            if (c == null) return;
            float d = Mathf.Max(0.01f, c.Duration);
            _fill.anchorMax = new Vector2(Mathf.Clamp01(c.Time / d), 1f);
            _inMark.anchorMin = _inMark.anchorMax = new Vector2(c.In / d, 0.5f);
            _outMark.anchorMin = _outMark.anchorMax = new Vector2(c.Out / d, 0.5f);
            _time.text = $"{Fmt(c.Time)} / {Fmt(c.Duration)}   CLIP {Fmt(c.ClipLength)}";
            var m = _theater.CurrentMoment;
            _moment.text = m != null ? $"{m.label}  +{m.points:N0}" : "";
            if (Time.frameCount % 20 == 0) Refresh();
        }

        private static string Fmt(float s) => $"{(int)(s / 60f)}:{s % 60f:00.0}";
    }

    /// <summary>Tap or drag along the bar to scrub.</summary>
    public sealed class ScrubBar : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Action<float> OnScrub;
        public void OnPointerDown(PointerEventData e) => Scrub(e);
        public void OnDrag(PointerEventData e) => Scrub(e);

        private void Scrub(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local)) return;
            float f = Mathf.Clamp01((local.x - rt.rect.xMin) / Mathf.Max(1f, rt.rect.width));
            OnScrub?.Invoke(f);
        }
    }
}
