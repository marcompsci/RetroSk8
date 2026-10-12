using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Save;
using RetroSk8.Scoring;
using RetroSk8.UI;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Phase 30: the SK8 RATING at runtime. Reads the save into <see cref="RatingFacts"/>, keeps the highest rating
    /// reached, and posts it to Game Center when it rises (never from a save that failed its tamper check).
    /// </summary>
    public static class RatingService
    {
        /// <summary>How much the last <see cref="Refresh"/> raised the best rating (0 when it didn't).</summary>
        public static int LastGain { get; private set; }

        public static RatingFacts Facts()
        {
            var d = SaveManager.Data;
            var f = new RatingFacts
            {
                NightLines = new List<string>(d.nightLines ?? new List<string>()),
                StoryChaptersDone = d.story != null ? d.story.ChaptersDone() : 0,
                StoryChapters = Story.Chapters.Length,
                SkateWins = d.skateWins, // only wins with assists off are counted (Phase 26)
                BestStreak = d.streak != null ? d.streak.best : 0,
            };
            foreach (var id in SkaterRating.Parks)
            {
                // Phase 26 rule: a best set with assists on stays off the leaderboards, so it stays out of the rating too.
                if (d.assistedBests != null && d.assistedBests.Contains(id)) continue;
                long best = 0;
                foreach (var r in d.locations) if (r.locationId == id) best = r.bestScore;
                if (best > 0) f.ParkBests[id] = best;
            }
            // City bests count only when set with assists off (RecordSpot / RecordRace).
            foreach (var e in d.ratedSpots) if (e.bestScore > 0) f.SpotBests[e.id] = e.bestScore;
            foreach (var e in d.ratedRaces) if (e.bestTime > 0f) f.RaceBests[e.id] = e.bestTime;

            var catalog = TrickBookService.Catalog;
            if (catalog != null && d.trickBook != null)
            {
                f.TricksInBook = catalog.Count;
                foreach (var info in catalog)
                {
                    var rec = d.trickBook.Find(info.Id);
                    if (rec == null) continue;
                    if (rec.landed > 0) f.TricksLanded++;
                    for (int bit = 0; bit < 4; bit++) if ((rec.claimed & (1 << bit)) != 0) f.TrickGoalsDone++;
                }
            }
            return f;
        }

        public static int Current => SkaterRating.Total(Facts());

        /// <summary>A city spot result, kept for the rating when it was set with assists off.</summary>
        public static void RecordSpot(string spotId, long score)
        {
            if (ActiveAssists.Any || string.IsNullOrEmpty(spotId) || score <= 0) return;
            var list = SaveManager.Data.ratedSpots;
            var e = list.Find(x => x.id == spotId);
            if (e == null) list.Add(new MedalEntry { id = spotId, bestScore = score });
            else e.bestScore = Math.Max(e.bestScore, score);
        }

        /// <summary>A city race time, kept for the rating when it was set with assists off.</summary>
        public static void RecordRace(string raceId, float seconds)
        {
            if (ActiveAssists.Any || string.IsNullOrEmpty(raceId) || seconds <= 0f) return;
            var list = SaveManager.Data.ratedRaces;
            var e = list.Find(x => x.id == raceId);
            if (e == null) list.Add(new MedalEntry { id = raceId, bestTime = seconds });
            else if (e.bestTime <= 0f || seconds < e.bestTime) e.bestTime = seconds;
        }

        /// <summary>Recomputes the rating; when it beats the best, saves it and posts it. Returns the current rating.</summary>
        public static int Refresh(bool save = true)
        {
            int rating = Current;
            var d = SaveManager.Data;
            LastGain = 0;
            if (rating > d.ratingBest)
            {
                LastGain = rating - d.ratingBest;
                d.ratingBest = rating;
                if (save) SaveManager.Save();
            }
            Post();
            return rating;
        }

        /// <summary>Sends the best rating to its board (Game Center keeps the max, so repeating is harmless).</summary>
        public static void Post()
        {
            var d = SaveManager.Data;
            if (d.ratingBest <= 0 || SaveManager.Integrity == SaveIntegrity.Edited || d.restoredFromBackup) return;
            GameCenter.SubmitScore(SkaterRating.LeaderboardId, d.ratingBest);
        }

        /// <summary>Park names for the tips ("HARBOR PLAZA" rather than "harbor_plaza").</summary>
        public static Func<string, string> ParkNames(ContentRegistry content) => id =>
        {
            var loc = content != null ? content.FindLocation(id) : null;
            return loc != null ? loc.displayName.ToUpperInvariant() : id.ToUpperInvariant();
        };
    }

    /// <summary>Phase 30: which deck the wear belongs to, and how worn it is.</summary>
    public static class BoardWearService
    {
        public static string DeckKey(SkaterLook look, CosmeticLoadout loadout)
        {
            if (look != null && look.customBoard && look.board != null) return "art:" + DeckDesign.Key(look.board);
            var deck = loadout?[CosmeticSlot.Deck];
            return deck != null ? "shop:" + deck.id : "shop:default";
        }

        /// <summary>The player's deck wear (0 when switched off). A different deck than last time starts fresh.</summary>
        public static float CurrentLevel(ContentRegistry content)
        {
            var d = SaveManager.Data;
            if (d.wear == null) d.wear = new BoardWearState();
            string key = DeckKey(d.look, content != null ? CosmeticsService.CurrentLoadout(content) : null);
            if (d.wear.UseDeck(key)) SaveManager.Save();
            return d.wear.Level;
        }
    }

    /// <summary>Phase 30: wears the deck as you grind and land, saved at the end of the session.</summary>
    public sealed class WearHook : MonoBehaviour
    {
        private PlayerController _player;
        private GrindController _grind;
        private RunController _run;
        private float _grindStart = -1f;
        private float _grindSeconds;
        private int _landings;

        public void Init(PlayerController player, RunController run)
        {
            _player = player;
            _run = run;
            _grind = player.GetComponent<GrindController>();
            player.Landed += OnLanded;
            if (_grind != null) { _grind.GrindStarted += OnGrindStart; _grind.GrindEnded += OnGrindEnd; }
            if (run != null) run.Finished += OnFinished;
        }

        private void OnLanded(LandingVerdict v) { if (v.Quality != LandingQuality.Bail) _landings++; }
        private void OnGrindStart(GrindRail _) => _grindStart = Time.time;

        private void OnGrindEnd()
        {
            if (_grindStart >= 0f) _grindSeconds += Mathf.Max(0f, Time.time - _grindStart);
            _grindStart = -1f;
        }

        private void OnFinished(RunResult _) => Flush();

        /// <summary>Adds this session's wear to the save (also when leaving early).</summary>
        public void Flush()
        {
            if (_grindSeconds <= 0f && _landings == 0) return;
            SaveManager.Data.wear.Add(_grindSeconds, _landings);
            _grindSeconds = 0f;
            _landings = 0;
            SaveManager.Save();
        }

        private void OnDestroy()
        {
            if (_player != null) _player.Landed -= OnLanded;
            if (_grind != null) { _grind.GrindStarted -= OnGrindStart; _grind.GrindEnded -= OnGrindEnd; }
            if (_run != null) _run.Finished -= OnFinished;
            Flush();
        }
    }

    /// <summary>
    /// Phase 30 HALL OF MEAT: measures every bail (speed going in, the drop, air, unfinished spin, how far the skater
    /// tumbles), scores it with <see cref="HallOfMeat"/>, and replaces the run's score with the slam total.
    /// </summary>
    public sealed class SlamHook : MonoBehaviour
    {
        private PlayerController _player;
        private BailHandler _bail;
        private RunController _run;
        private HudView _hud;
        private Rigidbody _rb;
        private readonly SlamSession _session = new SlamSession();
        private float _clock;

        private float _lastSpeed, _peakY, _lastAirTime, _lastAirYaw;
        private bool _wasAir;
        private bool _tracking;
        private SlamInput _input;
        private Vector3 _lastPos;

        public SlamSession Session => _session;

        /// <summary>Set up before the replay recorder, so the run's score is the slam total by the time it saves.</summary>
        public void Init(PlayerController player, RunController run)
        {
            _player = player;
            _run = run;
            _rb = player.GetComponent<Rigidbody>();
            _bail = player.GetComponent<BailHandler>();
            // Slams slide and tumble further, and stay down a little longer, so they're worth watching.
            player.bailKeepVelocity = 0.85f;
            player.bailSlideDeceleration = 4f;
            if (_bail != null)
            {
                _bail.bailDuration = 2.2f;
                _bail.BailStarted += OnBail;
                _bail.Respawned += OnRespawned;
            }
            run.Finished += OnFinished;
        }

        /// <summary>The HUD is built after the systems; it shows the slam total in place of the trick score.</summary>
        public void AttachHud(HudView hud)
        {
            _hud = hud;
            if (_hud == null) return;
            _hud.ScoreOverride = () => _session.Total;
            _hud.QuietBails = true;
            _hud.SetScoreTag("SLAMS");
        }

        private void Update()
        {
            if (_player == null) return;
            _clock += Time.deltaTime;
            if (_tracking)
            {
                var p = _player.transform.position;
                var step = p - _lastPos;
                step.y = 0f;
                if (step.magnitude < 5f) _input.Slide += step.magnitude; // ignore a teleport
                _lastPos = p;
                return;
            }
            bool air = _player.State == SkaterState.Airborne;
            float y = _player.transform.position.y;
            if (air && !_wasAir) _peakY = y;
            if (air)
            {
                _peakY = Mathf.Max(_peakY, y);
                _lastAirTime = _player.AirTime;
                _lastAirYaw = _player.AirYaw;
            }
            else if (_player.State != SkaterState.Bailed)
            {
                _peakY = y;
                _lastAirTime = 0f;
                _lastAirYaw = 0f;
            }
            _wasAir = air;
            if (_rb != null && _player.State != SkaterState.Bailed) _lastSpeed = _rb.linearVelocity.magnitude;
        }

        private void OnBail(BailReason reason)
        {
            _input = new SlamInput
            {
                Speed = _lastSpeed,
                Drop = Mathf.Max(0f, _peakY - _player.transform.position.y),
                AirTime = _lastAirTime,
                Spin = _lastAirYaw,
                HeadOn = reason == BailReason.Collision,
                OutOfBounds = reason == BailReason.OutOfBounds,
            };
            _lastPos = _player.transform.position;
            _tracking = true;
        }

        private void OnRespawned()
        {
            if (!_tracking) return;
            _tracking = false;
            var r = _session.Add(_input, _clock);
            _wasAir = false;
            _lastAirTime = _lastAirYaw = 0f;
            if (_hud == null) return;
            if (r.Points <= 0) _hud.ShowToast(r.Name ?? "NO SLAM", Theme.Cream, 1.2f);
            else if (r.Points < HallOfMeat.MinSlam) _hud.ShowToast("STUMBLE: HIT IT HARDER", Theme.Cream, 1.4f);
            else _hud.ShowToast($"{r.Name}  {r.Points:N0}" + (r.Chain > 1f ? $"  CHAIN x{r.Chain:0.##}" : ""), Theme.Coral, 2.2f);
        }

        private void OnFinished(RunResult result)
        {
            if (result == null) return;
            if (_tracking) { _tracking = false; _session.Add(_input, _clock); } // a slam still sliding at the end counts
            var d = SaveManager.Data;
            d.slamSessions++;
            var best = _session.Best;
            result.score = _session.Total;
            result.bestCombo = best != null ? best.Points : 0;
            result.bestComboLabel = best != null ? best.Name : "";
            result.combosBanked = _session.Count;
            // Assisted sessions (game speed, balance, landing help) don't set the record, like every other board.
            // The record has its own flag: result.newBest would make the replay recorder save this as the park's ghost.
            bool record = !result.assisted && best != null && best.Points > d.slamBest;
            result.newBest = false;
            SlamResults.NewRecord = record;
            if (record)
            {
                d.slamBest = best.Points;
                d.slamBestName = best.Name;
                GameCenter.SubmitScore(HallOfMeat.LeaderboardId, best.Points);
            }
            SlamResults.Last = best;
            SaveManager.Save();
        }

        private void OnDestroy()
        {
            if (_bail != null) { _bail.BailStarted -= OnBail; _bail.Respawned -= OnRespawned; }
            if (_run != null) _run.Finished -= OnFinished;
        }
    }

    /// <summary>Hands the best slam's breakdown to the Results screen.</summary>
    public static class SlamResults
    {
        public static SlamResult Last;
        public static bool NewRecord;
    }

    /// <summary>
    /// Phase 30 SPOT CONTEST: the three heats in progress (which park, this week's seed, your heat scores). Heats are
    /// separate park loads; Results shows the standings between them.
    /// </summary>
    public static class ContestSession
    {
        public static string ParkId { get; private set; }
        public static int WeekKey { get; private set; }
        public static readonly List<long> HeatScores = new List<long>();
        public static bool Active => !string.IsNullOrEmpty(ParkId);
        public static bool Finished => Active && HeatScores.Count >= SpotContest.Heats;
        public static int HeatNumber => HeatScores.Count + 1;
        /// <summary>Set when the final heat pays out (place, tokens), for the Results screen.</summary>
        public static int FinalPlace { get; private set; }
        public static int FinalTokens { get; private set; }

        /// <summary>A heat was loaded and hasn't finished (a restart or quitting mid-heat counts it as 0).</summary>
        private static bool s_heatOpen;

        /// <summary>Called when a heat's park loads. An unfinished previous heat (restarted) is recorded as 0.</summary>
        public static void HeatLoaded()
        {
            if (!Active || Finished) return;
            if (s_heatOpen) RecordHeat(0);
            s_heatOpen = !Finished;
        }

        public static void Begin(string parkId)
        {
            s_heatOpen = false;
            ParkId = parkId;
            WeekKey = WeeklyEvents.WeekKey(DateTime.Now);
            HeatScores.Clear();
            FinalPlace = 0;
            FinalTokens = 0;
        }

        public static void End()
        {
            s_heatOpen = false;
            ParkId = null;
            HeatScores.Clear();
        }

        public static string PlayerName => SaveManager.Data.settings.playerName;

        public static List<ContestEntry> Standings() => SpotContest.Standings(ParkId, WeekKey, HeatScores, PlayerName);

        /// <summary>Records a finished heat; after the last one, pays the podium and saves your best finish.</summary>
        public static void RecordHeat(long score)
        {
            if (!Active || Finished) return;
            s_heatOpen = false;
            HeatScores.Add(Math.Max(0, score));
            if (!Finished) return;
            var table = Standings();
            FinalPlace = SpotContest.PlaceOf(table);
            FinalTokens = SpotContest.Record(SaveManager.Data.contests, ParkId, FinalPlace, SpotContest.CountedTotal(HeatScores), WeekKey);
            if (FinalTokens > 0) SaveManager.AddTokens(FinalTokens);
            SaveManager.Save();
        }
    }

    /// <summary>Records each contest heat when its run ends.</summary>
    public sealed class ContestHook : MonoBehaviour
    {
        private RunController _run;

        public void Init(RunController run, HudView hud)
        {
            _run = run;
            ContestSession.HeatLoaded();
            // A restart used up the last heat (it counts as 0): go straight to the final standings.
            if (ContestSession.Finished) { _endNow = true; return; }
            run.Finished += OnFinished;
            hud?.SetScoreTag($"HEAT {Mathf.Min(SpotContest.Heats, ContestSession.HeatNumber)}/{SpotContest.Heats}");
        }

        private bool _endNow;

        private void Start()
        {
            if (_endNow && _run != null) _run.EndRunNow();
        }

        private void OnFinished(RunResult r)
        {
            if (r == null) return;
            ContestSession.RecordHeat(r.score);
            r.newBest = false;
        }

        private void OnDestroy()
        {
            if (_run != null) _run.Finished -= OnFinished;
        }
    }
}
