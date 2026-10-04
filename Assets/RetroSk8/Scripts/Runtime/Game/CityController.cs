using System;
using System.Collections.Generic;
using RetroSk8.Audio;
using RetroSk8.Core;
using RetroSk8.Feedback;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Save;
using RetroSk8.Scoring;
using UnityEngine;
using Theme = RetroSk8.UI.Theme;

namespace RetroSk8.Game
{
    /// <summary>
    /// Retro City's open-world layer, so different players get different games out of one map:
    /// explorers find spots and collect hidden tapes, line skaters chase medal challenges at each spot,
    /// and speed skaters race checkpoint routes through the streets. Positions come from
    /// <see cref="RetroCityLayout"/> (shared with the builder, the map and the tests).
    /// Discovery and tapes work in every mode except Party and the lesson; challenges and races are Explore
    /// (Free Skate) only, so they never interfere with a scored run.
    /// </summary>
    public sealed class CityController : MonoBehaviour
    {
        public const float TapePickupRadius = 1.6f;
        public const float MarkerRadius = 2.2f;
        /// <summary>A challenge fails if you skate this far beyond the spot's radius.</summary>
        public const float ChallengeLeaveFactor = 1.3f;

        private PlayerController _player;
        private ComboManager _combo;
        private RunController _run;
        private BailHandler _bail;
        private bool _activities;

        private readonly Dictionary<string, GameObject> _tapes = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _markers = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, GameObject> _raceStarts = new Dictionary<string, GameObject>();
        private Transform _root;
        private GameObject _gate;

        // Challenge state.
        private CitySpot _challenge;
        private float _challengeLeft;
        private long _challengeBest;
        // Race state.
        private RaceRun _race;
        private JamRun _jam;
        /// <summary>Markers and start gates re-arm only after you leave them, so finishing on one doesn't restart it.</summary>
        private string _disarmed;
        private CitySpot _currentSpot;

        public CityProgress Progress => SaveManager.Data.city;
        public bool ActivitiesEnabled => _activities;
        public CitySpot Challenge => _challenge;
        public float ChallengeTimeLeft => Mathf.Max(0f, _challengeLeft);
        public long ChallengeBest => _challengeBest;
        public RaceRun Race => _race;
        public CitySpot CurrentSpot => _currentSpot;
        public Vector3 PlayerPosition => _player != null ? _player.transform.position : Vector3.zero;
        public Vector3 PlayerHeading => _player != null ? _player.Heading : Vector3.forward;
        public bool Busy => _challenge != null || _race != null || _jam != null;
        /// <summary>Today's City Jam while it runs (Phase 15).</summary>
        public JamRun Jam => _jam;

        // Set each frame by CityLifeController (time, weather and street events).
        /// <summary>Clock and weather for the HUD, e.g. "18:40 · RAIN".</summary>
        public string LifeStatus { get; set; } = "";
        /// <summary>Banner for a running street event (shown when no challenge or race is on).</summary>
        public string EventBanner { get; set; }
        /// <summary>Where the HUD arrow points for the street event.</summary>
        public Vector3? EventTarget { get; set; }

        /// <summary>Shows a city toast (street events use this).</summary>
        public void Announce(string text, Color color) => Say(text, color);

        /// <summary>A short message for the city HUD (text, colour).</summary>
        public event Action<string, Color> Toast;
        /// <summary>Progress or state changed (map and HUD redraw).</summary>
        public event Action Changed;

        public static bool AppliesTo(string locationId, RunMode mode) =>
            locationId == ParkCatalog.RetroCity && mode != RunMode.Party && mode != RunMode.Tutorial && mode != RunMode.Duel && mode != RunMode.Replay;

        public void Init(PlayerController player, ComboManager combo, RunController run, bool activities)
        {
            _player = player;
            _combo = combo;
            _run = run;
            _bail = player.GetComponent<BailHandler>();
            _activities = activities;
            _root = new GameObject("CityActivities").transform;

            foreach (var t in RetroCityLayout.Tapes)
                if (!Progress.HasTape(t.Id)) _tapes[t.Id] = MakeTape(t);
            if (_activities)
            {
                foreach (var s in RetroCityLayout.Spots) _markers[s.Id] = MakeMarker(s);
                foreach (var r in RetroCityLayout.Races) _raceStarts[r.Id] = MakeArch("RaceStart_" + r.Id, GatePos(r, 0), GateYaw(r, 0), Palette.TapeYellow);
                _gate = MakeArch("NextGate", Vector3.zero, 0f, Palette.NeonCyan);
                _gate.SetActive(false);
            }
            combo.Banked += OnBanked;
        }

