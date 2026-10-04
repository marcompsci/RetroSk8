using System.Collections.Generic;
using RetroSk8.Audio;
using RetroSk8.Core;
using RetroSk8.Feedback;
using RetroSk8.Game;
using RetroSk8.Player;
using RetroSk8.Save;
using RetroSk8.Scoring;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Tutorial 2.0 (Phase 17): runs one trick lesson from the Trick Book. Puts the skater at the lesson's spot (and back
    /// there after every bail), coaches step by step, counts banked lines with the trick, pays the first pass and offers
    /// the next lesson. Free skate rules: no timer, no score.
    /// </summary>
    public sealed class LessonCoach : MonoBehaviour
    {
        private TrickLesson _lesson;
        private PlayerController _player;
        private ComboManager _combo;
        private BailHandler _bail;
        private int _reps;
        private bool _done;
        private int _placeFrames = 2;

        private Text _title, _step, _count;
        private RectTransform _fill;
        private GameObject _finishRow;

        public void Build(RectTransform safe, TrickLesson lesson, PlayerController player, ComboManager combo)
        {
            _lesson = lesson;
            _player = player;
            _combo = combo;
            _bail = player.GetComponent<BailHandler>();

            var card = UIFactory.Panel("LessonCard", safe, new Color(0.07f, 0.075f, 0.09f, 0.9f), true);
            UIFactory.Place(card.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -170f), new Vector2(800f, 330f));
            UIFactory.Scanlines(card.transform, 0.12f);
            var tag = UIFactory.TapeLabel("Tag", card.transform, "TRICK LESSON", 30, Theme.Tape, -2f);
            UIFactory.Place(tag.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(300f, 48f));
            _count = UIFactory.Label("Count", card.transform, "", 30, Theme.Cream, TextAnchor.MiddleRight);
            UIFactory.Place(_count.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -12f), new Vector2(260f, 44f));
            _title = UIFactory.Label("Title", card.transform, lesson.Title, 44, Theme.Tape, TextAnchor.UpperLeft);
            UIFactory.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -52f), new Vector2(750f, 60f));
            _step = UIFactory.Label("Step", card.transform, "", 28, Theme.Cream, TextAnchor.UpperLeft, false);
            _step.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(_step.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -116f), new Vector2(750f, 120f));

            var barBg = UIFactory.Panel("Bar", card.transform, new Color(1f, 1f, 1f, 0.15f));
            UIFactory.Place(barBg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 34f), new Vector2(440f, 18f));
            var fill = UIFactory.Panel("Fill", barBg.transform, Theme.Teal);
            _fill = fill.rectTransform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            var exit = UIFactory.MakeButton("Exit", card.transform, "EXIT", new Vector2(200f, 70f), Theme.Coral, () => Leave(null), 32);
            UIFactory.Place((RectTransform)exit.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 16f), new Vector2(200f, 70f));

            // Shown when the lesson is passed.
            _finishRow = UIFactory.Rect("Finish", safe).gameObject;
            var fr = (RectTransform)_finishRow.transform;
            UIFactory.Place(fr, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1000f, 110f));
            var h = _finishRow.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 24f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = false;
            var next = Next();
            if (next != null) UIFactory.MakeButton("Next", fr, "NEXT: " + next.Title, new Vector2(560f, 100f), Theme.Tape, () => Leave(next.Id), 32);
            UIFactory.MakeButton("Book", fr, "TRICK BOOK", new Vector2(360f, 100f), Theme.Cream, () => Leave(null), 34);
            UIFactory.MakeButton("Stay", fr, "KEEP SKATING", new Vector2(360f, 100f), Theme.Teal, () => _finishRow.SetActive(false), 34);
            _finishRow.SetActive(false);

            combo.BankedDetail += OnBanked;
            if (_bail != null) _bail.Respawned += Place;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_combo != null) _combo.BankedDetail -= OnBanked;
            if (_bail != null) _bail.Respawned -= Place;
        }

        private void Update()
        {
            // Wait a couple of frames so the level's own spawn has happened, then move to the lesson spot.
            if (_placeFrames <= 0) return;
            if (--_placeFrames == 0) Place();
        }

        private void Place()
        {
            var heading = Quaternion.Euler(0f, _lesson.Yaw, 0f) * Vector3.forward;
            _player.Teleport(new Vector3(_lesson.X, _lesson.Y, _lesson.Z), heading);
            if (_lesson.AnySpecial) FillSpecial();
        }

        private void FillSpecial()
        {
            var meter = _combo.Special;
            if (meter != null && !meter.IsReady) meter.Add(10_000_000);
        }

        private void OnBanked(IReadOnlyList<string> ids, long points)
        {
            if (_done) { if (_lesson.AnySpecial) FillSpecial(); return; }
            int n = TrickLessons.Matches(_lesson, new List<string>(ids), id => TrickBookService.Find(id)?.IsSpecial ?? id.StartsWith("sig_"));
            if (_lesson.AnySpecial) FillSpecial();
            if (n <= 0) return;
            _reps += n;
            AudioManager.Instance?.PlaySfx(SfxId.Bank, 0.8f, 1.2f);
            HapticsManager.Play(HapticKind.Light);
            if (_reps >= _lesson.Count) Finish();
            Refresh();
        }

        private void Finish()
        {
            _done = true;
            int tokens = TrickLessons.Complete(SaveManager.Data.lessons, _lesson);
            if (tokens > 0) SaveManager.AddTokens(tokens); else SaveManager.Save();
            _step.text = tokens > 0 ? $"LESSON PASSED!  +{tokens} TAPE TOKENS" : "LESSON PASSED AGAIN!";
            AudioManager.Instance?.PlaySfx(SfxId.Fanfare);
            HapticsManager.Play(HapticKind.Success);
            _finishRow.SetActive(true);
        }

        private void Refresh()
        {
            _count.text = $"{Mathf.Min(_reps, _lesson.Count)} / {_lesson.Count}";
            _fill.anchorMax = new Vector2(Mathf.Clamp01(_reps / (float)_lesson.Count), 1f);
            if (!_done) _step.text = _lesson.Steps.Length > 0 ? _lesson.Steps[TrickLessons.StepFor(_lesson, _reps)] : "";
        }

        private TrickLesson Next()
        {
            int i = System.Array.IndexOf(TrickLessons.All, _lesson);
            return i >= 0 && i + 1 < TrickLessons.All.Length ? TrickLessons.All[i + 1] : null;
        }

        /// <summary>Starts another lesson, or goes back to the menu with the Trick Book open.</summary>
        private void Leave(string nextLesson)
        {
            Time.timeScale = 1f;
            if (nextLesson != null) { LessonService.Start(nextLesson); return; }
            GameSession.LessonId = null;
            GameSession.OpenTricksOnMenu = true;
            SceneRouter.Load(SceneNames.MainMenu);
        }
    }
}
