# Retro Sk8 — security (Phase 19)

Retro Sk8 has **no server of its own**. Everything online runs on Apple's services:

| Part | Service | Who can write to it |
|---|---|---|
| Park and ghost gallery | CloudKit public database | Any iCloud user, through the app |
| Leaderboards and achievements | Game Center | The player's own copy of the game |
| Online S.K.A.T.E. | Game Center matchmaking (peer to peer) | The other player's phone |
| Cosmetic packs | StoreKit / the App Store | Apple only (signed transactions) |
| Progress, tokens, custom parks | A JSON file on the phone | The game (and anyone who can reach the phone's files) |

The rule all of Phase 19 follows: **anything that comes from outside this copy of the game is untrusted**. That includes a typed or pasted code, a gallery post, another player's name or score, and a message from the other phone.

Some things no client-only game can promise, so this document is explicit about them. On a jailbroken or modified phone, a player can change the game's memory while it runs. Retro Sk8 can make cheating pointless (impossible scores are dropped, and Game Center enforces score ranges). It can't make cheating impossible.

---

## What's protected, and how

### 1. Online gallery (CloudKit)
- **Permissions** (`Tools/CloudKit/RetroSk8.ckdb`):
  - Only the poster's iCloud account can change or delete a post (`GRANT WRITE TO "_creator"`).
  - Anyone signed in to iCloud can create posts. Everyone can read them.
  - Reports can be created by anyone signed in to iCloud, but other players can't read them (no World read). You review them in the CloudKit console.
- **Every downloaded field is checked** (`Core/Gallery.cs`, `Plugins/iOS/RetroSk8Gallery.mm`):
  - Wrong types become empty.
  - Text is capped at 64 characters and can't contain the list separators.
  - Record ids must be plain (letters, digits, `-_.:`).
  - Unknown park ids are dropped, numbers are clamped and future dates are pulled back.
  - Only a page's worth of lines is read.
- **Downloads must match their post:** a park post must contain a valid park code and a ghost post a valid ghost code. Both are size-checked before decoding (`CodeLimits`). A ghost's claimed target score is capped at the run maximum.
- **Names:** every name a player typed goes through the word filter before it's shown. The filter now also catches repeated letters ("FUUUCK"), `! | + 8 9 6` look-alikes and more words. It avoids short words that hide inside ordinary ones.
- **Posting limits:**
  - 5 posts a day, at least 60 seconds apart.
  - The same park or ghost can't be posted twice (by fingerprint).
  - Codes over 100,000 characters are refused in both C# and the native bridge.
  - These are client-side limits. CloudKit's own per-user quotas are the backstop (see "Not protected").
- **BLOCK** uses the poster's **iCloud user id**, which CloudKit sets and players can't edit. Copying someone's typed name doesn't get you blocked in their place, and doesn't let you dodge a block.

### 2. Share codes and ghost codes (typed, pasted, downloaded)
- **Size first:** park and challenge codes over 1,500 characters and ghost codes over 100,000 are refused before any decoding. The biggest the game writes are 424 and about 63,000.
- **Checks on content:**
  - Ghosts can't claim more samples than a 3-minute run (1,801).
  - Parks are re-validated piece by piece (`CustomPark.Sanitize`).
  - Names in codes are read in full, so a too-long name can't misalign the rest of the code.
- **Fuzz tests:** `Phase19Tests.FuzzTests` throw 3,000 random strings and 1,500 damaged or truncated real codes at every decoder. Nothing may throw, and nothing out of range may get through.

### 3. Leaderboards (Game Center)
- **Ceilings on every submission** (`Core/ScoreLimits.cs`, applied in `GameCenter.SubmitScore`):

  | Board | Accepted range |
  |---|---|
  | Run score | up to 4,500,000 (25,000 points a second for the longest run) |
  | Spot challenge | up to 3,000,000 (best combo) |
  | Race time | at least the straight-line gate path at 30 m/s, minus 20%, and at most 10 minutes |
  | S.K.A.T.E. wins | up to 100,000 |

  Anything outside is dropped and counted ("SCORES HELD" in the debug menu). Boards the game doesn't know are never sent. A test checks that every race's gold time stays above its floor.
- **Game Center enforces the same ranges:** `GameCenterSetup.md` now lists a score range for every leaderboard. Enter it in App Store Connect, and Game Center ignores out-of-range scores even from a modified copy of the game.
- **Stored bests from an edited save are never re-sent** (see 4). Scores from live runs are sent as they happen.

### 4. Save file
- **Tamper seal** (`Core/SaveSeal.cs`, `Save/SaveManager.cs`, `Save/SealKeys.cs`, `Plugins/iOS/RetroSk8Keychain.mm`):
  - Every save writes `retrosk8_save.json.seal`, an HMAC-SHA256 of the exact file.
  - The key is a random 32-byte secret made once per install and kept in the **iOS Keychain**, never in the save. It's available after first unlock and moves with encrypted backups.
- **What a broken seal does:** the save still loads (nobody loses progress to a false alarm). It's marked **EDITED** for good: the flag is sealed in, and re-saving doesn't clear it. An edited save's stored bests are never sent to Game Center.
- **No false alarms on old saves or new phones:** saves from before Phase 19, and saves on a phone whose key is new (a restore), count as **UNSEALED** and are sealed on the next save. They're never marked edited.
- **The debug menu** shows SAVE: SEALED, UNSEALED or EDITED.

### 5. Purchases (StoreKit)
- **Verified with Apple's signatures:** `Plugins/iOS/RetroSk8Entitlements.swift` asks **StoreKit 2** for the Apple ID's current entitlements. StoreKit only reports a transaction as `.verified` after checking Apple's signature (JWS), and refunded purchases are skipped.
- **Reconciled on every launch and after every purchase or restore** (`Shop.Reconcile`):
  - Packs in the save without a verified transaction are removed: a refund, an edited save, or a faked StoreKit 1 purchase.
  - Verified packs missing from the save are added.
  - A pack bought a moment ago is kept even if StoreKit 2 hasn't caught up, and is checked again at the next launch.
- **If the check can't run**, the saved packs are kept. The debug menu shows PACKS: VERIFIED or why.
- **Purchases are cosmetic only.** Nothing paid affects scores or leaderboards.

### 6. Online S.K.A.T.E.
- **Size and rate:** messages over 512 bytes are refused unread. Incoming messages are rate-limited (40 a second, burst 80).
- **Every field is checked** (`Core/DuelGuard.cs`):
  - The other player's name goes through the word filter (16 characters).
  - The trick label is printable ASCII only.
  - Points are clamped to the most a line can pay.
  - Turn numbers must be in range.
  - Ghost frames must be finite, inside the world, and have real rotations; otherwise they're dropped.
- **Protocol rules:** only one hello per match, so a late one can't swap who hosts. Frames are only shown while it's the other player's go. The park index is clamped.
- **Fuzz test:** 5,000 random packets go through the parser and the checks.

### 7. Secrets and build
- **No secrets in the app:** no API keys, tokens or passwords ship in the game. The Team ID in `ProjectSettings/RetroSk8BuildOptions.json` is public information (it appears in every signed app).
- **Tokens stay on your Mac:** the CloudKit management token lives in your Mac's Keychain (`cktool save-token`). Never commit it.
- **Native plugin checks:** **Retro Sk8 → Build iOS → Check Native Plugins** (bridge: `native-check`) compiles every plugin against the real iPhone SDK.

---

## Not protected (and why)
- **Changing the game while it runs:** on a jailbroken phone, a player can edit memory or patch the game, including reading the Keychain key and re-sealing a save. Only a game server that replays and verifies every run can stop that. The score ranges limit the damage.
- **Gallery spam from a modified client:** the posting limits run on the phone. CloudKit applies per-user quotas, and you can delete posts and see who posted them (creator id) in the CloudKit console. A real fix would need a server, or CloudKit with a server-side moderation job.
- **Who wins online S.K.A.T.E.:** it's peer to peer, so each phone trusts the other's claimed result (within the checks above). The S.K.A.T.E. wins board has a sane ceiling, but a modified client can still report wins it didn't earn, within that range.
- **Offline progress and tokens:** a player can edit their own save (the seal flags it). Tokens and unlocks only affect that player's own game.

## Release checklist
- [ ] **CloudKit:** import `Tools/CloudKit/RetroSk8.ckdb` to Development (**Build iOS → CloudKit: Send Gallery Schema**), test, then **deploy to Production**. Check that the Share type's permissions show *Creator: write, iCloud: create, World: read*, and RetroSk8Report has no World read.
- [ ] **CloudKit console:** a saved query for `RetroSk8Report` sorted by creation date, checked regularly. Delete the reported posts.
- [ ] **App Store Connect:** every leaderboard has its **score range** from `GameCenterSetup.md`.
- [ ] **App Store Connect:** In-App Purchases are non-consumable and cosmetic-only, and the Paid Apps agreement is active.
- [ ] **Native plugins:** **Check Native Plugins** passes, then a TestFlight archive succeeds (the Swift entitlement check only compiles in Xcode).
- [ ] **On a test device:**
  - Buy a pack in Sandbox, delete and reinstall, and confirm the pack comes back (verified).
  - Refund a Sandbox purchase, relaunch, and confirm the pack is removed.
- [ ] **Save:** edit the save file on a development build, relaunch, and confirm the debug menu shows SAVE: EDITED.
- [ ] **No secrets:** none in the repo (`git grep -i -E "token|secret|password|apikey"` shows only code and docs).
