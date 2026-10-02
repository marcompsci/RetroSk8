# Retro Sk8 — Phase 4 (Neon Warehouse and Rooftop Run)

An original, iOS-first arcade skateboarding prototype. Unity 6 LTS · C# · URP · Input System · Cinemachine 3.
Every name, layout, sound and piece of art in this project was created for Retro Sk8. See **IP-clean asset rules** below.

---

## 1. Setup

1. Install **Unity 6000.6.3f1** (the version this repo's `ProjectSettings/ProjectVersion.txt` pins) with the **iOS Build Support** module.
   The project started from Unity's URP template; Retro Sk8 lives entirely in `Assets/RetroSk8`.
   Cinemachine is optional: without it the game uses its built-in follow camera. Add `com.unity.cinemachine` 3.x in Package Manager to switch.
2. Clone this repo, then in Unity Hub choose **Add → Add project from disk** and pick the cloned folder. The packages in `Packages/manifest.json` resolve on first open.
   - If the Package Manager reports a version mismatch for URP (it is locked to your editor build), accept the version it suggests.
3. When Unity asks to enable the new **Input System** backend, click **Yes**. The editor restarts.
   (You can also set this manually: *Project Settings → Player → Active Input Handling → Input System Package (New)*.)
4. On first open (and once more after updating to a phase that adds scenes) you'll get a **"Run project setup now?"** dialog. Click **Set Up**, or run **Retro Sk8 → Setup Project** from the menu at any time. The setup:
   - creates and assigns a URP pipeline asset (`Assets/RetroSk8/Settings`)
   - creates the placeholder Lit material, the trick, scoring and location ScriptableObjects, and the `ContentRegistry`
   - creates `BootScene`, `MainMenuScene`, the three park scenes (`SkateScene_HarborPlaza`, `SkateScene_NeonWarehouse`, `SkateScene_RooftopRun`), `ResultsScene` and `CustomizationScene`, and adds them to Build Settings
   - upgrades older content: parks saved as *Coming soon* become playable and missing Spot Contracts are added
   - sets product name, landscape-only orientation, iOS 15 minimum and a **placeholder** bundle id (`com.retrosk8.prototype`; change it before shipping)

   Setup never overwrites existing assets or scenes. To overwrite the scenes, use **Retro Sk8 → Recreate Scenes**.
5. Open `SkateScene_HarborPlaza` and press **Play**. For the full flow (Boot → Park → Results), start from `BootScene`.

**iOS build:** *File → Build Profiles → iOS → Switch Platform → Build*, then open the Xcode project, set your signing team and run it on a device. The Taptic haptics bridge (`Plugins/iOS/RetroSk8Haptics.mm`) is compiled into the build automatically.

### Running on the iOS Simulator (Xcode)
1. You need **Unity Hub + Unity 6 LTS with the "iOS Build Support" module**. Xcode alone can't build a Unity game: Unity exports the Xcode project that Xcode then builds.
2. In Unity: **Retro Sk8 → Build iOS → Xcode Project for Simulator**. This exports `Builds/iOS-Simulator/Unity-iPhone.xcodeproj` and reveals it in Finder.
3. Open it in Xcode, choose the **Unity-iPhone** scheme and any iPhone simulator, and press **⌘R**. No signing team is needed for the Simulator.
4. For a physical iPhone, use **Xcode Project for Device** and set your team under *Signing & Capabilities*.

Keyboard controls work in the Simulator when *I/O → Keyboard → Connect Hardware Keyboard* is on. Mouse clicks drive the touch controls.

### Running the tests
- **Rules (EditMode):** *Window → General → Test Runner → EditMode → Run All* (assembly `RetroSk8.Tests.EditMode`).
- **Gameplay smoke tests (PlayMode):** *Test Runner → PlayMode → Run All* (assembly `RetroSk8.Tests.PlayMode`). Each test builds a fresh park (Harbor Plaza unless the test says otherwise) and drives the skater with scripted input.
- **Smoke tests on the Simulator:**
  1. Switch the platform to iOS and set *Player Settings → iOS → Target SDK* to **Simulator SDK**.
  2. In the Test Runner, choose **PlayMode → Run All Tests (iOS)**. Unity exports an Xcode project with the test runner built in.
  3. Run that project on a simulator from Xcode. Results stream back to the editor's Test Runner window, which must stay open.
- **Without Unity:** `cd DotnetTests && dotnet run`. This compiles `Scripts/Core` with the same rule tests, using a tiny NUnit-compatible runner, so it needs no NuGet access.

The tests write to a separate save file (`retrosk8_playmode_test_save.json`) and ignore saved tuning, so they never touch your progress.

---

## 2. Controls

| Action | Touch (default layout) | Keyboard (editor) | Gamepad / Bluetooth controller |
|---|---|---|---|
| Steer · push · brake | Left-thumb floating stick (up = push, down = brake) | WASD / arrows | Left stick |
| Charge ollie | Hold **JUMP** | Hold Space | Hold South (A / ✕) |
| Ollie | Release **JUMP** | Release Space | Release South |
| Air trick | Swipe on the right half (a swipe can start on JUMP) | I / K / J / L | Right-stick flick, D-pad, North/East buttons |
| Spin | Steer while airborne | A / D in air | Left stick in air |
| Grind | Tap **GRIND/MANUAL** near a rail or ledge | E | West (X / □) or R1 |
| Manual | Tap **GRIND/MANUAL** within 0.35 s of landing (a press up to 0.15 s *before* touchdown also counts) | E | West / R1 |
| Balance (grind or manual) | Stick left/right | A / D | Left stick |
| Pause | **II** button | Esc / P | Start / Menu |
| Debug menu | Pause → DEBUG | F1 or ` | Select / View |

**Trick selection.** The swipe direction picks the family and the stick direction at the moment of the swipe picks the variation.

| Swipe | Family | Stick neutral | Stick ← | Stick → | Stick ↑/↓ |
|---|---|---|---|---|---|
| ↑ | Flips | Tide Flip | Heel Tumble | Riffle Flip | Keel Twist |
| ↓ | Grabs | Anchor Grab | Porthole Grab | Tailhook | Buoy Snatch |
| ← / → | Shoves | Pivot Scoop | Gyre Scoop | Scoop Roll | Wharf Wheel |

- **Grinds** are chosen by the stick direction when you tap ACTION: Center Glide, Plank Slide, Crossbar Slide, Nose Needle.
- **Manuals:** stick ↑ gives Nose Cruise; anything else gives Tail Cruise.
- **Spins:** Half Cycle (180), Full Cycle (360), Cycle and a Half (540), and so on.
- **Specials:** when the SPECIAL meter under the score is full, your next air trick becomes that family's special (Lighthouse Twirl, Gull Dive or Tidal Twister).

---

## 3. Game rules implemented in Phase 1

- **Combo:** each trick adds base points and +1 to the multiplier (capped at ×20). The combo value is base × multiplier × Line Flow.
- **Repeat decay:** within one combo, the Nth repeat of a trick is worth base × 0.5^N (floored at 10%). This applies to grinds and manuals too, and repeats also make their balance meter harder.
- **Line Flow:** +10% for each extra trick category and +5% for each link between different line elements (air, ramp air, grind, manual), capped at +100%.
- **Banking:** a landing opens the manual window. If you don't start a manual, the combo banks when the window closes. A sketchy landing banks 80%.
- **Bails** erase the unbanked combo and empty the special meter. A bail happens when you:
  - land with a trick still more than 20% unfinished
  - land more than 50° off a 180° multiple
  - land sideways across a steep slope
  - hit a wall head-on at high speed
  - lose the balance meter
  - fall into the water
- **Two-Minute Run:** at 0:00 the combo in progress may still land (up to 8 s of overtime). You then go to Results, which shows score, best combo, combos, bails, goals and Tape Tokens.
- **Tape Tokens (placeholder economy):** 1 token per 5,000 points, saved locally.
- **Respawn safety:** the game records safe flat spots every 0.5 s and respawns at one that is at least 1.5 s old. You are respawned automatically when you fall out of bounds, stay airborne for more than 6 s, or stay wedged below 0.35 m/s for 2.5 s without braking. Pause → RESPAWN is always available.

All numbers live in `ScriptableObjects/Scoring/ScoringProfile_Default.asset` (scoring, balance, landing rules, manual window) and in the individual trick assets.

---

## 3b. Phase 2 feel pass

**Fixes**
- **No double jump:** a pop now sets a flag, so the late-ollie (coyote) window can't fire a second pop in the same air. The window is still available after rolling off an edge or exiting a grind.
- **Thin bars and posts are never ground:** they carry `NonGroundSurface`, and the ground probe checks every hit and picks the nearest valid one. You can't "stand" on a rail any more.
- **Landing snap:** it now also lifts the skater when a fast landing sinks the feet below the surface.
- **Rebuild Now** (level builder context menu) really rebuilds the park.
- **Live tuning is safe:** it runs on a runtime copy of the ScoringProfile, so it never edits the asset on disk.

**Feel**
- **Jump buffer:** release JUMP up to 0.12 s before touchdown and you pop on landing. Charge also builds while you hold JUMP in the air.
- **Landing align assist:** clean and sketchy landings turn the board onto the line of travel, so you roll away straight.
- **Steering response curve (exponent 1.6):** small stick moves give fine lines, and full stick still carves hard.
- **Grind snap radius grows with speed** (+0.04 m per m/s, capped at 2.2 m), so fast approaches are as forgiving as slow ones.
- **Camera:**
  - FOV widens with speed.
  - The camera pulls back while airborne and leads slightly ahead of the skater.
  - Small shake on landing, scaled by impact.

**Live feel tuning.** Open *Pause → DEBUG → FEEL TUNING* for about 27 sliders, grouped as Motor / Air / Landing / Balance / Camera. Changes apply instantly.
- **SAVE** writes `retrosk8_tuning.json` (next to the save file). It is re-applied on every launch, including on device and in the Simulator.
- **COPY JSON** puts the values on the clipboard. Paste them to me, or into the defaults, to make them permanent.
- **RESET** restores the project defaults and deletes the saved preset.

---

## 3c. Phase 3: menus, progression, modes

**Flow:** BootScene → **MainMenuScene** → park → Results → Home. The menu has four entries:
- **PLAY:** pick a park, then a mode:
  - **Two-Minute Run:** highest score before time runs out.
  - **Spot Contract:** three park goals in one two-minute run.
  - **Free Skate:** no timer and no tokens. Pause → END RUN finishes the session.
- **DAILY LINE:** the same three generated goals plus a target score for everyone on a given day. The bonus pays once per day.
- **CUSTOMIZE:** opens **CustomizationScene**, with a turntable preview and a shop for six slots: deck, wheels, grip, shirt, hat and palette (pants). There are 30 original items. Tapping an item tries it on; the button equips it or buys it with Tape Tokens. One free item per slot is owned from the start.
- **SETTINGS:** music, effects and ambience volume, haptics, and a reset-progress button that needs a second tap to confirm.

**Harbor Plaza Spot Contract** (the three examples from the brief):
- Land a 10,000-point line.
- Grind three separate rails.
- Clear the fountain gap.

**Gaps:** `GapZone` trigger volumes. A gap counts when you pass through one while airborne and then land cleanly or lock into a grind; a bail forfeits it. A cleared gap is added to the combo as a trick.
- **Fountain Gap:** 750 points, over the fountain bowl.
- **Container Gap:** 1,000 points, between the container stacks.

**Goals:** rails, tricks and gaps count only once their combo banks, so a line that ends in a bail never ticks a goal. Progress shows top-right during the run, with a toast and chime when a goal completes. Results list every goal.

**Tape Tokens.** They can only be earned by skating, never bought:
- 1 token per 5,000 points (not in Free Skate).
- +5 per contract goal, paid only the first time you complete it.
- +10 the first time you complete a whole contract.
- +20 for completing the Daily Line, once per day.

There are no loot boxes, no randomness and no real-money purchases. All values live in the ScoringProfile.

**Save v2** adds owned and equipped cosmetics, contract progress and the Daily Line record. v1 saves load and upgrade automatically.

---

## 3d. Phase 4: two new parks

Both parks are generated at runtime from code, like Harbor Plaza. Each park has a builder class (`NeonWarehouseBuilder`, `RooftopRunBuilder`) that derives from `ParkBuilder`, which holds the shared geometry helpers. `ParkCatalog` maps park ids to builders and lists each park's named gaps. Every layout is original.

**Neon Warehouse** (indoor, cool light, neon strips):
- Quarter pipes on the east and west walls.
- A low spine in the middle. Transferring over it counts as the **Spine Transfer** gap (600 points).
- A raised **conveyor line** with two belts that carry you toward the gap between them. Belt speed is kept when you ollie off, so the **Conveyor Gap** (900 points) is reachable at cruise speed.
- A broken **loading dock** along the south wall, with banks up and edge ledges. Jumping the collapsed middle section is the **Dock Gap** (800 points).
- A maintenance walkway in the north-west corner with a down-rail.
- A floor flat bar, a manual pad, a kicker and a pallet stack with a grindable edge.

**Rooftop Run** (two roofs above an invented skyline):
- **Roof A** has a drainage channel you can ride through or jump across (**Channel Gap**, 500 points), air vents, a vent duct with grindable edges, parapet ledges, a flat bar, a skylight manual pad, and two kickers aimed east.
- **Roof B** is 1.5 m lower, across a 6 m drop. Jumping it is the **Rooftop Gap** (1,200 points). It needs push speed or an ollie off a kicker. It has water towers with grindable round bases, a plywood construction quarter pipe and a scaffold bar.
- Falling between the roofs counts as out of bounds and respawns you.

**Spot Contracts:**
- Neon Warehouse: clear the conveyor gap, bank a 15,000-point combo, hold a manual for 4 seconds.
- Rooftop Run: clear the rooftop gap, grind three separate rails, land a 540 spin.

**Daily Line rotation:** the Daily Line moves to the next playable park each calendar day (`DailyLineGenerator.PickIndex`). The menu and the run both read the park's gaps from `ParkCatalog`, so the preview always matches the goals in the run.

**Updating an existing project:** after pulling Phase 4, run **Retro Sk8 → Setup Project** (or accept the dialog). This creates the two new scenes, marks the two parks as playable and adds their contracts. Your Harbor Plaza scene and saved progress are kept.

---

## 4. Architecture

```
Scripts/
  Core/      RetroSk8.Core — NO UnityEngine reference. Pure rules, fully unit-tested.
             ComboTracker, ScoringConfig, ScoreLedger, BalanceMeter, LandingJudge/SpinRules,
             RunTimer, GestureRules, SpecialMeter
  Runtime/   RetroSk8.Runtime — MonoBehaviours that feed events into Core.
    Data/      TrickDefinition, TrickLibrary, ScoringProfile, LocationDefinition, ContentRegistry, DefaultContent
    Input/     InputFrame, IInputSource, DeviceInputSource (kbd + any gamepad), TouchInputSource, PlayerInputRouter
    Player/    PlayerController (motor + state), TrickController, GrindController, ManualController,
               BailHandler, RespawnSafety, SkaterVisual, SkaterFactory
    Scoring/   ComboManager (combo + manual window + special), ScoreManager (banked total)
    Level/     HarborPlazaBuilder, ProcMesh, GrindRail, LevelInfo/KillVolume, PlaceholderMaterials
    Game/      SkateSceneInstaller (composition root), RunController, CameraRig, GameBootstrap, GameSession
    UI/        UIManager, HudView, TouchControlsView, PauseMenuView, DebugMenuView, ResultsView, UIFactory
    Audio/     AudioManager (Music/Effects/Ambience buses), ProceduralSfx, SkaterAudio
    Feedback/  HapticsManager (+ Plugins/iOS/RetroSk8Haptics.mm)
    Save/      SaveManager (local JSON, atomic write)
  Editor/    RetroSk8ProjectSetup (menu: Retro Sk8 → Setup Project)
Tests/EditMode/  NUnit tests for everything in Core
Tests/PlayMode/  Gameplay smoke tests (spawn, ollie, no double jump, flip banking, bail+respawn, grind, water respawn, run end,
                 contract goals into results, Daily Line, fountain gap, cosmetic buy/equip)
```

**Flow of one trick:** `PlayerInputRouter` merges all input sources into an `InputFrame` → `PlayerController` routes it by state (swipe → `TrickController`, action → `GrindController`/`ManualController`) → those call `ComboManager` → `ComboTracker` (Core) does the math → `HudView` and `SkaterAudio` react to events.

**Design choices**
- **The skate scene is almost empty on purpose.** `SkateSceneInstaller` builds the level, systems, skater, camera and UI in a fixed order, so dependencies are explicit and no scene-reference wiring can break.
- **State machine:** `SkaterState` (Rolling, Airborne, Grinding, Manual, Bailed) is owned by `PlayerController`. Sibling controllers request transitions and never set physics directly, except `GrindController`, which drives the body kinematically.
- **Arcade physics:** velocity is re-projected onto the ground plane each step, so quarter pipes and banks work at any speed. Steering has full grip. Takeoffs steeper than 65° get "vert assist", so you come back down onto the ramp.
- **Adding controllers:** implement `IInputSource` (for example a remappable MFi profile) and call `PlayerInputRouter.AddSource`. Gamepads, including Bluetooth and MFi, already work through `DeviceInputSource`.
- **Cinemachine** is used when the package is present (`RETROSK8_CINEMACHINE` version define). Otherwise a small built-in follower is used.
- **Addressables-ready:** gameplay reads content only through `ContentRegistry`. To move to Addressables, mark the trick, location and material assets addressable, give each list a label, and have the installer load the registry by address. No gameplay code changes are needed.
- **Audio mixer:** `AudioManager` works without a mixer. To use one, create an AudioMixer with groups Music, Effects and Ambience, expose their volumes as `MusicVolume`, `EffectsVolume` and `AmbienceVolume`, and assign it on the `AudioManager` (a prefab version comes in Phase 3).

---

## 5. Replacing placeholder assets

| Placeholder | Where | How to replace |
|---|---|---|
| Skater (primitives) | `SkaterVisual.Build()` | Make a prefab with your rigged model and keep the public methods (`SetTrickPose`, `SetBalancePose`, `PlayBail`, …) and drive an Animator from them. Instantiate it in `SkaterFactory`. |
| Park geometry | `HarborPlazaBuilder` | Build the park as a real scene or prefab. Keep a `LevelInfo` (spawn point, bounds, kill height) and a `GrindRail` on every grindable edge, and remove the builder. |
| Materials / colours | `PlaceholderMaterials`, `Palette` | Assign real materials in the prefabs. Keep `M_Placeholder_Lit` referenced by the registry until nothing uses it. |
| Sounds and music | `ProceduralSfx` (synthesised at runtime) | Import `.wav`/`.ogg` files and change `AudioManager.Clip()` to look up assigned clips first (keep the `SfxId` keys). |
| UI font / stickers | `UIFactory` (LegacyRuntime font, flat panels) | Swap in your licensed font and sprites in `UIFactory`. Every view is built through it. |
| Trick names / values | `ScriptableObjects/Tricks/*.asset` | Edit them in the Inspector. `DefaultContent` is used only for first-time setup and as a fallback. |

---

## 6. IP-clean asset rules (mandatory for every contributor)

1. **Only original material.** Do not use or reference any existing skateboarding game, franchise, publisher, pro skater, real skate brand, real skatepark, real song, existing map, HUD or menu layout, mission text, source code or audio.
2. **Names:** invent them. Check new trick, park, brand and character names with a quick web search to make sure they aren't existing trademarks or famous trick names. Generic words ("flip", "grab", "grind", "manual") are fine; established signature trick names are not.
3. **Art and audio:** create it yourself, commission it with a written assignment of rights, or use assets under a licence that explicitly allows commercial use (for example CC0). Record the source and licence of every third-party file in `Assets/RetroSk8/ThirdPartyNotices.md` (create it when you add the first one).
4. **No reference tracing.** Don't trace or closely copy screenshots, maps or UI from other games. Layouts must come from your own sketches.
5. **Temporary assets** must be generated in-project (as they are now) or be clearly named with a `TEMP_` prefix and listed in the notices file until they're replaced.
6. **Music:** placeholder music is synthesised by `ProceduralSfx.Music()`. Any replacement must be original or properly licensed. No sound-alikes of real songs.

*This is not legal advice. Have counsel review before a commercial release.*

---

## 7. Phase status

| Phase | Scope | Status |
|---|---|---|
| 1 | Architecture, Core rules + tests, Harbor Plaza slice, Two-Minute Run, results, touch/keyboard/gamepad input, placeholder audio, haptics, debug overlay | Done |
| 2 | Feel pass: bug fixes, jump buffer, landing/steer/grind assists, camera juice, live tuning panel, PlayMode smoke tests, iOS Simulator export | Done — still needs a hands-on playtest to lock numbers |
| 3 | Main menu, Customization scene, cosmetics and Tape Token shop, Spot Contracts, Daily Line, Free Skate entry, settings screen, GoalManager, gaps | Done |
| 4 | Neon Warehouse and Rooftop Run, conveyor belts, park contracts, Daily Line park rotation | **This delivery** — needs a hands-on playtest of both parks |
| 5 | ReplayManager (ghost capture + share clip), Addressables groups, device performance pass | Planned |
