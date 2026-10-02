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
- the marketing version `0.6.0` (`RetroSk8ProjectSetup.AppVersion`)
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
| 8 | Retro City open world (9 districts, tapes, spot medal challenges, checkpoint races, map + fast travel), Sunset Bowls, photo mode, scene fallback routing | **This delivery** — PlayMode tests written but not yet run in Unity; needs an on-device run |
