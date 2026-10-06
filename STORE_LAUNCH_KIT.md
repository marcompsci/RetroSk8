# Retro Sk8 — App Store launch kit (Phase 15, updated Phase 23)

Everything you need to take Retro Sk8 from a working build to TestFlight and the App Store. The copy below is a starting draft; change anything you like. Check each limit in App Store Connect as you paste, since Apple can change them.

---

## 1. Before you start

- **Apple Developer Program** membership (paid). Needed for TestFlight, Game Center and in-app purchases.
- **Bundle id:** `com.omariibell.retrosk8` (Setup Project keeps it). It must match the app record in App Store Connect.
- **Version:** 0.23.0 (Setup Project sets it). Build numbers go up automatically with every iOS build (Phase 6).
- **Unity:** run **Retro Sk8 → Setup Project**, then **Retro Sk8 → Ship Check**. Fix anything it lists before you build.
- **App name:** "Retro Sk8" might already be taken on the App Store. If App Store Connect refuses it, try a variant such as "Retro Sk8: Arcade Skate". The name on the home screen can stay "Retro Sk8".

## 2. Create the app record

1. [App Store Connect](https://appstoreconnect.apple.com) → **Apps → + → New App**.
2. Platform **iOS**. Name: see above. Primary language: English (U.S.). Bundle id: `com.omariibell.retrosk8`. SKU: anything, e.g. `RETROSK8-001`.
3. **App Information:**
   - Category: **Games** (primary: Sports; secondary: Arcade)
   - Content rights: *does not contain, show or access third-party content* (all art, music and names are original)

## 3. Listing copy (paste-ready)

**Subtitle** (30 characters max):
> Arcade skating, combos & parks

**Promotional text** (170 max, can change any time without a new build):
> Ten original parks, a whole city, a nine-chapter story and a Trick Book full of challenges. Land the line, bank the combo, beat your friends' ghosts.

**Keywords** (100 max, commas, no spaces):
> skate,skateboard,arcade,combo,tricks,retro,park,grind,ollie,kickflip,city,offline,ghost,sk8

**Description:**
> Push, pop and flow through a retro arcade skate world built from scratch.
>
> STRING THE LINE
> Flip, grab and shove in the air, then grind rails and ledges, stall on coping, ride walls, bonk anything in your way and manual between it all. Keep the line going to stack your multiplier, then land clean to bank it.
>
> TEN PARKS AND A WHOLE CITY
> Session Harbor Plaza, Neon Warehouse, Rooftop Run, Sunset Bowls, Floodgate Ditch, Moonlight Pier, Twin Screen Drive-In, the Off-Season Rink and the new Shipyard. Then explore Retro City: find spots, collect tapes, race the streets and take on the daily City Jam.
>
> A STORY TO FINISH
> Join a local crew and stop a slick sponsored crew from locking up the city's best spots, then take on the drive-in's Projectionists, the Rink Rats and the shipyard's Deckhands. Nine chapters, told in comic-book panels.
>
> THE TRICK BOOK
> Every trick in the game, with how to do it and four challenges each. Fill your book, earn Tape Tokens.
>
> PLAY YOUR WAY
> Touch controls you can rearrange, full controller support, iPad support, accessibility options and no internet needed for the core game.
>
> SKATE WITH FRIENDS
> Edit your replays with seven camera angles and auto slow-mo, framed for vertical video if you like. Send a ghost of your run, trade park and challenge codes, play S.K.A.T.E. or pass the phone for party games, including Trick Battle: call a trick, and everyone has to land it. Game Center leaderboards for every park, race and spot.
>
> Build your own parks (with touch or a controller), customize your skater down to the socks, and come back every day for the Daily Line, the Daily Trick and your streak. Your progress backs up to iCloud.
>
> Optional cosmetic packs change your look only. No ads. No loot boxes. No tracking.

**What's New:** the first App Store version doesn't show this field. For TestFlight's "What to Test" box (paste):
> New: the Off-Season Rink, Skate Story chapters 7 and 8, Trick Battle party mode, iCloud backup and save codes, a battery saver, and new gear. Please try a game of Trick Battle, the park editor with a controller, and Settings → SAVE & BACKUP.

**Support URL:** required. A simple page on your brand website works (contact email and a short FAQ).
**Marketing URL:** optional.
**Privacy Policy URL:** required for every app. See section 6.

## 4. Screenshots

App Store Connect needs at least one screenshot (up to 10) per device size. Supply the largest set and Apple scales it down for smaller devices. Retro Sk8 is landscape, so use the landscape sizes:

| Set | Size (landscape) | Required? |
|---|---|---|
| iPhone 6.9" | **2868 × 1320** | Yes |
| iPad 13" | **2752 × 2064** | Yes, because the app supports iPad |

PNG or JPEG, no transparency, exact pixel size.

**How to capture them in Unity:**
1. In the Game view, pick or add a resolution with the right aspect: 2868×1320 (iPhone) or 2752×2064 (iPad).
2. Press Play, set up the moment you want, then choose **Retro Sk8 → Store Screenshot (Play mode)**.
3. The PNG lands in `StoreScreenshots/iPhone69` or `StoreScreenshots/iPad13` next to the project's Assets folder, at the exact size, with no alpha. If the Game view aspect doesn't match, the edges are centre-cropped and the Console says so.

**Suggested 8 shots** (same list for both sets; the first three show in search results, so lead with action):
1. A big banked line in Neon Warehouse with the multiplier on screen
2. A board grind along the Off-Season Rink boards, bleachers behind
3. A pole jam on a speaker post at the Twin Screen Drive-In
4. Retro City overview with the City Jam banner
5. A story comic panel (FROST of the Rink Rats, or Val Sterling)
6. Trick Battle's handoff screen: "PASS TO …" with the called trick
7. The Trick Book with a few stars filled in
8. Create-a-Skater (CLOTHES tab) showing a custom outfit

**Captions** (optional text to add over each shot in an image editor; keep it short and readable on a phone):
1. STRING THE LINE · 2. GRIND THE BOARDS · 3. BONK EVERYTHING · 4. A WHOLE CITY TO SKATE · 5. AN 8-CHAPTER STORY · 6. PASS-AND-PLAY TRICK BATTLES · 7. EVERY TRICK, EXPLAINED · 8. YOUR SKATER, YOUR STYLE

Use **Photo Mode** (pause menu) for clean shots without the HUD where that looks better. Its SNAP button saves straight to Photos, with optional filters, frames and stickers (keep store screenshots unfiltered so they match the real game).

**App preview video (optional):** up to 3 per size, 15–30 seconds, recorded from the device (Results → clip export, or the iOS screen recorder). Skip it for the first release if time is short.

**Preview script (25 seconds, record in-game with the HUD on; no voice-over needed):**

| Time | Shot |
|---|---|
| 0–4 s | Roll in at Harbor Plaza, kickflip over the fountain gap, land clean (multiplier pops) |
| 4–9 s | Neon Warehouse: grind → manual → grab → bank a big line |
| 9–13 s | Off-Season Rink: Boards Hop over the boards, land, grind the bleacher handrail |
| 13–17 s | Drive-In: pole jam a speaker post, bonk a car bumper |
| 17–21 s | Retro City at night (Neon Nights week), quick race checkpoint |
| 21–25 s | Results screen with the score and a story comic panel |

The first frame is the poster frame unless you pick another, so start on a strong pose.

## 5. Age rating

Answer the questionnaire honestly. For Retro Sk8:
- No violence (bails are falls; nobody gets hurt), no mature themes, no gambling, no horror.
- **In-app purchases:** yes (cosmetic packs).
- **User-generated content:** without the online gallery, players share park and ghost codes only through other apps (Messages, email). **With the gallery on (Phase 16)**, players can browse and download other players' parks and ghosts in the app. Answer "yes" to user-generated content. The gallery has a word filter on names, REPORT, HIDE and BLOCK on every post, and you remove reported posts in the CloudKit console. Put a contact email in the app's support page, because Apple asks for one for user-generated content.
- **Unrestricted web access:** no.

It should come out at the lowest age band, but App Store Connect decides from your answers.

## 6. App Privacy ("nutrition label") and privacy policy

What the game does:
- Progress, ghosts, settings and custom parks are saved **on the device only**.
- No analytics, no ads, no tracking, no accounts of our own. The privacy manifest (`Platform/iOS/PrivacyInfo.xcprivacy`) declares no tracking and no collected data.
- **Game Center** (optional) is run by Apple; scores and achievements go to Apple, not to you.
- **In-app purchases** go through Apple; the game only learns which packs you own.
- **Reminders** are local notifications scheduled on the phone; nothing is sent to a server.
- **Save seal** (Phase 19): a random key kept in the iOS Keychain checks the save file for edits. It never leaves the phone.
- **Purchase check** (Phase 19): StoreKit 2 confirms on the phone which packs the Apple ID owns. Nothing is sent to a server of yours.
- **iCloud backup** (Phase 22): the save is copied to the player's own iCloud key-value storage. It sits in their iCloud account; you never receive it. SAVE CODES are only copied to the clipboard when the player asks.
- **Online gallery** (only if you turn it on, Phase 16): posts store the park or ghost, its name and the player's CODES name in Apple's CloudKit public database. That counts as **User Content → Other User Content**, used for app functionality, not linked to identity (the CODES name is whatever the player typed) and not used for tracking. Update the App Privacy answers and the privacy policy if you ship it.

From Phase 18 the gallery is **on** in builds, so declare **Other User Content** as above (without the gallery the answer would most likely be "Data Not Collected"). Double-check that against Apple's current definitions before you submit.

**Privacy policy page** (host it on your website; a starting draft):
> Retro Sk8 does not collect, store or share personal data. Your progress, settings, replays and custom parks are saved on your device, and your progress is backed up to your own iCloud account if you're signed in to iCloud. If you sign in to Game Center, Apple handles your scores and achievements under Apple's privacy policy. Purchases are processed by Apple. Optional reminders are scheduled on your device and can be turned off in Settings. Questions: [your email].

## 7. Game Center

Game Center is switched on for builds from Phase 18 (**Retro Sk8 → Build iOS → Enable Game Center**). Create these in App Store Connect → your app → **Game Center**. Every id must match exactly.

**The exact list is generated from the game:** **Retro Sk8 → Build iOS → Write Game Center Setup List** writes `GameCenterSetup.md` next to the Assets folder, with every leaderboard (format, sort order, recurring or not) and every achievement (points, descriptions, image name). Use that file as the source of truth; the tables below are a summary. Achievement images are in `AppStoreAssets/Achievements/` (1024 × 1024, made by `Tools/make_badges.py`).

**Leaderboards** (integer):

| Id | Sort | Notes |
|---|---|---|
| `retrosk8.score.harbor_plaza` | High to low | Best Two-Minute Run |
| `retrosk8.score.neon_warehouse` | High to low | |
| `retrosk8.score.rooftop_run` | High to low | |
| `retrosk8.score.sunset_bowls` | High to low | |
| `retrosk8.score.floodgate_ditch` | High to low | |
| `retrosk8.score.moonlight_pier` | High to low | New in Phase 15 |
| `retrosk8.score.drive_in` | High to low | New in Phase 18 |
| `retrosk8.score.offseason_rink` | High to low | New in Phase 21 |
| `retrosk8.score.shipyard` | High to low | New in Phase 24 |
| `retrosk8.score.retro_city` | High to low | |
| `retrosk8.weekly` | High to low | **Recurring**, weekly |
| `retrosk8.race.downtown_dash` | **Low to high** | Hundredths of a second |
| `retrosk8.race.ring_road` | **Low to high** | |
| `retrosk8.race.canal_cut` | **Low to high** | |
| `retrosk8.race.river_run` | **Low to high** | |
| `retrosk8.challenge.civic_steps` | High to low | Spot challenge best combo |
| `retrosk8.challenge.downtown_double` | High to low | |
| `retrosk8.challenge.retro_skatepark` | High to low | |
| `retrosk8.challenge.schoolyard` | High to low | |
| `retrosk8.challenge.parking_lot` | High to low | |
| `retrosk8.challenge.drained_canal` | High to low | |
| `retrosk8.challenge.mall_ledges` | High to low | |
| `retrosk8.challenge.backyard_pool` | High to low | |
| `retrosk8.challenge.loading_docks` | High to low | |
| `retrosk8.challenge.riverside_yards` | High to low | |
| `retrosk8.skate.wins` | High to low | S.K.A.T.E. wins |

Custom parks have no leaderboard.

**Achievements** (id = `retrosk8.ach.` + name; titles and descriptions are in `Core/Achievements.cs`):
`class_dismissed`, `first_bank`, `line_10k`, `line_50k`, `run_100k`, `spin_540`, `gap_hunter`, `contractor`, `all_contracts`, `daily_regular`, `tourist`, `double_feature`, `rink_rats`, `dry_dock`, `daily_driver`, `called_it`, `bonk_collector`, `mind_the_gap`.

## 8. In-app purchases

1. Sign the **Paid Apps agreement** (App Store Connect → Business), including banking and tax.
2. Your app → **In-App Purchases** → three **Non-Consumable** products:

| Product id | Name | Price |
|---|---|---|
| `com.omariibell.retrosk8.pack.nightshift` | Night Shift Pack | $1.99 |
| `com.omariibell.retrosk8.pack.desertheat` | Desert Heat Pack | $1.99 |
| `com.omariibell.retrosk8.pack.arcade` | Arcade Cabinet Pack | $1.99 |

3. Each needs a display name, a description ("Six original looks for your skater. Cosmetic only.") and a **review screenshot** of the shop.
4. Attach them to the version you submit (they're reviewed together the first time).
5. Test with a **Sandbox** account before submitting (README section 3l).

## 9. TestFlight

**One click (Phase 18):** **Retro Sk8 → Build iOS → TestFlight: Build, Archive + Upload** does steps 1–3 without opening Xcode, signing with your Team ID. It needs Xcode signed in to your developer account (Xcode → Settings → Accounts) and the app record from section 2. The result and any error, with a plain-English next step, are in `Temp/RetroSk8TestFlightReport.txt`.

By hand:
1. Unity: **Retro Sk8 → Build iOS → Xcode Project for iPhone (Release)**.
2. Xcode: open the project, set your team under Signing & Capabilities, choose **Any iOS Device (arm64)**, then **Product → Archive**.
3. Organizer → **Distribute App → App Store Connect → Upload**.
4. After processing (often 10–30 minutes) the build shows in App Store Connect → **TestFlight**. Answer the export-compliance question (the game uses only Apple's standard encryption, so it's usually "None of the algorithms mentioned").
5. **Internal testing:** add yourself and up to 100 team members; no review needed.
6. **External testing:** add a group and testers by email or a public link. The first build needs a short Beta App Review.

What to test on real devices (not yet tested anywhere):
- Every park loads and plays, including Moonlight Pier (falling off the pier should wash you back up) the Twin Screen Drive-In (bonks and pole jams) and the Off-Season Rink (grind the boards, Boards Hop, Bleacher Set)
- A Bonk Hunt City Jam (every third day)
- A City Jam start to finish
- The streak card on two days in a row
- REMINDERS on: the iOS permission prompt, then a reminder arriving
- Controllers, iPad layout, sandbox purchases and Restore, Game Center sign-in and the Leaderboards hub, online S.K.A.T.E.

## 10. Submit for review

- **App Review Information:** a contact name, phone and email. No demo account is needed (there are no logins).
- **Review notes** (paste):
  > Retro Sk8 needs no account. Game Center is optional. The three cosmetic packs are non-consumable and change appearance only; Restore Purchases is in the Shop. Reminders are optional local notifications, off by default (Settings → REMINDERS). Progress backs up to the player's own iCloud (Settings → SAVE & BACKUP). The online park/ghost gallery has a word filter and REPORT, HIDE and BLOCK on every post; reported posts are reviewed and removed by the developer. Party modes (Pass & Play) are played on one device; there is no chat.
- Choose **manual release** for the first version so you control launch day.
- Common rejection reasons to check first: a broken Restore Purchases, placeholder text anywhere, a crash on an iPad, and screenshots that don't match the real app.
