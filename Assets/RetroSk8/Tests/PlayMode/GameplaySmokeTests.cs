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
        private string _bailInfo = "";
        private static int s_sceneCounter;
        private string _locationId = ParkCatalog.HarborPlaza;

        private PlayerController Player => _installer.Player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Every frame advances game time by exactly 1/60 s, however fast the editor really runs. Unity throttles a
            // background editor to a few frames a second, and timing-based tests (holds, buffers, spins) then flaked.
            Time.captureDeltaTime = 1f / 60f;
            _scene = SceneManager.CreateScene("RetroSk8Test_" + (++s_sceneCounter));
            SceneManager.SetActiveScene(_scene);
            GameSession.Mode = RunMode.TwoMinuteRun;
            GameSession.DebugInfiniteTime = false;
            GameSession.EditPark = false;
            _locationId = ParkCatalog.HarborPlaza;
            SaveManager.UseFile("retrosk8_playmode_test_save.json");
            GhostStore.UseFolder("retrosk8_playmode_test_ghosts");
            GhostStore.DeleteAll();
            ReplayLibrary.UseFolder("retrosk8_playmode_test_replays");
            ReplayLibrary.DeleteAll();
            GameSession.CrewRecruitId = null;
            yield return ClearLeftovers();
            yield return Boot(null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            GameSession.EditPark = false;
            GameSession.Challenge = null;
            RetroSk8.Duel.DuelSession.End();
            SaveManager.UseFile(null);
            GhostStore.UseFolder(null);
            ReplayLibrary.UseFolder(null);
            GameSession.CrewRecruitId = null;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(_scene);
                while (op != null && !op.isDone) yield return null;
            }
        }

        /// <summary>Builds the park in the test scene. The installer is added to an inactive object so fields can be set before Awake.</summary>
        private IEnumerator Boot(Action<ContentRegistry> configure)
        {
            // Counters are fixture fields shared by every test: start each skater from zero (a bail counted by the
            // previous test once failed "Spawn_LandsAndStartsRolling").
            _landed = _banked = _bails = _respawns = 0;
            _lastBail = BailReason.None;
            _bailInfo = "";
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
            _installer.InputRouter.IgnoreDevices(); // only the script drives the skater
            // Only this test's skater counts: a skater left over from an earlier test (another scene that
            // wasn't unloaded) shares this fixture's counters through its own subscriptions.
            var me = _installer;
            Player.Landed += _ => { if (_installer == me) _landed++; };
            _installer.Combo.Banked += (_, __, ___) => { if (_installer == me) _banked++; };
            _installer.Combo.Bailed += (_, reason) =>
            {
                if (_installer != me) return;
                _bails++;
                _lastBail = reason;
                _bailInfo = $"air yaw {Player.AirYaw:0}, air time {Player.AirTime:0.00}, at {Player.transform.position}, t={Time.timeSinceLevelLoad:0.00}";
            };
            Player.GetComponent<BailHandler>().Respawned += () => { if (_installer == me) _respawns++; };
            yield return null;
        }

        /// <summary>Removes skaters and installers that outlived their test (they'd roll around and bump ours).</summary>
        private static IEnumerator ClearLeftovers()
        {
            bool any = false;
            foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerController>()) { UnityEngine.Object.Destroy(p.gameObject); any = true; }
            foreach (var i in UnityEngine.Object.FindObjectsByType<SkateSceneInstaller>()) { UnityEngine.Object.Destroy(i.gameObject); any = true; }
            if (any) yield return null;
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

        // ------------------------------------------------------------------ Phase 17: performance

        /// <summary>Where the perf numbers go (read by the remote bridge; not a pass/fail on its own).</summary>
        public const string PerfReportPath = "Temp/RetroSk8PerfReport.txt";

        /// <summary>
        /// Scene budget per park (Phase 17 performance pass): triangles actually drawn, renderers and materials, with
        /// the biggest meshes, written to Temp/RetroSk8PerfReport.txt. Fails if a park goes over budget for a phone.
        /// Garbage per frame and frame time are left to the device Profiler: in the editor its own allocations and
        /// background throttling swamp both (measured: switching off every Retro Sk8 script didn't lower the count).
        /// </summary>
        [UnityTest]
        public IEnumerator Perf_SceneBudget_StaysInBudget()
        {
            const long MaxTriangles = 150000;
            const int MaxMaterials = 80;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("PERF " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + $"  (scene budget per park; limits {MaxTriangles:N0} triangles, {MaxMaterials} materials)");
            var problems = new System.Collections.Generic.List<string>();
            foreach (var park in new[] { ParkCatalog.HarborPlaza, ParkCatalog.RetroCity, ParkCatalog.MoonlightPier, ParkCatalog.NeonWarehouse, ParkCatalog.DriveIn })
            {
                yield return Reboot(RunMode.FreeSkate, null, park);
                yield return WaitUntil(() => Player.State == SkaterState.Rolling, 3f, park + " touchdown");
                for (int i = 0; i < 30; i++) yield return null; // city traffic, pedestrians and effects spawn in

                int renderers = 0;
                long tris = 0;
                var materials = new System.Collections.Generic.HashSet<Material>();
                var byMesh = new System.Collections.Generic.Dictionary<string, (int count, long tris)>();
                foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    renderers++;
                    foreach (var m in r.sharedMaterials) if (m != null) materials.Add(m);
                    var f = r.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null) continue;
                    // Static batching points every batched renderer at one shared combined mesh; only its own
                    // submesh range (one per material) is drawn for it, so count just that range.
                    int first = 0, last = f.sharedMesh.subMeshCount;
                    if (r.isPartOfStaticBatch)
                    {
                        first = r.subMeshStartIndex;
                        last = Mathf.Min(last, first + r.sharedMaterials.Length);
                    }
                    long t = 0;
                    for (int sub = first; sub < last; sub++) t += (long)f.sharedMesh.GetIndexCount(sub) / 3;
                    tris += t;
                    string key = r.isPartOfStaticBatch ? "batched " + BaseName(r.name) : f.sharedMesh.name;
                    byMesh.TryGetValue(key, out var acc);
                    byMesh[key] = (acc.count + 1, acc.tris + t);
                }
                sb.AppendLine($"{park,-16} renderers {renderers,5}   materials {materials.Count,4}   tris {tris,10:N0}");
                var top = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, (int count, long tris)>>(byMesh);
                top.Sort((a, b) => b.Value.tris.CompareTo(a.Value.tris));
                for (int k = 0; k < top.Count && k < 6; k++)
                    sb.AppendLine($"    {top[k].Key,-28} x{top[k].Value.count,-5} {top[k].Value.tris,10:N0} tris");
                if (tris > MaxTriangles) problems.Add($"{park} draws {tris:N0} triangles");
                if (materials.Count > MaxMaterials) problems.Add($"{park} uses {materials.Count} materials");
            }
            _input.Steer = Vector2.zero;
            try { System.IO.Directory.CreateDirectory("Temp"); System.IO.File.WriteAllText(PerfReportPath, sb.ToString()); }
            catch (System.IO.IOException) { }
            Debug.Log("[RetroSk8] " + sb);
            Assert.AreEqual(0, problems.Count, string.Join("; ", problems) + $" (see {PerfReportPath})");
        }

        /// <summary>"Tree_12 (3)" → "Tree", so the report groups similar objects.</summary>
        private static string BaseName(string name)
        {
            int cut = name.IndexOfAny(new[] { '_', ' ', '(' });
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        [Test]
        public void LowPolyPrimitives_MatchUnitySizes_AndFaceOutward()
        {
            foreach (var type in new[] { PrimitiveType.Sphere, PrimitiveType.Capsule })
            {
                var mesh = RetroSk8.Level.PrimitiveMeshes.Get(type);
                var size = mesh.bounds.size;
                float height = type == PrimitiveType.Capsule ? 2f : 1f;
                Assert.AreEqual(1f, size.x, 0.01f, $"{type} width");
                Assert.AreEqual(height, size.y, 0.01f, $"{type} height");
                var tris = mesh.triangles;
                var verts = mesh.vertices;
                Assert.Less(tris.Length / 3, 300, $"{type} should be low-poly");
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                    var mid = (a + b + c) / 3f;
                    float half = (height - 1f) / 2f;
                    var outward = mid - new Vector3(0f, Mathf.Clamp(mid.y, -half, half), 0f);
                    Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), outward), 0f, $"{type} triangle {i / 3} faces inward");
                }
            }
        }

        [UnityTest]
        public IEnumerator Lessons_PutTheSkaterOnSolidGroundAtTheirSpot()
        {
            try
            {
                foreach (var lesson in TrickLessons.All)
                {
                    GameSession.LessonId = lesson.Id;
                    yield return Reboot(RunMode.FreeSkate, null, lesson.ParkId);
                    for (int i = 0; i < 5; i++) yield return null;
                    yield return WaitUntil(() => Player.State == SkaterState.Rolling, 3f, lesson.Id + " touchdown");
                    var p = Player.transform.position;
                    float flat = new Vector2(p.x - lesson.X, p.z - lesson.Z).magnitude;
                    Assert.Less(flat, 6f, $"{lesson.Id}: skater should start at the lesson spot (is at {p})");
                    Assert.Greater(p.y, -0.5f, $"{lesson.Id}: skater fell through ({p})");
                    Assert.AreEqual(0, _bails, $"{lesson.Id}: bailed at the start ({_bailInfo})");
                }
            }
            finally
            {
                GameSession.LessonId = null;
            }
        }

        [UnityTest]
        public IEnumerator Spawn_LandsAndStartsRolling()
        {
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");
            yield return Seconds(1f);
            Assert.IsTrue(Player.IsGrounded);
            Assert.Greater(Player.Speed, 4f, "auto-cruise should get the skater moving");
            Assert.AreEqual(0, _bails, $"bailed with {_lastBail} ({_bailInfo})");
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
            // Spin to ~90 degrees, then let go. Measured on the physics step, not by wall-clock time: a throttled
            // editor (Unity in the background) runs few frames, and a timed hold overshot to a clean 180.
            _input.Steer = new Vector2(1f, 0f);
            float spinEnd = Time.time + 1f;
            while (Mathf.Abs(Player.AirYaw) < 90f && Time.time < spinEnd) yield return new WaitForFixedUpdate();
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
            yield return null;
            Assert.AreEqual(SurfaceKind.Rubber, Player.GetComponent<RetroSk8.Audio.SkaterAudio>().CurrentSurface, "belts should sound like belts");
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
            Assert.AreEqual(ParkCatalog.All.Length, parks.Count);
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

        // ------------------------------------------------------------------ Phase 6

        [UnityTest]
        public IEnumerator Tutorial_HasNoTimer_AndCoachesThePush()
        {
            yield return Reboot(RunMode.Tutorial, null);
            var coach = UnityEngine.Object.FindAnyObjectByType<RetroSk8.UI.TutorialCoach>();
            Assert.IsNotNull(coach, "the tutorial run should show the coaching card");
            Assert.AreEqual(TutorialStep.Push, coach.Flow.Step);
            // Auto-cruise is above the push threshold, so rolling for a couple of seconds completes step 1.
            yield return WaitUntil(() => coach.Flow.Step == TutorialStep.Ollie, 5f, "push step to complete");
            Assert.IsFalse(_installer.Run.IsEnding, "the lesson has no time limit");
        }

        [UnityTest]
        public IEnumerator Surfaces_AreRecognized()
        {
            Assert.AreEqual(SurfaceKind.Wood, SurfaceLookup.Classify(Palette.Plywood));
            Assert.AreEqual(SurfaceKind.Metal, SurfaceLookup.Classify(Palette.Metal));
            Assert.AreEqual(SurfaceKind.Concrete, SurfaceLookup.Classify(Palette.Paving));
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");
            yield return Seconds(0.3f);
            Assert.AreEqual(SurfaceKind.Concrete, Player.GetComponent<RetroSk8.Audio.SkaterAudio>().CurrentSurface, "Harbor Plaza paving is concrete");
        }

        // ------------------------------------------------------------------ Phase 7: new tricks

        [UnityTest]
        public IEnumerator LipStall_OnCoping_DropsBackIn()
        {
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");
            // Just below the coping of the east north quarter pipe, coming up square to it.
            Player.Teleport(new Vector3(14f, 3.0f, 33.5f), Vector3.forward);
            Player.Body.linearVelocity = new Vector3(0f, 0.5f, 0.5f);
            var lip = Player.GetComponent<LipController>();
            Assert.IsTrue(lip.TryStart(StickZone.Neutral), "should lock onto the coping");
            Assert.AreEqual(SkaterState.LipStall, Player.State);
            yield return Seconds(1f);
            Assert.Greater(_installer.Combo.Tracker.BasePoints, LipRules.StartPoints, "points accrue while stalled");
            _input.PressAction(); // drop back in
            yield return WaitUntil(() => Player.State == SkaterState.Rolling || _bails > 0, 4f, "drop-in landing");
            Assert.AreEqual(0, _bails, $"bailed with {_lastBail}");
            yield return WaitUntil(() => _banked > 0, 3f, "lip combo to bank");
        }

        [UnityTest]
        public IEnumerator Wallride_AlongEastWall()
        {
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");
            var wall = Player.GetComponent<WallController>();
            bool rode = false;
            wall.WallStarted += m => rode |= m == WallMove.Wallride;
            Player.Teleport(new Vector3(45.5f, 1.2f, 20f), Vector3.back);
            Player.Body.linearVelocity = new Vector3(2.5f, 2f, -8f); // skimming into the wall
            _input.PressAction();                                     // arm the wall trick
            yield return WaitUntil(() => rode || _bails > 0, 1f, "wallride to start");
            Assert.IsTrue(rode, "glancing contact with the action pressed should wallride");
            yield return WaitUntil(() => Player.State == SkaterState.Rolling || _bails > 0, 5f, "landing after the wallride");
            Assert.AreEqual(0, _bails, $"bailed with {_lastBail}");
        }

        [UnityTest]
        public IEnumerator Revert_AfterRampLanding_KeepsComboOpen()
        {
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");
            // Drop onto the quarter-pipe face heading down it, as if coming back from a ramp air.
            Player.Teleport(new Vector3(-14f, 2.2f, 32.2f), Vector3.back);
            Player.Body.linearVelocity = new Vector3(0f, -2f, -1f);
            yield return WaitUntil(() => _landed > 0 || _bails > 0, 3f, "ramp landing");
            Assert.AreEqual(0, _bails, $"bailed with {_lastBail}");
            Vector3 before = Player.Heading;
            _input.Swipe(SwipeDirection.Right);
            yield return null;
            yield return null;
            Assert.Less(Vector3.Dot(before, Player.Heading), -0.5f, "revert spins the board around");
            bool hasRevert = false;
            foreach (var e in _installer.Combo.Tracker.Entries) hasRevert |= e.TrickId == RevertRules.Id;
            Assert.IsTrue(hasRevert, "the revert joins the open combo");
        }

        [UnityTest]
        public IEnumerator PassAndPlay_SetLine_HandsToNextPlayer()
        {
            GameSession.PartyGame = PartyGame.Letters;
            GameSession.PartyPlayers = 2;
            yield return Reboot(RunMode.Party, null);
            var party = _installer.Party;
            Assert.IsNotNull(party);
            Assert.AreEqual(PartyPhase.Handoff, party.Rules.Phase);
            party.Ready();
            Assert.AreEqual(PartyPhase.Playing, party.Rules.Phase);
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "touchdown at the spawn");

            Player.Teleport(new Vector3(1.5f, 4f, 0f), Vector3.forward); // fountain gap: a guaranteed combo
            yield return WaitUntil(() => party.Rules.Phase == PartyPhase.Handoff || _bails > 0, 6f, "the set to finish");
            Assert.AreEqual(0, _bails, $"bailed with {_lastBail}");
            Assert.AreEqual(1, party.Rules.CurrentIndex, "player 2 now has to match");
            Assert.Greater(party.Rules.Target, 0);
        }

        [UnityTest]
        public IEnumerator VisualFx_AreBuilt_AndSkaterHasLimbs()
        {
            var fx = UnityEngine.Object.FindAnyObjectByType<VisualFx>();
            Assert.IsNotNull(fx);
            Assert.IsNotNull(fx.Dust, "dust is on unless visual effects are set to low");
            var visual = Player.GetComponentInChildren<SkaterVisual>();
            Assert.IsNotNull(visual.transform.Find("Pose/Body/Hips/HipL/Knee"), "jointed legs");
            visual.SetCrouch(1f);
            yield return null;
            visual.SetCrouch(0f);
        }

        // ------------------------------------------------------------------ Phase 8

        [UnityTest]
        public IEnumerator SunsetBowls_Builds_AndSkaterRollsOut() => CheckPark(ParkCatalog.SunsetBowls, 5);

        [UnityTest]
        public IEnumerator RetroCity_Builds_AndSkaterRollsOut() => CheckPark(ParkCatalog.RetroCity, 20);

        private IEnumerator BootCity(RunMode mode)
        {
            SaveManager.Data.city = new CityProgress();
            yield return Reboot(mode, null, ParkCatalog.RetroCity);
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");
        }

        [UnityTest]
        public IEnumerator City_TapesAndSpots_AreFound_AndChallengeStartsAtMarker()
        {
            yield return BootCity(RunMode.FreeSkate);
            var city = _installer.City;
            Assert.IsNotNull(city, "Explore in Retro City runs the city layer");
            Assert.IsTrue(city.ActivitiesEnabled);
            int tokens = SaveManager.Data.tapeTokens;

            var tape = RetroCityLayout.Tapes[2]; // on the west street
            Player.Teleport(new Vector3(tape.X, 0.1f, tape.Z), Vector3.forward);
            yield return WaitUntil(() => city.Progress.HasTape(tape.Id), 1f, "tape pickup");
            Assert.AreEqual(tokens + CityProgress.TapeTokens, SaveManager.Data.tapeTokens);

            var spot = RetroCityLayout.FindSpot("parking_lot");
            Player.Teleport(new Vector3(spot.MarkerX, 0.1f, spot.MarkerZ), Vector3.forward);
            yield return WaitUntil(() => city.Progress.HasSpot(spot.Id), 1f, "spot discovery");
            yield return WaitUntil(() => city.Challenge == spot, 1f, "challenge to start on its marker");

            Player.Teleport(new Vector3(-35f, 0.1f, -20f), Vector3.forward); // leave the spot
            yield return WaitUntil(() => city.Challenge == null, 1f, "leaving the spot to end the challenge");
            Assert.AreEqual(Medal.None, city.Progress.ChallengeMedal(spot.Id));
        }

        [UnityTest]
        public IEnumerator City_Race_PassesGatesInOrder_AndRecordsATime()
        {
            yield return BootCity(RunMode.FreeSkate);
            var city = _installer.City;
            var race = RetroCityLayout.FindRace("canal_cut");
            city.StartRace(race);
            Assert.IsNotNull(city.Race);
            Assert.AreEqual(1, city.Race.NextGate, "starting puts you through the start gate");

            // Skipping a gate does nothing.
            Player.Teleport(new Vector3(race.Gates[4], 0.1f, race.Gates[5]), Vector3.forward);
            yield return Seconds(0.2f);
            Assert.AreEqual(1, city.Race.NextGate);

            for (int i = 1; i < race.GateCount; i++)
            {
                var gate = city.NextGatePosition.Value;
                Player.Teleport(gate + Vector3.up * 0.1f, Vector3.forward);
                int index = i;
                yield return WaitUntil(() => city.Race == null || city.Race.NextGate > index, 1f, "gate " + i);
            }
            Assert.IsNull(city.Race, "race finished");
            Assert.Greater(city.Progress.RaceBest(race.Id), 0f);
            Assert.AreNotEqual(Medal.None, city.Progress.RaceMedal(race.Id));
        }

        [UnityTest]
        public IEnumerator City_TimedRun_FindsSpots_ButHasNoChallenges()
        {
            yield return BootCity(RunMode.TwoMinuteRun);
            var city = _installer.City;
            Assert.IsNotNull(city);
            Assert.IsFalse(city.ActivitiesEnabled);
            var spot = RetroCityLayout.FindSpot("parking_lot");
            Player.Teleport(new Vector3(spot.MarkerX, 0.1f, spot.MarkerZ), Vector3.forward);
            yield return WaitUntil(() => city.Progress.HasSpot(spot.Id), 1f, "spot discovery");
            yield return Seconds(0.3f);
            Assert.IsNull(city.Challenge, "no challenges in a scored run");
        }

        [UnityTest]
        public IEnumerator City_FastTravel_OnlyToFoundSpots()
        {
            yield return BootCity(RunMode.FreeSkate);
            var city = _installer.City;
            var spot = RetroCityLayout.FindSpot("backyard_pool");
            Assert.IsFalse(city.TravelTo(spot), "unfound spots can't be travelled to");
            city.Progress.FindSpot(spot.Id);
            Assert.IsTrue(city.TravelTo(spot));
            yield return null;
            var p = Player.transform.position;
            Assert.Less(new Vector2(p.x - spot.MarkerX, p.z - spot.MarkerZ).magnitude, 1f);
            yield return Seconds(0.3f);
            Assert.AreEqual(0, _bails, $"bailed with {_lastBail} after fast travel");
            Assert.IsNull(city.Challenge, "arriving doesn't start the challenge");
        }

        [UnityTest]
        public IEnumerator PhotoMode_RestoresTheGameplayCamera()
        {
            var cam = Camera.main;
            var rig = UnityEngine.Object.FindAnyObjectByType<CameraRig>();
            _installer.Run.SetPaused(true);
            var before = cam.transform.position;
            _installer.UI.Photo.Enter();
            Assert.IsTrue(_installer.UI.Photo.Active);
            Assert.IsFalse(rig.enabled, "the follow camera pauses in photo mode");
            yield return null;
            _installer.UI.Photo.Exit();
            Assert.IsTrue(rig.enabled);
            Assert.Less(Vector3.Distance(before, cam.transform.position), 0.01f);
            _installer.Run.SetPaused(false);
        }

        [UnityTest]
        public IEnumerator BorrowedScene_BuildsTheRequestedPark()
        {
            // What SceneRouter does when a new park's scene isn't in Build Settings: load another park's scene
            // and ask the installer to swap the builder.
            foreach (var root in _scene.GetRootGameObjects()) UnityEngine.Object.Destroy(root);
            yield return null;
            new GameObject("Harbor Plaza").AddComponent<HarborPlazaBuilder>();
            GameSession.Mode = RunMode.FreeSkate;
            GameSession.LocationId = ParkCatalog.SunsetBowls;
            GameSession.ParkOverride = true;
            _locationId = ParkCatalog.HarborPlaza; // the borrowed scene's own park
            yield return Boot(null);
            Assert.AreEqual(ParkCatalog.SunsetBowls, GameSession.LocationId);
            Assert.IsFalse(GameSession.ParkOverride);
            Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<HarborPlazaBuilder>());
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<SunsetBowlsBuilder>());
            Assert.IsTrue(Physics.Raycast(_installer.Level.spawnPoint.position + Vector3.up, Vector3.down, 3f));
        }
            // ------------------------------------------------------------------ Phase 9

        private IEnumerator BootCustom(bool edit)
        {
            var park = CustomPark.Create(CustomParkIds.ForSlot(1), "TEST PARK");
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Ledge, x = 8, z = 10 });
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.FlatRail, x = 30, z = 10 });
            park.pieces.Add(new ParkPiece { kind = (int)PieceKind.Bowl, x = 4, z = 26 });
            SaveManager.SaveCustomPark(park);
            GameSession.LocationId = park.id;
            GameSession.EditPark = edit;
            yield return Reboot(RunMode.FreeSkate, null, park.id);
        }

        [UnityTest]
        public IEnumerator CustomPark_BuildsFromTheSave_AndSkaterRollsOut()
        {
            yield return BootCustom(false);
            Assert.AreEqual(CustomParkIds.ForSlot(1), GameSession.LocationId);
            var builder = UnityEngine.Object.FindAnyObjectByType<CustomParkBuilder>();
            Assert.IsNotNull(builder);
            Assert.AreEqual(3, builder.PieceRoots.Count);
            Assert.GreaterOrEqual(GrindRail.Active.Count, 4, "ledge edges, flat rail, bowl coping");
            Assert.IsNull(_installer.Editor);
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "first touchdown");
            yield return Seconds(1.5f);
            Assert.AreEqual(0, _bails, $"bailed with {_lastBail}");
            Assert.Greater(Player.Speed, 4f);
        }

        [UnityTest]
        public IEnumerator ParkEditor_TapSelects_ArrowsMove_AndSaveKeepsIt()
        {
            yield return BootCustom(true);
            var editor = _installer.Editor;
            Assert.IsNotNull(editor, "edit sessions open the editor");
            Assert.IsFalse(Player.enabled, "the skater waits on the start pad while editing");

            var (cx, cz) = CustomPark.WorldCenter(editor.Park.pieces[1]);
            editor.SelectAtWorld(cx, cz);
            Assert.AreEqual(1, editor.Selected, "tapping a piece selects it");

            int x = editor.Park.pieces[1].x, z = editor.Park.pieces[1].z;
            Assert.IsTrue(editor.Move(-1, 0));
            Assert.IsTrue(editor.Move(0, 1));
            Assert.AreEqual(x - 1, editor.Park.pieces[1].x);
            Assert.AreEqual(z + 1, editor.Park.pieces[1].z);
            yield return null;

            var builder = UnityEngine.Object.FindAnyObjectByType<CustomParkBuilder>();
            var rail = builder.PieceRoots[1].GetComponentInChildren<GrindRail>();
            Assert.IsNotNull(rail, "the moved rail was rebuilt");
            Assert.AreEqual(cx - CustomPark.CellSize, rail.transform.TransformPoint(rail.localPoints[0]).x, 0.05f);

            Assert.IsTrue(editor.AddPiece(PieceKind.Kicker));
            Assert.AreEqual(4, editor.Park.pieces.Count);
            Assert.AreEqual(3, editor.Selected, "a new piece is selected");
            Assert.IsTrue(editor.Rotate());
            Assert.IsTrue(editor.DeleteSelected());
            Assert.AreEqual(3, editor.Park.pieces.Count);

            editor.Save();
            var saved = SaveManager.FindCustomPark(CustomParkIds.ForSlot(1));
            Assert.AreEqual(x - 1, saved.pieces[1].x);
            Assert.AreEqual(3, saved.pieces.Count);
        }

        [UnityTest]
        public IEnumerator CreateASkater_LookIsApplied()
        {
            var visual = Player.GetComponentInChildren<SkaterVisual>();
            var look = new SkaterLook { hairStyle = (int)HairStyle.Afro, eyewear = (int)Eyewear.Shades, customBoard = true };
            visual.ApplyLook(look);
            yield return null;
            var hair = visual.transform.Find("Pose/Body/Hips/Hair");
            Assert.IsNotNull(hair);
            Assert.Greater(hair.childCount, 0);
            Assert.IsFalse(visual.transform.Find("Pose/Body/Hips/Cap").GetComponent<MeshRenderer>().enabled, "big hair hides the cap");
            Assert.Greater(visual.transform.Find("Pose/Body/Hips/Eyewear").childCount, 0);
            var deck = visual.Board.Find("Deck").GetComponent<MeshRenderer>().sharedMaterial;
            StringAssert.StartsWith("Textured_", deck.name, "custom board graphic is a texture");
        }

        [UnityTest]
        public IEnumerator Career_PaysChapterOne_WhenItsGoalsAreMet()
        {
            SaveManager.Data.career = new CareerState();
            var rec = SaveManager.Data.Record("harbor_plaza");
            rec.bestScore = 99999;
            rec.bestCombo = 99999;
            if (!SaveManager.Data.stats.gapIds.Contains(ParkCatalog.FountainGap)) SaveManager.Data.stats.gapIds.Add(ParkCatalog.FountainGap);
            int tokens = SaveManager.Data.tapeTokens;
            var u = CareerService.Check();
            Assert.IsTrue(u.NewChapters.Exists(c => c.Number == 1));
            Assert.GreaterOrEqual(SaveManager.Data.tapeTokens, tokens + Career.Chapters[0].RewardTokens);
            Assert.IsFalse(CareerService.Check().NewChapters.Exists(c => c.Number == 1), "paid once");
            CareerService.TakePending();
            yield return null;
        }
            // ------------------------------------------------------------------ Phase 10

        [UnityTest]
        public IEnumerator NoComply_FromAPopWithTheStickDown()
        {
            yield return StartOnLane();
            var tricks = Player.GetComponent<TrickController>();
            string started = null;
            tricks.TrickStarted += t => started = t.id;
            _input.Steer = new Vector2(0f, -1f);
            _input.JumpHeld = true;
            yield return Seconds(0.05f);
            _input.JumpHeld = false;
            yield return WaitUntil(() => started != null, 1f, "pop variant");
            _input.Steer = Vector2.zero;
            Assert.AreEqual("no_comply", started);
        }

        [UnityTest]
        public IEnumerator ChallengeRun_ShowsTheTargetInTheHud()
        {
            GameSession.Challenge = new ScoreChallenge { LocationId = ParkCatalog.HarborPlaza, Target = 1234, From = "TESTER" };
            yield return Reboot(RunMode.TwoMinuteRun, null, ParkCatalog.HarborPlaza);
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<RetroSk8.UI.ChallengeHudView>());
            Assert.AreEqual(GameSession.Challenge, GameSession.ActiveChallengeFor(ParkCatalog.HarborPlaza));
        }

        [UnityTest]
        public IEnumerator LivingCity_HasTraffic_NightFalls_AndEventsPopUp()
        {
            yield return BootCity(RunMode.FreeSkate);
            var life = _installer.CityLife;
            Assert.IsNotNull(life);
            life.Modifier = WeeklyModifier.None; // this week's event may lock the city at night or in rain
            Assert.AreEqual(10, life.CarCount);
            Assert.Less(life.NightAmount, 0.2f, "runs start in the afternoon");
            life.SkipAhead(CityLife.DaySeconds * (1f - CityLife.StartTime)); // to midnight
            yield return null;
            Assert.Greater(life.NightAmount, 0.9f);
            life.SkipAhead(CityLife.FirstEventDelay);
            yield return null;
            yield return null;
            Assert.IsTrue(life.EventRunning, "a street event starts once it's due");
            Assert.IsNotNull(_installer.City.EventBanner);
        }

        [UnityTest]
        public IEnumerator Duel_VsCpu_TakesTurns()
        {
            var session = RetroSk8.Duel.DuelSession.StartCpu(DuelBot.Level.Easy, 0, 42);
            yield return Reboot(RunMode.Duel, null, ParkCatalog.HarborPlaza);
            var duel = _installer.Duel;
            Assert.IsNotNull(duel);
            Assert.AreEqual(0, session.Duel.Actor, "you set first against the CPU");
            yield return WaitUntil(() => duel.State == RetroSk8.Duel.DuelController.LocalState.Attempting, 5f, "countdown");
            Player.Teleport(new Vector3(1.5f, 4f, 0f), Vector3.forward); // fountain gap: a guaranteed combo
            yield return WaitUntil(() => session.Duel.Turn >= 1, 10f, "your set to finish");
            Assert.AreEqual(1, session.Duel.Actor, "the CPU is up next");
            Assert.AreEqual(RetroSk8.Duel.DuelController.LocalState.Watching, duel.State);
            yield return WaitUntil(() => session.Duel.Turn >= 2, 12f, "the CPU's attempt");
            Assert.AreEqual(0, session.Duel.Letters(0), "you haven't missed anything yet");
        }
            // ------------------------------------------------------------------ Phase 11

        private IEnumerator ShortRunWithACombo()
        {
            yield return Reboot(RunMode.TwoMinuteRun, c => c.FindLocation("harbor_plaza").runDurationSeconds = 4f, ParkCatalog.HarborPlaza);
            RunResult result = null;
            _installer.Run.Finished += r => result = r;
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "touchdown");
            Player.Teleport(new Vector3(1.5f, 4f, 0f), Vector3.forward); // fountain gap: a guaranteed combo
            yield return WaitUntil(() => result != null, 14f, "run to finish");
            Assert.Greater(result.score, 0);
        }

        [UnityTest]
        public IEnumerator FinishedRun_SavesAReplay_ThatTheEditorPlays()
        {
            yield return ShortRunWithACombo();
            string id = ReplayLibrary.LastSavedId;
            Assert.IsNotNull(id, "scored runs save a replay automatically");
            var entry = ReplayLibrary.Index.Find(id);
            Assert.IsNotNull(entry);
            Assert.Greater(entry.moments.Count, 0, "banked lines are kept for the replay labels");

            GameSession.ReplayId = id;
            yield return Reboot(RunMode.Replay, null, ParkCatalog.HarborPlaza);
            var theater = _installer.Theater;
            Assert.IsNotNull(theater);
            Assert.IsTrue(theater.Loaded);
            Assert.IsFalse(Player.gameObject.activeSelf, "the live skater sits out");
            Assert.Greater(theater.Clock.Duration, 1f);
            theater.SeekFraction(0.5f);
            theater.CycleCamera();
            Assert.AreEqual(ReplayCamera.Fisheye, theater.CameraMode);
            yield return Seconds(0.3f);
        }

        [UnityTest]
        public IEnumerator RidingCrew_PerksApply()
        {
            SaveManager.Data.crew = new CrewState();
            SaveManager.Data.crew.Recruit("pilar"); // +6% points
            SaveManager.Data.crew.Recruit("dex");   // special fills faster
            yield return Reboot(RunMode.FreeSkate, null, ParkCatalog.HarborPlaza);
            Assert.AreEqual(1.06f, _installer.Combo.CrewFactor, 0.001f);
            Assert.AreEqual(1.25f, _installer.Combo.SpecialFactor, 0.001f);
        }

        [UnityTest]
        public IEnumerator BeatingACrewScore_RecruitsThem_AndCountsForTheWeek()
        {
            SaveManager.Data.crew = new CrewState();
            long combosBefore = WeeklyService.State.Get(WeeklyCounters.Combos);
            GameSession.CrewRecruitId = "pilar";
            GameSession.Challenge = new ScoreChallenge { LocationId = ParkCatalog.HarborPlaza, Target = 1, From = "PILAR" };
            yield return ShortRunWithACombo();
            Assert.IsTrue(SaveManager.Data.crew.IsRecruited("pilar"));
            Assert.Greater(SaveManager.Data.crew.xp, 0, "banked points give crew XP");
            Assert.Greater(WeeklyService.State.Get(WeeklyCounters.Combos), combosBefore, "banked combos count for the week");
            CareerService.TakePending();
        }
            // ---------------------------------------------------------------- Phase 12

        [UnityTest]
        public IEnumerator GhostCode_FromAFinishedRun_RacesAsARival()
        {
            yield return ShortRunWithACombo();
            Assert.IsNotNull(GameSession.LastRunTrack, "the finished run is kept for SEND GHOST");
            long score = GameSession.LastResult != null ? GameSession.LastResult.score : GameSession.LastRunTrack.Score;
            string code = ShareService.GhostCode(ParkCatalog.HarborPlaza, score);
            Assert.IsNotNull(code);
            Assert.IsTrue(GhostCodes.TryDecode(code, out var challenge, out var error), error);
            Assert.AreEqual(ParkCatalog.HarborPlaza, challenge.LocationId);

            GameSession.Challenge = challenge;
            yield return Reboot(RunMode.TwoMinuteRun, null, ParkCatalog.HarborPlaza);
            Assert.IsNotNull(_installer.Ghost, "the friend's ghost skates");
            Assert.AreSame(_installer.Ghost, GhostPlayer.Rival);
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<RetroSk8.UI.ChallengeHudView>(), "the HUD tracks their score");
            yield return Seconds(0.5f);
            Assert.Greater(GhostPlayer.Rival.PlaybackTime, 0.2f);
        }

        [UnityTest]
        public IEnumerator StorePurchase_UnlocksThePacksLooks_AndTokensCantBuyThem()
        {
            SaveManager.Data.ownedPacks.Clear();
            SaveManager.Data.tapeTokens = 1000;
            var content = ContentRegistry.WithDefaults(DefaultContent.CreateRegistry());
            var pack = Shop.Packs[0];
            var item = content.cosmetics.Find(c => c != null && c.id == pack.ItemIds[0]);
            Assert.IsNotNull(item, "pack items are in the content");
            Assert.IsFalse(CosmeticsService.IsOwned(item));
            Assert.AreEqual(PurchaseResult.PackOnly, CosmeticsService.Buy(item));
            Assert.AreEqual(1000, SaveManager.Data.tapeTokens, "no tokens spent");

            StoreEvent.TryParse("purchased|" + pack.ProductId + "|", out var e);
            StoreService.Handle(e);
            Assert.IsTrue(StoreService.Owns(pack));
            Assert.IsTrue(CosmeticsService.IsOwned(item));
            Assert.IsTrue(CosmeticsService.Equip(item));
            var other = content.cosmetics.Find(c => c != null && c.id == Shop.Packs[1].ItemIds[0]);
            Assert.IsFalse(CosmeticsService.IsOwned(other), "one pack doesn't unlock another");

            SaveManager.ResetAll();
            Assert.IsTrue(StoreService.Owns(pack), "a progress reset never takes away a paid pack");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Radio_TunesInAStation_AndBackToThemes()
        {
            var audio = RetroSk8.Audio.AudioManager.Ensure();
            string heard = null;
            System.Action<string> onSong = t => heard = t;
            audio.NowPlaying += onSong;
            try
            {
                audio.SetMusicMode(MusicMode.Radio, 1);
                Assert.AreEqual(MusicMode.Radio, RetroSk8.Audio.AudioManager.Mode);
                yield return WaitUntil(() => heard != null, 15f, "a radio song to render and start");
                StringAssert.StartsWith(Radio.Stations[1].Name, heard);
                audio.SkipSong();
                audio.SetMusicMode(MusicMode.Off, 1);
                Assert.AreEqual("", audio.NowPlayingText);
            }
            finally
            {
                audio.NowPlaying -= onSong;
                audio.SetMusicMode(MusicMode.ParkThemes, 0);
            }
        }

        [UnityTest]
        public IEnumerator Controller_HidesTheTouchControls()
        {
            var hide = UnityEngine.Object.FindAnyObjectByType<TouchAutoHide>();
            Assert.IsNotNull(hide);
            var group = hide.GetComponent<CanvasGroup>();
            InputDeviceTracker.ForceForTests(true);
            Assert.AreEqual(0f, group.alpha, 0.001f);
            Assert.IsFalse(group.blocksRaycasts);
            InputDeviceTracker.ForceForTests(false);
            Assert.AreEqual(1f, group.alpha, 0.001f);
            yield return null;
        }
            // ---------------------------------------------------------------- Phase 13

        [UnityTest]
        public IEnumerator FloodgateDitch_Builds_AndDropsYouInFromTheDam()
        {
            yield return Reboot(RunMode.FreeSkate, null, ParkCatalog.FloodgateDitch);
            Assert.IsNotNull(Player);
            Assert.AreEqual(3, _installer.Level.gaps.Count, "transfer, outlet and spillway gaps");
            Assert.Greater(Player.transform.position.y, 3.5f, "spawn is on top of the dam");
            yield return WaitUntil(() => Player.IsGrounded, 3f, "landing on the dam");
        }

        // ---------------------------------------------------------------- Phase 19

        [Test]
        public void SaveSeal_CatchesAHandEditedSave_AndKeepsTheFlag()
        {
            SaveManager.UseFile("retrosk8_seal_test_save.json");
            try
            {
                if (System.IO.File.Exists(SaveManager.FilePath)) System.IO.File.Delete(SaveManager.FilePath);
                if (System.IO.File.Exists(SaveManager.SealPath)) System.IO.File.Delete(SaveManager.SealPath);
                SaveManager.Load(); // a fresh save (ResetAll would also clear the real ghost store)
                SaveManager.Data.tapeTokens = 120;
                SaveManager.Save();
                Assert.IsTrue(System.IO.File.Exists(SaveManager.SealPath), "a seal is written next to the save");
                SaveManager.Load();
                Assert.AreEqual(SaveIntegrity.Sealed, SaveManager.Integrity);

                string json = System.IO.File.ReadAllText(SaveManager.FilePath);
                System.IO.File.WriteAllText(SaveManager.FilePath, json.Replace("\"tapeTokens\": 120", "\"tapeTokens\": 999999"));
                SaveManager.Load();
                Assert.AreEqual(999999, SaveManager.Data.tapeTokens, "the edit is loaded (nothing is thrown away)");
                Assert.AreEqual(SaveIntegrity.Edited, SaveManager.Integrity);
                SaveManager.Save();
                SaveManager.Load();
                Assert.AreEqual(SaveIntegrity.Edited, SaveManager.Integrity, "re-saving doesn't launder an edited save");
            }
            finally
            {
                System.IO.File.Delete(SaveManager.FilePath);
                System.IO.File.Delete(SaveManager.SealPath);
                SaveManager.UseFile("retrosk8_playmode_test_save.json");
            }
        }

        [Test]
        public void GameCenter_DropsImpossibleScores()
        {
            int before = GameCenter.Rejected;
            Assert.IsTrue(GameCenter.Allowed("retrosk8.score.harbor_plaza", 50000));
            Assert.IsFalse(GameCenter.Allowed("retrosk8.score.harbor_plaza", long.MaxValue));
            Assert.IsFalse(GameCenter.Allowed("retrosk8.race.downtown_dash", 1));
            Assert.AreEqual(before + 2, GameCenter.Rejected);
        }

        // ---------------------------------------------------------------- Phase 18

        [UnityTest]
        public IEnumerator DriveIn_Builds_WithBonkablesAndASafeSpawn()
        {
            yield return Reboot(RunMode.FreeSkate, null, ParkCatalog.DriveIn);
            Assert.IsNotNull(Player);
            Assert.AreEqual(3, _installer.Level.gaps.Count, "car hop, intermission air, snack bar hop");
            Assert.GreaterOrEqual(BonkTarget.Count, DriveInBuilder.Rows.Length * DriveInBuilder.PostX.Length, "speaker posts are bonkable");
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 3f, "touchdown");
            Assert.AreEqual(0, _bails, $"bailed at the start ({_bailInfo})");
        }

        [UnityTest]
        public IEnumerator RollingIntoASpeakerPost_PoleJams_InsteadOfBailing()
        {
            yield return Reboot(RunMode.FreeSkate, null, ParkCatalog.DriveIn);
            yield return WaitUntil(() => Player.State == SkaterState.Rolling, 3f, "touchdown");
            // The post at (-21, row 2) leans away from a skater riding north; start 10 m short of it.
            Player.Teleport(new Vector3(-21f, 0.05f, -9f), Vector3.forward);
            var moves = new System.Collections.Generic.List<BonkMove>();
            System.Action<BonkMove> onBonk = m => moves.Add(m);
            Player.Bonked += onBonk;
            try
            {
                _input.Steer = new Vector2(0f, 1f);
                yield return WaitUntil(() => moves.Count > 0, 5f, "hitting the speaker post");
            }
            finally
            {
                Player.Bonked -= onBonk;
                _input.Steer = Vector2.zero;
            }
            Assert.AreEqual(BonkMove.PoleJam, moves[0], "rolling into a post fast is a pole jam");
            Assert.AreEqual(SkaterState.Airborne, Player.State, "a pole jam launches you");
            Assert.AreEqual(0, _bails, $"bailed ({_bailInfo})");
        }

        [UnityTest]
        public IEnumerator RetroCity_EverySpotHasSomethingToBonk()
        {
            yield return Reboot(RunMode.FreeSkate, null, ParkCatalog.RetroCity);
            int spotsWith = 0;
            foreach (var spot in RetroCityLayout.Spots)
                if (GameObject.Find(spot.Id + "_Bonk0") != null) spotsWith++;
            Assert.GreaterOrEqual(spotsWith, RetroCityLayout.Spots.Count - 1, "Bonk Hunt needs bonkables at (almost) every spot");
        }

        [UnityTest]
        public IEnumerator RetroCity_HasTheRiversideYards()
        {
            yield return Reboot(RunMode.FreeSkate, null, ParkCatalog.RetroCity);
            Assert.IsNotNull(GameObject.Find("Boxcar_A"), "the yards are built");
            Assert.IsTrue(_installer.Level.playableBounds.Contains(new Vector3(0f, 1f, 190f)), "the yards are inside the playable area");
        }

        [UnityTest]
        public IEnumerator StoryStep_ClearsOnlyWhenTheRunBeatsIt()
        {
            var step = Story.FindStep("s1_pilar");
            SaveManager.Data.story = new StoryState();
            GameSession.StoryOutroPending = null;
            GameSession.StoryStepId = step.Id;
            GameSession.Challenge = new ScoreChallenge { LocationId = step.LocationId, Target = step.Target, From = step.Rival };
            try
            {
                yield return Reboot(RunMode.TwoMinuteRun, c => c.FindLocation("harbor_plaza").runDurationSeconds = 4f, ParkCatalog.HarborPlaza);
                RunResult result = null;
                _installer.Run.Finished += r => result = r;
                yield return WaitUntil(() => Player.State == SkaterState.Rolling, 2f, "touchdown");
                Player.Teleport(new Vector3(1.5f, 4f, 0f), Vector3.forward);
                yield return WaitUntil(() => result != null, 14f, "run to finish");
                bool beat = result.score > step.Target;
                Assert.AreEqual(beat, SaveManager.Data.story.IsCleared(step.Id));
                Assert.AreEqual(beat ? step.Id : null, GameSession.StoryOutroPending);
            }
            finally
            {
                GameSession.StoryStepId = null;
                GameSession.StoryOutroPending = null;
            }
        }

        [UnityTest]
        public IEnumerator Outfit_BuildsCutsAndBoardShapes()
        {
            var go = new GameObject("OutfitTest");
            var visual = go.AddComponent<SkaterVisual>();
            visual.Build();
            var look = new SkaterLook
            {
                shirtStyle = (int)ShirtStyle.Hoodie, shirtColor = 4, bottomsStyle = (int)BottomsStyle.Shorts, sockColor = 3,
                shoeStyle = (int)ShoeStyle.HighTop, deckShape = (int)DeckShape.Cruiser, wheelColor = 6, truckColor = 1, gripColor = 2,
            };
            visual.ApplyLook(look);
            yield return null;
            Assert.IsNotNull(go.transform.Find("Pose/Body/Hips/Hood"), "hoodie adds a hood");
            Assert.Greater(go.GetComponentsInChildren<Transform>(true).Length, 40);
            look.shirtStyle = (int)ShirtStyle.Tee;
            visual.ApplyLook(look);
            yield return null;
            Assert.IsNull(go.transform.Find("Pose/Body/Hips/Hood"), "changing the cut removes the old parts");
            UnityEngine.Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Fireworks_FireWithoutErrors()
        {
            Assert.IsNotNull(_installer.Fx);
            _installer.Fx.Fireworks(Player.transform.position + Vector3.up * 3f, 60);
            Assert.IsNotNull(_installer.Juice, "juice runs in normal runs");
            yield return Seconds(0.3f);
        }
    }
}
