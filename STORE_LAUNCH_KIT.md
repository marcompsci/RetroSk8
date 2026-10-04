# Retro Sk8 — App Store launch kit (Phase 15)

Everything you need to take Retro Sk8 from a working build to TestFlight and the App Store. The copy below is a starting draft; change anything you like. Check each limit in App Store Connect as you paste, since Apple can change them.

---

## 1. Before you start

- **Apple Developer Program** membership (paid). Needed for TestFlight, Game Center and in-app purchases.
- **Bundle id:** `com.omariibell.retrosk8` (Setup Project keeps it). It must match the app record in App Store Connect.
- **Version:** 0.16.0 (Setup Project sets it). Build numbers go up automatically with every iOS build (Phase 6).
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
> Seven original parks, a whole city, a story to finish and a Trick Book full of challenges. Land the line, bank the combo, beat your friends' ghosts.

**Keywords** (100 max, commas, no spaces):
> skate,skateboard,arcade,combo,tricks,retro,park,grind,ollie,kickflip,city,offline,ghost,sk8

**Description:**
> Push, pop and flow through a retro arcade skate world built from scratch.
>
> STRING THE LINE
> Flip, grab and shove in the air, then grind rails and ledges, stall on coping, ride walls and manual between it all. Keep the line going to stack your multiplier, then land clean to bank it.
>
> SEVEN PARKS AND A WHOLE CITY
> Session Harbor Plaza, Neon Warehouse, Rooftop Run, Sunset Bowls, Floodgate Ditch and the new Moonlight Pier. Then explore Retro City: find spots, collect tapes, race the streets and take on the daily City Jam.
>
> A STORY TO FINISH
> Join a local crew and stop a slick sponsored crew from locking up the city's best spots, told in comic-book panels.
>
> THE TRICK BOOK
> Every trick in the game, with how to do it and four challenges each. Fill your book, earn Tape Tokens.
>
> PLAY YOUR WAY
> Touch controls you can rearrange, full controller support, iPad support, accessibility options and no internet needed for the core game.
>
> SKATE WITH FRIENDS
> Send a ghost of your run, trade park and challenge codes, play S.K.A.T.E. or pass the phone for party games. Game Center leaderboards for every park, race and spot.
>
> Build your own parks, customize your skater down to the socks, and come back every day for the Daily Line and your streak.
>
> Optional cosmetic packs change your look only. No ads. No loot boxes. No tracking.

**What's New (this version):**
> Moonlight Pier: a new park at night. Daily City Jams in Retro City. A daily streak with Tape Savers. Optional reminders. Comic cutscenes with your crew, the Trick Book, and a Leaderboards hub.

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

**Suggested 6 shots** (same list for both sets):
1. A big banked line in Neon Warehouse with the multiplier on screen
2. Plank Gap on Moonlight Pier at night
3. Retro City overview with the City Jam banner
4. A story comic panel (Pilar or Val Sterling)
5. The Trick Book with a few stars filled in
6. Create-a-Skater (CLOTHES tab) showing a custom outfit

Use **Photo Mode** (pause menu) for clean shots without the HUD where that looks better.

**App preview video (optional):** up to 3 per size, 15–30 seconds, recorded from the device (Results → clip export, or the iOS screen recorder). Skip it for the first release if time is short.

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
- **Online gallery** (only if you turn it on, Phase 16): posts store the park or ghost, its name and the player's CODES name in Apple's CloudKit public database. That counts as **User Content → Other User Content**, used for app functionality, not linked to identity (the CODES name is whatever the player typed) and not used for tracking. Update the App Privacy answers and the privacy policy if you ship it.

So without the gallery, the answer in App Store Connect → **App Privacy** is most likely **"Data Not Collected"**. With the gallery on, declare Other User Content as above. Double-check that against Apple's current definitions before you submit.

**Privacy policy page** (host it on your website; a starting draft):
> Retro Sk8 does not collect, store or share personal data. Your progress, settings, replays and custom parks are saved only on your device. If you sign in to Game Center, Apple handles your scores and achievements under Apple's privacy policy. Purchases are processed by Apple. Optional reminders are scheduled on your device and can be turned off in Settings. Questions: [your email].

## 7. Game Center

Turn on **Retro Sk8 → Build iOS → Enable Game Center**, then create these in App Store Connect → your app → **Game Center**. Every id must match exactly.

**Leaderboards** (integer):

| Id | Sort | Notes |
|---|---|---|
| `retrosk8.score.harbor_plaza` | High to low | Best Two-Minute Run |
| `retrosk8.score.neon_warehouse` | High to low | |
| `retrosk8.score.rooftop_run` | High to low | |
| `retrosk8.score.sunset_bowls` | High to low | |
| `retrosk8.score.floodgate_ditch` | High to low | |
| `retrosk8.score.moonlight_pier` | High to low | New in Phase 15 |
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
`class_dismissed`, `first_bank`, `line_10k`, `line_50k`, `run_100k`, `spin_540`, `gap_hunter`, `contractor`, `all_contracts`, `daily_regular`, `tourist`.

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

1. Unity: **Retro Sk8 → Build iOS → Xcode Project for iPhone (Release)**.
2. Xcode: open the project, set your team under Signing & Capabilities, choose **Any iOS Device (arm64)**, then **Product → Archive**.
3. Organizer → **Distribute App → App Store Connect → Upload**.
4. After processing (often 10–30 minutes) the build shows in App Store Connect → **TestFlight**. Answer the export-compliance question (the game uses only Apple's standard encryption, so it's usually "None of the algorithms mentioned").
5. **Internal testing:** add yourself and up to 100 team members; no review needed.
6. **External testing:** add a group and testers by email or a public link. The first build needs a short Beta App Review.

What to test on real devices (not yet tested anywhere):
- Every park loads and plays, including Moonlight Pier (falling off the pier should wash you back up)
- A City Jam start to finish
- The streak card on two days in a row
- REMINDERS on: the iOS permission prompt, then a reminder arriving
- Controllers, iPad layout, sandbox purchases and Restore, Game Center sign-in and the Leaderboards hub, online S.K.A.T.E.

## 10. Submit for review

- **App Review Information:** a contact name, phone and email. No demo account is needed (there are no logins).
- **Review notes** (paste):
  > Retro Sk8 needs no account. Game Center is optional. The three cosmetic packs are non-consumable and change appearance only; Restore Purchases is in the Shop. Reminders are optional local notifications, off by default (Settings → REMINDERS).
- Choose **manual release** for the first version so you control launch day.
- Common rejection reasons to check first: a broken Restore Purchases, placeholder text anywhere, a crash on an iPad, and screenshots that don't match the real app.
