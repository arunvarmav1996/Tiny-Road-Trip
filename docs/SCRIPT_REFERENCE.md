# Script Reference

All C# gameplay scripts in the project (TextMesh Pro example scripts excluded). Line numbers refer to the current working tree.

| Script | Location | Attached to (SampleScene unless noted) |
|---|---|---|
| PlayerController | `Assets/Scripts/PlayerController.cs` | Player |
| PlayerCollision | `Assets/PlayerCollision.cs` | Player |
| Spawner | `Assets/Spawner.cs` | ObstacleSpawner |
| BackgroundScroller | `Assets/Scripts/BackgroundScroller.cs` | road (4) |
| ResponsiveCamera | `Assets/Scripts/ResponsiveCamera.cs` (**untracked in git**) | Main Camera |
| GameManager | `Assets/GameManager.cs` | GameManager |
| AudioManager | `Assets/AudioManager.cs` | AudioManager |
| Sounds | `Assets/Scripts/Sounds.cs` | (serializable data class) |
| MainMenu | `Assets/Scripts/MainMenu.cs` | Main Camera in both scenes; Play button target |
| ScoreBridge | `Assets/Scripts/ScoreBridge.cs` | ScoreUI |
| NewBehaviourScript | `Assets/Scripts/NewBehaviourScript.cs` | Not used (empty template) |

---

## PlayerController
**Purpose:** reads input and moves the player car between lanes and forward/back.

- `Update()` → `HandleTouchInput()`, `HandleInput()`, `MoveToTargetLane()`.
- **Touch (`HandleTouchInput`, L53-106):** legacy `Input.touchCount` / `Input.GetTouch(0)`. Tracks one finger. Records the start point on `Began`. On `Ended`/`Canceled`, a delta ≥ `minSwipeDistance` (60 px, raw screen pixels) picks the dominant axis → `MoveLeft/Right/Forward/Backward`. **Swipe-only: taps do nothing**, and the move fires on finger release.
- **Keyboard (`HandleInput`, L113-133):** arrow keys → same move methods (logs "Left/Right Button Pressed !").
- **Moves:** each sets `targetPosition` ± `laneDistance` (1.1). Limits come from `minMoveDir (-2, -4.75)` / `maxMoveDir (2, 2.1)`, checked against the *current* (mid-lerp) `transform.position`. Settled reachable positions: x ∈ {−2.16, −1.06, 0.04, 1.14, 2.24}, y from −5.49 to 2.21.
- `MoveToTargetLane()`: `transform.position = Lerp(pos, target, moveSpeed*dt)`. It writes the transform directly while a dynamic Rigidbody2D is attached (pre-existing design).
- `OnCollisionEnter2D`: plays "PlayerCrash" on **any** collision, including the boundary walls.
- Dependencies: a GameObject tagged `Audio` with `AudioManager` (found in `Awake`). `using UnityEngine.AI` is unused.
- Unused fields: `numLanes`, `currentLane`, `dashSpeed`.

## PlayerCollision
**Purpose:** game-over trigger. `Start` caches `FindObjectOfType<GameManager>()` (obsolete in Unity 6 → warning CS0618). `OnCollisionEnter2D` with tag `Obstacle` → `gameManager.OnPlayerHitObstacle()`.

## Spawner
**Purpose:** spawns obstacles and ramps the difficulty.
- Every frame: `timer += dt`, `timeAlive += dt`. When `timer ≥ _obstacleSpawnTime`, it spawns and resets.
- `SpawnObstacle()`: random lane from `lanePositions` (5 transforms at world y = 5.94), random sprite. Instantiates `Obstacle.prefab` (kinematic RB2D), sets gravity 0 and `linearVelocity = (0, −_obstacleSpeed)`, then `Destroy(obs, 6f)`.
- Difficulty: `_obstacleSpawnTime = spawnInterval / timeAlive^0.1`, `_obstacleSpeed = obstacleSpeed × timeAlive^0.2` (scene values: 1.5 s and 2 u/s).
- ⚠ **`using UnityEditor.Build.Content;` (L3)** is a runtime script importing an Editor-only namespace. It compiles in the Editor but **fails Android/iOS player builds** (CS0246/CS0234). The import is unused.

## BackgroundScroller
**Purpose:** endless road scroll.
- `Start`: caches the BoxCollider2D and Rigidbody2D, sets `height = collider.size.y` (**2.89, local/unscaled**), disables the collider, then sets `rb.linearVelocity = (0, −2)` (migrated from `rb.velocity`).
- `Update`: if `position.y < −height`, it moves the road up by `height`. The road therefore oscillates between y ≈ 0 and −2.89 forever. It relies on the sprite being much taller than the screen. There is a single tile.
- Warning CS0108: field `collider` hides `Component.collider`.

## ResponsiveCamera (new, untracked)
**Purpose:** adapts the orthographic size to the screen aspect.
- `Awake` and **every `Update`**: `orthographicSize = referenceOrthographicSize × (refAspect / currentAspect)`, with refAspect = 1080/1920.
- Mathematically this is a **"fit width"** camera: visible width stays 5.598 world units on every device, and visible height changes.
- Runtime only (no `[ExecuteAlways]`). In Edit mode the Game view and Simulator show the serialized size 4.976 instead.
- The Editor log shows an earlier version failed to compile (CS1513/CS1529/CS1022 at lines 4, 6, 48). The current 42-line version compiles successfully (Tundra build success afterwards).

## GameManager
- `Start`: `timeScale = 1`, plays BackgroundMusic + CarRev, hides GameOverUI.
- `OnPlayerHitObstacle()`: `timeScale = 0`, stops music, plays GameOver, shows GameOverUI.
- `RestartGame()` → LoadScene(1); `Close()` → LoadScene(0) (bound to UI buttons).
- `OnCollisionEnter2D` only runs for collisions on the GameManager object itself, which has no collider, so it is dead code.

## AudioManager / Sounds
- Singleton with `DontDestroyOnLoad`. Duplicates call `Destroy(gameObject)` and return **before** creating their AudioSources.
- `Play/Stop(name)` look up a `Sounds` entry by name.
- ⚠ [ASSUMPTION – needs runtime check] On Restart, SampleScene's own AudioManager is a duplicate. `PlayerController.Awake` uses `FindGameObjectWithTag("Audio")`, which may return that duplicate (destroyed only at end of frame). Its `source` fields are null, so `PlayerController.OnCollisionEnter2D` → `audioManager.Play` could throw a NullReferenceException after a restart. This bug predates the migration.

## MainMenu
`PlayGame()` → LoadScene(1); `Close()` → timeScale 1, LoadSceneAsync(0).
⚠ [CONFIRMED, also in the committed 2022 scene] In `MainMenu.unity` **both** the Play Button and the **QuitButton** call `MainMenu.PlayGame`. Quit starts the game, and no `Application.Quit` exists anywhere.

## ScoreBridge
A static `Instance` holder with a `score` float, intended for Visual Scripting. The ScoreCount graph writes the TMP text directly.

## Visual Scripting assets
- `ScoreCount.asset` (used on ScoreUI): Update → Score = Score + deltaTime × 10 → ScoreUI text.
- `PlayerCollisionGraph.asset`, `Scripts/GameManager.asset`: **not referenced by any scene or prefab.**