        private void OnDestroy()
        {
            if (_combo != null) _combo.Banked -= OnBanked;
            if (_root != null) Destroy(_root.gameObject);
        }

        private void OnBanked(ComboResult result, string label, LandingQuality quality)
        {
            if (_jam != null && _player != null)
            {
                var stop = _jam.Current;
                var p = _player.transform.position;
                bool inside = stop != null && Near(p, stop.Spot.X, stop.Spot.Z, stop.Spot.Radius);
                if (_jam.AddBanked(result.Points, inside))
                {
                    if (_jam.Finished) { EndJam(); return; }
                    Say($"STOP CLEARED!  NEXT: {_jam.Current.Spot.Name.ToUpperInvariant()}", Theme.Tape);
                    AudioManager.Ensure().PlaySfx(SfxId.GoalComplete, 0.8f);
                    HapticsManager.Play(HapticKind.Success);
                }
                Changed?.Invoke();
            }
            if (_challenge != null && result.Points > _challengeBest)
            {
                _challengeBest = result.Points;
                Changed?.Invoke();
            }
        }

        private void Update()
        {
            if (_player == null || _run == null || _run.IsPaused || _run.IsEnding) return;
            var p = _player.transform.position;
            float dt = Time.deltaTime;

            SpinPickups();
            CheckDiscovery(p);
            CheckTapes(p);
            if (!_activities) return;
            if (_bail != null && _bail.IsBailing) return;

            if (_disarmed != null && FarFromDisarmed(p)) _disarmed = null;
            if (_challenge != null) TickChallenge(p, dt);
            else if (_race != null) TickRace(p, dt);
            else if (_jam != null) TickJam(p, dt);
            else CheckStarts(p);
        }

        // ---------------------------------------------------------------- explore

        private void CheckDiscovery(Vector3 p)
        {
            var spot = RetroCityLayout.SpotAt(p.x, p.z);
            if (spot == _currentSpot) return;
            _currentSpot = spot;
            if (spot == null) { Changed?.Invoke(); return; }
            int tokens = Progress.FindSpot(spot.Id);
            if (tokens > 0)
            {
                SaveManager.AddTokens(tokens);
                Say($"SPOT FOUND: {spot.Name.ToUpperInvariant()}  +{tokens}", Theme.Tape);
                CareerCheck();
                AudioManager.Ensure().PlaySfx(SfxId.GoalComplete, 0.7f);
                HapticsManager.Play(HapticKind.Success);
            }
            Changed?.Invoke();
        }

        private void CheckTapes(Vector3 p)
        {
            string got = null;
            foreach (var kv in _tapes)
            {
                if ((kv.Value.transform.position - (p + Vector3.up * 0.6f)).sqrMagnitude > TapePickupRadius * TapePickupRadius) continue;
                got = kv.Key;
                break;
            }
            if (got == null) return;
            Destroy(_tapes[got]);
            _tapes.Remove(got);
            int tokens = Progress.CollectTape(got);
            WeeklyService.Count(WeeklyCounters.Tapes, 1, save: false);
            SaveManager.AddTokens(tokens);
            Say($"TAPE {Progress.tapes.Count}/{RetroCityLayout.Tapes.Count}  +{tokens}", Theme.Tape);
            CareerCheck();
            AudioManager.Ensure().PlaySfx(SfxId.Bank, 0.8f, 1.4f);
            HapticsManager.Play(HapticKind.Light);
            Changed?.Invoke();
        }

        private void SpinPickups()
        {
            float spin = Time.time * 120f;
            float bob = Mathf.Sin(Time.time * 2.4f) * 0.12f;
            foreach (var t in _tapes.Values)
            {
                t.transform.rotation = Quaternion.Euler(0f, spin, 0f);
                var c = t.transform.GetChild(0);
                c.localPosition = new Vector3(0f, bob, 0f);
            }
        }

        // ---------------------------------------------------------------- starts

