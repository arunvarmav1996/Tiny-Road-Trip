# Unity 2022.3 → Unity 6 (6000.3.17f1) Migration Status

Evidence: `git diff HEAD` (the last commit is the 2022.3.62f3 state; the upgrade is **uncommitted**, ~540 changed files), `Packages/manifest.json`, `ProjectSettings/*`, `Editor.log`.

## 1. What the upgrade already changed [CONFIRMED]

| Area | Change |
|---|---|
| Editor | 2022.3.62f3 → 6000.3.17f1 |
| Packages | collab-proxy 2.12.4, feature.2d 2.0.2, probuilder 5.2.4 → 6.0.9, test-framework 1.6.0, timeline 1.8.12, **ugui 1.0.0 → 2.0.0 (TextMesh Pro merged into uGUI; `com.unity.textmeshpro` removed)**, visualscripting 1.9.11. Added: device-simulator.devices, multiplayer.center, modules.accessibility / adaptiveperformance / vectorgraphics |
| API | `Rigidbody2D.velocity` → `linearVelocity` in `BackgroundScroller.cs` and `Spawner.cs` (done correctly) |
| Player settings | Android Min SDK 22 → **25**, iOS min 12 → **15**, macOS 12, new Unity 6 fields (`androidApplicationEntry`, `androidMinAspectRatio`, etc.) |
| Assets | TMP resources re-serialized; **TMP Examples & Extras imported** (untracked, ~170 files, includes demo scenes and scripts) |
| New | `Assets/Scripts/ResponsiveCamera.cs` (untracked) |

## 2. Compatibility problems found

| # | Severity | Issue | Status |
|---|---|---|---|
| M1 | 🔴 **Build blocker** | `Assets/Spawner.cs:3` has `using UnityEditor.Build.Content;` in a runtime script. Editor compiles succeed, **Android/iOS player builds fail** (UnityEditor isn't available in players). The import is unused. | CONFIRMED (code). Not yet observed in a build log (no build attempted). |
| M2 | 🟠 Gameplay | Camera/scaling across aspect ratios (see `CAMERA_AND_SCALING.md`) | CONFIRMED |
| M3 | 🟠 Testing | Touch controls in Device Simulator (see `INPUT_SYSTEM_ANALYSIS.md`) | Narrowed down; needs a 1-minute Editor test |
| M4 | 🟠 Config | Auto-rotation allows landscape (`defaultScreenOrientation: 4`) for a portrait-only game. This was already the case in 2022, but the new camera script makes landscape much worse. | CONFIRMED |
| M5 | 🟡 Store | `AndroidTargetArchitectures: 1` (ARMv7 only) and `scriptingBackend: {}` (platform default; Mono [ASSUMPTION]). Google Play requires **ARM64**, which needs IL2CPP. `applicationIdentifier` is empty and `companyName` is "DefaultCompany". | CONFIRMED (settings); predates migration |
| M6 | 🟡 Warning | `FindObjectOfType` obsolete (PlayerCollision.cs:13), CS0618 | CONFIRMED (Editor.log) |
| M7 | 🟡 Warning | `BackgroundScroller.collider` hides `Component.collider`, CS0108 | CONFIRMED (Editor.log) |
| M8 | ⚪ Noise | Editor.log: repeated `SerializationException: Unable to find type 'UnityEngine.Experimental.Rendering.RayTracingShader'` comes from Editor tooling deserialization, not game code. Harmless to the game. | CONFIRMED (log) |
| M9 | ⚪ Housekeeping | `AGENTS.md` (Code Assist) still says Unity 2022.3.62f3; 540 uncommitted upgrade changes; TMP Examples (& Extras) imported unnecessarily (adds demo scenes, URP/HDRP sample shaders) | CONFIRMED |
| M10 | ⚪ Visual Scripting | VS 1.9.11 works (score updates in Play). After upgrades, run **Edit ▸ Project Settings ▸ Visual Scripting ▸ Regenerate Nodes** once, if any node shows as missing. | ASSUMPTION |

## 3. Pre-existing bugs (not caused by migration, but found during analysis)

- **Quit button calls `PlayGame`** in MainMenu (also in the 2022 commit), and there is no `Application.Quit`.
- `PlayerController.OnCollisionEnter2D` plays the crash sound on any collision, including the side walls.
- Possible AudioManager duplicate reference after Restart (see `SCRIPT_REFERENCE.md`), [ASSUMPTION].
- `GameManager.OnCollisionEnter2D` is dead code.
- Unused scripts/assets: `NewBehaviourScript.cs`, `PlayerCollisionGraph.asset`, `Scripts/GameManager.asset`, `MainMenu` component on the gameplay camera, `Scene2 Level.unity`.

These are listed for awareness. They are **not** part of the proposed fix unless approved.

## 4. Risks

- **Uncommitted upgrade:** the whole migration is uncommitted on `Difficulty-Setup` (now carried onto `docs/unity6-analysis`). Commit a "Unity 6 upgrade" snapshot **before** applying fixes, so each fix is reviewable and revertible.
- Rendering stays on the Built-in pipeline, which is still supported in Unity 6 but is legacy. No change is recommended now.
- No player build has been made with Unity 6 yet (nothing in the log). M1 will surface on the first build.
