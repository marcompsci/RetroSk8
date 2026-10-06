using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Player;
using RetroSk8.Save;
using RetroSk8.Scoring;
using UnityEngine;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>
    /// Runs the first-run lesson (RunMode.Tutorial): listens to the skater, feeds <see cref="TutorialFlow"/>,
    /// and shows a coaching card with the current skill, a progress bar and a SKIP button.
    /// Text adapts to touch or keyboard. Finishing pays a one-time Tape Token reward, then ends the run.
    /// </summary>
    public sealed class TutorialCoach : MonoBehaviour
    {
        private const float EndDelay = 3f;

        private TutorialFlow _flow;
        private PlayerController _player;
        private RunController _run;
        private HudView _hud;
        private bool _touch;

        private Text _stepLabel;
        private Text _title;
        private Text _body;
        private RectTransform _barFill;
        private bool _flipThisAir, _grabThisAir;
        private float _grindTime, _manualTime;
        private float _endAt = -1f;

        public TutorialFlow Flow => _flow;

        public void Build(RectTransform safe, PlayerController player, ComboManager combo, RunController run, HudView hud)
        {
            _flow = new TutorialFlow();
            _player = player;
            _run = run;
            _hud = hud;
            _touch = UIManager.IsTouchDevice;

            var card = UIFactory.Panel("TutorialCard", safe, new Color(0.07f, 0.075f, 0.09f, 0.88f), true);
            UIFactory.Place(card.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -170f), new Vector2(760f, 300f));
            UIFactory.Scanlines(card.transform, 0.12f);

            var tag = UIFactory.TapeLabel("Tag", card.transform, "HOW TO SKATE", 30, Theme.Tape, -2f);
            UIFactory.Place(tag.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(300f, 48f));
            _stepLabel = UIFactory.Label("Step", card.transform, "", 30, Theme.Cream, TextAnchor.MiddleRight);
            UIFactory.Place(_stepLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -12f), new Vector2(200f, 44f));

            _title = UIFactory.Label("Title", card.transform, "", 46, Theme.Tape, TextAnchor.UpperLeft);
            UIFactory.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -52f), new Vector2(710f, 60f));
            _body = UIFactory.Label("Body", card.transform, "", 32, Theme.Cream, TextAnchor.UpperLeft, false);
            UIFactory.Place(_body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -116f), new Vector2(710f, 110f));

            var barBg = UIFactory.Panel("Bar", card.transform, new Color(1f, 1f, 1f, 0.15f));
            UIFactory.Place(barBg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 30f), new Vector2(460f, 18f));
            var fill = UIFactory.Panel("Fill", barBg.transform, Theme.Teal);
            _barFill = fill.rectTransform;
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.pivot = new Vector2(0f, 0.5f);
            _barFill.offsetMin = _barFill.offsetMax = Vector2.zero;

            var skip = UIFactory.MakeButton("Skip", card.transform, "SKIP", new Vector2(200f, 70f), Theme.Coral, Skip, 32);
            UIFactory.Place((RectTransform)skip.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 16f), new Vector2(200f, 70f));

            player.StateChanged += (from, to) =>
            {
                if (to == SkaterState.Airborne && from != SkaterState.Airborne) _flipThisAir = _grabThisAir = false;
            };
            player.GetComponent<TrickController>().TrickStarted += t =>
            {
                if (t.family == TrickFamily.Flip) _flipThisAir = true;
                if (t.family == TrickFamily.Grab) _grabThisAir = true;
            };
            player.Landed += v => _flow.OnLanded(v.Quality, v.HalfTurns, _flipThisAir, _grabThisAir);
            combo.Banked += (result, _, __) => _flow.OnBanked(result.TrickCount);
            combo.Bailed += (_, __) => _flow.OnBailed();
            _flow.StepCompleted += OnStepCompleted;
            Refresh();
        }

        private void Update()
        {
            if (_flow == null || _player == null) return;
            if (_endAt >= 0f)
            {
                if (Time.time >= _endAt) { _endAt = -1f; _run.EndRunNow(); }
                return;
            }
            if (_run.IsPaused || _flow.IsDone) return;

            float dt = Time.deltaTime;
            var state = _player.State;
            if (state == SkaterState.Rolling && _player.IsGrounded) _flow.OnRolling(dt, _player.Speed);
            _grindTime = state == SkaterState.Grinding ? _grindTime + dt : 0f;
            _manualTime = state == SkaterState.Manual ? _manualTime + dt : 0f;
            if (_grindTime > 0f) _flow.OnGrinding(_grindTime);
            if (_manualTime > 0f) _flow.OnManual(_manualTime);

            _barFill.anchorMax = new Vector2(_flow.Progress, 1f);
        }

        private void OnStepCompleted(TutorialStep finished)
        {
            if (_flow.IsDone) Complete();
            else _hud?.ShowToast("NICE!", Theme.Teal, 1.2f);
            Refresh();
        }

        private void Complete()
        {
            var s = SaveManager.Data.settings;
            s.tutorialDone = true;
            string toast = "LESSON COMPLETE";
            if (!s.tutorialRewarded)
            {
                s.tutorialRewarded = true;
                SaveManager.AddTokens(TutorialFlow.RewardTokens); // also saves
                toast += $"  +{TutorialFlow.RewardTokens} TAPE TOKENS";
            }
            else SaveManager.Save();
            _hud?.ShowToast(toast, Theme.Tape, EndDelay);
            _endAt = Time.time + EndDelay;
        }

        private static TutorialCoach s_active;

        /// <summary>Phase 21: the pause menu's SKIP LESSON (the card's SKIP can't be reached with a controller mid-run).</summary>
        public static bool SkipActive()
        {
            if (s_active == null || s_active._flow == null || s_active._flow.IsDone) return false;
            s_active._run.SetPaused(false);
            s_active.Skip();
            return true;
        }

        private void OnEnable() => s_active = this;
        private void OnDisable() { if (s_active == this) s_active = null; }

        private void Skip()
        {
            if (_flow.IsDone) return;
            _flow.SkipAll();
            SaveManager.Data.settings.tutorialDone = true;
            SaveManager.Save();
            _run.EndRunNow();
        }

        private void Refresh()
        {
            _stepLabel.text = _flow.IsDone ? "DONE" : $"{_flow.StepNumber}/{TutorialFlow.StepCount}";
            _title.text = Title(_flow.Step);
            _body.text = Body(_flow.Step, _touch);
            _barFill.anchorMax = new Vector2(_flow.Progress, 1f);
        }

        public static string Title(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Push: return "PUSH";
                case TutorialStep.Ollie: return "OLLIE";
                case TutorialStep.Flip: return "FLIP TRICK";
                case TutorialStep.Grab: return "GRAB";
                case TutorialStep.Spin: return "SPIN";
                case TutorialStep.Grind: return "GRIND";
                case TutorialStep.Manual: return "MANUAL";
                case TutorialStep.Combo: return "BANK A COMBO";
                default: return "YOU'RE READY";
            }
        }

        public static string Body(TutorialStep step, bool touch)
        {
            switch (step)
            {
                case TutorialStep.Push:
                    return touch ? "Push the left stick up to build speed." : "Hold W (or the left stick up) to push and build speed.";
                case TutorialStep.Ollie:
                    return touch ? "Hold JUMP to crouch, let go to pop. Land it." : "Hold Space to crouch, release to pop. Land it.";
                case TutorialStep.Flip:
                    return touch ? "Ollie, then swipe UP on the right side for a flip. Land it." : "Ollie, then press I in the air for a flip. Land it.";
                case TutorialStep.Grab:
                    return touch ? "Ollie, then swipe DOWN for a grab. Land it." : "Ollie, then press K in the air for a grab. Land it.";
                case TutorialStep.Spin:
                    return touch ? "Steer left or right in the air to spin. Land a 180." : "Hold A or D in the air to spin. Land a 180.";
                case TutorialStep.Grind:
                    return touch
                        ? "Ollie next to a rail or ledge and tap GRIND. Keep the needle centred with the stick."
                        : "Ollie next to a rail or ledge and press E. Keep the needle centred with A / D.";
                case TutorialStep.Manual:
                    return touch
                        ? "Tap GRIND/MANUAL right as you land, then balance for 1.5 seconds."
                        : "Press E right as you land, then balance with A / D for 1.5 seconds.";
                case TutorialStep.Combo:
                    return "Link 3 tricks without bailing (flip, grind, manual...) and land clean to bank them.";
                default:
                    return "Lesson complete. Heading to the results...";
            }
        }
    }
}