        private void CheckStarts(Vector3 p)
        {
            foreach (var s in RetroCityLayout.Spots)
            {
                if (s.Id == _disarmed || !Near(p, s.MarkerX, s.MarkerZ, MarkerRadius)) continue;
                StartChallenge(s);
                return;
            }
            foreach (var r in RetroCityLayout.Races)
            {
                if (r.Id == _disarmed || !Near(p, r.Gates[0], r.Gates[1], RetroCityLayout.GateRadius)) continue;
                StartRace(r);
                return;
            }
        }

        // ---------------------------------------------------------------- spot challenge

        public void StartChallenge(CitySpot spot)
        {
            if (!_activities || Busy || spot == null) return;
            _combo.Discard();
            _challenge = spot;
            _challengeLeft = RetroCityLayout.ChallengeSeconds;
            _challengeBest = 0;
            _disarmed = spot.Id;
            SetWorldMarkersVisible(false);
            Say($"{spot.Name.ToUpperInvariant()}: {spot.ChallengeText.ToUpperInvariant()}", Theme.Cream);
            AudioManager.Ensure().PlaySfx(SfxId.SpecialReady, 0.8f);
            HapticsManager.Play(HapticKind.Medium);
            Changed?.Invoke();
        }

        private void TickChallenge(Vector3 p, float dt)
        {
            float leave = _challenge.Radius * ChallengeLeaveFactor;
            if (!Near(p, _challenge.X, _challenge.Z, leave))
            {
                Say("LEFT THE SPOT: CHALLENGE OVER", Theme.Coral);
                EndChallenge(false);
                return;
            }
            _challengeLeft -= dt;
            if (_challengeLeft > 0f) return;
            if (_combo.HasPendingBank) _combo.BankNow(); // a landed line counts on the buzzer
            EndChallenge(true);
        }

