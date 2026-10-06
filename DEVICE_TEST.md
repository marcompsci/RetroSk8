# Retro Sk8: on-device test checklist (Phase 26)

Everything below passes its automated tests in Unity, but **none of it has run on a real iPhone yet**. Work through it on a TestFlight build. Tick each box, and for anything that fails, write down what you did and what happened. A screenshot or screen recording helps a lot.

**Before you start**
- [ ] Install **TestFlight** from the App Store, open the invite email (sent to your Apple ID email), and install Retro Sk8. New builds arrive automatically: the internal group **Me** has automatic distribution on.
- [ ] In TestFlight, check the build number matches the newest upload (0.25.0 (11) or later).
- [ ] Settings → Game Center: you're signed in.
- [ ] Have a Bluetooth controller nearby (PlayStation, Xbox or MFi) and, for two items, a second Apple device.

To send feedback from inside TestFlight, take a screenshot while playing and tap **Share Beta Feedback**.

---

## 1. First launch and basics
- [ ] The app opens to the title, then the main menu. Note any freeze longer than ~3 seconds.
- [ ] Play **How to Skate** to the end. Every step advances and the lesson pays its tokens once.
- [ ] A Two-Minute Run at Harbor Plaza: touch controls respond, the timer ends the run, and Results shows your score.
- [ ] The phone stays reasonably cool over 10 minutes, and the frame rate feels steady. Settings → CONTROLS & ACCESSIBILITY shows the frame-rate mode; try **BATTERY SAVER**.
- [ ] Low battery (Low Power Mode on): the game drops to 30 fps on its own (the frame-rate line says why).

## 2. New and never-tested parks
- [ ] **Off-Season Rink:** skate boards_hop, bleacher_set (needs real air, not a flat ollie) and rink_end_air. The run-up to the bleachers is clear (no cone).
- [ ] **Shipyard:** Container Canyon, Quay Drop and Gangway Set count only when you really clear them. The crane cable reaches its hook.
- [ ] Each park plays its own ambience and music.

## 3. Story mode (chapters 7–10)
- [ ] Chapter 7 (Drive-In): bonk steps count bonks and pole jams.
- [ ] Chapter 8 (Rink) and chapter 9 (Shipyard): line battles show the rival's live score in the HUD.
- [ ] **Chapter 10, ALL CITY:** play all four steps. The S.K.A.T.E. finale against REWIND is the hardest CPU.
- [ ] After The Last Line, the closing panels run into the **epilogue** and end with "THE END. THANKS FOR SKATING."
- [ ] The **All-City Hoodie** unlocks (CUSTOMIZE → SHIRT), and back at the menu the messages include **ACHIEVEMENT: ALL CITY**.
- [ ] The story screen fits all ten chapters, and four-step chapter 10 shows all its cards without anything running off the screen.
- [ ] Comic portraits show REWIND and STATIC in violet tanks.

## 4. Retro City ghost races (new)
- [ ] Explore City → open the map → tap a race flag (>). Finish it: you get a medal and NEW BEST.
- [ ] Race it again: your cyan best ghost skates with you, and each gate shows **-0.42** (ahead, teal) or **+1.10** (behind, coral) in the toast and the race banner.
- [ ] Bail during a race: the ghost waits while the race clock is stopped.
- [ ] Beat your ghost: the result toast shows first, then **ACHIEVEMENT: PHOTO FINISH** a couple of seconds later.
- [ ] Settings → GHOST: OFF hides your own race ghost (the splits still show).
- [ ] Map → **SEND … GHOST** → "RACE GHOST COPIED!". Paste the code into Notes: it starts with `RR:`.
- [ ] Main menu → CODES → PASTE that code → it shows the race, the time and **RACE IT**. Tap it: Retro City opens and the race starts against a **pink** ghost.
- [ ] After that race, CODES → RECENT GHOST RACES lists it with times (e.g. `0:27.3 VS 0:28.4`).
- [ ] Send a code to yourself by Messages or Mail and paste it back from there (long codes get wrapped; they should still work).

