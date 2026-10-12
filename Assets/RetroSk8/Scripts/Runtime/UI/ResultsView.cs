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

            // Phase 30: the SK8 RATING moves with every result; contests and slams get their own results screens.
            int rating = RatingService.Refresh();
            int ratingGain = RatingService.LastGain;
            if (r.mode == RunMode.Contest && ContestSession.Active) { BuildContest(safe, r, rating, ratingGain); return; }
            if (r.mode == RunMode.HallOfMeat) { BuildSlam(safe, r); return; }

            var header = UIFactory.Panel("Header", safe, Theme.Coral);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1500f, 150f));
            string heading = string.IsNullOrEmpty(r.modeLabel) ? "RUN COMPLETE" : r.modeLabel + " COMPLETE";
            // Phase 30: the run's grade against the park's par (Two-Minute Runs at the ten parks), in the header so it
            // never collides with the challenge, story and assist labels.
            var grade = r.mode == RunMode.TwoMinuteRun ? RetroSk8.Core.SkaterRating.GradeFor(r.locationId, r.score) : RetroSk8.Core.RunGrade.None;
            if (grade != RetroSk8.Core.RunGrade.None) heading += "  ·  " + RetroSk8.Core.SkaterRating.GradeLabel(grade);
            var title = UIFactory.Label("Title", header.transform, heading, grade != RetroSk8.Core.RunGrade.None ? 66 : 80, Theme.Cream, TextAnchor.MiddleCenter);
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

            if (r.assisted)
            {
                // Phase 26: beside the score, opposite NEW BEST (the stats block below is already full on Daily Line runs).
                var assisted = UIFactory.TapeLabel("Assisted", safe, "ASSISTS ON · NOT ON GAME CENTER", 28, Theme.Cream, -4f);
                UIFactory.Place(assisted.transform.parent as RectTransform, new Vector2(0.5f, 0.71f), new Vector2(1f, 0.5f), new Vector2(-460f, 0f), new Vector2(540f, 60f));
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
            // Phase 30: the rating (a fourth line; Daily Line runs already use four, so theirs shows in Records).
            if (r.dailyTargetScore <= 0)
            {
                sb.Append('\n').Append(RatingLine(rating, ratingGain));
                if (grade != RetroSk8.Core.RunGrade.None && grade != RetroSk8.Core.RunGrade.S)
                    sb.Append($"   ·   NEXT GRADE AT {RetroSk8.Core.SkaterRating.NextGradeScore(r.locationId, r.score):N0}");
            }

            var stats = UIFactory.Label("Stats", safe, sb.ToString(), 40, Theme.Cream, TextAnchor.UpperCenter);
            stats.lineSpacing = 1.2f;
            UIFactory.Place(stats.rectTransform, new Vector2(0.5f, 0.61f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1900f, 240f));

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
                if (challenge.Ghost != null)
                {
                    who = (string.IsNullOrEmpty(challenge.From) ? "THE" : challenge.From + "'S") + " GHOST";
                    ShareService.RecordRival(challenge, r.locationName.ToUpperInvariant(), r.score);
                }
                var ch = UIFactory.TapeLabel("ChallengeResult", safe, beat ? "YOU BEAT " + who + "!" : "SHORT OF " + who, 34, beat ? Theme.Teal : Theme.Coral, -2f);
                UIFactory.Place(ch.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -200f), new Vector2(620f, 64f));
            }

            // Story mode: did this run clear the step?
            var storyStep = StoryService.Active;
            if (storyStep != null && storyStep.Objective != RetroSk8.Core.StoryObjective.Skate)
            {
                bool cleared = GameSession.StoryOutroPending == storyStep.Id;
                var st = UIFactory.TapeLabel("StoryResult", safe, cleared ? "STORY CLEARED! HOME FOR WHAT HAPPENS NEXT" : "STORY: NOT YET. RETRY!", 30, cleared ? Theme.Teal : Theme.Coral, 2f);
                UIFactory.Place(st.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -276f), new Vector2(720f, 60f));
            }

            // Career goals or chapters this run finished (paid here, so the tokens total above is before them).
            CareerService.Check();
            var career = CareerService.TakePending();
            // Unlocks, chapters and cleared story steps get a fanfare.
            if (career.Count > 0 || (r.newAchievements != null && r.newAchievements.Count > 0))
                RetroSk8.Audio.AudioManager.Ensure().PlaySfx(RetroSk8.Audio.SfxId.Fanfare, 0.8f);
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

            // Challenge a friend: a ghost code (your whole run, so they race your line) or, failing that, a score code.
            bool timed = GameSession.Mode == RunMode.TwoMinuteRun && r.score > 0;
            string ghostCode = timed ? ShareService.GhostCode(r.locationId, r.score) : null;
            string code = ghostCode ?? (timed ? ShareService.ChallengeCode(r.locationId, r.score) : null);
            if (code != null)
            {
                var challengeButton = UIFactory.MakeButton("Challenge", row, ghostCode != null ? "SEND MY GHOST" : "CHALLENGE A FRIEND", new Vector2(460f, 120f), Theme.Coral, null, 34);
                var label = challengeButton.GetComponentInChildren<Text>();
                challengeButton.onClick.AddListener(() =>
                {
                    ShareService.Copy(code);
                    label.text = ghostCode != null ? "GHOST COPIED!" : "CODE COPIED!";
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

        private static string RatingLine(int rating, int gain) =>
            $"SK8 RATING {rating:N0} · {RetroSk8.Core.SkaterRating.RankName(rating)}" + (gain > 0 ? $"  (+{gain:N0})" : "");

        // ------------------------------------------------------------------ Phase 30: Spot Contest heats

        private void BuildContest(RectTransform safe, RunResult r, int rating, int ratingGain)
        {
            bool done = ContestSession.Finished;
            int heat = ContestSession.HeatScores.Count;
            Header(safe, done ? "CONTEST RESULTS" : $"HEAT {heat} OF {RetroSk8.Core.SpotContest.Heats}", r.locationName);

            var table = ContestSession.Standings();
            int place = RetroSk8.Core.SpotContest.PlaceOf(table);
            // One row per skater: place, name, the three heats, best-two total (cells, so the columns line up).
            float[] colX = { -700f, -560f, -60f, 160f, 380f, 640f };
            string[] heads = { "", "", "HEAT 1", "HEAT 2", "HEAT 3", "BEST 2" };
            for (int c = 0; c < heads.Length; c++) Cell(safe, heads[c], colX[c], 0, Theme.Teal, 30);
            for (int i = 0; i < table.Count; i++)
            {
                var e = table[i];
                var color = e.IsYou ? Theme.Tape : Theme.Cream;
                Cell(safe, RetroSk8.Core.SpotContest.PlaceLabel(i + 1), colX[0], i + 1, color, 36);
                Cell(safe, e.IsYou ? "> " + Short(e.Name) : Short(e.Name), colX[1], i + 1, color, 36, TextAnchor.MiddleLeft);
                for (int h = 0; h < RetroSk8.Core.SpotContest.Heats; h++)
                    Cell(safe, h < e.Heats.Count ? e.Heats[h].ToString("N0") : "-", colX[2 + h], i + 1, color, 36);
                Cell(safe, e.Total.ToString("N0"), colX[5], i + 1, color, 36);
            }

            string status;
            if (done)
            {
                int tokens = ContestSession.FinalTokens;
                status = place > 3 ? $"{RetroSk8.Core.SpotContest.PlaceLabel(place)} PLACE. TOP THREE PAYS. NEW RIVALS NEXT WEEK."
                    : tokens > 0 ? $"{RetroSk8.Core.SpotContest.PlaceLabel(place)} PLACE!  +{tokens} TAPE TOKENS"
                    : $"{RetroSk8.Core.SpotContest.PlaceLabel(place)} PLACE! (THIS PODIUM ALREADY PAID THIS WEEK)";
                if (place <= 3) RetroSk8.Audio.AudioManager.Ensure().PlaySfx(RetroSk8.Audio.SfxId.Fanfare, 0.8f);
            }
            else
            {
                long need = RetroSk8.Core.SpotContest.ToMoveUp(ContestSession.ParkId, ContestSession.WeekKey, ContestSession.HeatScores, ContestSession.PlayerName);
                status = $"THIS HEAT {r.score:N0} · YOU'RE {RetroSk8.Core.SpotContest.PlaceLabel(place)}"
                         + (need > 0 ? $" · {need:N0} NEXT HEAT TO MOVE UP" : place == 1 ? " · HOLD THE LEAD" : "");
            }
            var st = UIFactory.TapeLabel("Status", safe, status, 36, done && place <= 3 ? Theme.Tape : Theme.Cream, -1.5f);
            UIFactory.Place(st.transform.parent as RectTransform, new Vector2(0.5f, 0.27f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 70f));
            var rl = UIFactory.Label("Rating", safe, RatingLine(rating, ratingGain), 30, Theme.Cream, TextAnchor.MiddleCenter, false);
            UIFactory.Place(rl.rectTransform, new Vector2(0.5f, 0.205f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 44f));

            var row = ButtonRow(safe);
            string park = r.locationId;
            if (!done)
                UIFactory.MakeButton("NextHeat", row, $"HEAT {heat + 1}", new Vector2(460f, 120f), Theme.Tape, () => SceneRouter.LoadPark(park, LocationScene(park)), 52);
            else
                UIFactory.MakeButton("Again", row, "ENTER AGAIN", new Vector2(460f, 120f), Theme.Tape, () => { ContestSession.Begin(park); SceneRouter.LoadPark(park, LocationScene(park)); }, 44);
            UIFactory.MakeButton("Home", row, done ? "HOME" : "QUIT CONTEST", new Vector2(460f, 120f), Theme.Cream, () => { ContestSession.End(); Load(SceneNames.MainMenu); }, 40);
        }

        // ------------------------------------------------------------------ Phase 30: Hall of Meat

        private void BuildSlam(RectTransform safe, RunResult r)
        {
            Header(safe, "HALL OF MEAT", r.locationName);
            var score = UIFactory.Label("Score", safe, r.score.ToString("N0"), 130, Theme.Tape);
            UIFactory.Place(score.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 150f));
            if (SlamResults.NewRecord)
            {
                var best = UIFactory.TapeLabel("NewBest", safe, "BIGGEST SLAM YET!", 40, Theme.Teal, 6f);
                UIFactory.Place(best.transform.parent as RectTransform, new Vector2(0.5f, 0.72f), new Vector2(0f, 0.5f), new Vector2(460f, 70f), new Vector2(420f, 72f));
            }
            var slam = SlamResults.Last;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"SLAMS {r.combosBanked}      TOTAL {r.score:N0}");
            if (slam != null && slam.Points > 0)
            {
                sb.AppendLine($"BIGGEST: {slam.Name}  {slam.Points:N0}" + (slam.Chain > 1f ? $"  (CHAIN x{slam.Chain:0.##})" : ""));
                var parts = new System.Text.StringBuilder();
                foreach (var p in slam.Parts) parts.Append(parts.Length > 0 ? "  ·  " : "").Append($"{p.Name} {p.Points:N0}");
                sb.AppendLine(parts.ToString());
            }
            else sb.AppendLine("NO REAL SLAMS. GO FASTER, DROP HIGHER, HIT SOMETHING.");
            var d = SaveManager.Data;
            sb.Append(d.slamBest > 0 ? $"YOUR RECORD: {d.slamBestName} {d.slamBest:N0}" : "");
            var stats = UIFactory.Label("Stats", safe, sb.ToString(), 36, Theme.Cream, TextAnchor.UpperCenter, false);
            stats.lineSpacing = 1.2f;
            UIFactory.Place(stats.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1900f, 260f));
            var tip = UIFactory.Label("Tip", safe, "SPEED, HEIGHT, UNFINISHED SPINS AND HEAD-ON HITS SCORE BIG. SLAMS WITHIN 8 SECONDS CHAIN.", 28, Theme.Teal, TextAnchor.MiddleCenter, false);
            UIFactory.Place(tip.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1900f, 44f));

            var row = ButtonRow(safe);
            string park = r.locationId;
            UIFactory.MakeButton("Retry", row, "AGAIN", new Vector2(460f, 120f), Theme.Tape, () => SceneRouter.LoadPark(park, LocationScene(park)), 52);
            UIFactory.MakeButton("Home", row, "HOME", new Vector2(460f, 120f), Theme.Cream, () => Load(SceneNames.MainMenu), 48);
            string replayId = RetroSk8.Replay.ReplayLibrary.LastSavedId;
            var saved = replayId != null ? RetroSk8.Replay.ReplayLibrary.Index.Find(replayId) : null;
            // Only this session's replay (a session with no slams saves none, and LastSavedId is then an older run).
            if (saved != null && saved.locationId == park && saved.mode == r.modeLabel && saved.score == r.score && r.score > 0)
                UIFactory.MakeButton("Watch", row, "WATCH THE SLAMS", new Vector2(460f, 120f), Theme.Coral, () => ReplaysPanelView.Watch(replayId, park), 34);
        }

        private static string Short(string name) => string.IsNullOrEmpty(name) ? "" : name.Length > 16 ? name.Substring(0, 16) : name;

        /// <summary>A contest table cell: column centre <paramref name="x"/> (from the screen centre), row 0 = headings.</summary>
        private static void Cell(RectTransform safe, string text, float x, int row, Color color, int size, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var l = UIFactory.Label("Cell", safe, text, size, color, anchor, false);
            float width = anchor == TextAnchor.MiddleLeft ? 440f : 220f;
            float left = anchor == TextAnchor.MiddleLeft ? x : x - width * 0.5f;
            UIFactory.Place(l.rectTransform, new Vector2(0.5f, 0.75f), new Vector2(0f, 1f), new Vector2(left, -row * 50f), new Vector2(width, 48f));
        }

        private static void Header(RectTransform safe, string heading, string parkName)
        {
            var header = UIFactory.Panel("Header", safe, Theme.Coral);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1500f, 150f));
            var title = UIFactory.Label("Title", header.transform, heading, 80, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Stretch(title.rectTransform);
            UIFactory.Scanlines(header.transform, 0.2f);
            var park = UIFactory.TapeLabel("Park", safe, (parkName ?? "").ToUpperInvariant(), 40, Theme.Tape, -3f);
            UIFactory.Place(park.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(460f, 64f));
        }

        private static RectTransform ButtonRow(RectTransform safe)
        {
            var row = UIFactory.Rect("Buttons", safe);
            UIFactory.Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1500f, 130f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = layout.childControlWidth = false;
            return row;
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