        private void EndChallenge(bool completed)
        {
            var spot = _challenge;
            _challenge = null;
            if (completed)
            {
                var medal = MedalRules.ForScore(_challengeBest, spot.Bronze, spot.Silver, spot.Gold);
                var before = Progress.ChallengeMedal(spot.Id);
                int tokens = Progress.RecordChallenge(spot.Id, _challengeBest, medal);
                if (medal > Medal.None) WeeklyService.Count(WeeklyCounters.CityMedals, 1, save: false);
                if (_challengeBest > 0) GameCenter.SubmitScore(Leaderboards.Challenge(spot.Id), _challengeBest);
                if (tokens > 0) SaveManager.AddTokens(tokens); else SaveManager.Save();
                string extra = tokens > 0 ? $"  +{tokens}" : medal > Medal.None && medal <= before ? "  (BEST: " + MedalRules.Label(before) + ")" : "";
                Say($"{MedalRules.Label(medal)}  {_challengeBest:N0}{extra}", medal > Medal.None ? Theme.Tape : Theme.Coral);
                AudioManager.Ensure().PlaySfx(medal > Medal.None ? SfxId.GoalComplete : SfxId.LandSketchy);
                HapticsManager.Play(medal > Medal.None ? HapticKind.Success : HapticKind.Heavy);
                CareerCheck();
            }
            SetWorldMarkersVisible(true);
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- race

        public void StartRace(CityRace race)
        {
            if (!_activities || Busy || race == null) return;
            _race = new RaceRun(race);
            var p = _player.transform.position;
            _race.TryPass(p.x, p.z); // standing in the start gate: the clock starts now
            if (!_race.Started)
            {
                // Started from the map or a test: put the skater at the line first.
                _player.Teleport(GatePos(race, 0) + Vector3.up * 0.05f, GateHeading(race, 0));
                _race.TryPass(race.Gates[0], race.Gates[1]);
            }
            _disarmed = race.Id;
            SetWorldMarkersVisible(false);
            ShowNextGate();
            Say($"{race.Name.ToUpperInvariant()}: GO!", Theme.Tape);
            AudioManager.Ensure().PlaySfx(SfxId.SpecialReady);
            HapticsManager.Play(HapticKind.Medium);
            Changed?.Invoke();
        }

        /// <summary>Time limit: well past bronze the race just ends.</summary>
        public static float RaceTimeLimit(CityRace race) => race.Bronze * 1.5f;

        private void TickRace(Vector3 p, float dt)
        {
            _race.Tick(dt);
            if (_race.TryPass(p.x, p.z))
            {
                if (_race.Finished) { EndRace(true); return; }
                AudioManager.Ensure().PlaySfx(SfxId.Bank, 0.7f, 1.2f);
                HapticsManager.Play(HapticKind.Light);
                ShowNextGate();
                Changed?.Invoke();
                return;
            }
            if (_race.Elapsed > RaceTimeLimit(_race.Race))
            {
                Say("OUT OF TIME", Theme.Coral);
                EndRace(false);
            }
        }

        /// <summary>Gives up the current race or challenge (pause menu or map).</summary>
        public void Abandon()
        {
            if (_jam != null) { _jam.Abandon(); EndJam(); }
            if (_race != null) EndRace(false);
            if (_challenge != null) EndChallenge(false);
        }

        private void EndRace(bool finished)
        {
            var run = _race;
            _race = null;
            if (_gate != null) _gate.SetActive(false);
            if (finished)
            {
                var medal = run.Result;
                float best = Progress.RaceBest(run.Race.Id);
                int tokens = Progress.RecordRace(run.Race.Id, run.Elapsed, medal) * WeeklyService.RaceTokenFactor;
                WeeklyService.Count(WeeklyCounters.Races, 1, save: false);
                if (medal > Medal.None) WeeklyService.Count(WeeklyCounters.CityMedals, 1, save: false);
                if (medal == Medal.Gold) WeeklyService.Count(WeeklyCounters.RaceGold, 1, save: false);
                GameCenter.SubmitScore(Leaderboards.Race(run.Race.Id), Leaderboards.RaceScore(run.Elapsed));
                if (tokens > 0) SaveManager.AddTokens(tokens); else SaveManager.Save();
                bool record = best <= 0f || run.Elapsed < best;
                Say($"{MedalRules.Label(medal)}  {FormatTime(run.Elapsed)}{(record ? "  NEW BEST" : "")}{(tokens > 0 ? $"  +{tokens}" : "")}",
                    medal > Medal.None ? Theme.Tape : Theme.Coral);
                AudioManager.Ensure().PlaySfx(SfxId.GoalComplete);
                HapticsManager.Play(HapticKind.Success);
                CareerCheck();
            }
            SetWorldMarkersVisible(true);
            Changed?.Invoke();
        }

        private void ShowNextGate()
        {
            if (_gate == null || _race == null || _race.Finished) return;
            int i = _race.NextGate;
            _gate.transform.SetPositionAndRotation(GatePos(_race.Race, i), Quaternion.Euler(0f, GateYaw(_race.Race, i), 0f));
            _gate.SetActive(true);
        }

        /// <summary>World position of the gate the racer must reach next (for the HUD arrow), or null.</summary>
        public Vector3? NextGatePosition =>
            _race != null && !_race.Finished ? GatePos(_race.Race, _race.NextGate)
            : _jam != null && _jam.Phase == JamPhase.Travel && _jam.Current != null ? new Vector3(_jam.Current.Spot.X, 0f, _jam.Current.Spot.Z)
            : (Vector3?)null;

        // ---------------------------------------------------------------- city jam (Phase 15)

        public static int Today => Streaks.DayNumber(DateTime.Now);

        /// <summary>Today's jam route (the same for every player today).</summary>
        public static List<JamStop> TodaysJam() => CityJam.Plan(Today, RetroCityLayout.Spots);

        public void StartJam()
        {
            if (!_activities || Busy) return;
            _combo.Discard();
            _jam = new JamRun(TodaysJam());
            SetWorldMarkersVisible(false);
            Say($"CITY JAM! FIRST STOP: {_jam.Current.Spot.Name.ToUpperInvariant()}", Theme.Tape);
            AudioManager.Ensure().PlaySfx(SfxId.SpecialReady);
            HapticsManager.Play(HapticKind.Medium);
            Changed?.Invoke();
        }

        private void TickJam(Vector3 p, float dt)
        {
            _jam.Tick(dt);
            var stop = _jam.Current;
            if (!_jam.Finished && stop != null)
            {
                if (_jam.Phase == JamPhase.Session && !Near(p, stop.Spot.X, stop.Spot.Z, stop.Spot.Radius * ChallengeLeaveFactor)) _jam.LeftStop();
                else if (_jam.UpdatePosition(Near(p, stop.Spot.X, stop.Spot.Z, stop.Spot.Radius)))
                {
                    _combo.Discard(); // only lines started at the stop count
                    Say($"SESSION! BANK {stop.Target:N0} IN {CityJam.SessionSeconds:0}s", Theme.Cream);
                    AudioManager.Ensure().PlaySfx(SfxId.SpecialReady, 0.7f);
                    HapticsManager.Play(HapticKind.Light);
                    Changed?.Invoke();
                }
            }
            if (_jam.Finished) EndJam();
        }

        private void EndJam()
        {
            var run = _jam;
            _jam = null;
            var medal = run.Result;
            var rec = SaveManager.Data.jam;
            int tokens = CityJam.Record(rec, Today, medal, run.Total);
            if (medal > Medal.None) WeeklyService.Count(WeeklyCounters.CityMedals, 1, save: false);
            if (tokens > 0) SaveManager.AddTokens(tokens); else SaveManager.Save();
            string why = string.IsNullOrEmpty(run.EndReason) ? "ALL STOPS!" : run.EndReason;
            Say($"JAM OVER · {why} · {run.Cleared}/{run.Stops.Count} STOPS · {MedalRules.Label(medal)}{(tokens > 0 ? $"  +{tokens}" : "")}",
                medal > Medal.None ? Theme.Tape : Theme.Coral);
            AudioManager.Ensure().PlaySfx(medal > Medal.None ? SfxId.GoalComplete : SfxId.LandSketchy);
            HapticsManager.Play(medal > Medal.None ? HapticKind.Success : HapticKind.Heavy);
            SetWorldMarkersVisible(true);
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- fast travel

        /// <summary>Map fast travel: only to spots you have already found.</summary>
        public bool TravelTo(CitySpot spot)
        {
            if (spot == null || !Progress.HasSpot(spot.Id)) return false;
            Abandon();
            if (_bail != null && _bail.IsBailing) _bail.RespawnNow();
            _combo.Discard();
            var target = new Vector3(spot.MarkerX, 0.05f, spot.MarkerZ);
            var toward = new Vector3(spot.X - spot.MarkerX, 0f, spot.Z - spot.MarkerZ);
            // Land just beside the marker so it doesn't start the challenge on arrival.
            var heading = toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.forward;
            _disarmed = spot.Id;
            _player.Teleport(target, heading);
            _currentSpot = spot;
            Changed?.Invoke();
            return true;
        }

        // ---------------------------------------------------------------- helpers

        public static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            float s = seconds - m * 60f;
            return $"{m}:{s:00.0}";
        }

        private bool FarFromDisarmed(Vector3 p)
        {
            var spot = RetroCityLayout.FindSpot(_disarmed);
            if (spot != null) return !Near(p, spot.MarkerX, spot.MarkerZ, MarkerRadius * 2f);
            var race = RetroCityLayout.FindRace(_disarmed);
            return race == null || !Near(p, race.Gates[0], race.Gates[1], RetroCityLayout.GateRadius * 1.5f);
        }

        private static bool Near(Vector3 p, float x, float z, float r)
        {
            float dx = p.x - x, dz = p.z - z;
            return dx * dx + dz * dz <= r * r;
        }

        private void Say(string text, Color color) => Toast?.Invoke(text, color);

        /// <summary>City progress feeds the career; announce anything it just finished.</summary>
        private void CareerCheck()
        {
            var u = CareerService.Check();
            if (!u.Any) return;
            var msgs = CareerService.TakePending();
            if (msgs.Count > 0) Say(msgs[msgs.Count - 1], Theme.Coral);
        }

        private void SetWorldMarkersVisible(bool visible)
        {
            foreach (var m in _markers.Values) m.SetActive(visible);
            foreach (var r in _raceStarts.Values) r.SetActive(visible);
        }

        private static Vector3 GatePos(CityRace race, int i)
        {
            float y = GroundHeight(race.Gates[i * 2], race.Gates[i * 2 + 1]);
            return new Vector3(race.Gates[i * 2], y, race.Gates[i * 2 + 1]);
        }

        /// <summary>Gates face along the route (towards the next gate, or from the previous one for the last).</summary>
        private static Vector3 GateHeading(CityRace race, int i)
        {
            int a = i < race.GateCount - 1 ? i : i - 1;
            var d = new Vector3(race.Gates[(a + 1) * 2] - race.Gates[a * 2], 0f, race.Gates[(a + 1) * 2 + 1] - race.Gates[a * 2 + 1]);
            return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
        }

        private static float GateYaw(CityRace race, int i)
        {
            var h = GateHeading(race, i);
            return Mathf.Atan2(h.x, h.z) * Mathf.Rad2Deg;
        }

        /// <summary>Ground under a point (gates and markers sit on whatever is there).</summary>
        private static float GroundHeight(float x, float z)
        {
            if (Physics.Raycast(new Vector3(x, 30f, z), Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0f;
        }

        // ---------------------------------------------------------------- visuals (no colliders)

        private GameObject MakeTape(CityTape t)
        {
            var go = new GameObject("Tape_" + t.Id);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(t.X, t.Y, t.Z);
            var pivot = new GameObject("Bob").transform;
            pivot.SetParent(go.transform, false);
            // A chunky cassette: shell, label and two reels.
            var shell = PrimitiveMeshes.CreateVisual("Shell", PrimitiveType.Cube, pivot, Vector3.zero, new Vector3(0.7f, 0.45f, 0.1f), Palette.TapeYellow);
            shell.GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(Palette.TapeYellow, 1.6f);
            PrimitiveMeshes.CreateVisual("Label", PrimitiveType.Cube, pivot, new Vector3(0f, 0.06f, 0f), new Vector3(0.56f, 0.2f, 0.12f), Palette.Cream);
            foreach (float x in new[] { -0.15f, 0.15f })
            {
                var reel = PrimitiveMeshes.CreateVisual("Reel", PrimitiveType.Cylinder, pivot, new Vector3(x, 0.06f, 0f), new Vector3(0.12f, 0.07f, 0.12f), Palette.Ink);
                reel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            return go;
        }

        private GameObject MakeMarker(CitySpot s)
        {
            var go = new GameObject("Challenge_" + s.Id);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(s.MarkerX, GroundHeight(s.MarkerX, s.MarkerZ), s.MarkerZ);
            var ring = PrimitiveMeshes.CreateVisual("Ring", PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.03f, 0f), new Vector3(MarkerRadius * 2f, 0.02f, MarkerRadius * 2f), Palette.NeonPink);
            ring.GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(Palette.NeonPink, 1.2f);
            var post = PrimitiveMeshes.CreateVisual("Beam", PrimitiveType.Cylinder, go.transform, new Vector3(0f, 2.5f, 0f), new Vector3(0.12f, 2.5f, 0.12f), Palette.NeonPink);
            post.GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(Palette.NeonPink, 2f);
            var star = PrimitiveMeshes.CreateVisual("Star", PrimitiveType.Cube, go.transform, new Vector3(0f, 5.2f, 0f), new Vector3(0.6f, 0.6f, 0.6f), Palette.TapeYellow);
            star.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            star.GetComponent<MeshRenderer>().sharedMaterial = PlaceholderMaterials.GetEmissive(Palette.TapeYellow, 2f);
            return go;
        }

        private GameObject MakeArch(string name, Vector3 pos, float yaw, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            float half = RetroCityLayout.GateRadius * 0.8f, h = 4.2f;
            var mat = PlaceholderMaterials.GetEmissive(color, 2f);
            foreach (float x in new[] { -half, half })
                PrimitiveMeshes.CreateVisual("Post", PrimitiveType.Cube, go.transform, new Vector3(x, h * 0.5f, 0f), new Vector3(0.25f, h, 0.25f), color)
                    .GetComponent<MeshRenderer>().sharedMaterial = mat;
            PrimitiveMeshes.CreateVisual("Bar", PrimitiveType.Cube, go.transform, new Vector3(0f, h, 0f), new Vector3(half * 2f + 0.25f, 0.3f, 0.25f), color)
                .GetComponent<MeshRenderer>().sharedMaterial = mat;
            // Checker strip under the bar so it reads as a start/checkpoint banner.
            for (int i = 0; i < 8; i++)
                PrimitiveMeshes.CreateVisual("Check", PrimitiveType.Cube, go.transform,
                    new Vector3(-half + (i + 0.5f) * (half * 2f / 8f), h - 0.35f, 0f), new Vector3(half * 2f / 8f, 0.4f, 0.12f),
                    i % 2 == 0 ? Palette.Ink : Palette.Cream);
            return go;
        }
    }
}
