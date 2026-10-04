using RetroSk8.Game;
using RetroSk8.Level;
using RetroSk8.Replay;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>ResultsScene composition root and view: score, best combo, goals, Tape Tokens, retry, home.</summary>
    public sealed class ResultsView : MonoBehaviour
    {
        private void Start()
        {
            GameBootstrap.ApplyRuntimeSettings();
            Time.timeScale = 1f;
            UIFactory.EnsureEventSystem();
            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Theme.Ink;
            }

            var r = GameSession.LastResult ?? new RunResult { locationName = "Harbor Plaza", locationId = "harbor_plaza" };
            var canvas = UIFactory.CreateCanvas("Results", 0, transform);
            var bg = UIFactory.Panel("Background", canvas.transform, Theme.Ink);
            UIFactory.Stretch(bg.rectTransform);
            var safe = UIFactory.SafeArea(canvas.transform);

            var header = UIFactory.Panel("Header", safe, Theme.Coral);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1500f, 150f));
            string heading = string.IsNullOrEmpty(r.modeLabel) ? "RUN COMPLETE" : r.modeLabel + " COMPLETE";
            var title = UIFactory.Label("Title", header.transform, heading, 80, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Stretch(title.rectTransform);
            UIFactory.Scanlines(header.transform, 0.2f);

            var park = UIFactory.TapeLabel("Park", safe, r.locationName.ToUpperInvariant(), 40, Theme.Tape, -3f);
            UIFactory.Place(park.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(460f, 64f));

            var score = UIFactory.Label("Score", safe, r.score.ToString("N0"), 150, Theme.Tape);
            UIFactory.Place(score.rectTransform, new Vector2(0.5f, 0.71f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 170f));
            if (r.newBest)
            {
                var best = UIFactory.TapeLabel("NewBest", safe, "NEW BEST!", 44, Theme.Teal, 6f);
                UIFactory.Place(best.transform.parent as RectTransform, new Vector2(0.5f, 0.71f), new Vector2(0f, 0.5f), new Vector2(460f, 70f), new Vector2(320f, 72f));
            }

            string bestCombo = r.bestCombo > 0 ? $"{r.bestCombo:N0}  ({r.bestComboLabel})" : "—";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"BEST COMBO   {bestCombo}");
            sb.AppendLine($"COMBOS LANDED   {r.combosBanked}      BAILS   {r.bails}");
            if (r.dailyTargetScore > 0)
                sb.AppendLine(r.score >= r.dailyTargetScore ? $"DAILY TARGET {r.dailyTargetScore:N0}  ■ BEATEN" : $"DAILY TARGET {r.dailyTargetScore:N0}");
            sb.Append($"TAPE TOKENS   +{r.tapeTokensEarned}");
            if (r.tokensFromGoals > 0 || r.tokensFromDaily > 0)
                sb.Append($"   (SCORE {r.tokensFromScore} · GOALS {r.tokensFromGoals} · DAILY {r.tokensFromDaily})");
            sb.Append($"   TOTAL {SaveManager.Data.tapeTokens}");
            var stats = UIFactory.Label("Stats", safe, sb.ToString(), 40, Theme.Cream, TextAnchor.UpperCenter);
            stats.lineSpacing = 1.2f;
            UIFactory.Place(stats.rectTransform, new Vector2(0.5f, 0.61f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1800f, 240f));

            if (r.goalsTotal > 0)
            {
                var goalText = new System.Text.StringBuilder($"GOALS {r.goalsCompleted}/{r.goalsTotal}\n");
                for (int i = 0; i < r.goalDescriptions.Count; i++)
                    goalText.AppendLine((r.goalCompleted[i] ? "■ " : "□ ") + r.goalDescriptions[i]);
                var goals = UIFactory.Label("Goals", safe, goalText.ToString(), 36, Theme.Teal, TextAnchor.UpperCenter, false);
                goals.lineSpacing = 1.15f;
                UIFactory.Place(goals.rectTransform, new Vector2(0.5f, 0.41f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1600f, 220f));
            }

            if (r.newAchievements != null && r.newAchievements.Count > 0)
            {
                var ach = UIFactory.Label("Achievements", safe, "UNLOCKED  ·  " + string.Join("  ·  ", r.newAchievements).ToUpperInvariant(),
                    34, Theme.Tape, TextAnchor.MiddleCenter, false);
                float y = r.goalsTotal > 0 ? 0.205f : 0.41f;
                UIFactory.Place(ach.rectTransform, new Vector2(0.5f, y), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1900f, 50f));
            }

            // Score challenges: how you did against a friend's code, and a code to send back.
            var challenge = GameSession.ActiveChallengeFor(r.locationId);
            if (challenge != null)
            {
                bool beat = r.score > challenge.Target;
                string who = string.IsNullOrEmpty(challenge.From) ? "THE CHALLENGE" : challenge.From + "'S " + challenge.Target.ToString("N0");
                var ch = UIFactory.TapeLabel("ChallengeResult", safe, beat ? "YOU BEAT " + who + "!" : "SHORT OF " + who, 34, beat ? Theme.Teal : Theme.Coral, -2f);
                UIFactory.Place(ch.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -200f), new Vector2(620f, 64f));
            }

            // Career goals or chapters this run finished (paid here, so the tokens total above is before them).
            CareerService.Check();
            var career = CareerService.TakePending();
            if (career.Count > 0)
            {
                string line = career[career.Count - 1];
                if (career.Count > 1) line += $"   (+{career.Count - 1} MORE IN CAREER)";
                var c = UIFactory.Label("Career", safe, line, 28, Theme.Tape, TextAnchor.UpperRight);
                c.horizontalOverflow = HorizontalWrapMode.Wrap;
                UIFactory.Place(c.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -200f), new Vector2(640f, 120f));
            }

            var row = UIFactory.Rect("Buttons", safe);
            UIFactory.Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1200f, 130f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 60f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = layout.childControlWidth = false;

            // Retry keeps the same mode (GameSession.Mode is unchanged) and the same park.
            UIFactory.MakeButton("Retry", row, "RETRY", new Vector2(460f, 120f), Theme.Tape, () => SceneRouter.LoadPark(r.locationId, LocationScene(r.locationId)), 56);
            bool hasMenu = Application.CanStreamedLevelBeLoaded(SceneNames.MainMenu);
            var home = UIFactory.MakeButton("Home", row, "HOME", new Vector2(460f, 120f), Theme.Cream, () => Load(SceneNames.MainMenu), 48);
            home.interactable = hasMenu;

            // A ReplayKit clip of the run, when recording was on (iOS only).
            var clip = ClipRecorder.State;
            if (clip == ClipState.Saving || clip == ClipState.Ready)
            {
                row.sizeDelta = new Vector2(1640f, 130f);
                _shareButton = UIFactory.MakeButton("Share", row, "", new Vector2(460f, 120f), Theme.Teal, ClipRecorder.Share, 44);
                _shareLabel = _shareButton.GetComponentInChildren<Text>();
                UpdateShare();
            }

            // Challenge a friend to beat this score (copies a code to paste anywhere).
            string code = GameSession.Mode == RunMode.TwoMinuteRun && r.score > 0 ? ShareService.ChallengeCode(r.locationId, r.score) : null;
            if (code != null)
            {
                var challengeButton = UIFactory.MakeButton("Challenge", row, "CHALLENGE A FRIEND", new Vector2(460f, 120f), Theme.Coral, null, 34);
                var label = challengeButton.GetComponentInChildren<Text>();
                challengeButton.onClick.AddListener(() =>
                {
                    ShareService.Copy(code);
                    label.text = "CODE COPIED!";
                });
            }

            // Watch this run again in the replay editor (it was saved automatically).
            string replayId = RetroSk8.Replay.ReplayLibrary.LastSavedId;
            var saved = replayId != null ? RetroSk8.Replay.ReplayLibrary.Index.Find(replayId) : null;
            if (saved != null && saved.locationId == r.locationId)
                UIFactory.MakeButton("Watch", row, "WATCH REPLAY", new Vector2(460f, 120f), Theme.Cream, () => ReplaysPanelView.Watch(replayId, r.locationId), 34);

            // Fit however many buttons there are.
            int count = row.childCount;
            float width = count >= 5 ? 340f : count > 2 ? 400f : 460f;
            layout.spacing = count > 2 ? 24f : 60f;
            foreach (Transform child in row) ((RectTransform)child).sizeDelta = new Vector2(width, 120f);
            row.sizeDelta = new Vector2(count * width + (count - 1) * layout.spacing, 130f);
        }

        private Button _shareButton;
        private Text _shareLabel;

        private void Update()
        {
            if (_shareButton != null) UpdateShare();
        }

        private void UpdateShare()
        {
            var state = ClipRecorder.State;
            _shareButton.interactable = state == ClipState.Ready;
            _shareLabel.text = state == ClipState.Ready ? "SHARE CLIP" : state == ClipState.Saving ? "SAVING CLIP..." : "NO CLIP";
        }

        private static string LocationScene(string id) => ParkCatalog.SceneFor(id);

        private static void Load(string scene)
        {
            if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
            else Debug.LogWarning($"[RetroSk8] {scene} is not in Build Settings.");
        }
    }
}
