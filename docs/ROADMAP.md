# Tiny Road Trip — Roadmap

Status of the features, mechanics and release tasks agreed so far. `[x]` done on `main`,
`[~]` partly done or on an unmerged branch, `[ ]` not started.

## Fun / gameplay

- [x] Game Over summary with distance, best distance, coins, best combo (PR #3)
- [x] Pause button + auto-pause when the app is minimised (PR #4)
- [x] Distance meter in km (PR #3)
- [x] Snappier swipe: lane change mid-drag (PR #5)
- [~] Juice
  - [x] Crash shake + flash, shield-save kick, near-miss sparks (PR #5)
  - [~] Coin "ding" rising in pitch with the combo (`feature/polish-2`)
  - [~] Speed lines during Nitro (`feature/polish-2`)
  - [ ] Different crash sounds
- [~] First-run swipe hint, shown once (`feature/polish-2`)
- [ ] Optional: tap left/right half of the screen to steer
- [~] Difficulty curve with busy/calm waves (`feature/difficulty-waves`)
- [~] Traffic patterns: pairs, wall with gaps, zigzag + fairness rule (`feature/traffic-patterns`); slow clusters not done
- [ ] Car speed variety; lane-changers with turn-signal blink
- [ ] Two-lane trucks (needs a wide sprite), parked cars
- [ ] Road hazards: potholes (sprite exists), cones, oil slicks
- [ ] Siren power-up: traffic pulls aside for 5 s ("Siren" sound already in `AudioManager`)
- [ ] Score multiplier for driving high on screen

## Retention / meta

- [ ] Garage shop: car skins + power-up upgrades (`CoinManager.TrySpend()` ready)
- [ ] Missions, 3 at a time
- [ ] Daily reward streak
- [ ] Revive once per run (coins or rewarded ad)
- [ ] Biomes every ~1 km: city, beach, countryside
- [ ] Google Play Games leaderboard + share

## Release / store

- [x] Quit button fixed, Volume button wired, Debug.Log cleanup
- [x] Branches merged, real-device touch test
- [x] 60 fps on mobile (PR #5)
- [ ] Heat / battery test on device
- [ ] Company name, package ID (`com.<you>.tinyroadtrip`, permanent), app icons
- [~] IL2CPP + ARM64 + ARMv7 set, test APK builds (`feature/traffic-patterns`); tick "Build App Bundle" for the Play upload
- [ ] Upload keystore — back it up in two places; losing it blocks updates
- [~] R8 minify on; target SDK auto (36 installed); bump version code each upload
- [ ] Remove unused content (test APK is 74 MB; `com.unity.ai.inference` adds Sentis shaders to the build): analytics and AI packages from release, TMP Examples & Extras, `Scene2 Level`, unused Visual Scripting graphs
- [ ] Replace placeholder art: power-up orbs, coin, shield, nitro
- [~] Text drafts in `docs/STORE_LISTING.md` + `docs/PRIVACY_POLICY.md`. Play Console: listing, screenshots, feature graphic, privacy policy, Data safety, content rating
- [ ] Closed test: 12+ testers for 14 days (start early)
- [ ] itch.io: WebGL module, Gzip + decompression fallback, portrait embed, page art

## Suggested order

1. Merge `feature/difficulty-waves` and `feature/polish-2` after a phone test.
2. Traffic patterns + fairness rule.
3. Release setup (identity, IL2CPP/ARM64, AAB, keystore) and start the Play closed test.
4. Siren power-up, then the shop and missions.