## 5. Assists and colour vision (new)
Settings → CONTROLS & ACCESSIBILITY → ASSISTS column.
- [ ] **GAME SPEED 70%:** the whole run is slower, smooth (no stutter in physics), and the two-minute timer runs in game time. Pause and unpause keep 70%. A big line's slow-mo comes back to 70%, not 100%.
- [ ] **BALANCE ASSIST: AUTO:** hold a long grind or manual without steering. It stays balanced. Steering hard to one side still drops you.
- [ ] **BALANCE ASSIST: STEADY:** noticeably calmer than OFF.
- [ ] **LANDING WINDOW: WIDEST:** slightly under-rotated spins land instead of bailing, and still score as the right spin.
- [ ] With any assist on, Results says "ASSISTS ON · THIS RUN STAYS OFF THE GAME CENTER BOARDS". A new best with assists doesn't appear on the park's Game Center board.
- [ ] Turn all assists off, set a new best: it does reach Game Center.
- [ ] **COLOUR VISION:** cycle STANDARD → RED-GREEN → BLUE-YELLOW → HIGH CONTRAST. The swatch row changes live. After DONE and the next screen, the HUD (combo, bails, medals) uses the new colours.
- [ ] Turn on iPhone Settings → Accessibility → Display & Text Size → Color Filters and try each palette with the matching filter.

## 6. Saves and iCloud backup
- [ ] Settings → SAVE & BACKUP: BACK UP TO ICLOUD NOW, then make some progress, then RESTORE ICLOUD BACKUP (tap twice): progress returns to the backup and sound volumes match it.
- [ ] Delete the app, reinstall it from TestFlight, and restore from iCloud: progress comes back.
- [ ] COPY SAVE CODE, then PASTE SAVE CODE on a second device (or after a reinstall): the progress loads. Auto-backup does **not** turn on from a pasted code.

## 7. Party, duels and online
- [ ] **Pass & Play → Trick Battle** with typed names: turns rotate, and finishing gives **Called It**.
- [ ] **S.K.A.T.E. vs CPU** at each level.
- [ ] **Online S.K.A.T.E.** (Game Center) against a friend or your second device: a full game, letters in sync, and assists switched off for both phones.

## 8. Daily, weekly and reminders
- [ ] **Daily Trick:** finish today's, see the 14-day calendar tick, and get the tokens.
- [ ] Weekly event banner shows (this week's event and goals).
- [ ] Settings → REMINDERS on: iOS asks for notification permission, and a reminder arrives the next day.

## 9. Sharing, photos and replays
- [ ] **Photo mode** (pause → PHOTO): try each FILTER / FRAME / STICKERS, then **SNAP**. iOS asks for Photos permission (add-only) once, and the photo is in your Photos library with the look applied.
- [ ] Results → **WATCH REPLAY**: the replay editor's DRONE, LOW ANGLE and HELMET cameras, SLOW-MO, and the **9:16** framing (black bars, not stretched).
- [ ] Export a clip (ReplayKit) and share it.
- [ ] Results → **SEND MY GHOST** → paste in CODES → race it.
- [ ] **Online gallery** (CODES → GALLERY): post a park, browse, report one. If CloudKit isn't deployed yet (GO_LIVE.md step 3), note what the screen says.

## 10. Store and Game Center
- [ ] Shop: buy a cosmetic pack with the TestFlight sandbox account (TestFlight purchases are free), then **Restore Purchases** after a reinstall.
- [ ] Story-reward shirts are never for sale in the shop.
- [ ] Game Center dashboard opens from the menu, and achievements earned during testing show there once they're set up (GO_LIVE.md step 4).
- [ ] Leaderboards hub shows the global and friends tabs.

## 11. Controllers and accessibility
- [ ] With a controller: navigate every menu, play a run, edit a park in Create-a-Park (map mode), and SKIP LESSON from the pause menu.
- [ ] **VoiceOver** on: menus read their buttons aloud, and the main menu card is read sensibly.
- [ ] Reduce Motion (iPhone setting) on a fresh install: the game turns its own REDUCED MOTION on.
- [ ] iPad (if you have one): layout fills the screen without stretching.

---

**When you're done:** tell Claude which boxes failed (with what you saw). Crash reports from TestFlight show up in App Store Connect → Retro Sk8 → TestFlight → Crashes.
