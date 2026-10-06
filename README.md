# Retro Sk8 — Phase 7 (new skater, new tricks, accessibility, pass & play)

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

### Putting it on your iPhone
You need: a Mac with **Xcode**, **Unity Hub with the "iOS Build Support" module** for 6000.6.3, an iPhone with a USB cable, and an Apple ID. A free Apple ID works: the app runs for 7 days, then you rebuild. A paid developer account ($99/year) lasts a year and allows TestFlight.

1. **Unity → Retro Sk8 → Setup Project** (once). It sets the app name, landscape orientation, a bundle id of `com.<your Mac user name>.retrosk8`, and frame-timing stats.
   - Then **Retro Sk8 → Ship Check**. It lists anything still missing (scenes, bundle id, version, input system, URP, content, icon, last test run), offers to run Setup if that fixes it, and writes `Temp/RetroSk8ShipCheck.txt`.
   - Optional but recommended: **Retro Sk8 → Run All Tests**. It runs the EditMode and PlayMode suites and writes `Temp/RetroSk8TestReport.txt` (totals on the first line, then any failures).
2. **Unity → Retro Sk8 → Build iOS → Xcode Project for iPhone (Release)**. The first time, Unity switches the platform to iOS, which takes a few minutes. When it's done, Finder opens `Builds/iOS-Device/Unity-iPhone.xcodeproj`.
3. Double-click it to open in **Xcode**. Click **Unity-iPhone** at the top of the left sidebar, then the **Signing & Capabilities** tab. Tick **Automatically manage signing** and choose your **Team** (add your Apple ID under *Xcode → Settings → Accounts* if the list is empty).
   - If Xcode says the bundle identifier isn't available, change it to something unique (for example add `.dev` to the end) in the same tab.
4. Plug in the iPhone, unlock it and tap **Trust**. On iOS 16 or later, turn on *Settings → Privacy & Security → Developer Mode* and restart the phone when asked.
5. Pick your iPhone in Xcode's device menu at the top of the window and press **⌘R**.
6. The first launch is blocked until you trust yourself as a developer: on the phone, *Settings → General → VPN & Device Management → your Apple ID → Trust*. Then open Retro Sk8.

After that, each new build is: **Build iOS → Xcode Project for iPhone (Release)** → **Replace** when Unity asks → **⌘R** in Xcode. Every build bumps the build number automatically.

