# Store listing drafts

Drafts for Google Play and itch.io. Replace `<…>` placeholders before publishing.

## Google Play

**App name** (max 30): `Tiny Road Trip`

**Short description** (max 80):
> Swipe through rush-hour traffic, grab coins and power-ups, and drive as far as you can!

**Full description** (max 4000):
> Hit the road in Tiny Road Trip, a one-thumb arcade racer you can play in seconds.
>
> Swipe left and right to weave across five lanes. Swipe up and down to pull ahead or drop back. Traffic gets busier the farther you drive, with short calm stretches to catch your breath. How far can you go?
>
> **Features**
> • Simple swipe controls, perfect for quick sessions
> • Five lanes of traffic that speeds up as you go
> • Near misses build a combo for bonus distance. Pass close to cars to score big.
> • Collect coins along the way
> • Power-ups:
>   – Nitro: a burst of speed
>   – Shield: survive one crash
>   – Slow-Time: traffic slows down
>   – Coin Magnet: pulls coins in
> • Beat your best distance on every run
> • Pause anytime. The game pauses itself when you leave the app.
> • Plays offline. No account, no sign-up.
>
> Buckle up and see how far your tiny car can go!

**Category:** Game › Racing (alternative: Arcade)
**Tags:** racing, arcade, casual, endless, offline
**Contact email:** `<your support email>`
**Privacy policy URL:** `<public URL of PRIVACY_POLICY.md, e.g. GitHub Pages or itch page>`

**Graphics needed**
- App icon 512×512 PNG (32-bit)
- Feature graphic 1024×500
- 2–8 phone screenshots, portrait (e.g. 1080×1920). Suggested shots:
  1. Mid-run with traffic
  2. Near-miss combo popup
  3. A power-up active (Nitro speed lines)
  4. Game Over summary with best distance
  5. Main menu

**Content rating questionnaire (IARC):** no violence beyond cartoon vehicle collisions (no injury shown), no user interaction, no gambling, no purchases. Expected rating: PEGI 3 / Everyone.

**Target audience:** 13+ is the simplest choice. Choosing under-13 brings Families policy requirements.

**Ads / in-app purchases:** No / No (current build).

## Data safety form (Play Console)

Based on the current project:
- Unity Analytics, Ads, IAP, Crash Reporting and Performance Reporting are all **disabled** in `UnityConnectSettings`.
- Game progress (coins, best distance, settings) is stored **only on the device** in PlayerPrefs.

Suggested answers:
- Does your app collect or share any of the required user data types? **No**
- Is all user data encrypted in transit? **Not applicable** (no data leaves the device)
- Do you provide a way for users to request deletion? **Not applicable**. Uninstalling removes all data.

**Check before submitting:**
- `ProjectSettings.asset` has `submitAnalytics: 1`. This is Unity's built-in engine analytics: anonymous device and engine stats sent to Unity. Turn it off in Player Settings › Other › "Enable Analytics" if your licence allows it. If it stays on, Google's guidance treats it as collected **App info and performance › Diagnostics**: not shared, not linked to the user, used for analytics. Answer the form accordingly.
- The `com.unity.ai.assistant` / `com.unity.ai.inference` packages are editor tools. Confirm they add nothing to the player build, or remove them before release.

## itch.io page

**Title:** Tiny Road Trip
**Short tagline:** One-thumb traffic dodging. How far can you drive?
**Classification:** Game · Genre: Racing · Tags: arcade, casual, endless-runner, mobile, one-button
**Pricing:** Free / pay-what-you-want (suggested)

**Description:**
> Swipe through five lanes of traffic, chain near misses for combo bonuses, and grab power-ups: Nitro, Shield, Slow-Time and Coin Magnet. Traffic comes in waves, so keep your cool in the busy stretches. Beat your best distance!
>
> **Controls**
> - Phone: swipe left/right to change lane, up/down to move forward/back
> - Desktop (web build): arrow keys
>
> Made with Unity.

**Uploads:** Android APK (signed with the same keystore as Play), plus a WebGL build later (portrait 540×960 embed, fullscreen button on).
**Cover image:** 630×500.
