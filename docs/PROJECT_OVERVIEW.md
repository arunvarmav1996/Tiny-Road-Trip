# Tiny Road Trip — Project Overview

> Analysis date: 2026-10-25 · Branch analysed: `Difficulty-Setup` (working tree, incl. uncommitted Unity 6 upgrade changes)
> Project path: `G:\Game Projects\Tiny-Road-Trip` · Editor: **Unity 6000.3.17f1** (upgraded from 2022.3.62f3)
>
> Legend used in all docs: **[CONFIRMED]** = verified from project files / Editor log · **[ASSUMPTION]** = inferred, needs in-Editor verification.

## 1. Game purpose

A portrait, top-down, endless lane-dodging driving game for Android. The player drives an ambulance up a scrolling road.
Obstacle vehicles spawn at the top in one of five lanes and travel downwards, getting faster and more frequent over time.
Hitting an obstacle ends the run and shows a Game Over panel with Restart and Close buttons. The score is a timer-based counter driven by a Visual Scripting graph. [CONFIRMED]

## 2. Scenes and flow

Build settings (`ProjectSettings/EditorBuildSettings.asset`) [CONFIRMED]:

| Index | Scene | Role |
|---|---|---|
| 0 | `Assets/Scenes/MainMenu.unity` | Animated menu (UI-only background: road, sea, sand, cars, trees), Play / Quit / Volume buttons |
| 1 | `Assets/Scenes/SampleScene.unity` | The gameplay scene |
| — | `Assets/Scenes/Scene2 Level.unity` | ProBuilder blockout (cubes, stairs, a light). **Not in build** and unrelated to the shipped game |

```
MainMenu ──Play button → MainMenu.PlayGame() → LoadScene(1)──▶ SampleScene
SampleScene: obstacle hit → GameManager.OnPlayerHitObstacle() → timeScale = 0, GameOverUI on
   ├─ Restart → GameManager.RestartGame() → LoadScene(1)
   └─ Close   → GameManager.Close()       → LoadScene(0)
```

## 3. Gameplay scene hierarchy (SampleScene) [CONFIRMED from scene YAML]

| GameObject | Key components / values |
|---|---|
| **Main Camera** | Orthographic, size **4.9764**, pos (0, −1.24, −0.9), clear = solid colour **navy (0.19, 0.30, 0.47)**; `ResponsiveCamera` (ref 1080×1920, ref size 4.976419); `AudioListener`; `MainMenu` (unused here) |
| **Parent_Road** | scale (1.225, 1, 1); 3D `BoxCollider` (unused by 2D physics) |
| └ **road (4)** | `SpriteRenderer` (Road2 PNG 1024×2890 px, PPU 100), local pos (0, −0.9), scale (0.448, 0.537); `Rigidbody2D` dynamic, gravity 0; `BoxCollider2D` 10.24×2.89 (disabled at runtime); `BackgroundScroller` |
| **Player** | Ambulance sprite, pos (0.04, −3.287), scale 0.05; `Rigidbody2D` dynamic, gravity 0; `BoxCollider2D`; `PlayerController` (laneDistance 1.1, moveSpeed 5, numLanes 5, minSwipeDistance 60); `PlayerCollision` |
| └ Ambulance Light_0 (1) | Animated siren light |
| **ObstacleSpawner** | pos (0.01, 5.94); `Spawner` (5 sprites, Obstacle prefab, spawnInterval 1.5, obstacleSpeed 2, factors 0.1 / 0.2) |
| └ Lane1–Lane5 | local x = −2.152, −1.109, 0, 1.095, 2.142 |
| Left/RightBoundaryCollider | `BoxCollider2D` at x = ±3.24 (inner edges ≈ ±2.74) |
| GameManager | `GameManager` (GameOverUI ref) |
| AudioManager (tag `Audio`) | `AudioManager` singleton with 6 sounds |
| UI (Canvas, Screen Space-Camera with no camera, so it behaves like Overlay) | ScoreUI (TMP text + Visual Scripting `ScoreCount` graph + `ScoreBridge`) |
| GameOverUI (Canvas, Overlay, CanvasScaler 1080×1920, match 0.5) | Panel, GAME OVER text, RestartButton, Close Button |
| EventSystem | `EventSystem` + `StandaloneInputModule` (legacy) |
| Obstacle (prefab instance, inactive) | Template only |

## 4. Architecture summary

- **Plain MonoBehaviours, no framework.** Communication happens through `FindObjectOfType`, `FindGameObjectWithTag`, a static singleton (`AudioManager.instance`) and UnityEvent button bindings.
- **World-space gameplay.** Lanes, movement limits, spawn points and boundaries are all **hard-coded world coordinates**, independent of the screen. Only *how much of the world is visible* changes per device, through the camera.
- **Physics 2D:** the player and road are dynamic `Rigidbody2D`s. Obstacles are kinematic, moved with `linearVelocity`. The player is moved by writing `transform.position` (Lerp) every frame.
- **Input:** legacy Input Manager only (`Input.GetTouch`, `Input.GetKeyDown`). See `INPUT_SYSTEM_ANALYSIS.md`.
- **Score:** a Visual Scripting ScriptMachine on ScoreUI (`Assets/Scripts/ScoreCount.asset`). Each frame it adds deltaTime × 10 to the `Score` variable and writes it to the TMP text.
- **Rendering:** Built-in Render Pipeline (no SRP asset assigned). Sprites only.

## 5. Folder structure (Assets/)

| Folder | Contents |
|---|---|
| `Scripts/` | Most gameplay scripts + Visual Scripting graphs (`ScoreCount`, `PlayerCollisionGraph` – unused, `GameManager.asset` – unused) |
| *(root)* | `AudioManager.cs`, `GameManager.cs`, `PlayerCollision.cs`, `Spawner.cs` (scripts are split between root and `Scripts/`) |
| `Scenes/` | 3 scenes (above) |
| `Prefabs/` | `Obstacle.prefab`, `Tree (2).prefab`, road textures (`Road2 - 09-11-2025 00-08-25.png` is the one in use) |
| `Sprites/`, `Models/` (≈2100 files: Kenney UI pack, vehicle packs), `Animation Sprites/`, `Animations/` | Art and animation |
| `Audio/`, `Sound FX/`, `Fun Happy Beats Vol. 8/` | Audio |
| `TextMesh Pro/` | TMP resources + **Examples & Extras (newly imported, untracked)** |
| `Scene 2/`, `ProBuilder Data/`, `Physics/` | Blockout / misc |

## 6. Original Android design intent (inferred)

- Portrait 9:16 (reference 1080×1920 in both the CanvasScaler and ResponsiveCamera). The camera size 4.976 makes the 5.625-unit-wide road fill a 9:16 screen almost exactly. [CONFIRMED by maths, see `CAMERA_AND_SCALING.md`]
- Swipe controls: left/right switch lanes, up/down move forward/back. Arrow keys are an editor/debug fallback. [CONFIRMED]
- No device-specific scaling originally. On 9:16 Android phones (the common target in 2022) the fixed camera looked correct. [ASSUMPTION]

## 7. Limitations of this analysis

- The Unity Editor was **not connected** (MCP bridge not running), so nothing was inspected live. Scenes and prefabs were parsed as text YAML (Force Text serialization is on). The Editor log (`%LOCALAPPDATA%\Unity\Editor\Editor.log`) was used for runtime evidence.
- Not verifiable offline: the exact Sprite bounds after import, Animator/Timeline runtime effects, Visual Scripting graph execution, and live Device Simulator behaviour.
