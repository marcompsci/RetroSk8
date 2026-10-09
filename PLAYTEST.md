# Retro Sk8 playtest: you + 5 friends (Phase 29)

**Feature freeze.** Nothing new goes in until this pass is done. Only fixes for what testers hit.

## 1. Get the build to five friends

Friends aren't on your App Store Connect team, so they join as **external testers** with a public link:

1. App Store Connect → Retro Sk8 → TestFlight → **External Testing → + → group "Friends"**.
2. Add build **0.29.0** (or the newest). Fill **Test Information** once: a short "what to test" (paste section 3),
   a feedback email, and the privacy policy URL (`docs/privacy.html` on GitHub Pages).
3. Submit for **Beta App Review** (usually under a day for the first build, faster after).
4. Turn on **Public Link** and set the **tester limit to 6**. Send the link with the message in section 3.

Later builds to the same group only need a quick review, and often none.

## 2. You first (before friends)

Play one session cold on your own phone with `DEVICE_TEST.md` §1–§3 and §12 (perf log). Anything that crashes or
blocks the first 10 minutes gets fixed before the link goes out.

## 3. Message to send (edit freely)

> I made a skateboarding game and I'd love 20 minutes of you playing it before it goes on the App Store.
> 1. Open this link on your iPhone, install TestFlight, then Retro Sk8: <PUBLIC LINK>
> 2. Play like you normally would. Don't read instructions first.
> 3. When something feels off, breaks or confuses you, take a screenshot. TestFlight asks if you want to send it as
>    feedback; add one line about what happened.
> 4. When you're done, answer the 6 questions below (text me or reply in TestFlight).

## 4. The six questions

1. What were you trying to do in the first two minutes, and did it work?
2. Where did you get stuck or confused? (screen or moment)
3. Did the controls feel good, floaty or stiff? Which trick felt best, and which felt worst?
4. Did anything crash, freeze, stutter, or drain the battery or heat the phone?
5. What made you want to keep playing, and what made you want to stop?
6. Would you show it to a friend? Why or why not?

Also note their phone model and iOS version (TestFlight records it with each screenshot).

## 5. Triage (what gets fixed this week)

| Level | Meaning | Rule |
| --- | --- | --- |
| **P0 crash / blocker** | crash, freeze, lost save, can't finish onboarding | fix before anything else, new build same day |
| **P1 feel** | 2+ testers say a control, trick or camera feels wrong | fix this week |
| **P2 confusion** | someone didn't find or understand something | fix if it's text or layout; otherwise log |
| **P3 idea** | "it would be cool if…" | log for after the freeze, don't build |

Claude can read TestFlight screenshot feedback and crash reports from App Store Connect (while you're signed in in the
browser pane), sort them into this table and fix P0/P1 items.

## 6. Done when

- No open P0s, every P1 fixed or decided against, and two friends replayed the fixed build without new P0s.
- The perf log from at least one older phone is in.

Then the freeze lifts and the art style phase starts.

## Testing translations (for 1.1)

1.0 ships in English only. Native Spanish or Japanese speakers can type `LANG PREVIEW` on the Codes screen, pick the
language under Settings → Language, play, and send screenshots of anything that reads wrong. Type the code again to
switch the preview off. Details: `AppStoreAssets/Metadata/hold-1.1/README.md`.
