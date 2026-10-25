# Input System Analysis

## 1. Current configuration [CONFIRMED]

| Item | Value | Source |
|---|---|---|
| Active Input Handling | **0 = Input Manager (Old)** | `ProjectSettings/ProjectSettings.asset` → `activeInputHandler: 0` |
| `com.unity.inputsystem` package | **Not installed** | `Packages/manifest.json` |
| UI input module | `StandaloneInputModule` (legacy) in both scenes | scene YAML |
| Gameplay input API | `Input.touchCount`, `Input.GetTouch(0)`, `Input.GetKeyDown(KeyCode.*Arrow)` | `PlayerController.cs` |

This combination is **internally consistent**. Nothing references the new Input System, so there is no missing-module or "Both vs Old" conflict. UI buttons don't intercept legacy `Input.GetTouch`, because legacy Input is not routed through the EventSystem.

## 2. Control scheme

- **Swipe** (touch): drag at least 60 screen pixels and **release**. The dominant axis picks left/right lane change or forward/back.
- **Tap:** no action (deltas under 60 px are discarded).
- **Keyboard:** arrow keys (Editor/debug).
- One finger tracked (`fingerId` lock).

## 3. Device Simulator facts (Unity docs)

- Clicking the simulated screen generates touches for the **active** input backend (Old, New or both). Legacy `Input.GetTouch` **is supported**.
- **Input is only simulated in Play mode.**
- **Single touch only** (no multi-touch).

## 4. Runtime evidence from Editor.log [CONFIRMED]

From the recent Play sessions in `%LOCALAPPDATA%\Unity\Editor\Editor.log`:
- "Left/Right Button Pressed !" ×51 and "Moving Forward/Backward" ×24 were logged. **Every** forward/backward stack trace comes from `HandleInput()` (keyboard, lines 127/131). None comes from `HandleTouchInput()`.
- Three "Game Over !" events. Collision → GameManager works.
- No input-related exceptions or errors.

**Conclusion:** movement, lane logic and collisions work at runtime in Unity 6. The failure is isolated to **touch events not reaching or not completing `HandleTouchInput`**. (Left/right swipes can't be checked in the log, because `MoveLeft/Right` don't log.)

## 5. Root-cause assessment

**Not the cause** [CONFIRMED]: input backend mismatch, a missing Input System package, EventSystem conflicts, or broken movement code. All of these were ruled out above.

**No code defect explains the symptom on its own.** The touch code is correct for legacy input in the Simulator. Remaining candidates, ranked by likelihood [ASSUMPTION – each needs a 1-minute test in the Editor]:

1. **Gesture mismatch:** in the Simulator people usually *click/tap*. The game only reacts to a *drag-and-release* of ≥ 60 device px, so a tap does nothing and controls look broken. This is the most likely cause.
2. **Release outside the simulated screen.** If the mouse button is released outside the device image, `Ended` may not arrive, so the swipe never fires.
3. **Simulator not the active Play-mode view / not in Play mode.** Input is only injected in Play mode into the Simulator view. Clicking a Simulator tab while the Game tab is the play view, or in Edit mode, does nothing.
4. **Swipe fires only on release.** Even when it works, it feels unresponsive in the Simulator compared with keyboard input.

## 6. Diagnostic procedure (no code changes)

1. Open **Window ▸ General ▸ Device Simulator**, choose a device and make it the active view. Enter **Play mode**.
2. Press on the road, **drag upwards ~1/4 of the screen and release inside the device screen.** The Console should show "Moving Forward" with a stack trace through `HandleTouchInput`.
3. If that works, the issue is gesture expectation (cause 1/2). If not, apply fix A2 below (temporary logging) to see whether `Input.touchCount` ever becomes > 0.

## 7. Recommended solution (safest first)

| Step | Change | Risk |
|---|---|---|
| A1 | Run the diagnostic above. | None |
| A2 | Temporary `Debug.Log` of `touchCount/phase/position` in `HandleTouchInput` (removed afterwards). | None |
| A3 | **Minimal robustness patch to `PlayerController` only:** (a) fire the swipe as soon as the threshold is crossed during `Moved` (keep `Ended` as fallback), so a lost `Ended` no longer drops input; (b) Editor-only mouse-drag fallback (`#if UNITY_EDITOR`, used only when `Input.touchCount == 0`, so it can't double-fire with simulated touches). Lane distance, limits, speeds and swipe semantics stay unchanged. | Low |
| A4 (optional, design decision) | Scale `minSwipeDistance` by `Screen.dpi` so swipe length feels the same on tablets and phones. | Low, changes feel slightly |

**Not recommended now:** migrating to the new Input System. That means installing a package and changing the active backend, which carries more risk and isn't needed to fix this.
