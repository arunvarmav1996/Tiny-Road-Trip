# Fix Plan (awaiting approval; nothing below has been applied)

Principles: minimal edits to existing files, no new camera script, no package changes, gameplay values unchanged (lane distance, speeds, spawn rates, difficulty curve, collision setup).

## 0. Prerequisite

| Step | Action | Files |
|---|---|---|
| P0 | Commit the current Unity 6 upgrade state as one snapshot commit, so fixes are isolated. *(Needs your OK; this commits ~540 files.)* | — |

## 1. Prioritised fixes

| Pri | ID | Root cause | Fix | Files touched | Risk |
|---|---|---|---|---|---|
| 1 | **F1** | Runtime script imports `UnityEditor` (M1) | Delete the unused `using UnityEditor.Build.Content;` line | `Assets/Spawner.cs` (1 line) | None |
| 2 | **F2** | Width-fit camera crops iPad vertically and leaves a top gap on tall phones; Edit-mode preview shows the old fixed size | Change `ResponsiveCamera` to `size = max(referenceOrthographicSize, safeHalfWidth / aspect)` with serialized `safeHalfWidth = 2.75` (first shipped as 2.65, raised after render tests); recalc only on resolution change | `Assets/Scripts/ResponsiveCamera.cs` | Low. 9:16 output identical to the original |
| 3 | **F3** | Navy clear colour visible on wider-than-9:16 screens | **S1:** set Main Camera Background to a roadside colour (you choose the colour), **or S2:** add a roadside sprite behind the road (you choose the art) | `SampleScene.unity` (camera colour or one new child GameObject) | Low, visual only |
| 4 | **F4** | Landscape auto-rotation for a portrait game | Player Settings ▸ Resolution ▸ Default Orientation = **Portrait** | `ProjectSettings.asset` | Low |
| 5 | **F5** | Touch "not working" in Simulator | (a) Run the diagnostic in `INPUT_SYSTEM_ANALYSIS.md` §6. (b) If confirmed, patch `HandleTouchInput`: trigger on `Moved` once the threshold is crossed (keep `Ended` fallback), plus an Editor-only mouse-drag fallback used only when `touchCount == 0` | `Assets/Scripts/PlayerController.cs` | Low. Same swipe semantics and thresholds |
| 6 | F6 (optional) | Store readiness | IL2CPP + ARM64, set package name/company | `ProjectSettings.asset` | Low; longer builds |
| 7 | F7 (optional) | Warnings | `FindObjectOfType` → `FindFirstObjectByType`; rename/`new` the hidden `collider` field | `PlayerCollision.cs`, `BackgroundScroller.cs` | Very low |
| 8 | F8 (optional) | Pre-existing bugs | Quit button → `Application.Quit()` method; crash sound only for obstacles; robust AudioManager lookup (`AudioManager.instance`) | `MainMenu.cs`, `MainMenu.unity`, `PlayerController.cs` | Low. Changes behaviour slightly, so it needs your decision |

## 2. Implementation steps (F1–F5)

1. **F1:** remove line 3 of `Spawner.cs`. Wait for recompile and check the Console for no errors.
2. **F2:** edit `ResponsiveCamera.UpdateCamera()`:
   - `float aspect = (float)Screen.width / Screen.height;`
   - `cam.orthographicSize = Mathf.Max(referenceOrthographicSize, safeHalfWidth / aspect);`
   - cache the last `Screen.width/height` and return early when unchanged.
   - Keep the existing serialized fields so the scene reference stays valid. `referenceWidth/Height` become informational.
3. **F3:** apply S1 or S2 (your choice of colour/art).
4. **F4:** set Default Orientation to Portrait.
5. **F5:** run the diagnostic first; patch only if needed.

## 3. Testing checklist

All tests in **Play mode**, Device Simulator, portrait, auto-rotate off:

| Device | Check |
|---|---|
| 9:16 reference (e.g. Galaxy S7 / any 1080×1920) | Looks identical to before: road edges at screen edges, same framing |
| iPhone 12 mini | All 5 lanes fully visible; no navy at top or bottom; road edges visible or slightly trimmed |
| Tall Android 20:9 (e.g. Pixel / Galaxy S2x) | No navy strip at the top during scrolling |
| iPad (10th gen) and iPad Pro 12.9 | Player fully visible at its lowest and highest positions; obstacles enter from the top edge like on the reference; side areas show the chosen fill, not navy |
| iPhone with notch (e.g. iPhone 15) | Score UI unaffected (UI already fixed) |

Gameplay regression (each device):
- [ ] Swipe left/right moves exactly one lane; swipe up/down moves forward/back; arrow keys still work
- [ ] Tap does nothing (unchanged design) unless F5 changes are approved
- [ ] Player can't leave the road; walls still block
- [ ] Collision with an obstacle → Game Over panel, music stops, GameOver sound
- [ ] Restart and Close work; play 2–3 restarts and watch the Console for NullReference
- [ ] Spawn rate and speed visibly ramp up as before (compare at 30 s and 60 s)
- [ ] Score counter increments as before

Build:
- [ ] **File ▸ Build Profiles ▸ Android ▸ Build** completes (verifies F1)
- [ ] Install on a real Android phone; test swipe and orientation lock
