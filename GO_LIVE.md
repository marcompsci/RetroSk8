# Retro Sk8 — going live (Phase 20)

Do these in order. Each one needs you signed in with the Apple developer account for Team **X6LZQ3FS36**. Apple's websites change their layout from time to time, so if a button has moved, look for the same words nearby.

## 1. Create the app in App Store Connect (once)

TestFlight uploads fail with **"Error Downloading App Information"** until this exists.

1. Go to **appstoreconnect.apple.com** → **Apps** → **+** → **New App**.
2. Fill it in:
   - **Platforms:** iOS
   - **Name:** Retro Sk8. If the name is taken, add a word, e.g. "Retro Sk8: Street Arcade". This name can be changed later; the bundle id can't.
   - **Primary language:** English (U.S.)
   - **Bundle ID:** `com.omariibell.retrosk8`. If it isn't in the list, open Xcode once with the archived project (or run the TestFlight build again) so automatic signing registers it, then reload.
   - **SKU:** `retrosk8` (private, any text)
   - **User access:** Full Access
3. Click **Create**.

## 2. Upload the first TestFlight build

1. In Unity: **Retro Sk8 → Build iOS → TestFlight: Upload Last Archive Again** (or tell Claude, which uses the bridge command `testflight-upload`). If you've changed code since the last archive, use **TestFlight: Build, Archive + Upload** instead.
2. Wait for the email "has completed processing" (usually 5–30 minutes).
3. App Store Connect → your app → **TestFlight**.
   - Export compliance is already answered in the build (`ITSAppUsesNonExemptEncryption = NO`), so the build shouldn't ask.
   - **Internal Testing** → **+** → make a group (e.g. "Me") → add yourself → add the build.
4. On your iPhone, install **TestFlight** from the App Store, accept the invite and install Retro Sk8.

## 3. Turn on the online gallery (CloudKit)

1. Make a management token: **icloud.developer.apple.com** → **CloudKit Console** → your container `iCloud.com.omariibell.retrosk8` → **Settings** (or the account menu) → **Tokens & Keys** → **+** next to Management Tokens. Copy the token.
2. In Terminal on your Mac:
   ```
   xcrun cktool save-token --type management
   ```
   Paste the token when asked. It's stored in your Mac's Keychain. Never put it in the project.
3. In Unity: **Retro Sk8 → Build iOS → CloudKit: Check Gallery Schema**, then **CloudKit: Send Gallery Schema (Development)** (bridge: `cloudkit-check`, then `cloudkit`).
4. Test the gallery from a TestFlight or Xcode build (post a park, browse, report one).
5. CloudKit Console → **Deploy Schema Changes…** → to **Production**. TestFlight and App Store builds use Production.
6. Check the permissions (see `SECURITY.md` → Release checklist): **RetroSk8Share** shows *Creator: write, iCloud: create, World: read*, and **RetroSk8Report** has no World read.

## 4. Set up Game Center

All IDs, titles, ranges and points are in **`GameCenterSetup.md`** (24 leaderboards, 12 achievements, 570 of 1000 points). Copy each ID exactly.

App Store Connect → your app → **Game Center** (under the app's Services or Features area).

**Each leaderboard**
1. **Leaderboards** → **+** → **Classic Leaderboard**.
2. Set these fields:
   - **Reference name:** the title
   - **Leaderboard ID:** the ID from the table
   - **Score format type:** Integer, or Elapsed Time – hundredths of a second for races
   - **Score submission type:** Best Score
   - **Sort order:** as in the table
   - **Score range:** from the table (the anti-cheat range)
3. **Localization** → **+** → English (U.S.).
   - **Display name:** the title.
   - **Score format:** pick the plain number style, or the time style for races.
4. For `retrosk8.weekly`, choose **Recurring Leaderboard** instead.
   - Start on a Monday at 00:00.
   - Duration: 1 week. Repeats: weekly.

**Each achievement**
1. **Achievements** → **+**.
2. Set these fields:
   - **Reference name:** the title
   - **Achievement ID:** from the table
   - **Points:** from the table
   - **Hidden:** No
   - **Achievable more than once:** No
3. **Localization** → English (U.S.).
   - **Title:** the title.
   - **Pre-earned description** and **Earned description:** from the table.
   - **Image:** the PNG named in the table, from `AppStoreAssets/Achievements/` (1024 × 1024).

**Then**
- On the app version page, in the **Game Center** section, tick **Game Center** and add all the leaderboards and achievements. TestFlight builds can use them before you submit for review.
- Sign in to Game Center on your test iPhone (Settings → Game Center). Scores from TestFlight builds go to the real (live) boards only after the version is approved; until then they're visible in the TestFlight build.

## 5. Before submitting for review

Follow `STORE_LAUNCH_KIT.md` (screenshots, text, age rating, privacy) and the release checklist at the end of `SECURITY.md`.
