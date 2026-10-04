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
        private GameObject _recordsPanel;
        private GameObject _careerPanel;
        private GameObject _createParkPanel;
        private GameObject _codesPanel;
        private GameObject _duelPanel;
        private GameObject _crewPanel;
        private GameObject _weeklyPanel;
        private GameObject _replaysPanel;
        private GameObject _shopPanel;
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
            GameSession.EditPark = false;
            GameSession.Challenge = null;
            RetroSk8.Duel.DuelSession.End(); // back at the menu: any S.K.A.T.E. match is over
            GameSession.CrewRecruitId = null;
            GameSession.ReplayId = null;
            if (content == null) content = DefaultContent.CreateRegistry();
            content = ContentRegistry.WithDefaults(content);
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
            gameObject.AddComponent<NowPlayingView>().Build(_safe, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -40f));

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

            var records = UIFactory.Rect("RecordsPanel", _safe);
            UIFactory.Stretch(records);
            records.gameObject.AddComponent<RecordsPanelView>().Build(records, content, () => _recordsPanel.SetActive(false));
            _recordsPanel = records.gameObject;
            _recordsPanel.SetActive(false);

            var createPark = UIFactory.Rect("CreateParkPanel", _safe);
            UIFactory.Stretch(createPark);
            createPark.gameObject.AddComponent<CreateParkPanelView>().Build(createPark, () => _createParkPanel.SetActive(false));
            _createParkPanel = createPark.gameObject;
            _createParkPanel.SetActive(false);

            var career = UIFactory.Rect("CareerPanel", _safe);
            UIFactory.Stretch(career);
            _careerPanel = career.gameObject;
            _careerPanel.SetActive(false);
            career.gameObject.AddComponent<CareerPanelView>().Build(career, content, () => _careerPanel.SetActive(false), () =>
            {
                _careerPanel.SetActive(false);
                _createParkPanel.SetActive(true);
            });
            _crewPanel = Panel("CrewPanel", r => r.gameObject.AddComponent<CrewPanelView>().Build(r, content, () => _crewPanel.SetActive(false)));
            _weeklyPanel = Panel("WeeklyPanel", r => r.gameObject.AddComponent<WeeklyPanelView>().Build(r, () => _weeklyPanel.SetActive(false)));
            _replaysPanel = Panel("ReplaysPanel", r => r.gameObject.AddComponent<ReplaysPanelView>().Build(r, () => _replaysPanel.SetActive(false)));
            _shopPanel = Panel("ShopPanel", r => r.gameObject.AddComponent<ShopPanelView>().Build(r, content, () => { _shopPanel.SetActive(false); RefreshInfo(); }));
            StoreService.Init(); // also picks up any App Store purchase left unfinished last time

            var duelPanel = UIFactory.Rect("DuelPanel", _safe);
            UIFactory.Stretch(duelPanel);
            duelPanel.gameObject.AddComponent<DuelPanelView>().Build(duelPanel, content, () => _duelPanel.SetActive(false));
            _duelPanel = duelPanel.gameObject;
            _duelPanel.SetActive(false);

            var codesPanel = UIFactory.Rect("CodesPanel", _safe);
            UIFactory.Stretch(codesPanel);
            codesPanel.gameObject.AddComponent<CodesPanelView>().Build(codesPanel, content, () => _codesPanel.SetActive(false));
            _codesPanel = codesPanel.gameObject;
            _codesPanel.SetActive(false);

            // Pay any career chapter finished by the last run.
            CareerService.Check();

            // Game Center (when built in): sign in quietly, then mirror local bests and achievements.
            GameCenter.Authenticate();

            RefreshInfo();
        }

        private bool _gameCenterSynced;

        private void Update()
        {
            // Sign-in finishes asynchronously; mirror local progress once it does.
            if (_gameCenterSynced || !GameCenter.IsAuthenticated) return;
            _gameCenterSynced = true;
            ProgressService.SyncGameCenter(content);
        }

        /// <summary>A full-screen menu panel, built hidden.</summary>
        private GameObject Panel(string name, System.Action<RectTransform> build)
        {
            var rect = UIFactory.Rect(name, _safe);
            UIFactory.Stretch(rect);
            build(rect);
            rect.gameObject.SetActive(false);
            return rect.gameObject;
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
            UIFactory.Place(col, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(80f, 36f), new Vector2(620f, 700f));
            var layout = col.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = layout.childControlHeight = false;

            UIFactory.MakeButton("Play", col, "PLAY", new Vector2(560f, 104f), Theme.Tape, OnPlay, 58);
            UIFactory.MakeButton("Career", col, "CAREER", new Vector2(560f, 70f), Theme.Coral, () => _careerPanel.SetActive(true), 40);
            UIFactory.MakeButton("Skate", col, "S.K.A.T.E. BATTLE", new Vector2(560f, 70f), Theme.Coral, () => _duelPanel.SetActive(true), 38);
            UIFactory.MakeButton("Explore", col, "EXPLORE CITY", new Vector2(560f, 70f), Theme.Teal, StartExplore, 38);
            UIFactory.MakeButton("CreatePark", col, "CREATE-A-PARK", new Vector2(560f, 70f), Theme.Teal, () => _createParkPanel.SetActive(true), 38);
            UIFactory.MakeButton("Daily", col, "DAILY LINE", new Vector2(560f, 70f), Theme.Cream, StartDaily, 38);

            // Two smaller rows: crew, weekly event, replays; lesson, gear + Create-a-Skater, settings.
            SmallRow(col, ("Crew", "CREW", () => _crewPanel.SetActive(true)), ("Weekly", "THIS WEEK", () => _weeklyPanel.SetActive(true)), ("Replays", "REPLAYS", () => _replaysPanel.SetActive(true)));
            SmallRow(col, ("HowTo", "HOW TO", StartTutorial), ("Customize", "SKATER", OpenCustomize), ("Settings", "SETTINGS", () => _settingsPanel.SetActive(true)));
        }

        private static void SmallRow(RectTransform col, params (string name, string label, System.Action click)[] buttons)
        {
            var row = UIFactory.Rect("SmallRow", col);
            row.sizeDelta = new Vector2(560f, 70f);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childControlWidth = h.childControlHeight = false;
            foreach (var b in buttons) UIFactory.MakeButton(b.name, row, b.label, new Vector2(180f, 70f), Theme.Cream, b.click, 28);
        }

        private void BuildInfoCard()
        {
            var card = UIFactory.Panel("InfoCard", _safe, Theme.InkSoft);
            UIFactory.Place(card.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 60f), new Vector2(980f, 560f));
            _tokens = UIFactory.Label("Tokens", card.transform, "", 40, Theme.Tape, TextAnchor.UpperLeft);
            UIFactory.Place(_tokens.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -34f), new Vector2(340f, 60f));
            _info = UIFactory.Label("Info", card.transform, "", 30, Theme.Cream, TextAnchor.UpperLeft, false);
            _info.lineSpacing = 1.1f;
            UIFactory.Place(_info.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -110f), new Vector2(910f, 430f));
            var records = UIFactory.MakeButton("Records", card.transform, "RECORDS", new Vector2(220f, 76f), Theme.Teal, () => _recordsPanel.SetActive(true), 32);
            UIFactory.Place((RectTransform)records.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(220f, 76f));
            var codes = UIFactory.MakeButton("Codes", card.transform, "CODES", new Vector2(180f, 76f), Theme.Cream, () => _codesPanel.SetActive(true), 32);
            UIFactory.Place((RectTransform)codes.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-258f, -22f), new Vector2(180f, 76f));
            var shop = UIFactory.MakeButton("Shop", card.transform, "SHOP", new Vector2(170f, 76f), Theme.Tape, () => _shopPanel.SetActive(true), 32);
            UIFactory.Place((RectTransform)shop.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-452f, -22f), new Vector2(170f, 76f));
        }

        private void RefreshInfo()
        {
            _tokens.text = $"TOKENS  {SaveManager.Data.tapeTokens:N0}";
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
            parkLayout.spacing = 24f;
            parkLayout.childAlignment = TextAnchor.MiddleCenter;
            parkLayout.childControlWidth = parkLayout.childControlHeight = false;
            int parkCount = Mathf.Max(1, content.locations.FindAll(x => x != null).Count);
            float parkWidth = Mathf.Min(560f, (1900f - (parkCount - 1) * 24f) / parkCount);
            foreach (var loc in content.locations)
            {
                if (loc == null) continue;
                var l = loc;
                string label = l.isPlayable ? l.displayName.ToUpperInvariant() : l.displayName.ToUpperInvariant() + "\nCOMING SOON";
                Color tint = !l.isPlayable ? new Color(0.4f, 0.4f, 0.42f) : l.id == ParkCatalog.RetroCity ? Theme.Tape : Theme.Cream;
                var b = UIFactory.MakeButton("Park_" + l.id, parks, label, new Vector2(parkWidth, 220f), tint, () => SelectPark(l), parkCount > 3 ? 36 : 44);
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
            modeLayout.spacing = 30f;
            UIFactory.MakeButton("TwoMinute", modes, "TWO-MINUTE RUN", new Vector2(440f, 120f), Theme.Tape, () => StartRun(RunMode.TwoMinuteRun), 40);
            UIFactory.MakeButton("Contract", modes, "SPOT CONTRACT", new Vector2(440f, 120f), Theme.Teal, () => StartRun(RunMode.SpotContract), 40);
            UIFactory.MakeButton("Free", modes, "FREE SKATE", new Vector2(440f, 120f), Theme.Cream, () => StartRun(RunMode.FreeSkate), 40);
            UIFactory.MakeButton("Party", modes, "PASS & PLAY", new Vector2(440f, 120f), Theme.Coral, () => _partyPanel.SetActive(true), 40);

            var partyRoot = UIFactory.Rect("PartySetup", root);
            UIFactory.Stretch(partyRoot);
            BuildPartySetup(partyRoot);
            _partyPanel = partyRoot.gameObject;
            _partyPanel.SetActive(false);

            _contractText = UIFactory.Label("ContractGoals", root, "", 32, Theme.Teal, TextAnchor.UpperCenter, false);
            UIFactory.Place(_contractText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -520f), new Vector2(1600f, 180f));

            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Coral, () => _playPanel.SetActive(false), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 50f), new Vector2(300f, 90f));

            SelectPark(_selected != null && _selected.isPlayable ? _selected : content.FindLocation(ParkCatalog.HarborPlaza));
        }

        private GameObject _partyPanel;
        private Text _partyPlayers;
        private Text _partyGame;
        private Text _partyRules;

        private void BuildPartySetup(RectTransform root)
        {
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.97f), true);
            UIFactory.Stretch(dim.rectTransform);
            var title = UIFactory.TapeLabel("Title", root, "PASS & PLAY", 64, Theme.Coral, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(560f, 100f));

            var players = UIFactory.Rect("Players", root);
            UIFactory.Place(players, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(900f, 110f));
            var minus = UIFactory.MakeButton("Minus", players, "-", new Vector2(130f, 110f), Theme.Cream, () => ChangePlayers(-1), 64);
            UIFactory.Place((RectTransform)minus.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(130f, 110f));
            var plus = UIFactory.MakeButton("Plus", players, "+", new Vector2(130f, 110f), Theme.Cream, () => ChangePlayers(1), 64);
            UIFactory.Place((RectTransform)plus.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(130f, 110f));
            _partyPlayers = UIFactory.Label("Count", players, "", 52, Theme.Cream, TextAnchor.MiddleCenter);
            UIFactory.Place(_partyPlayers.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 110f));

            var game = UIFactory.MakeButton("Game", root, "", new Vector2(900f, 110f), Theme.Tape, () =>
            {
                GameSession.PartyGame = GameSession.PartyGame == PartyGame.Letters ? PartyGame.ScoreTurns : PartyGame.Letters;
                RefreshParty();
            }, 46);
            UIFactory.Place((RectTransform)game.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(900f, 110f));
            _partyGame = game.GetComponentInChildren<Text>();

            _partyRules = UIFactory.Label("Rules", root, "", 34, Theme.Cream, TextAnchor.UpperCenter, false);
            _partyRules.lineSpacing = 1.2f;
            UIFactory.Place(_partyRules.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -490f), new Vector2(1700f, 260f));

            var start = UIFactory.MakeButton("Start", root, "START", new Vector2(440f, 120f), Theme.Coral, () => StartRun(RunMode.Party), 56);
            UIFactory.Place((RectTransform)start.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 50f), new Vector2(440f, 120f));
            var back = UIFactory.MakeButton("Back", root, "BACK", new Vector2(300f, 90f), Theme.Cream, () => _partyPanel.SetActive(false), 42);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 50f), new Vector2(300f, 90f));
            RefreshParty();
        }

        private void ChangePlayers(int delta)
        {
            GameSession.PartyPlayers = Mathf.Clamp(GameSession.PartyPlayers + delta, PartyRules.MinPlayers, PartyRules.MaxPlayers);
            RefreshParty();
        }

        private void RefreshParty()
        {
            _partyPlayers.text = $"{GameSession.PartyPlayers} PLAYERS";
            bool letters = GameSession.PartyGame == PartyGame.Letters;
            _partyGame.text = letters ? "GAME: LETTERS (TAP TO CHANGE)" : "GAME: SCORE TURNS (TAP TO CHANGE)";
            string park = _selected != null ? _selected.displayName.ToUpperInvariant() : "THE SELECTED PARK";
            _partyRules.text = letters
                ? $"One phone, passed around. The setter banks any combo; everyone else must bank {Mathf.RoundToInt(PartyRules.MatchFactor * 100f)}% of it\nor take a letter. Spell {PartyRules.Word} and you're out. Last skater standing wins.\nPark: {park}"
                : $"One phone, passed around. Each player gets {PartyRules.ScoreTurnSeconds:0} seconds. Highest score wins.\nPark: {park}";
        }

        private void SelectPark(LocationDefinition loc)
        {
            _selected = loc;
            if (_partyRules != null) RefreshParty();
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
            SceneRouter.LoadPark(_selected.id, _selected.sceneName);
        }

        /// <summary>First PLAY on a fresh install offers the lesson once; after that PLAY goes straight to the park picker.</summary>
        private void OnPlay()
        {
            var s = SaveManager.Data.settings;
            if (!s.tutorialDone && _tutorialPrompt == null)
            {
                ShowTutorialPrompt();
                return;
            }
            _playPanel.SetActive(true);
        }

        private GameObject _tutorialPrompt;

        private void ShowTutorialPrompt()
        {
            var root = UIFactory.Rect("TutorialPrompt", _safe);
            UIFactory.Stretch(root);
            _tutorialPrompt = root.gameObject;
            var dim = UIFactory.Panel("Dim", root, new Color(0.07f, 0.075f, 0.09f, 0.9f), true);
            UIFactory.Stretch(dim.rectTransform);
            var card = UIFactory.Panel("Card", root, Theme.Ink, true);
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 560f));
            var title = UIFactory.TapeLabel("Title", card.transform, "NEW HERE?", 60, Theme.Tape, -2f);
            UIFactory.Place(title.transform.parent as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440f, 96f));
            var body = UIFactory.Label("Body", card.transform,
                $"A two-minute lesson covers pushing, ollies, flips, grabs, spins, grinds, manuals and combos.\nFinish it for +{TutorialFlow.RewardTokens} Tape Tokens.",
                38, Theme.Cream, TextAnchor.MiddleCenter, false);
            UIFactory.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(960f, 220f));
            var learn = UIFactory.MakeButton("Learn", card.transform, "LEARN THE BASICS", new Vector2(520f, 110f), Theme.Tape, StartTutorial, 42);
            UIFactory.Place((RectTransform)learn.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-15f, 40f), new Vector2(520f, 110f));
            var skip = UIFactory.MakeButton("Skip", card.transform, "JUST SKATE", new Vector2(380f, 110f), Theme.Cream, () =>
            {
                SaveManager.Data.settings.tutorialDone = true;
                SaveManager.Save();
                _tutorialPrompt.SetActive(false);
                _playPanel.SetActive(true);
            }, 42);
            UIFactory.Place((RectTransform)skip.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(15f, 40f), new Vector2(380f, 110f));
        }

        private void StartTutorial()
        {
            GameSession.Mode = RunMode.Tutorial;
            SceneRouter.LoadPark(ParkCatalog.HarborPlaza, content.FindLocation(ParkCatalog.HarborPlaza).sceneName);
        }

        /// <summary>Open-world Retro City: no timer; spots, tapes, medal challenges, races, map fast travel.</summary>
        private void StartExplore()
        {
            var city = content.FindLocation(ParkCatalog.RetroCity);
            GameSession.Mode = RunMode.FreeSkate;
            SceneRouter.LoadPark(ParkCatalog.RetroCity, city != null ? city.sceneName : SceneNames.RetroCity);
        }

        private void StartDaily()
        {
            var park = DailyPark();
            GameSession.Mode = RunMode.DailyLine;
            SceneRouter.LoadPark(park.id, park.sceneName);
        }

        private static void OpenCustomize() => Load(SceneNames.Customization);

        private static void Load(string scene) => SceneRouter.Load(scene);
    }
}