### TestFlight (keeps the app for 90 days, and lets friends test it)
Needs a paid **Apple Developer Program** membership.
1. In [App Store Connect](https://appstoreconnect.apple.com) → **Apps → + → New App**: platform iOS, name *Retro Sk8* (or any free name), the same bundle id as Unity's Player Settings, and any SKU.
2. Build as above, then in Xcode choose **Any iOS Device (arm64)** as the destination and **Product → Archive**.
3. In the Organizer window that opens: **Distribute App → App Store Connect → Upload**, keeping the defaults.
4. After processing (about 10–30 minutes, and you'll get an email), open the app in App Store Connect → **TestFlight**. Add yourself under *Internal Testing*, then install the **TestFlight** app on your phone and accept the invite.

The build already includes everything App Store Connect checks for:
- the original app icon and launch screen (`Art/AppIcon`, generated by `Tools/make_icons.py`)
- a privacy manifest declaring no tracking and no data collection
- `ITSAppUsesNonExemptEncryption = NO`, so there's no export-compliance question
- full-screen landscape on iPad
- the marketing version from `RetroSk8ProjectSetup.AppVersion` (0.26.0 now)
Use the **Development + Profiler** build only when you want Unity's Profiler connected. It runs slower, so judge the feel on Release builds.

The native bridges (`Plugins/iOS/RetroSk8Haptics.mm` for the Taptic Engine, `RetroSk8ReplayKit.mm` for run clips) compile into the build automatically, and `RetroSk8IOSPostBuild` links ReplayKit.

### Running on the iOS Simulator (Xcode)
1. You need **Unity Hub + Unity 6 LTS with the "iOS Build Support" module**. Xcode alone can't build a Unity game: Unity exports the Xcode project that Xcode then builds.
2. In Unity: **Retro Sk8 → Build iOS → Xcode Project for Simulator**. This exports `Builds/iOS-Simulator/Unity-iPhone.xcodeproj` and reveals it in Finder.
3. Open it in Xcode, choose the **Unity-iPhone** scheme and any iPhone simulator, and press **⌘R**. No signing team is needed for the Simulator.
4. For a physical iPhone, follow **Putting it on your iPhone** above.

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
| Lip trick | Tap **GRIND/MANUAL** at the top of a quarter pipe, square to the coping (stick picks the stall); tap again or JUMP to drop in | E | West / R1 |
| Wallride / wallplant | Tap **GRIND/MANUAL** in the air just before touching a wall: glancing = wallride, head-on = wallplant. JUMP on the wall = wallie | E | West / R1 |
| Revert | Swipe left/right within 0.3 s of landing back on a ramp | J / L | Right-stick flick |
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

## 3e. Phase 5: ghosts, clips, device performance

**Best-run ghost.** Every run is recorded at 20 samples per second: position, rotation and the skater's pose, so flips, grinds and bails replay too. When a run sets a new best score at a park, it is saved as that park's ghost (`<persistent data>/ghosts/<park>.ghost`, a few hundred KB). In your next **Two-Minute Run** there, a glowing cyan ghost skates your best line next to you. It has no collisions and doesn't score. *Settings → GHOST* turns it off. *Reset progress* deletes ghosts too.

**Share clips (iPhone).** Turn on *Settings → CLIPS* (only shown where iOS supports screen recording). Each run is recorded with Apple's ReplayKit; iOS asks permission the first time. On the Results screen, **SHARE CLIP** opens Apple's preview, where you can trim, save to Photos or share. The microphone is never recorded. Quitting a run early throws the clip away.

**Device performance.** Aimed at a steady 60 FPS on phones:
- Each park's hundreds of static pieces are batched together when the park builds, which cuts draw calls.
- On device, the game renders at up to 1080p internally (sharp on phones, much cheaper than native 3x resolution) and caps shadow distance at 45 m.
- **Adaptive resolution:** when frames run long for 1.5 s, the render scale steps down (to 0.7 at most). With 5 s of headroom it steps back up. It uses the real CPU/GPU frame times from frame-timing stats, which Setup enables.
- *Debug menu → PERFORMANCE HUD* shows FPS, CPU and GPU milliseconds, and the current render scale on screen. The debug overlay shows the same numbers.
- The editor and desktop builds never change your URP asset.

**Addressables (optional).** Install **Addressables** from the Package Manager, then run **Retro Sk8 → Setup Addressables Groups**. It creates `RetroSk8_Core`, `RetroSk8_Parks` and `RetroSk8_Cosmetics` groups, using asset names as addresses and labels like `park` and `cosmetic`. The game still loads content through `ContentRegistry` references; switching runtime loading over to Addressables is future work. This tool hasn't been run yet because the package isn't installed in the current project.

---

## 3f. Phase 6: lesson, records, Game Center, sound

**How to Skate (first-run lesson).** The first time you press PLAY, the game offers a two-minute lesson. It's also on the main menu as **HOW TO SKATE**. The lesson runs in Harbor Plaza with no timer, one skill at a time: push, ollie, flip, grab, spin, grind, manual, then bank a 3-trick combo. Each step completes when you actually do it (`Core/Tutorial.cs`). The coaching card shows touch or keyboard instructions to match your device. Finishing pays **+50 Tape Tokens** once; **SKIP** is always there.

**Records and achievements.** The **RECORDS** button on the main menu's info card shows:
- best score, best combo and contract stars per park
- Daily Lines cleared, gaps found, and your token total
- 11 original achievements with progress (`Core/Achievements.cs`)

Achievements unlocked by a run are listed on the Results screen. Everything works offline.

**Game Center (optional, needs a paid Apple account).**
1. Turn on **Retro Sk8 → Build iOS → Enable Game Center**. Builds then add the Game Center capability, and the game signs in quietly on launch. With it off, builds still sign with a free Apple ID and the game uses only local records.
2. In App Store Connect → your app → **Services → Game Center**, create:
   - **Leaderboards** (Classic, High Score, integer): `retrosk8.score.harbor_plaza`, `retrosk8.score.neon_warehouse`, `retrosk8.score.rooftop_run`
   - **Achievements** (`retrosk8.ach.` + id): `class_dismissed`, `first_bank`, `line_10k`, `line_50k`, `run_100k`, `spin_540`, `gap_hunter`, `contractor`, `all_contracts`, `daily_regular`, `tourist`. Titles and descriptions are in `Core/Achievements.cs`.
3. New best scores and achievements are reported after each run. Local bests sync when you sign in. **RECORDS → GAME CENTER** opens Apple's leaderboards.

**Sound and music pass.**
- **Music:** four original songs are composed in code (`Core/MusicComposer.cs`): drums, bass, chords and a seeded melody in an AABA form, rendered as seamless loops.
  - Menu: 112 BPM, minor
  - Harbor Plaza: 96 BPM, swung dorian
  - Neon Warehouse: 124 BPM, four-on-the-floor minor
  - Rooftop Run: 108 BPM, bright lydian

  Songs crossfade between menu and park, and the music ducks for a moment when you bail.
- **Surface sounds:** the wheels sound different on concrete, plywood, metal and conveyor belts, and grinds differ between metal (rails, coping) and ledges. Landings on plywood thump lower. `Level/SurfaceLookup.cs` picks the surface from a `SurfaceTag` or from the placeholder material's palette colour.

---

## 3g. Phase 7: look, tricks, accessibility, pass & play

**New skater and visual polish.**
- The skater is now an original low-poly figure with jointed limbs, a capped head and chunky shoes. Knees bend for crouches, with two-bone leg IK keeping the feet on the deck. Arms balance on grinds and reach for grabs, and bails flail.
- Landings kick up dust tinted by the surface. Metal grinds and wallrides throw sparks.
- Speed lines appear at high speed.
- Each park has a post-processing look: bloom (strong in Neon Warehouse), a soft vignette and a little contrast. Lighting uses a three-colour ambient for more depth.
- *Visual effects: Low* turns particles and post-processing off.

**New tricks** (rules in `Core/AdvancedTricks.cs`; runtime in `WallController` and `LipController`):
- **Lip tricks:** stall on quarter-pipe coping when you come up square to it. The stick picks Coping Stall, Nose Hang, Tail Perch, Lean Plant or Hand Hold. Points accrue while you hold it, and you drop back in after 2.5 s or on a tap.
- **Wallrides:** ride along walls for up to 1.4 s with reduced gravity. JUMP for a **Wallie**. A head-on hit is a **Wallplant** that springs you back off the wall.
- **Reverts:** spin back to fakie right after a ramp landing. The combo stays open, so revert into manual keeps the line going.
- The new trick types count as new categories for Line Flow.

**Controls & accessibility** (Settings → CONTROLS & ACCESSIBILITY):
- **Touch layout editor:** drag the steer stick, JUMP and GRIND buttons anywhere; set button size (75–140%) and stick sensitivity; swap sides for a left-handed layout; reset.
- **Reduced motion:** no landing shake, speed-FOV punch or speed lines.
- **Larger text:** small UI text 15% bigger.
- **Colour-safe HUD:** success/fail colours become blue/orange instead of teal/coral.
- **Visual effects:** full or low.

**Pass & Play** (PLAY → PASS & PLAY, 2–4 players on one phone; rules in `Core/PartyRules.cs`):
- **Letters:** the setter banks any combo. Everyone else must bank 80% of it or take a letter. Spell R-E-T-R-O and you're out; the last skater standing wins.
- **Score turns:** 60 seconds each, highest score wins.
- A hand-off screen between turns says who's up and what they need, and the final screen offers a rematch.

---

## 3h. Phase 8: Retro City and Sunset Bowls

**Two new places to skate**, giving five in total:
- **Retro City** (`RetroCityBuilder`) is an open skate city about 250 m across. It has a street grid with grindable curbs and benches, a ring of neon-banded buildings, and nine districts, each with its own spot:
  - Civic Steps (an 8-stair with rail and hubbas)
  - Downtown Double Set
  - a public Retro Skatepark (mini ramp, bowl, funbox, quarter)
  - Schoolyard Banks
  - Parking Lot Curbs
  - a Drained Canal you can ride end to end
  - Mall Ledges
  - a Backyard Pool
  - Loading Docks with a dock gap and a wallride wall
- **Sunset Bowls** (`SunsetBowlsBuilder`) is an all-transition park at golden hour. It has a deep end, a shallow bowl, a mini ramp, a volcano, a funbox, a flat rail and a quarter-pipe wall.
- Both parks have spot contracts, gaps for the Daily Line, their own music (City: swung dorian groove; Bowls: bright major) and city ambience.

**Different ways to play the city** (`Core/RetroCity.cs` holds the rules; `Game/CityController.cs` runs them). Menu → **EXPLORE CITY** opens Retro City with no timer.
- **Explorers:** skate into a district to *find* it (+5 Tape Tokens). There are 20 hidden cassette **tapes** to collect (+10 each), placed on ledges, over gaps and down in the canal.
- **Line skaters:** every spot has a pink **challenge marker**. Roll onto it for a 40-second session where your best banked combo earns bronze, silver or gold. Leaving the spot ends the session.
- **Speed skaters:** three **checkpoint races** (Downtown Dash, Ring Road, Canal Cut). Roll through the yellow start arch. Only the next gate is shown, and a HUD arrow points to it. Gold, silver and bronze are awarded by time, and the best time is saved.
- **Map:** Pause → **MAP** shows streets, found spots with their medals, race starts and your position. Tap a found spot to fast travel there, or tap a race flag to warp to its start line.
- Two-Minute Runs, contracts and Daily Lines in the city still count spot discovery and tapes. Challenges and races are Explore-only, so they never interfere with a scored run.
- Medals pay the *difference* when you improve (bronze 5, silver 15, gold 35 tokens). City progress appears in Records.

**Photo mode** works in every park: Pause → **PHOTO**. The game freezes and the HUD hides. Drag to orbit the skater; use +/− to zoom, UP/DOWN to move the focus, and WIDE/TIGHT to change the lens. Use the phone's own screenshot to save the shot. EXIT puts the gameplay camera back.

**New parks work without re-running Setup.** If a park's scene isn't in Build Settings yet, `SceneRouter` loads another park scene and the installer swaps in the right builder. `ContentRegistry.WithDefaults` also adds any built-in park or contract that your saved registry is missing. Running **Retro Sk8 → Setup Project** is still recommended, because it creates the real scenes, icons and version number.

**About real skatepark blueprints.** Real parks' plans were deliberately not copied. Real parks, brands and places are off-limits under the IP rules (section 6), and park designs are their designers' work. Instead, obstacle *sizes* follow public design guidance, and every layout is original:
- stair rise and run of about 0.22 m and 0.42 m
- ledges 0.3–0.6 m tall
- mini ramps about 1.2–1.5 m
- bowls 1.6–2.8 m deep with a flat bottom and 2–3 m transitions
- rails about 0.3–0.5 m high

Sources consulted: [Surfertoday obstacle guide](https://www.surfertoday.com/skateboarding/the-definitive-guide-to-skateboard-obstacles-and-elements), [ofstandard.com park dimensions](https://ofstandard.com/general/standard-dimensions-of-skateboard-park), [OC Ramps ramp construction details](https://ocramps.com/pages/a-frame-construction-details), [World Skate facility certification rules (PDF)](https://www.worldskate.org/images/Documents/2023/FINAL_World_Skate_-_Facility_Certification_Rules_Vesrion_1.0_-_October_2021_1.pdf), [K-State "Skate parks: a guide for landscape architects and planners" (PDF)](https://krex.k-state.edu/dspace/bitstream/2097/954/1/DesmondPoirier2008.pdf).

---

## 3i. Phase 9: Create-a-Park, Career, Create-a-Skater, ship prep

**Create-a-Park** (menu → CREATE-A-PARK; rules in `Core/CustomPark.cs`, editor in `ParkEditorController` / `ParkEditorView`, builder in `CustomParkBuilder`):
- Six park slots. BUILD opens a new slot with a small starter layout; EDIT, FREE SKATE, 2-MIN RUN and DELETE (tap twice) work on saved parks.
- The lot is 80 × 80 m on a grid of 2 m cells, with walls all round and a start pad at the south edge that always stays clear.
- **12 obstacles**, all built from the same spot kit as the real parks: ledge, flat rail, stairs (with a handrail, a hubba and a back bank), kicker, quarter pipe, bank, funbox, mini ramp, bowl (with banks up to the deck), manual pad, bench and wall.
- **Tap an obstacle to select it**, then use the big **arrow pad** to move it one cell at a time: up, down, left or right. Holding an arrow keeps it moving. On a Mac the arrow keys (or WASD), R to turn and Delete also work.
- With a piece selected you can TURN it 90°, change its SIZE (low/mid/high), COPY it or DELETE it. Moves are blocked by the grid edge, the start pad and other pieces; the editor says "BLOCKED" when that happens.
- Drag the map to look around and zoom with + and −. Pick a theme (Daylight, Neon Night, Sunset), rename the park in the top bar, SAVE, then **SKATE IT**. In a custom park, Pause → **EDIT PARK** goes back to the editor.
- Custom parks keep their own best scores and ghosts. Up to 60 pieces per park.

**Career** (menu → CAREER; rules in `Core/Career.cs`, `Game/CareerService.cs`):
- Eight chapters with fictional crews and sponsors: Harbor Plaza → Neon Warehouse → Rooftop Run → Sunset Bowls → Retro City spots and tapes → city races → build your own park → Retro Legend.
- Each chapter has a short story and three goals that read your normal progress, so goals you finish in any mode count. Goals cover park scores, combos, named gaps, contract stars, city spots, tapes and medals, race medals, Create-a-Park, daily lines and overall scores.
- Finishing a chapter pays Tape Tokens, gives you a title (DOCK RAT … RETRO LEGEND) and opens the next one. GO takes you to the right place.
- Goal and chapter messages appear on the Results screen, in the city, and when you save a park.

**Create-a-Skater + board maker** (menu → SKATER → CREATE-A-SKATER; `Core/SkaterLook.cs`):
- Body and face: 8 skin tones, 7 hair styles (cap only, short, afro, bun, long, mohawk, twists), 8 hair colours, 3 builds, eyewear and shoe colour. Gear (deck, wheels, grip, shirt, hat, pants) stays in the token shop.
- **Board art:** 12 patterns, 12 colours, and 10 original 8 × 8 pixel stickers placed on the nose, middle or tail. Five of the stickers unlock through Career chapters.
- Every change previews live on the turntable skater. SAVE keeps it, and the in-game skater wears it everywhere.

**Ship prep:**
- **Retro Sk8 → Ship Check** and **Retro Sk8 → Run All Tests** write report files that can be read outside Unity (see *Putting it on your iPhone*).
- Setup now sets version **0.9.0**. It only raises older prototype versions and never lowers one you set yourself.

---

## 3j. Phase 10: share codes, trick pack, living city, S.K.A.T.E.

**Share codes** (`Core/ShareCodes.cs`). Codes are plain text, so they can go in any message app; there's no server.
- **Park codes** (`RP-…`): Create-a-Park → SHARE copies a slot's code. PASTE PARK CODE (or menu → CODES) adds a friend's park to your first empty slot.
- **Challenge codes** (`RC-…`): after a Two-Minute Run, **CHALLENGE A FRIEND** copies a code for your score. A challenge on your own park carries the whole park with it.
- Your friend pastes the code in menu → CODES and taps TAKE IT ON. They see "BEAT 25,430 · FROM YOU" during the run, and Results says whether they did it.
- Codes ignore case, spaces and dashes, and treat I/L/O as 1/1/0. A checksum catches typos.
- Your name on codes is set in CODES.

**Trick & style pack** (`Core/StyleTricks.cs`). Every new trick is a variation on an input you already use:

| Trick | How |
|---|---|
| No-Comply | Hold the stick **down** and tap JUMP on flat ground or out of a manual |
| Boneless | Hold the stick **down** and do a charged pop |
| Bluntslide | Stick **down** as you start a grind on a ledge or coping |
| One-Foot Manual / Casper | Stick **left** / **right** as you start a manual |
| Footplant | Stick **down** when you hit a wall head-on |
| Signature special | Full special meter + flip swipe: your style's own trick |

- Pick a **style** in Create-a-Skater: Street (Gridline Lightning), Vert (Skylight Orbit), Tech (Tidewater Triple) or Flow (Sundown Carousel).
- Up on the stick stays free for pushing.

**Living city** (`Core/CityLife.cs`, `Game/CityLifeController.cs`):
- An 8-minute day. Runs start in the afternoon and go through golden hour to night, when the neon glows brighter.
- Rain showers come and go, with fog and a rain sound.
- Cars circle the ring road and the downtown block. They honk if you're in their lane, and a hit knocks you off your board.
- Pedestrians walk the sidewalks and hop out of your way.
- **Street events** pop up in Explore:
  - **Block Party:** 2× points in a district for a minute.
  - **Golden Tape:** grab it within 45 s for 40 tokens.
  - **Photo Shoot:** land 1,500+ near the camera for 30 tokens.
- The HUD shows the clock and weather, and the arrow points to the event.

**Game of S.K.A.T.E.** (menu → S.K.A.T.E. BATTLE; rules in `Core/SkateDuel.cs`, runtime in `Runtime/Duel/`):
- **Rules:** the setter banks any line, and the other skater must bank 80% of it or take a letter. The setter keeps setting until they miss, then the set passes. Spell S-K-A-T-E and you lose.
- **Live online** through **Game Center real-time matches**: invite a friend or get matched. Apple handles matchmaking and relays the messages, so there's no server to run.
  - On your turn you get a 3-second countdown, then one 25-second attempt.
  - Your skater streams to the other phone, which watches you live as a pink ghost.
  - Rematch and forfeit-on-leave are handled.
  - **Requirements:** Game Center turned on in the build, a paid Apple developer account, and two signed-in iPhones. The native side (`Plugins/iOS/RetroSk8Duel.mm`) has only been syntax-checked against stand-in headers. It has **not** been run on devices.
- **Vs CPU** (Easy / Medium / Hard, any park) uses the same turn flow offline and works everywhere.

**Polish:** every scene now fades in from black.

---

## 3k. Phase 11: first iPhone build, replay editor, crew, weekly events

**First iPhone build: the checklist.** Do these in order. Steps 2 and 3 write report files that Claude can read and fix from directly.
1. In Unity: **Retro Sk8 → Setup Project**. It creates the missing park scenes (Rooftop Run, Sunset Bowls and Retro City weren't there yet), adds them to Build Settings and sets the version (0.12.0 as of Phase 12). It keeps your bundle id `com.omariibell.retrosk8`.
2. **Retro Sk8 → Ship Check.** Every line should say OK except the tests line, which step 3 fixes. If it offers **Run Setup**, say yes.
3. **Retro Sk8 → Run All Tests.** This takes a few minutes; Unity enters Play mode by itself for the PlayMode half. Leave it alone until the Console prints the test report.
4. In Unity Hub, make sure 6000.6.3 has the **iOS Build Support** module (Installs → ⚙ → Add modules).
5. **Retro Sk8 → Build iOS → Xcode Project for iPhone (Release)**, then follow *Putting it on your iPhone* (section 1): sign with your Apple ID, plug in the phone and press ⌘R in Xcode.

**Replay editor** (`Core/ReplayLibrary.cs`, `Replay/ReplayTheater.cs`):
- Every finished run is saved automatically. The newest 6 are kept, and starred replays are kept for good (up to 20).
- In Free Skate or Pass & Play, Pause → **SAVE REPLAY** keeps the last two minutes.
- Open a replay from Results (**WATCH REPLAY**) or menu → **REPLAYS**.
- **Playback controls:** play/pause, ±5 s, 1× / 0.5× / 0.25×, a scrub bar with ticks where you banked lines (the line's name pops up as it plays), and **SET IN / SET OUT** to pick a clip.
- **Cameras:** Follow, **Fisheye** (low and wide, filmer-on-a-board), **Tripod** (a filmer set up ahead of you), and free **Orbit**.
- **EXPORT CLIP** records the in→out range with the UI hidden through ReplayKit, then **SHARE CLIP** opens the iOS share sheet. This needs a device; elsewhere, use iOS screen recording.

**Crew mode** (menu → CREW; `Core/Crew.cs`):
- There are eight original, fictional skaters, each with a style, a home park and a perk:
  - +6% points
  - +20% Tape Tokens
  - special meter fills 25% faster
- **Recruit** someone by beating them at S.K.A.T.E. (CPU at their skill) or beating their score in a Two-Minute Run at their park.
- **Two ride with you** at a time, and their perks add up.
- Every banked combo gives **crew XP** (1 per 100 points). That builds 10 crew levels, each paying tokens, and levels 3, 6 and 9 unlock crew stickers for the board maker.

**Weekly events** (menu → THIS WEEK; `Core/WeeklyEvents.cs`):
- A new event every Monday, the same for everyone, picked from the ISO week number:
  - Neon Nights: the city stays dark.
  - Rain Season: it rains all week.
  - Race Week: double race tokens.
  - Builders' Week
  - Gap Hunt: double gap points.
  - Crew Week: double crew XP.
- Each event has three goals (+40 tokens each, +60 for all three).

**Game Center leaderboards.** These only work after you create them in App Store Connect with these ids:

| Leaderboard id | What | Sort |
|---|---|---|
| `retrosk8.weekly` | Best run this week (set it as a **recurring** weekly leaderboard) | High to low |
| `retrosk8.race.downtown_dash`, `.ring_road`, `.canal_cut` | City race times in hundredths of a second | **Low to high** |
| `retrosk8.challenge.<spot id>` (9 spots) | Best spot-challenge combo | High to low |
| `retrosk8.skate.wins` | S.K.A.T.E. games won | High to low |
| `retrosk8.score.<park id>` (Phase 6) | Best score per park | High to low |

## 3l. Phase 12: sound & music, controllers + iPad, ghost races, shop

**Sound & music** (`Core/Radio.cs`, `Audio/AudioManager.cs`, `Audio/ProceduralSfx.cs`):
- **In-game radio:** three stations of original songs written by the music composer (nothing licensed): LOW TIDE FM (laid-back swing), CONCRETE 101 (fast and loud) and SUNSET CASSETTE (bright). Each song plays about 80 seconds, then the station moves on in a shuffled order.
- The music button (Settings, or the pause menu) cycles **PARK THEMES → each radio station → OFF**. NEXT SONG skips. A small "now playing" card shows when a song starts. The radio keeps playing across menus and parks.
- Radio songs render on a background thread, so tuning in never stalls the game.
- **New sounds:** a distant crowd cheers lines over 6,000 points and goes "ooh" at big slams (parks only; Settings → CROWD to turn it off), wind on big airs, a coin chime for purchases, countdown beeps.

**Controllers + iPad:**
- Any Xbox, PlayStation or MFi controller already skated (Phase 1). Now it also drives **every menu**: D-pad / left stick to move, **A / Cross** to press, **B / Circle** to go back, left/right to change sliders. A tape-yellow frame shows where you are, and covered buttons are skipped. During a run only overlays (pause, maps, end screens) can be selected, so JUMP never presses a HUD button.
- The on-screen touch controls fade out while a controller is in use and come back on the first touch. The pause menu shows the controller layout when one is connected.
- **iPad:** the app now targets iPhone **and iPad** (Setup Project sets it; Ship Check checks it). On squarer screens the UI scales to a wider canvas (`Core/UiScale.cs`), so the wide menus fit on 4:3 iPads and older 16:9 iPhones.
- The Xcode project declares controller support (`GCSupportsControllerUserInteraction`, extended gamepad profile).

**Async ghost races** (`Core/GhostCodes.cs`):
- After a Two-Minute Run, Results → **SEND MY GHOST** copies a ghost code: your whole run, not just your score. Send it any way you like (Messages, email, notes).
- A friend opens CODES → **PASTE** → **RACE THE GHOST**. Your run skates next to theirs as a pink ghost, and the HUD shows their score as it happens ("OMARI 12,300 · YOU +450 · BEAT 25,430").
- Ghost codes are long (about 35-40k characters for a full run), so paste them; don't type them. Line breaks that mail apps add are ignored. Custom parks travel inside the code.
- CODES lists your **recent ghost races** (won/lost, scores).
- **Friends leaderboards:** Records → **FRIENDS** (builds with Game Center on) shows your Game Center friends' best on each park, from the existing `retrosk8.score.<park id>` leaderboards (friends-only scope). Phase 14 folds this into Records → **LEADERBOARDS**.

**Shop + cosmetic packs** (menu → SHOP; `Core/Shop.cs`, `Game/StoreService.cs`, `Plugins/iOS/RetroSk8Store.mm`):
- **FEATURED TODAY:** four Tape Token items, the same for everyone that day. The first is 25% off.
- **Three optional App Store packs**, looks only (no stats, no tokens, no random rewards), each six items: NIGHT SHIFT, DESERT HEAT, ARCADE CABINET. Pack items show "IN SHOP PACK" in SKATER until bought.
- **RESTORE PURCHASES** brings packs back on a new phone (App Review requires it).
- A progress reset never removes a paid pack.
- In the Unity editor the shop runs a labelled **TEST STORE** that unlocks without charging, so you can try it.

**To sell the packs for real (App Store Connect):**
1. You need the **Paid Apps agreement** (banking and tax) signed in App Store Connect → Business.
2. App Store Connect → your app → **In-App Purchases** → add three **Non-Consumable** products with exactly these ids. Give each a name, a price (the code assumes $1.99) and a review screenshot of the shop.

| Product id | Pack |
|---|---|
| `com.omariibell.retrosk8.pack.nightshift` | Night Shift Pack |
| `com.omariibell.retrosk8.pack.desertheat` | Desert Heat Pack |
| `com.omariibell.retrosk8.pack.arcade` | Arcade Cabinet Pack |

3. To test before release, use a **Sandbox tester** (Users and Access → Sandbox) on the iPhone (Settings → App Store → Sandbox Account). The build gets the In-App Purchase capability automatically.
4. Purchases are verified by the App Store on the device; there is no receipt server. That's fine for cosmetics.
5. The bridge uses StoreKit 1, so Xcode may show deprecation warnings. They're harmless; it still works on current iOS.

---

## 3m. Phase 13: story, a new park, a bigger city, outfits, onboarding + juice

**Story mode: "The Last Spot"** (menu → STORY; `Core/Story.cs`, `UI/StoryPanelView.cs`):
- Six chapters, two steps each, played in order:
  - **Score runs:** beat a score in a Two-Minute Run.
  - **Line battles:** out-skate a rival. Their score climbs in the HUD as the run goes.
  - **S.K.A.T.E. games:** beat a rival on flat.
- Each step opens and closes with comic panels. NEXT reads on and SKIP jumps ahead.
- Clearing a step pays tokens (40, more for the finale). Results says whether you cleared it; HOME plays what happens next.
- The story: you arrive in Retro City, join Pilar and Dex, and stop The Gloss (a slick, sponsored rival crew) from booking the city's spots as private film sets. It ends in a showdown with Val Sterling at Floodgate Ditch.
- Every character and name is original and fictional.

**New park: Floodgate Ditch** (`Level/FloodgateDitchBuilder.cs`):
- A concrete drainage channel at dusk. You drop in from the top of a 4.4 m dam down a steep spillway.
- A 4 m ditch with 1.8 m banks and coping on both lips, made for bank-to-bank transfers. A kicker launches over the outlet wall.
- Yards on both sides: an 8-stair with a handrail, a culvert mini ramp, sluice ledges, a hubba and a flat bar.
- Named gaps: Ditch Transfer, Outlet Gap, Spillway Drop.
- It has a contract, its own song and water ambience, works with share codes, ghost codes and S.K.A.T.E. (index 5), and appears in the Daily Line rotation.
- **Setup Project** creates its scene (`SkateScene_FloodgateDitch`). Until then it borrows another park's scene, like custom parks do.

**Bigger city: Riverside Yards** (north of the old city wall; `Core/RetroCity.cs`):
- A freight yard with two boxcars and a 4 m Boxcar Gap between them, track rails, a loading platform with a ledge and stairs, and a bank-to-wall.
- 4 new tapes (24 total), a new spot challenge and a new race, **River Run**.
- The pause map now shows the yards.

**Clothes, shoes and board** (Create-a-Skater, which now has five tabs):
- **CLOTHES:** shirt cut (Tee, Long Sleeve, Hoodie, Tank, Flannel, Jersey), shirt colour and trim, bottoms (Jeans, Chinos, Cargos, Shorts, Sweats) and their colour.
- **SHOES:** style (Low-Top, High-Top, Slip-On, Chunky), colour, soles and socks.
- **BOARD:** deck (shop or your own graphic), shape (Popsicle, Cruiser, Old School), wheels, trucks and grip tape.
- **BOARD ART:** the board maker, as before.
- Every colour row starts on **SHOP / CLASSIC**, which keeps the equipped shop gear, so older saves look exactly as before. Pick a colour to override it.

**Onboarding + juice:**
- **First launch:** a three-panel welcome, then straight into the lesson. Players who already did the lesson never see it.
- **Tips:** one-time hints during your first runs. Examples: no ollie yet, airs without tricks, sketchy landings, no grinds, special ready, a lost combo. Each shows once, ever.
- **Big moments:**
  - Fireworks over banked lines of 8,000+.
  - A short slow-motion beat on lines of 15,000+ (physics stays smooth).
  - Fireworks are skipped with Low effects. Slow-mo is skipped with Reduced Motion and is off in S.K.A.T.E. and Pass & Play.
- **Fanfares** for unlocks and cleared story steps.
- **Title screen:** the bands slide in, the logo pops, the tagline drops, then the logo sways gently.

## 3n. Phase 14: comic cutscenes, Trick Book, Leaderboards hub

**Comic cutscenes with real characters** (`UI/ComicView.cs`, `UI/PortraitStudio.cs`, `UI/ComicArt.cs`, `Core/StoryCast.cs`):
- Story panels and the welcome are now full comic pages: a spinning sunburst behind an inked panel, halftone dots, a speech bubble that types out each line, a name sticker that slams in, and a **VS!** starburst when a rival talks. Narration sits in a yellow caption box.
- Each speaker appears as a real 3D skater, rendered live by a tiny off-screen studio (its own layer far below the world, one render texture, on only while a comic is open). **YOU** wears your own Create-a-Skater look and equipped gear; the crew wear their roster looks; The Gloss wear matching cream-and-coral jerseys, and Val Sterling wears the hoodie and shades.
- Panels slide in from alternating sides. Tap anywhere (or NEXT) to read on; SKIP is always there; the last button reads **LET'S GO!**. Reduced Motion turns off the sway and the slide-ins.

**Trick Book** (menu → **TRICKS**, which replaces HOW TO; `Core/TrickBook.cs`, `Game/TrickBookService.cs`, `UI/TrickBookView.cs`):
- Every trick in the game, in eight tabs: Flips, Grabs, Shoves, Grinds, Manuals, Lips + Walls, Spins + More, Specials.
- Each trick has a **how to** written from the game's real input rules, your stats (times landed, best line, longest line, parks) and **four challenges**:
  - Land it 10 times (+25)
  - Land it in a 5-trick line (+25)
  - Land it at 3 parks (+25)
  - Land it in a 10,000+ line (+40)
- Only **banked** lines count (a bail doesn't), and gaps aren't tricks. Payouts show on the results screen.
- **PLAY THE LESSON** in the Trick Book starts the two-minute lesson.

**Leaderboards hub** (Records → **LEADERBOARDS**, which replaces FRIENDS; `Core/LeaderboardHub.cs`, `UI/LeaderboardsView.cs`):
- Every Game Center board in one screen, in four groups: parks, races (times), spot challenges and events (this week's best run, S.K.A.T.E. wins). Flip boards with the arrows.
- **FRIENDS** or **EVERYONE**. Everyone shows the top 10, then (if you're further down) the players right around you.
- Your row is highlighted, the player just above you is marked **NEXT UP**, and the strip at the bottom says what it takes: "BEAT #4 MAYA: 1,201 MORE".
- Signed out or offline, it still shows your own best for each board.
- Native: `RetroSk8_GCLoadScores` (global or friends scope, with a total-player line) in `RetroSk8GameCenter.mm`. The boards must exist in App Store Connect (ids above).

## 3o. Phase 15: tests on demand, Moonlight Pier, City Jam, streaks + reminders, launch kit

**Tests on demand** (`Editor/RetroSk8RemoteBridge.cs`):
- The open editor watches `Temp/RetroSk8Remote.request`. Write one command per line: `refresh` (import changed scripts), `shipcheck`, `tests` (all EditMode + PlayMode tests, then Ship Check). Progress goes to `Temp/RetroSk8Remote.log`; reports go to the usual `Temp/RetroSk8TestReport.txt` and `Temp/RetroSk8ShipCheck.txt`.
- It never acts while compiling or playing, and Ship Check runs without dialogs this way. The menu items work as before.

**New park: Moonlight Pier** (`Level/MoonlightPierBuilder.cs`):
- A seaside boardwalk at night. The shore plaza has a funbox, seawall ledges, benches, planters, manual pads and a raised promenade with a 6-stair, a handrail and a hubba.
- A long wooden pier with grindable railings on both sides. Halfway out the planks are missing: hit the kicker and clear the **Plank Gap**. The T-shaped pier head has a quarter pipe facing back to shore. Fall in the sea and you wash back up.
- Named gaps: Plank Gap, Boardwalk Hop, Pier End Air. A contract, its own song (swung, dreamy) and wave ambience with a far bell buoy. Works with share codes, ghost codes and S.K.A.T.E. (index 6) and the Daily Line rotation.
- **Setup Project** creates `SkateScene_MoonlightPier` (the prompt asks again for v15). Until then it borrows another park's scene.

**City Jam** (Retro City; map → **START CITY JAM**; `Core/CityJam.cs`):
- A daily route of four spots, the same for every player that day. Ride to each stop (the HUD arrow points the way), then bank its target inside the spot within 30 seconds. The whole jam has a 5-minute clock.
- 2 stops = bronze, 3 = silver, all 4 = gold (+20 / +40 / +80 tokens). Replaying the same day only pays the improvement. Leaving a stop mid-session ends the jam.
- The map lists today's route and your medal.

**Daily streak** (`Core/Streaks.cs`, `UI/StreakView.cs`):
- Opening the game on a new day checks in and shows a 7-day reward card (10, 10, 15, 15, 20, 25, then 50 on day 7), plus +100 every 30 days.
- **Tape Savers:** you earn one every 7 days (hold up to 2). Each covers one missed day so the streak survives. If you miss more days than you have savers, the streak restarts at day 1 (your best is kept). Changing the clock backwards never costs you anything.

**Reminders** (Settings → **REMINDERS**, iOS only, off by default; `Core/Streaks.cs` `NotificationPlan`, `Plugins/iOS/RetroSk8Notify.mm`):
- Local notifications only, nothing leaves the phone. Turning it on shows the iOS permission prompt.
- What it schedules each time the game goes to the background: tomorrow 6 pm "keep your streak" (if you have a streak of 2+), otherwise tomorrow 10 am "new Daily Line"; and Sunday 6 pm if the weekly event still has goals left.
- No reminders between 9 pm and 9 am, and at most one a day.
- Not included: "a rival beat your score". That needs a server to send push notifications.

**Launch kit:** `STORE_LAUNCH_KIT.md` covers listing copy, screenshot sizes, the age rating, App Privacy, the full Game Center id list, IAP setup, TestFlight and review notes.
- **Retro Sk8 → Store Screenshot (Play mode)** saves the Game view at the exact App Store size (2868×1320 iPhone, 2752×2064 iPad) as an opaque PNG in `StoreScreenshots/`.

## 3p. Phase 16: tests fixed in Unity, iPhone build from the bridge, skater animation, Park editor 2.0, online gallery

**Tests now run in Unity, and they pass on their own** (`Editor/RetroSk8RemoteBridge.cs`, `EditorTestReport/RetroSk8TestReport.cs`):
- **Run All Tests** runs EditMode, then PlayMode. One call with both modes only ran EditMode in Unity 6. The report merges both suites.
- First real run: all EditMode tests passed, and PlayMode found real problems, now fixed:
  - **Points rounding:** Unity's Mono does float maths at higher precision than .NET, so a score could come out one point off (1,169 vs 1,170; a S.K.A.T.E. target of 4,001 vs 4,000). Points now round through `Core/ScoreMath.cs`, and match targets use an exact integer percent.
  - **CPU duel:** after your attempt the screen now switches to "watching" in the same frame.
  - **Timing-based tests:** PlayMode tests now step game time by exactly 1/60 s per frame (`Time.captureDeltaTime`). A background editor runs only a few frames a second, and timed holds and spins flaked.
  - **Real input in tests:** tests ignore the real keyboard, controllers and touch (`PlayerInputRouter.IgnoreDevices`). A controller left on the desk, or keys typed in another app, could steer the test skater.
- The bridge also logs compile errors and takes two more commands: `build-ios` / `build-ios-dev`. Both export the device Xcode project and write `Temp/RetroSk8BuildReport.txt` with the result, time, size and every error and warning.

**Skater animation polish** (`Core/SkaterMotion.cs`, `Player/SkaterMotionDriver.cs`, `Player/SkaterVisual.cs`):
- **Push:** while speeding up, the back foot steps off beside the front truck, sweeps back along the ground and returns to the tail. The arms counter-swing.
- **Ollie pop:** the board snaps nose-up off the tail, then levels out.
- **Landing squash:** a quick dip on touchdown, heavier after big airs. Braking crouches you down.
- **Carving and air:** you lean into turns, and tuck in the air.
- **Flips:** the board rises higher and the front foot flicks it.
- **Slam:** the body pitches over, hits the ground with a small bounce and slides, arms flailing. The board flies off on its own arc, spinning, and bounces away.
- Tricks, grinds, manuals and bails still take over the pose while they run. The board's pop and lean are captured in replays and ghosts.

**Park editor 2.0** (Create-a-Park; `Core/CustomPark.cs`):
- **LONGER / SHORTER:** ledges, rails, walls and manual pads get up to 3 steps longer (2 cells each). Quarter pipes and banks get wider.
- **BEND:** rails curve left or right, a little or a lot (up to 1.6 m at the middle). A bent rail takes three columns.
- **UNDO / REDO:** up to 40 steps, covering add, move, turn, height, length, bend, copy, delete and theme.
- **SKATE IT** test-rides the park, as before.
- **Share codes:** parks that use length or bend use code version 2, which carries two extra fields per piece. Everything else stays version 1, so older builds can still open it.

**Online gallery** (CODES → **GALLERY**; `Core/Gallery.cs`, `Game/GalleryService.cs`, `UI/GalleryView.cs`, `Plugins/iOS/RetroSk8Gallery.mm`):
- **Browse:** the newest **PARKS** and **GHOSTS** other players posted. **GET** adds a park to Create-a-Park or races a ghost.
- **Post:** post one of your parks (the > button picks which) or your last Two-Minute Run's ghost. Up to 5 posts a day, and posts show your CODES name.
- **Safety (required for user content on the App Store):**
  - Names are cleaned and run through a word filter.
  - Every post has **REPORT** (pick a reason; it is also hidden for you), **HIDE** and **BLOCK** (you won't see that author again). You can **DELETE** your own posts.
  - Reports arrive as `RetroSk8Report` records for you to review in the CloudKit console. Delete bad posts there.
- **In the editor** it runs a labelled TEST GALLERY in memory.
- **To turn it on for devices:**
  1. **Retro Sk8 → Build iOS → Enable Online Gallery** (needs a paid team). Builds then add iCloud with CloudKit and the default container `iCloud.com.omariibell.retrosk8`.
  2. In the [CloudKit console](https://icloud.developer.apple.com), in the container's **Public** database, create the record types:
     - `RetroSk8Share`, with fields `kind` (Int64), `name` (String), `author` (String), `detail` (Int64), `location` (String) and `code` (String)
     - `RetroSk8Report`, with fields `target` (String) and `reason` (String)
  3. Add the indexes: `kind` queryable, `createdTimestamp` sortable, `recordName` queryable.
  4. Deploy the schema to Production before release.
  5. Posting needs the player signed in to iCloud. Browsing doesn't.
- With the gallery off, devices show "the online gallery isn't in this build yet".

## 3q. Phase 17: on your iPhone, performance pass, a smoother skater, Tutorial 2.0

**Getting it on your iPhone.** The Xcode project is built for you. Retro Sk8's remote bridge exported `Builds/iOS-Device/Unity-iPhone.xcodeproj` (0.17.0, 0 errors). Follow *Putting it on your iPhone* (section 1) from step 3. If something stops you, check this list:
- **"Signing for Unity-iPhone requires a development team"**: choose your Team under **Signing & Capabilities**. If the list is empty, add your Apple ID in *Xcode → Settings → Accounts*.
- **"Failed to register bundle identifier"** or **"not available"**: someone already uses `com.omariibell.retrosk8` with a different team. Add `.dev` to the end of the bundle id in the same tab.
- **Free Apple ID:** it works as long as **Game Center** and **Online Gallery** stay off (Retro Sk8 → Build iOS menu). Both are off by default. The app expires after 7 days; build and run again to renew it.
- **The iPhone isn't in the device menu:** unlock it, tap **Trust**, and turn on *Settings → Privacy & Security → Developer Mode* (the phone restarts). Then wait for Xcode's "Preparing iPhone" to finish.
- **"Untrusted Developer" on launch:** *Settings → General → VPN & Device Management → your Apple ID → Trust*.
- **The build log:** every iOS build writes `Temp/RetroSk8BuildReport.txt` and `Temp/RetroSk8PostBuild.txt`. The post-build file lists which capabilities went into the Xcode project.
- Tell Claude what Xcode or the phone says, word for word (or send a screenshot), and it can fix it from there.

**Performance pass** (`Tests/PlayMode` → `Perf_SceneBudget_StaysInBudget`, report in `Temp/RetroSk8PerfReport.txt`):
- **The old triangle count was wrong.** Static batching makes every batched object share one combined mesh, and the first report counted that whole mesh once per object (Retro City: 6.7 million). The report now counts only each object's own part. Measured counts: Retro City **38,516** triangles, Moonlight Pier 13,052, Harbor Plaza 8,192, Neon Warehouse 6,108. All are light for a phone.
- **Low-poly spheres and capsules** (`PrimitiveMeshes.Get`): trees, lamp heads, pedestrians and the moon use 168–192-triangle meshes instead of Unity's 768/832. They're the same size, and colliders don't change.
- **A scene budget test:** it fails if a park goes over 150,000 triangles or 80 materials. The report lists each park's biggest meshes.
- **Garbage and frame time belong to the device Profiler.** In the editor, Unity's own allocations swamp the game's. Switching off every Retro Sk8 script didn't lower the count. A background editor also throttles its frame rate. To measure on a phone: **Build iOS → Development + Profiler**, run it from Xcode, then *Window → Analysis → Profiler* in Unity.
- **The HUD** rebuilds its timer, combo and trick text only when they change, not every frame.

**Skater model** (`Core/SkaterShapes.cs`, `Player/SkaterMeshes.cs`):
- The body is rounded from smooth turned shapes: tapered arms and legs, calves, a chest, hips and a head.
- The face has eyes, brows, a nose and a mouth. The shoes have toe caps.
- Poses and outfits work as before.

**Tutorial 2.0: trick lessons** (`Core/TrickLessons.cs`, `Game/LessonService.cs`, `UI/LessonCoach.cs`):
- Eight short lessons at the right spot of Moonlight Pier: lip tricks, reverts, wallrides and wallplants, bluntslides, manual styles, no-comply and boneless, big spins, and specials.
- Start one from a trick's page in the **Trick Book** (**LESSON: …**). The coach puts you at the spot and shows one line at a time. Land the trick in 3 separate banked lines to pass.
- Bailing puts you back at the spot. The specials lesson fills your meter.
- The first pass pays 15 Tape Tokens. When you finish: **NEXT** lesson, back to the **TRICK BOOK**, or **KEEP SKATING**.

## 3r. Phase 18: TestFlight, Game Center + gallery on, on-device profiling, the Drive-In and bonks

**TestFlight without opening Xcode** (`Editor/RetroSk8TestFlight.cs`, `Core/TestFlightRules.cs`):
- **Team ID:** builds sign with the team in `ProjectSettings/RetroSk8BuildOptions.json` (`teamId`, set to `X6LZQ3FS36`). Setup Project copies it into Player Settings. Change it with the bridge command `team XXXXXXXXXX`.
- **Retro Sk8 → Build iOS → TestFlight: Build, Archive + Upload:** exports the Release Xcode project, then runs `xcodebuild archive` and `-exportArchive` with automatic signing and uploads to App Store Connect. xcodebuild runs in the background, so Unity stays usable.
  - Full output goes to `Temp/RetroSk8Xcode.log`.
  - A short report goes to `Temp/RetroSk8TestFlightReport.txt`: the result, the important lines, and a plain-English next step for common failures (Xcode not signed in, no app record, build number already used, bundle id taken).
  - **Upload Last Archive Again** re-sends the last archive.
  - Bridge commands: `testflight`, `testflight-upload`.
- **Before the first upload:**
  1. In Xcode → Settings → Accounts, sign in with the paid account.
  2. Create the app in App Store Connect (STORE_LAUNCH_KIT section 2) with bundle id `com.omariibell.retrosk8`.

**Game Center and the online gallery are on** (`ProjectSettings/RetroSk8BuildOptions.json`):
- **Game Center setup list:** **Retro Sk8 → Build iOS → Write Game Center Setup List** (bridge: `gamecenter`) writes `GameCenterSetup.md`. It lists every leaderboard and achievement exactly as the game submits them, generated from the game's own lists, so the IDs can't drift.
  - Achievement points total 495 of the 1,000 allowed.
  - Achievement images are in `AppStoreAssets/Achievements/` (1024 × 1024, original art from `Tools/make_badges.py`).
- **Gallery schema:** the CloudKit schema is in `Tools/CloudKit/RetroSk8.ckdb`.
  1. Once, in Terminal: `xcrun cktool save-token --type management`. Make the token in the CloudKit console → Settings → Tokens.
  2. Then **Retro Sk8 → Build iOS → CloudKit: Check Gallery Schema / Send Gallery Schema (Development)** (bridge: `cloudkit-check`, `cloudkit`). Results go to `Temp/RetroSk8CloudKit.txt`.
  3. Deploy to Production yourself in the CloudKit console before release.
- **If the iCloud container doesn't exist yet:** developer.apple.com → Identifiers → **iCloud Containers** → + → `iCloud.com.omariibell.retrosk8`. Then turn on iCloud for the app's identifier and pick that container.

**On-device profiling** (`Core/PerfStats.cs`, `Game/PerfCapture.cs`):
- **What it records:** for every scene, the average and 95th-percentile frame time, the worst frame, hitches (frames over 50 ms), the load time and, in Development builds, garbage per frame.
- **Where it goes:** each scene adds one line to `perf_log.txt` (the last 40 are kept) and prints a `[RetroSk8 PERF]` line in Xcode's console, with a verdict: SMOOTH, HITCHY, SLOW or GARBAGE.
- **Reading it:** the debug menu's **COPY PERF LOG** puts the log on the clipboard to paste anywhere. The perf HUD also shows garbage per frame and hitches.
- **How to run it:** **Build iOS → Xcode Project for iPhone (Development + Profiler)**, skate each park for a minute, then copy the log.
- **Fixed from a code audit:** city and event banners, the arrow distance, the city clock, goal lines and the combo points now rebuild their text only when what they show changes (points at most ~12 times a second). The combo label reuses one buffer. City spot and race checks no longer box an enumerator every frame. Ground and surface lookups use `TryGetComponent`.

**Twin Screen Drive-In** (park 8; `Level/DriveInBuilder.cs`), an original old movie lot at night:
- **Screens:** two giant screens with long banks to wallride them, and the Intermission quarter pipe between them.
- **Parking rows:** three rows of grindable curbs lined with slanted speaker posts, plus parked cars.
- **The Car Hop:** a kicker over a car parked across the centre aisle.
- **South end:** a snack bar with picnic benches, a funbox (Snack Bar Hop) and a mini ramp.
- **Also new:** its own song and ambience (crickets, a projector, a murmuring film), a contract, Game Center board `retrosk8.score.drive_in`, and share-code index 7.

**Bonks and pole jams** (`Core/BonkRules.cs`, `Level/BonkTarget.cs`). There's no new button:
- **Bonk:** in the air, hit a cone, hydrant, car bumper or speaker post and you tap off it (with a little lift and a clank) instead of bailing.
- **Pole Jam:** roll fast (4 m/s or more) into a slanted post to ride up it and launch.
- They're in the Trick Book with a new **BONKS + POLE JAMS** lesson at the Drive-In.
- Every Retro City spot now has a cone, a hydrant and a signpost nearby.

**Bonk Hunt City Jam** (`Core/CityJam.cs`): every third day the City Jam is a **BONK HUNT JAM**. Targets are lower, but at each stop only lines with a bonk or pole jam count.

## 3s. Phase 19: security (gallery, anti-cheat, purchases, saves, online S.K.A.T.E.) and Bonk Week

The full write-up, including what isn't protected and the release checklist, is in **`SECURITY.md`**. In short:
- **Gallery** (`Core/Gallery.cs`, `Core/CodeLimits.cs`, `Plugins/iOS/RetroSk8Gallery.mm`):
  - Every downloaded field is type- and size-checked.
  - A post must hold the kind of code it claims.
  - Posts are spaced 60 s apart and can't be repeated.
  - BLOCK uses the poster's iCloud id, not the typed name.
  - The word filter is stronger without blocking ordinary words.
- **Codes:** every decoder refuses oversized input before decoding. Ghosts are capped at 3 minutes of samples. Names in codes can no longer misalign the code. Fuzz tests throw thousands of random and damaged codes at every decoder.
- **Leaderboards** (`Core/ScoreLimits.cs`):
  - Impossible scores and race times are never sent (the debug menu counts them as SCORES HELD).
  - `GameCenterSetup.md` now gives each board a **score range** to set in App Store Connect, so Game Center ignores outliers too.
- **Saves** (`Core/SaveSeal.cs`, `Save/SealKeys.cs`, `Plugins/iOS/RetroSk8Keychain.mm`):
  - An HMAC seal is keyed from the iOS Keychain.
  - A hand-edited save loads normally but is marked EDITED for good, and its stored bests aren't sent to Game Center.
  - Old saves and restored phones count as unsealed, never edited.
- **Purchases** (`Plugins/iOS/RetroSk8Entitlements.swift`, `Shop.Reconcile`):
  - StoreKit 2 verified entitlements (Apple-signed) decide which packs you own, checked every launch and after every purchase or restore.
  - Refunded or faked packs are removed; verified ones are restored.
- **Online S.K.A.T.E.** (`Core/DuelGuard.cs`):
  - Messages are size-capped and rate-limited.
  - Names are filtered, labels cleaned and points clamped, and broken ghost frames are dropped.
  - Only one hello per match is accepted.
- **New tools:**
  - **Retro Sk8 → Build iOS → Check Native Plugins** (bridge: `native-check`) compiles every Objective-C++ and Swift plugin against the real iPhone SDK.
  - The debug menu shows SAVE / PACKS / SCORES HELD at a glance.
- **Bonk Week:** a new weekly event. Bonks and pole jams score double, with goals for 15 bonks, a 25,000 run and a city medal.

## 3t. Phase 20: TestFlight + go-live steps, controller and accessibility polish, Story chapter 7

- **Going live** (`GO_LIVE.md`): click-by-click steps for the first TestFlight upload, the CloudKit token and schema, and every Game Center leaderboard and achievement. The TestFlight upload explains "Error Downloading App Information" (the App Store Connect app record doesn't exist yet).
- **Accessibility:**
  - On first launch the game copies the phone's **Reduce Motion** and text size (Larger Text, Bold Text or VoiceOver) into its own settings, once (`Core/AccessibilityDefaults.cs`, `Plugins/iOS/RetroSk8Accessibility.mm`).
  - **VoiceOver** can read and press every menu button (`UI/ScreenReaderBridge.cs`, Unity's accessibility hierarchy).
  - Large Text now scales small labels fully and big titles gently, so nothing overflows.
  - Reduce Motion also stills the HUD pulses and popups, fireworks, pickups, the editor arrow, the turntable and city event markers.
  - Colour-blind-safe UI colours (Okabe-Ito orange and blue), plus text cues where colour was the only signal: IN/OUT on the replay markers, "> " on the active S.K.A.T.E. player and the selected Trick Book card.
- **Controllers:** the lesson finish row can be driven by pad, Back works on more screens (Home, Exit, Done), the keyboard works again after a pad disconnects, and Photo Mode uses the right stick to orbit and the triggers to zoom.
- **Skate Story chapter 7, DOUBLE FEATURE:** two line battles at the Twin Screen Drive-In against The Projectionists (MARQUEE, then REEL). Each needs the score **and** a number of bonks or pole jams in banked lines (3, then 6). New achievement **Double Feature** (75 points, badge in `AppStoreAssets/Achievements`).

## 3u. Phase 21: Off-Season Rink, controller support in the park editor, bug sweep

- **Off-Season Rink** (park 9; `Level/OffseasonRinkBuilder.cs`): an original town ice arena in summer, with the ice gone and the concrete left bare.
  - The rink boards are long ledges (grind either edge) with doors in the sides, and each end has a quarter pipe.
  - Bleachers on both sides are 10-stair sets with two handrails and a hubba each.
  - Bonkables: goal frames, penalty boxes, a parked ice-resurfacing cart and cones.
  - Gaps: **Boards Hop** (kicker over the west boards), **Bleacher Set**, **Rink End Air**. A contract, its own song and ambience, a leaderboard (`retrosk8.score.offseason_rink`), and share-code index 8.
- **Park editor with a controller:** the map has a cursor.
  - Stick moves the cursor, A selects, the D-pad moves the piece (or the cursor), X turns, Y copies, LB/RB undo/redo, and the triggers zoom.
  - Start (or B) switches to the editor's buttons and back. B never leaves the editor.
  - The hint line shows the controls for whichever device you're using.
- **Tutorial:** the pause menu has **SKIP LESSON**, so a controller can skip How to Skate.
- **Fixes:**
  - A Two-Minute Run at another park no longer shows a story "RETRY" message.
  - VoiceOver reads symbol buttons by name ("+" → "Zoom in") and keeps up when a list scrolls.

## 3v. Phase 22: Skate Story chapter 8, the rink lesson and Late Skate week, iCloud save backup

- **Skate Story chapter 8, OFF-SEASON** (Off-Season Rink): The Rink Rats, the crew who sweep the rink and skate it after hours.
  - Open Skate: score more than 30,000.
  - Board to Board: a line battle against SLAPSHOT (40,000).
  - Centre Ice: S.K.A.T.E. against FROST on the hardest bot.
  - New achievement **Rink Rats** (75 points, badge). Game Center now has 13 achievements, 645 of 1000 points.
- **Trick lesson BOARD GRINDS** at the rink: lock into a grind on the rink boards. The Trick Book links every grind to it.
- **Weekly event LATE SKATE:** bank 40 grinds, finish 3 runs at the rink, score 30,000.
- **Save & backup** (Settings → SAVE & BACKUP; `Core/SaveBackup.cs`, `Save/CloudBackup.cs`, `Plugins/iOS/RetroSk8CloudSave.mm`):
  - **iCloud backup:** automatic when the app goes to the background (at most every 10 minutes), or BACK UP NOW. It uses iCloud key-value storage.
  - **Restore:** shows how old the backup is, its tokens and story progress, then asks again before replacing anything.
  - **SAVE CODE:** copy or paste your whole save as text, to move progress without iCloud.
  - Codes are size-capped, checksummed and decompressed with a hard limit. A restored save is flagged, so its stored bests aren't re-sent to Game Center. Paid packs always come from the App Store, never from a backup.

## 3w. Phase 23: App Store listing pack, Trick Battle, a customization drop, battery and memory

- **App Store listing** (`STORE_LAUNCH_KIT.md`): the copy is updated for nine parks and eight story chapters. It adds an 8-shot screenshot list with captions, a 25-second preview video script, TestFlight "What to Test" text, iCloud privacy answers and review notes.
- **Trick Battle** (Pass & Play):
  - The setter calls a trick by landing a line; the line's best trick is the call.
  - Everyone else has to land that trick in a line, or take a letter. Spell RETRO and you're out.
  - Players can type their **own names** on the setup screen. Names are cleaned and word-filtered, and repeats get a number.
- **Customization drop:** eight new token items: Rink Lines, Frost Split and Marquee Bands decks; Ice Blue and Marquee Pink wheels; Rink Grip; Frost Cap; Rink Sweats.
  - Two shirts can only be won in the story: the **Projectionist Tee** (chapter 7) and the **Rink Rats Jersey** (chapter 8). They're never sold or featured.
- **Battery and heat** (Settings → CONTROLS & ACCESSIBILITY → FRAME RATE; `Core/PowerPolicy.cs`, `Plugins/iOS/RetroSk8Power.mm`):
  - **AUTO** runs at 60 FPS, and drops to 30 in Low Power Mode, when the phone is warm, or below 20% battery (not while charging).
  - **60 FPS** always aims for 60 (it still drops when the phone is critically hot).
  - **BATTERY SAVER** runs at 30 FPS with a lower render scale and no shadows.
- **Memory:** an iOS memory warning frees cached songs and unused assets. The FPS overlay shows the power plan and the warning count.
- **Rename:** the chapter 8 crew is now **The Rink Rats** (achievement `rink_rats`), so it doesn't clash with the Night Shift pack and career chapter.

## 3x. Phase 24: review fixes, the Shipyard, Daily Trick, replay editor upgrades

- **Independent code review of Phases 21–23, with fixes:**
  - A new phone no longer auto-backs up an empty save over your old phone's iCloud backup. Auto-backup starts only after a manual backup or a restore.
  - A typed-in save code can't unlock pack items or story shirts. The game no longer re-sends achievements from a restored or edited save. Player names in codes are filtered.
  - Moved a cone that blocked the Boards Hop run-up.
  - Late Skate joins the weekly rotation from week 2026-43, so this week's event doesn't change partway through.
  - iCloud starts syncing at launch, and a missing backup on a new phone gets a "try again soon" message.
  - A restore applies its text-size and frame-rate settings straight away.
- **Shipyard** (park 10; `Level/ShipyardBuilder.cs`): an original dockyard at dawn.
  - A raised quay reached by a slipway, with bollards to bonk, a gangway stair set with two handrails, and a kerb.
  - **Container Canyon**, a 5 m gap between two container rows, with ramps up and down.
  - A gantry crane over grindable tracks, with a hook to pole jam.
  - Gaps: Container Canyon, Quay Drop, Gangway Set. It has a contract, its own song and ambience, a leaderboard, and share-code index 9.
- **Daily Trick** (main menu, next to DAILY LINE; `Core/DailyTricks.cs`): one trick task a day, the same for everyone.
  - Tasks: land a named trick 3 times, bank a big line, chain grinds, land a 540/720, or bonk through 3 lines.
  - Rewards: 30 Tape Tokens, plus 5 for each day in a row (up to +30). A 14-day calendar shows the days you finished.
- **Replay editor:**
  - Three new cameras: DRONE, LOW ANGLE and HELMET (seven in all).
  - **SLOW-MO: AUTO** eases into 0.3x through the second before each banked line.
  - **9:16** frames the shot as a centred vertical strip with black sides, so an exported clip crops cleanly to 9:16 (Photos → Edit → Crop → 9:16). ReplayKit only records the whole screen, so the clip itself is still landscape.

## 3y. Phase 25: second review, Skate Story chapter 9, Photo mode 2.0, achievements pass

- **Independent review of Phase 24, with fixes:**
  - Gap zones are raised so a plain ollie can't farm them: Container Canyon, Quay Drop, Gangway Set and the rink's Bleacher Set.
  - An edited or restored save records the achievements it already implies silently, once. Only new ones go to Game Center.
  - Pasting a save code no longer turns on auto-backup (only an iCloud restore does).
  - A restore applies its sound volumes.
  - 9:16 replays clear the sides to black with a background camera, and reframe on rotation.
  - Typed CODES names go through the word filter.
  - The main menu card lists your 5 best parks plus "MORE IN RECORDS".
  - Pass & Play turns don't count toward the owner's Daily Trick or bonk total.
  - Bluntslides count as grinds.
  - The crane cable reaches its hook.
- **Skate Story chapter 9, DRY DOCK** (Shipyard): The Deckhands.
  - Shift Change: score more than 34,000.
  - Bollard Run: a line battle against RIVET, 45,000 with 4 bonks.
  - On the Quay: S.K.A.T.E. against ANCHOR.
  - Win the story-only **Deckhand Jacket**.
- **Photo mode 2.0** (`Core/PhotoFx.cs`, `Game/PhotoSaver.cs`, `Plugins/iOS/RetroSk8Photos.mm`):
  - Looks: FILTER (VHS, FILM, B&W), FRAME (TAPE, INSTANT, COMIC) and STICKERS (STARS, BURST), all drawn from code.
  - **SNAP** hides the buttons, captures the screen, applies the look, keeps a PNG in the app and saves it to Photos. Photos access is add-only; iOS asks once (`NSPhotoLibraryAddUsageDescription`).
- **Achievements pass:** Dry Dock (75), Daily Driver (7 Daily Tricks in a row, 50), Called It (finish a Trick Battle, 25), Bonk Collector (100 bonks, 50), Mind the Gap (Container Canyon, 25). That makes 18 achievements, 870 of 1000 points, with badges in `AppStoreAssets/Achievements`.

## 3z. Phase 26: TestFlight device pass, the story finale, ghost races, assists

- **TestFlight:** the Retro Sk8 app record exists, the first build (0.25.0) is uploaded, and an internal group with automatic distribution sends every new build to your phone. **`DEVICE_TEST.md`** is the checklist for everything that has never run on a real iPhone.
- **Skate Story chapter 10, ALL CITY (the finale):** The Originals, the crew who built the city's first spots, take you back across the city.
  - Where It Started (Harbor Plaza): more than 30,000.
  - Across Town (Rooftop Run): a line battle against STATIC, 46,000.
  - One More Take (Floodgate Ditch): a line battle against VAL STERLING, 52,000 with 5 bonks.
  - The Last Line (Harbor Plaza): S.K.A.T.E. against REWIND, the hardest CPU.
  - Win the story-only **All-City Hoodie**. The finale's closing panels run into an **epilogue** with every crew.
  - The story screen's chapter list and step cards now size to fit (nine chapters already ran off the bottom).
- **Ghost races in Retro City** (`Core/RaceGhosts.cs`):
  - Every checkpoint race records your run. A new best saves its ghost and its **split time at every gate**.
  - Next time, your best ghost skates the race with you (hidden with GHOST: OFF), and each gate shows **-0.42 / +1.10** against it, in the toast and the race banner.
  - **Race ghost codes** (`RR:`): the city map's **SEND … GHOST** copies your best run of a race. Paste one in CODES, tap **RACE IT**, and Retro City opens straight into that race against your friend's pink ghost. Results go in RECENT GHOST RACES as times.
  - Ghost codes and race ghost codes share one frame packer (`GhostCodes.WriteFrames/ReadFrames`).
- **Assists** (Settings → CONTROLS & ACCESSIBILITY → ASSISTS; `Core/Assists.cs`, `Game/ActiveAssists.cs`):
  - **Game speed** 100 / 90 / 80 / 70%. The physics step scales with it, and pause and slow-mo return to it.
  - **Balance assist:** STEADY (a calmer grind and manual meter) or AUTO (the meter rights itself unless you steer off).
  - **Landing window:** WIDE or WIDEST. More landings count as clean, and spins still count.
  - Runs with assists on still earn tokens, story steps and achievements. They aren't sent to Game Center leaderboards, a park's assisted best stays local, and Results says so. Assists are off in online S.K.A.T.E. and replays.
- **Colour vision** replaces the colour-safe switch: STANDARD, RED-GREEN SAFE (the Phase 20 palette), BLUE-YELLOW SAFE (a pink highlight instead of tape yellow, which tritan vision loses against cream text) and HIGH CONTRAST. A live swatch shows each palette. Old saves with colour-safe on move to red-green. Tests check each palette with a standard colour-vision simulation and CIELAB ΔE.
- **Achievements:** All City (finish the story, 100) and Photo Finish (beat a ghost in a city race, 25). That makes 20 achievements, 995 of 1000 points.

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
| 4 | Neon Warehouse and Rooftop Run, conveyor belts, park contracts, Daily Line park rotation | Done — needs a hands-on playtest on device |
| 5 | Best-run ghosts, ReplayKit share clips, device performance pass (batching, adaptive resolution, perf HUD), iPhone build guide, Addressables groups tool | Done — needs an on-device run |
| 6 | TestFlight readiness (icon, launch screen, privacy manifest, build numbers), How to Skate lesson, records + achievements, Game Center, composed music + surface sounds | Done — needs an on-device run |
| 7 | Low-poly skater with IK poses, particles + post-processing, lip tricks, wallrides/wallplants/wallies, reverts, touch layout editor + accessibility options, pass-and-play (Letters, Score turns) | Done — needs an on-device run |
| 8 | Retro City open world (9 districts, tapes, spot medal challenges, checkpoint races, map + fast travel), Sunset Bowls, photo mode, scene fallback routing | Done, compiled clean in Unity — PlayMode tests not yet run; needs an on-device run |
| 9 | Create-a-Park (12 obstacles, tap-to-select, arrow-pad moves, 6 slots), Career (8 chapters), Create-a-Skater + board maker, Ship Check + test report menus | Done, compiled clean in Unity — PlayMode tests not yet run |
| 10 | Share codes (parks + score challenges), trick & style pack, living city (day/night, rain, traffic, pedestrians, street events), S.K.A.T.E. (Game Center live + vs CPU), scene fades | Done, compiled clean in Unity — online S.K.A.T.E. untested on devices; PlayMode tests not yet run |
| 11 | First-build checklist, replay editor (cameras, scrub, in/out, clip export), crew mode (8 recruits, perks, XP levels), weekly events + Game Center leaderboards | Done, compiled clean in Unity — PlayMode tests not yet run; needs an on-device run |
| 12 | In-game radio (3 stations), crowd + air sounds, controller menu navigation, iPad support, ghost codes + ghost races, friends leaderboards, shop with daily deal + App Store cosmetic packs | Done, compiled clean in Unity — PlayMode tests not yet run; StoreKit and controllers untested on devices |
| 13 | Story mode (6 chapters, comic panels, line battles), Floodgate Ditch park, Riverside Yards city district + River Run race, clothes/shoes/board-part customization, welcome + tips, fireworks, slow-mo, fanfares, animated title | Done, compiled clean in Unity — PlayMode tests not yet run |
| 14 | Comic cutscenes with live 3D character portraits, Trick Book (every trick, how-to, 4 challenges each), Leaderboards hub (all boards, global/friends, next target) | Done, compiled clean in Unity — PlayMode tests not yet run |
| 15 | Remote test bridge, Moonlight Pier park, daily City Jam, daily streak + Tape Savers, opt-in local reminders, store screenshot tool + App Store launch kit | Done, compiled clean in Unity |
| 16 | Tests fixed and run in Unity, iPhone build via the bridge, skater animation polish, Park editor 2.0 (stretch, bend, undo/redo), online gallery (CloudKit) with report/hide/block | Done, all tests pass in Unity |
| 17 | iPhone install guide + troubleshooting, performance pass (correct triangle counts, low-poly primitives, scene budget test, HUD caching), smoother skater model with a face, Tutorial 2.0 trick lessons | Done, 341 tests pass in Unity |
| 18 | TestFlight pipeline (archive + upload from Unity), Game Center + gallery on (generated setup list, badges, CloudKit schema), on-device profiling + allocation fixes, Twin Screen Drive-In, bonks + pole jams, Bonk Hunt City Jam | Done, 394 tests pass in Unity (with Phase 19) |
| 19 | Security: gallery lockdown, input limits + fuzz tests, leaderboard anti-cheat + score ranges, save seal (Keychain HMAC), StoreKit 2 verified purchases, hardened online S.K.A.T.E., SECURITY.md; Bonk Week | Done — 394 tests pass in Unity, all 9 native plugins compile for iOS, iOS Xcode project builds (0.19.0) |
| 20 | Go-live guide (TestFlight, CloudKit, Game Center), system accessibility defaults, VoiceOver menus, colour-safe UI, Reduce Motion everywhere, controller polish, Story chapter 7 (bonk battles) + Double Feature achievement | Done — 404 tests pass in Unity, 10 native plugins compile, iOS build 0.20.0 |
| 21 | Off-Season Rink park, park editor controller support, SKIP LESSON by pad, story retry fix, VoiceOver fixes | Done — verified with 25 |
| 22 | Skate Story chapter 8 (The Rink Rats) + Rink Rats achievement, BOARD GRINDS lesson, Late Skate week, iCloud save backup + SAVE CODES | Done — verified with 25 |
| 23 | App Store listing pack, Trick Battle + custom names, customization drop + story-reward shirts, battery/heat frame-rate policy, memory warnings | Done — verified with 25 |
| 24 | Review fixes for 21–23, Shipyard park, Daily Trick + 14-day calendar, replay cameras + auto slow-mo + 9:16 framing | Done — verified with 25 |
| 25 | Second review + fixes, Story chapter 9 (The Deckhands), Photo mode 2.0 (filters, frames, stickers, save to Photos), 5 new achievements | Done — 21–25 verified in Unity: 467 tests pass, 13 native plugins compile, iOS build 0.25.0, first TestFlight upload |
| 26 | TestFlight internal testing + device checklist (`DEVICE_TEST.md`), Story chapter 10 finale (The Originals) + epilogue, city race ghosts with splits + race ghost codes, assists (game speed, balance, landing) + colour-vision palettes, 2 achievements | **This delivery** |
