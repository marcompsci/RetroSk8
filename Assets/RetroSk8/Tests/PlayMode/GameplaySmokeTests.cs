using System;
using System.Collections;
using NUnit.Framework;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Game;
using RetroSk8.Input;
using RetroSk8.Player;
using RetroSk8.Replay;
using RetroSk8.Save;
using RetroSk8.Level;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RetroSk8.Tests.PlayMode
{
    /// <summary>
    /// End-to-end smoke tests: each test spins up a fresh Harbor Plaza through SkateSceneInstaller,
    /// drives the skater with ScriptedInputSource and checks the core loop.
    /// Runs in the editor (Test Runner → PlayMode) and on devices/Simulator ("Run all in player").
    /// </summary>
    public class GameplaySmokeTests
    {
        // A clear straight lane on the west side of the plaza, heading toward the waterfront.
        private static readonly Vector3 LaneStart = new Vector3(-35f, 0.1f, 28f);
        private static readonly Vector3 LaneHeading = Vector3.back;

        private Scene _scene;
        private SkateSceneInstaller _installer;
        private ScriptedInputSource _input;
        private int _landed;
        private int _banked;
        private int _bails;
        private int _respawns;
        private BailReason _lastBail;
        private static int s_sceneCounter;
        private string _locationId = ParkCatalog.HarborPlaza;

        private PlayerController Player => _installer.Player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _scene = SceneManager.CreateScene("RetroSk8Test_" + (++s_sceneCounter));
            SceneManager.SetActiveScene(_scene);
            GameSession.Mode = RunMode.TwoMinuteRun;
            GameSession.DebugInfiniteTime = false;
            _locationId = ParkCatalog.HarborPlaza;
            SaveManager.UseFile("retrosk8_playmode_test_save.json");
            GhostStore.UseFolder("retrosk8_playmode_test_ghosts");
            GhostStore.DeleteAll();
            yield return Boot(null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            SaveManager.UseFile(null);
            GhostStore.UseFolder(null);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(_scene);
                while (op != null && !op.isDone) yield return null;
            }
        }

        /// <summary>Builds the park in the test scene. The installer is added to an inactive object so fields can be set before Awake.</summary>
        private IEnumerator Boot(Action<ContentRegistry> configure)
        {
            var content = DefaultContent.CreateRegistry();
            configure?.Invoke(content);

            var go = new GameObject("TestInstaller");
            go.SetActive(false);
            _installer = go.AddComponent<SkateSceneInstaller>();
            _installer.content = content;
            _installer.location = content.FindLocationExact(_locationId);
            _installer.loadResultsScene = false;
            _installer.applySavedTuning = false;
            go.SetActive(true);

            _input = new ScriptedInputSource();
            _installer.InputRouter.AddSource(_input);
            Player.Landed += _ => _landed++;
            _installer.Combo.Banked += (_, __, ___) => _banked++;
            _installer.Combo.Bailed += (_, reason) => { _bails++; _lastBail = reason; };
            Player.GetComponent<BailHandler>().Respawned += () => _respawns++;
            yield return null;
        }

        private IEnumerator WaitUntil(Func<bool> condition, float timeout, string what)
        {
            float end = Time.time + timeout;
            while (!condition())
            {
                if (Time.time > end) Assert.Fail($"Timed out after {timeout:0.0}s waiting for: {what} (state {Player.State}, pos {Player.transform.position})");
                yield return null;
            }
        }

        private IEnumerator Seconds(float s)
        {
            float end = Time.time + s;
            while (Time.time < end) yield return null;
        }

        private IEnumerator StartOnLane()
        {
            Player.Teleport(LaneStart, LaneHeading);
            yield return WaitUntil(() => Player.State == SkaterState.Rolling && Player.Speed > 6f, 4f, "rolling at speed on the test lane");
        }

        private IEnumerator Ollie(float holdSeconds)
        {
            _input.JumpHeld = true;
            yield return Seconds(holdSeconds);
            _input.JumpHeld = false;
            yield return WaitUntil(() => Player.State == SkaterState.Airborne, 0.5f, "takeoff");
        }

        // ------------------------------------------------------------------ tests

        [UnityTest]
        public IEnumerator Level_SpawnIsOnGround_AndRailsExist()
        {
            Assert.IsNotNull(_installer.Level.spawnPoint);
            Assert.IsTrue(Physics.Raycast(_installer.Level.spawnPoint.position + Vector3.up, Vector3.down, out var hit, 3f), "no ground under spawn");
            Assert.Less(Mathf.Abs(hit.point.y), 0.1f);
            Assert.GreaterOrEqual(RetroSk8.Level.GrindRail.Active.Count, 10, "expected the Harbor Plaza rails and ledges");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Spawn_LandsAndStartsRolling()
        {
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");
            yield return Seconds(1f);
            Assert.IsTrue(Player.IsGrounded);
            Assert.Greater(Player.Speed, 4f, "auto-cruise should get the skater moving");
            Assert.AreEqual(0, _bails);
        }

        [UnityTest]
        public IEnumerator Ollie_LandsCleanly_WithoutBail()
        {
            yield return StartOnLane();
            yield return Ollie(0.3f);
            yield return WaitUntil(() => _landed > 0 && Player.State == SkaterState.Rolling, 2.5f, "landing");
            Assert.AreEqual(0, _bails, "a straight ollie must not bail");
        }

        [UnityTest]
        public IEnumerator Ollie_CannotDoubleJump()
        {
            yield return StartOnLane();
            yield return Ollie(0.2f);
            float yAfterFirstPop = Player.Body.linearVelocity.y;
            _input.JumpHeld = true;
            yield return null;
            _input.JumpHeld = false;
            yield return null;
            yield return null;
            Assert.LessOrEqual(Player.Body.linearVelocity.y, yAfterFirstPop + 0.01f, "a second pop in the air was applied");
        }

        [UnityTest]
        public IEnumerator FlipTrick_BanksPoints()
        {
            yield return StartOnLane();
            yield return Ollie(0.45f);
            _input.Swipe(SwipeDirection.Up);
            yield return WaitUntil(() => _banked > 0 || _bails > 0, 3f, "combo to bank");
            Assert.AreEqual(0, _bails, $"bailed with {_lastBail}");
            Assert.GreaterOrEqual(_installer.Score.Ledger.Total, 400, "Tide Flip is worth 400");
        }

        [UnityTest]
        public IEnumerator SidewaysLanding_Bails_ThenRespawns()
        {
            yield return StartOnLane();
            yield return Ollie(0.45f);
            // ~90 degrees of air spin at the default 560 deg/s, then let go.
            _input.Steer = new Vector2(1f, 0f);
            yield return Seconds(90f / Player.motor.airSpinRate);
            _input.Steer = Vector2.zero;
            yield return WaitUntil(() => _bails > 0, 3f, "bail on a sideways landing");
            Assert.AreEqual(BailReason.OverRotated, _lastBail);
            yield return WaitUntil(() => _respawns > 0 && Player.State != SkaterState.Bailed, 3f, "respawn after bail");
        }

        [UnityTest]
        public IEnumerator Grind_StartsOnFlatBar_AndScores()
        {
            // The west flat bar runs along z from -14 to 6 at x = -20; start beside it, moving along it.
            Player.Teleport(new Vector3(-19.6f, 0.1f, 5f), Vector3.back);
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "touchdown beside the bar");
            yield return Seconds(0.3f);
            _input.PressAction();
            yield return WaitUntil(() => Player.State == SkaterState.Grinding, 0.5f, "grind to start");

            var grind = Player.GetComponent<GrindController>();
            float end = Time.time + 0.8f;
            while (Time.time < end && Player.State == SkaterState.Grinding)
            {
                _input.Steer = new Vector2(grind.Balance.Lean > 0f ? -1f : 1f, 0f); // perfect balance
                yield return null;
            }
            _input.Steer = Vector2.zero;
            Assert.AreEqual(0, _bails, $"bailed with {_lastBail}");
            Assert.Greater(_installer.Combo.Tracker.BasePoints + _installer.Score.Ledger.Total, 120f, "grind points should accrue");
        }

        [UnityTest]
        public IEnumerator FallingIntoWater_RespawnsOnLand()
        {
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");
            Player.Teleport(new Vector3(0f, 3f, -45f), Vector3.back); // over the harbor water
            yield return WaitUntil(() => _respawns > 0, 5f, "out-of-bounds respawn");
            Assert.AreEqual(BailReason.OutOfBounds, _lastBail);
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "touchdown after respawn");
            Assert.Greater(Player.transform.position.y, -0.5f);
        }

        [UnityTest]
        public IEnumerator Timer_EndsRun_AndProducesResult()
        {
            GameSession.LastResult = null;
            yield return Reboot(RunMode.TwoMinuteRun, c => c.FindLocation("harbor_plaza").runDurationSeconds = 2f);

            RunResult result = null;
            _installer.Run.Finished += r => result = r;
            yield return WaitUntil(() => result != null, 12f, "run to finish");
            Assert.AreSame(result, GameSession.LastResult);
            Assert.AreEqual("harbor_plaza", result.locationId);
            Assert.GreaterOrEqual(result.score, 0);
        }

        // ------------------------------------------------------------------ Phase 3

        /// <summary>Tears down the current park and builds a fresh one in another mode.</summary>
        private IEnumerator Reboot(RunMode mode, Action<ContentRegistry> configure, string locationId = null)
        {
            if (locationId != null) _locationId = locationId;
            foreach (var root in _scene.GetRootGameObjects()) UnityEngine.Object.Destroy(root);
            yield return null;
            GameSession.Mode = mode;
            _landed = _banked = _bails = _respawns = 0;
            yield return Boot(configure);
        }

        [UnityTest]
        public IEnumerator SpotContract_TracksThreeGoals_IntoTheResult()
        {
            yield return Reboot(RunMode.SpotContract, c => c.FindLocation("harbor_plaza").runDurationSeconds = 2f);
            Assert.IsTrue(_installer.Goals.HasGoals);
            Assert.AreEqual(3, _installer.Goals.Tracker.Total);

            RunResult result = null;
            _installer.Run.Finished += r => result = r;
            yield return WaitUntil(() => result != null, 12f, "contract run to finish");
            Assert.AreEqual(3, result.goalsTotal);
            Assert.AreEqual(3, result.goalDescriptions.Count);
            Assert.AreEqual("SPOT CONTRACT", result.modeLabel);
        }

        [UnityTest]
        public IEnumerator DailyLine_BuildsThreeGoals_ForToday()
        {
            yield return Reboot(RunMode.DailyLine, null);
            Assert.AreEqual(3, _installer.Goals.Tracker.Total);
            Assert.IsNotNull(_installer.Goals.Daily);
            Assert.AreEqual(GameSession.TodayKey, _installer.Goals.Daily.Date);
        }

        [UnityTest]
        public IEnumerator FountainGap_ClearsOnLanding_AndScores()
        {
            var gaps = Player.GetComponent<GapTracker>();
            string cleared = null;
            gaps.GapCleared += z => cleared = z.gapId;
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");

            // Drop through the gap volume above the drained fountain onto the bowl floor.
            Player.Teleport(new Vector3(1.5f, 4f, 0f), Vector3.forward);
            yield return WaitUntil(() => cleared != null || _bails > 0, 3f, "gap to clear on landing");
            Assert.AreEqual(0, _bails, $"bailed with {_lastBail}");
            Assert.AreEqual("fountain_gap", cleared);
            yield return WaitUntil(() => _banked > 0, 2f, "gap combo to bank");
            Assert.GreaterOrEqual(_installer.Score.Ledger.Total, 750);
        }

        [UnityTest]
        public IEnumerator Cosmetics_BuyAndEquip_ChangesLoadout()
        {
            var content = _installer.content;
            var deck = content.cosmetics.Find(c => c.id == "deck_buoy_stripes");
            Assert.IsNotNull(deck);
            SaveManager.ResetAll();
            Assert.AreEqual(PurchaseResult.NotEnoughTokens, CosmeticsService.Buy(deck));

            SaveManager.AddTokens(deck.price);
            Assert.AreEqual(PurchaseResult.Ok, CosmeticsService.Buy(deck));
            Assert.AreEqual(0, SaveManager.Data.tapeTokens);
            Assert.IsTrue(CosmeticsService.Equip(deck));
            Assert.AreSame(deck, CosmeticsService.CurrentLoadout(content)[CosmeticSlot.Deck]);

            // The visual accepts the loadout without errors.
            Player.GetComponentInChildren<SkaterVisual>().ApplyLoadout(CosmeticsService.CurrentLoadout(content));
            yield return null;
        }

        // ------------------------------------------------------------------ Phase 4

        /// <summary>Shared checks for every park: spawn on ground, gap ids match the catalog, enough rails, a clean roll-out.</summary>
        private IEnumerator CheckPark(string locationId, int minRails)
        {
            yield return Reboot(RunMode.FreeSkate, null, locationId);
            var level = _installer.Level;
            Assert.AreEqual(locationId, GameSession.LocationId);
            Assert.IsNotNull(level.spawnPoint);
            Assert.IsTrue(Physics.Raycast(level.spawnPoint.position + Vector3.up, Vector3.down, out var hit, 3f), "no ground under spawn");
            Assert.Less(Mathf.Abs(hit.point.y - level.spawnPoint.position.y), 0.2f, "spawn should sit on the floor");
            Assert.IsFalse(level.IsOutOfBounds(level.spawnPoint.position), "spawn must be inside the playable bounds");

            var expected = new System.Collections.Generic.List<string>();
            foreach (var g in ParkCatalog.GapsFor(locationId)) expected.Add(g.Id);
            var actual = new System.Collections.Generic.List<string>();
            foreach (var g in level.gaps) actual.Add(g.gapId);
            CollectionAssert.AreEquivalent(expected, actual, "builder gaps must match ParkCatalog (the Daily Line uses the catalog)");

            Assert.GreaterOrEqual(GrindRail.Active.Count, minRails);

            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");
            yield return Seconds(2f);
            Assert.AreEqual(0, _bails, $"bailed with {_lastBail} at {Player.transform.position}");
            Assert.Greater(Player.Speed, 4f, "auto-cruise should get the skater moving");
        }

        [UnityTest]
        public IEnumerator NeonWarehouse_Builds_AndSkaterRollsOut() => CheckPark(ParkCatalog.NeonWarehouse, 10);

        [UnityTest]
        public IEnumerator RooftopRun_Builds_AndSkaterRollsOut() => CheckPark(ParkCatalog.RooftopRun, 10);

        [UnityTest]
        public IEnumerator Conveyor_CarriesTheSkater()
        {
            yield return Reboot(RunMode.FreeSkate, null, ParkCatalog.NeonWarehouse);
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");
            Player.Teleport(new Vector3(18f, 1.6f, -19f), Vector3.forward);
            yield return WaitUntil(() => Player.IsGrounded && Player.GroundConveyorVelocity != Vector3.zero, 2f, "touchdown on the belt");
            Assert.Greater(Player.GroundConveyorVelocity.z, 0f, "belt should push toward the conveyor gap");
            Assert.AreEqual(0, _bails);
        }

        [UnityTest]
        public IEnumerator RooftopGap_ClearsOnLanding()
        {
            yield return Reboot(RunMode.FreeSkate, null, ParkCatalog.RooftopRun);
            var gaps = Player.GetComponent<GapTracker>();
            string cleared = null;
            gaps.GapCleared += z => cleared = z.gapId;
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");

            // Launch across the drop between the roofs and land on the lower roof.
            Player.Teleport(new Vector3(13.5f, 1f, -4f), Vector3.right);
            Player.GetComponent<Rigidbody>().linearVelocity = new Vector3(9f, 0f, 0f);
            yield return WaitUntil(() => cleared != null || _bails > 0, 3f, "rooftop gap to clear on landing");
            Assert.AreEqual(0, _bails, $"bailed with {_lastBail}");
            Assert.AreEqual(ParkCatalog.RooftopGap, cleared);
        }

        [UnityTest]
        public IEnumerator NewParks_HaveSpotContracts()
        {
            yield return Reboot(RunMode.SpotContract, null, ParkCatalog.NeonWarehouse);
            Assert.AreEqual(3, _installer.Goals.Tracker.Total);
            yield return Reboot(RunMode.SpotContract, null, ParkCatalog.RooftopRun);
            Assert.AreEqual(3, _installer.Goals.Tracker.Total);
        }

        [UnityTest]
        public IEnumerator DailyLine_RunsOnTodaysRotatedPark()
        {
            var content = DefaultContent.CreateRegistry();
            var parks = content.PlayableLocations();
            Assert.AreEqual(3, parks.Count);
            var today = parks[DailyLineGenerator.PickIndex(GameSession.TodayKey, parks.Count)];
            yield return Reboot(RunMode.DailyLine, null, today.id);
            Assert.AreEqual(today.id, _installer.Goals.Daily.LocationId);
            Assert.AreEqual(3, _installer.Goals.Tracker.Total);
        }

        // ------------------------------------------------------------------ Phase 5

        [UnityTest]
        public IEnumerator BestRun_SavesGhost_ThatRacesTheNextRun()
        {
            SaveManager.ResetAll(); // no best score yet, no ghosts
            yield return Reboot(RunMode.TwoMinuteRun, c => c.FindLocationExact(ParkCatalog.HarborPlaza).runDurationSeconds = 4f);
            Assert.IsNull(_installer.Ghost, "no ghost before any best run");

            RunResult result = null;
            _installer.Run.Finished += r => result = r;
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");
            Player.Teleport(new Vector3(1.5f, 4f, 0f), Vector3.forward); // fountain gap: guaranteed points
            yield return WaitUntil(() => _banked > 0, 4f, "gap combo to bank");
            yield return WaitUntil(() => result != null, 12f, "run to finish");

            Assert.IsTrue(result.newBest);
            Assert.Greater(_installer.Recorder.Track.Count, 40, "about 20 samples per second of run");
            var saved = GhostStore.Load(ParkCatalog.HarborPlaza);
            Assert.IsNotNull(saved, "a new best should write the ghost");
            Assert.AreEqual(result.score, saved.Score);

            yield return Reboot(RunMode.TwoMinuteRun, null);
            Assert.IsNotNull(_installer.Ghost, "the saved ghost should appear in the next Two-Minute Run");
            yield return Seconds(1f);
            Assert.Greater(_installer.Ghost.PlaybackTime, 0.5f);
            Assert.IsNull(_installer.Ghost.GetComponentInChildren<Collider>(), "ghosts must never collide");
        }

        [UnityTest]
        public IEnumerator Ghost_StaysOff_InOtherModes_AndWhenHidden()
        {
            var track = new ReplayTrack { LocationId = ParkCatalog.HarborPlaza, Score = 10 };
            for (int i = 0; i < 40; i++)
                track.Add(new ReplayFrame { Time = i * 0.05f, Position = new RVec3(0f, 0f, 20f - i * 0.4f), Rotation = RQuat.Identity, Pose = RQuat.Identity, Body = RQuat.Identity, Board = RQuat.Identity });
            Assert.IsTrue(GhostStore.Save(track));

            yield return Reboot(RunMode.FreeSkate, null);
            Assert.IsNull(_installer.Ghost);

            SaveManager.Data.settings.ghostHidden = true;
            yield return Reboot(RunMode.TwoMinuteRun, null);
            Assert.IsNull(_installer.Ghost);

            SaveManager.Data.settings.ghostHidden = false;
            yield return Reboot(RunMode.TwoMinuteRun, null);
            Assert.IsNotNull(_installer.Ghost);
        }
    }
}
