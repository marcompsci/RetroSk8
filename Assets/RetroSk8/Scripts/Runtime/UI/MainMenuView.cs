using System.Text;
using RetroSk8.Audio;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Level;
using RetroSk8.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>MainMenuScene composition root: Play (park + mode), Customize, Daily Line, Settings.</summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        public ContentRegistry content;

        private RectTransform _safe;
        private GameObject _playPanel;
        private GameObject _settingsPanel;
        private Text _tokens;
        private Text _info;
        private LocationDefinition _selected;
        private Text _contractText;
        private Text _parkLabel;

        private void Start()
        {
            GameBootstrap.ApplyRuntimeSettings();
            Time.timeScale = 1f;
            SaveManager.Load();
            if (content == null) content = DefaultContent.CreateRegistry();
            _selected = content.FindLocation(GameSession.LocationId);

            var audio = AudioManager.Ensure();
            audio.PlayMusic();
            audio.StopAmbience();

            UIFactory.EnsureEventSystem();
            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Theme.Ink;
            }

            var canvas = UIFactory.CreateCanvas("MainMenu", 0, transform);
            var bg = UIFactory.Panel("Background", canvas.transform, Theme.Ink);
            UIFactory.Stretch(bg.rectTransform);
            // Diagonal tape bands: the menu's signature motif.
            Band(canvas.transform, Theme.Coral, new Vector2(0f, -120f), -14f, 150f);
            Band(canvas.transform, Theme.Tape, new Vector2(0f, -270f), -14f, 44f);

            _safe = UIFactory.SafeArea(canvas.transform);
            BuildTitle();
            BuildMainButtons();
            BuildInfoCard();

            var play = UIFactory.Rect("PlayPanel", _safe);
            UIFactory.Stretch(play);
            BuildPlayPanel(play);
            _playPanel = play.gameObject;
            _playPanel.SetActive(false);

            var settings = UIFactory.Rect("SettingsPanel", _safe);
            UIFactory.Stretch(settings);
            settings.gameObject.AddComponent<SettingsPanelView>().Build(settings, () => _settingsPanel.SetActive(false));
            _settingsPanel = settings.gameObject;
            _settingsPanel.SetActive(false);

            RefreshInfo();
        }

        private static void Band(Transform parent, Color color, Vector2 pos, float angle, float height)
        {
            var band = UIFactory.Panel("Band", parent, color);
            UIFactory.Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(4200f, height));
            band.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void BuildTitle()
        {
            var titleBg = UIFactory.Panel("TitlePanel", _safe, Theme.Ink);
            UIFactory.Place(titleBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -40f), new Vector2(980f, 250f));
            var title = UIFactory.Label("Title", titleBg.transform, "RETRO SK8", 190, Theme.Tape, TextAnchor.MiddleLeft);
            title.GetComponent<Outline>().effectDistance = new Vector2(8f, -8f);
            UIFactory.Stretch(title.rectTransform, 20f);
            UIFactory.Scanlines(titleBg.transform, 0.25f);
            var tag = UIFactory.TapeLabel("Tagline", _safe, "ORIGINAL ARCADE SKATE", 34, Theme.Cream, 3f);
            UIFactory.Place(tag.transform.parent as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(620f, -270f), new Vector2(460f, 60f));
        }

        private void BuildMainButtons()
        {
            var col = UIFactory.Rect("Buttons", _safe);
            UIFactory.Place(col, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(80f, 60f), new Vector2(620f, 560f));
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 26f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = layout.childControlHeight = false;

            UIFactory.MakeButton("Play", col, "PLAY", new Vector2(560f, 120f), Theme.Tape, () => _playPanel.SetActive(true), 64);
            UIFactory.MakeButton("Daily", col, "DAILY LINE", new Vector2(560f, 100f), Theme.Teal, StartDaily, 48);
            UIFactory.MakeButton("Customize", col, "CUSTOMIZE", new Vector2(560f, 100f), Theme.Cream, OpenCustomize, 48);
            UIFactory.MakeButton("Settings", col, "SETTINGS", new Vector2(560f, 100f), Theme.Cream, () => _settingsPanel.SetActive(true), 48);
        }

        private void BuildInfoCard()
        {
            var card = UIFactory.Panel("InfoCard", _safe, Theme.InkSoft);
            UIFactory.Place(card.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 60f), new Vector2(980f, 560f));
            _tokens = UIFactory.Label("Tokens", card.transform, "", 48, Theme.Tape, TextAnchor.UpperLeft);
            UIFactory.Place(_tokens.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -30f), new Vector2(900f, 60f));
            _info = UIFactory.Label("Info", card.transform, "", 34, Theme.Cream, TextAnchor.UpperLeft, false);
            _info.lineSpacing = 1.2f;
            UIFactory.Place(_info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -110f), new Vector2(910f, 430f));
        }

        private void RefreshInfo()
        {
            _tokens.text = $"TAPE TOKENS  {SaveManager.Data.tapeTokens}";
            var sb = new StringBuilder();
            foreach (var loc in content.PlayableLocations())
            {
                var rec = SaveManager.Data.Record(loc.id);
                var contract = content.FindContract(loc.id);
                sb.Append($"{loc.displayName.ToUpperInvariant()}   BEST {rec.bestScore:N0}");
                if (contract != null) sb.Append($"   {Stars(SaveManager.ContractStars(loc.id), contract.goals.Count)}");
                sb.AppendLine();
            }

            var dailyPark = DailyPark();
            var daily = DailyLineGenerator.Generate(GameSession.TodayKey, dailyPark.id, ParkCatalog.GapsFor(dailyPark.id));
            bool claimed = SaveManager.DailyClaimed(GameSession.TodayKey);
            sb.AppendLine();
            sb.AppendLine(claimed
                ? $"TODAY'S DAILY LINE · {dailyPark.displayName.ToUpperInvariant()}  ■ CLEARED"
                : $"TODAY'S DAILY LINE · {dailyPark.displayName.ToUpperInvariant()}  (+{content.scoringProfile?.scoring.tokensDailyBonus ?? 20})");
            foreach (var g in daily.Goals) sb.AppendLine("  • " + g.description);
            sb.Append($"  TARGET SCORE {daily.TargetScore:N0}");
            _info.text = sb.ToString();
        }

        private static string Stars(int done, int total)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < total; i++) sb.Append(i < done ? "★" : "☆");
            return sb.ToString();
        }

        /// <summary>Today's Daily Line park: rotates through the playable parks, one per day.</summary>
        private LocationDefinition DailyPark()
        {
            var parks = content.PlayableLocations();
            if (parks.Count == 0) return content.FindLocation(ParkCatalog.HarborPlaza);
            return parks[DailyLineGenerator.PickIndex(GameSession.TodayKey, parks.Count)];
        }

        // ------------------------------------------------------------------ play panel

        private void BuildPlayPanel(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.94f), true);
            UIFactory.Stretch(dim.rectTransform);

            var title = UIFactory.TapeLabel("Title", root, "PICK A SPOT", 64, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(560f, 100f));

            var parks = UIFactory.Rect("Parks", root);
            UIFactory.Place(parks, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1900f, 250f));
            var parkLayout = parks.gameObject.AddComponent<HorizontalLayoutGroup>();
            parkLayout.spacing = 40f;
            parkLayout.childAlignment = TextAnchor.MiddleCenter;
            parkLayout.childControlWidth = parkLayout.childControlHeight = false;
            foreach (var loc in content.locations)
            {
                if (loc == null) continue;
                var l = loc;
                string label = l.isPlayable ? l.displayName.ToUpperInvariant() : l.displayName.ToUpperInvariant() + "\nCOMING SOON";
                var b = UIFactory.MakeButton("Park_" + l.id, parks, label, new Vector2(560f, 220f), l.isPlayable ? Theme.Cream : new Color(0.4f, 0.4f, 0.42f), () => SelectPark(l), 44);
                b.interactable = l.isPlayable;
            }

            _parkLabel = UIFactory.Label("Selected", root, "", 40, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Place(_parkLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -450f), new Vector2(1600f, 60f));

            var modes = UIFactory.Rect("Modes", root);
            UIFactory.Place(modes, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(1900f, 130f));
            var modeLayout = modes.gameObject.AddComponent<HorizontalLayoutGroup>();
            modeLayout.spacing = 40f;
            modeLayout.childAlignment = TextAnchor.MiddleCenter;
            modeLayout.childControlWidth = modeLayout.childControlHeight = false;
            UIFactory.MakeButton("TwoMinute", modes, "TWO-MINUTE RUN", new Vector2(560f, 120f), Theme.Tape, () => StartRun(RunMode.TwoMinuteRun), 46);
            UIFactory.MakeButton("Contract", modes, "SPOT CONTRACT", new Vector2(560f, 120f), Theme.Teal, () => StartRun(RunMode.SpotContract), 46);
            UIFactory.MakeButton("Free", modes, "FREE SKATE", new Vector2(560f, 120f), Theme.Cream, () => StartRun(RunMode.FreeSkate), 46);

            _contractText = UIFactory.Label("ContractGoals", root, "", 32, Theme.Teal, TextAnchor.UpperCenter, false);
            UIFactory.Place(_contractText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -520f), new Vector2(1600f, 180f));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => _playPanel.SetActive(false), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 50f), new Vector2(300f, 90f));

            SelectPark(_selected != null && _selected.isPlayable ? _selected : content.FindLocation(ParkCatalog.HarborPlaza));
        }

        private void SelectPark(LocationDefinition loc)
        {
            _selected = loc;
            var rec = SaveManager.Data.Record(loc.id);
            _parkLabel.text = $"{loc.displayName.ToUpperInvariant()}  ·  BEST {rec.bestScore:N0}";
            var contract = content.FindContract(loc.id);
            if (contract == null) { _contractText.text = ""; return; }
            var done = SaveManager.Data.Contract(loc.id).completedGoalIds;
            var sb = new StringBuilder("SPOT CONTRACT GOALS\n");
            foreach (var g in contract.goals) sb.AppendLine((done.Contains(g.id) ? "■ " : "□ ") + g.description);
            _contractText.text = sb.ToString();
        }

        // ------------------------------------------------------------------ navigation

        private void StartRun(RunMode mode)
        {
            if (_selected == null || !_selected.isPlayable) return;
            GameSession.Mode = mode;
            GameSession.LocationId = _selected.id;
            Load(_selected.sceneName);
        }

        private void StartDaily()
        {
            var park = DailyPark();
            GameSession.Mode = RunMode.DailyLine;
            GameSession.LocationId = park.id;
            Load(park.sceneName);
        }

        private static void OpenCustomize() => Load(SceneNames.Customization);

        private static void Load(string scene)
        {
            if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
            else Debug.LogWarning($"[RetroSk8] {scene} is not in Build Settings. Run 'Retro Sk8 > Setup Project'.");
        }
    }
}
