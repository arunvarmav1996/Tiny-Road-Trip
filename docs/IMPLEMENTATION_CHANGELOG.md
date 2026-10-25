# Implementation Changelog — Collectibles, Power-Ups and Near-Miss Combo

Branch: `feature/collectibles-powerups` (from `4f730d2`).
Backup of the uncommitted working state before this work: `git stash list` → `stash@{0}: baseline before collectibles/power-ups` (stored with `git stash store`; the working tree was not touched).

## 1. Architecture analysis (what the new features hook into)

Verified in the Editor (Play mode) and from the source on 2026-10-25:

| Existing system | How it works | Integration point |
|---|---|---|
| Forward motion | The player car never moves forward. The world scrolls: `BackgroundScroller` gives the road `Rigidbody2D` a velocity of −2 u/s. `Spawner` gives each obstacle a kinematic velocity of −speed, where speed = 2 × t^0.2. | A **gameplay speed multiplier** applied to those velocities (Slow-Time, Nitro). `Time.timeScale` stays untouched: the game already uses it for game over (0). |
| Obstacles | `Spawner` instantiates `Obstacle.prefab` (kinematic RB2D, BoxCollider2D 0.64×1.33, tag `Obstacle`) in one of 5 lane transforms at y = 5.94, then `Destroy(obs, 6f)`. | `Spawner` keeps a list of live obstacles, which near-miss detection and fair collectible placement need, and rescales their velocity when the multiplier changes. |
| Player | Dynamic RB2D (Y and rotation frozen, continuous), BoxCollider2D 0.67×1.66, tag `Player`. Moved by `PlayerController` lerping `transform.position`. Lanes x ≈ −2.16 … 2.24. | Collectibles use **trigger** colliders, so player movement and collision code are untouched. |
| Game over | `PlayerCollision.OnCollisionEnter2D` (tag `Obstacle`) → `GameManager.OnPlayerHitObstacle()` → `Time.timeScale = 0`, Game Over UI. | Shield check inserted **before** the game-over call. `GameManager` raises a `GameOver` event (coins save, combo resets). |
| Restart / Close | `SceneManager.LoadScene(1 / 0)`. Nothing persists except the `AudioManager` singleton. | New systems are scene objects with no statics, so a restart resets them naturally. Coins persist through PlayerPrefs. |
| Score | Visual Scripting graph `ScoreCount` on `ScoreUI`: float object variable `Score` += deltaTime × 10 each frame, written to the TMP text. | `ScoreBridge` (already on `ScoreUI`, previously unused) gets `AddPoints()`, which adds to that variable. The graph is not edited. |
| Input | Legacy Input Manager (`Input.GetTouch`, arrow keys). | Unchanged. All pickups are collected by driving into them. |
| UI | `UI` canvas (score, Constant Pixel Size) and `GameOverUI` canvas (Scale With Screen Size 1080×1920). | A **separate** `CollectiblesHUD` canvas (Scale With Screen Size 1080×1920, safe-area aware, sorted behind Game Over). Existing canvases are untouched. |

## 2. Integration decisions

1. **Speed multiplier instead of timeScale.** `GameplaySpeedController` multiplies all active modifiers (Slow-Time × Nitro) and ramps smoothly. Road, obstacles and collectibles rescale their own velocities on change. Player steering (`PlayerController`) is not scaled, so it stays responsive.
2. **Spawn spacing preserved.** `Spawner`'s spawn timer advances by `deltaTime × multiplier`, so spacing between cars stays the same during Slow-Time and Nitro. The difficulty clock (`timeAlive`) still uses real game time, so the difficulty curve is unchanged. With multiplier = 1 (no power-up), spawning is identical to before.
3. **Obstacle lifetime is distance-based.** `Destroy(obs, 6f)` would delete slowed cars in the middle of the screen. Obstacles are now removed when they pass `despawnBelowY` (−12, below every screen), with a 60 s safety fallback. Side benefit: cars no longer vanish while still visible at the bottom of tall phones.
4. **Pure logic in a testable assembly.** Combo maths, coin wallet, timers, weighted picking and near-miss maths live in `TinyRoadTrip.Gameplay.Core` (an asmdef with no scene dependencies) and are covered by Edit Mode tests. MonoBehaviours stay in Assembly-CSharp, next to the existing scripts.
5. **Designer configuration.** One `CollectibleDefinition` ScriptableObject per collectible: spawn weight, value, duration, magnitude, visuals and pickup sound. Spawner, combo and near-miss tuning are serialized Inspector fields.
6. **Scene setup is an Editor menu tool** (`Tools ▸ Tiny Road Trip ▸ Set Up Collectibles`). It creates or updates the assets, prefab, HUD and scene objects idempotently and wires every reference, so the setup can be re-run or reviewed.

## 3. Change log

Commits on `feature/collectibles-powerups`:
- `d158c6c`: the code.
- `7637203`: scene wiring and assets.
- A docs commit follows.

### 3.1 Existing scripts modified (minimal hooks; behaviour unchanged when no power-up is active)

| File | Change | Why |
|---|---|---|
| `Assets/Spawner.cs` | Optional `speedController`. Spawn timer × multiplier. Obstacle velocity = base × multiplier, rescaled on change. Public `ActiveObstacles` list. `DespawnObstacle` / `MarkHit`. Removal below y −12 (60 s safety net) replaces `Destroy(obs, 6f)`. | Slow-Time / Nitro, near-miss detection, fair collectible placement, shield. A fixed 6 s lifetime would delete slowed cars mid-screen. |
| `Assets/Scripts/BackgroundScroller.cs` | Optional `speedController`; road velocity × multiplier; `BaseScrollSpeed` property. | Slow-Time / Nitro; collectibles ride the road. |
| `Assets/PlayerCollision.cs` | `gameManager` is now serialized (falls back to `FindFirstObjectByType`, replacing the obsolete `FindObjectOfType`). Optional `powerUps` (shield check before game over) and `combo` (reset on hit). | Shield, combo reset. Removes the CS0618 warning. |
| `Assets/GameManager.cs` | `public event Action GameOver`, raised in `OnPlayerHitObstacle`. | Coin save and combo reset on game over. |
| `Assets/Scripts/ScoreBridge.cs` | `AddPoints(float)` adds to the Visual Scripting `Score` variable. | Near-miss and Nitro bonuses without editing the score graph. |

Not modified: `PlayerController`, `ResponsiveCamera`, `AudioManager`, `MainMenu`, the score graph, the `UI` and `GameOverUI` canvases, the obstacle prefab, the input settings and all packages.

### 3.2 New scripts

| Folder | Files |
|---|---|
| `Assets/Scripts/Gameplay/Core/` (asmdef `TinyRoadTrip.Gameplay.Core`) | `ComboTracker`, `CoinWallet` (+ `ICoinStorage`), `PlayerPrefsCoinStorage`, `EffectTimer`, `SpeedModifiers` (+ `SpeedSource`), `GameplayMath` |
| `Assets/Scripts/Gameplay/` | `GameplaySpeedController` |
| `Assets/Scripts/Gameplay/Collectibles/` | `CollectibleDefinition` (+ `CollectibleKind`), `Collectible`, `CollectibleSpawner`, `CoinManager` |
| `Assets/Scripts/Gameplay/PowerUps/` | `PowerUpEffect`, `SlowTimeEffect`, `ShieldEffect`, `NitroEffect`, `CoinMagnetEffect`, `PowerUpManager` |
| `Assets/Scripts/Gameplay/NearMiss/` | `NearMissDetector`, `ComboManager` |
| `Assets/Scripts/Gameplay/UI/` | `CollectiblesHUD`, `SafeAreaFitter` |
| `Assets/Editor/` | `CollectiblesSetup` (menu **Tools ▸ Tiny Road Trip ▸ Set Up Collectibles**) |
| `Assets/Tests/EditMode/` (asmdef `TinyRoadTrip.Gameplay.Tests.EditMode`) | `GameplayCoreTests` (17 tests) |

### 3.3 New assets

- `Assets/Data/Collectibles/Collectible_{Coin,SlowTime,Shield,Nitro,CoinMagnet}.asset`
- `Assets/Prefabs/Collectibles/Collectible.prefab`

### 3.4 Scene changes (`Assets/Scenes/SampleScene.unity`)

- **Added:**
  - `GameplaySystems`: speed controller, coin manager, power-up manager and four effects, combo manager, near-miss detector, and a `CollectiblePool` child holding the `CollectibleSpawner`.
  - Under `Player`: `ShieldBubble`, `NitroFlame` and `MagnetRing` (sprites, disabled by default).
  - `CollectiblesHUD` canvas: Overlay, sort order −1 (behind the score and Game Over canvases), Scale With Screen Size 1080×1920, no GraphicRaycaster so it can never block touches, safe-area aware. Its top row is positioned below the score glyphs at runtime.
- **Changed:** only new serialized references on `ObstacleSpawner`, `road (4)` and `Player/PlayerCollision`.
- **Verified:** all 111 previously existing scene objects are still present and their names are unchanged (object ID and name comparison against `HEAD`).
- **Left out:** your own uncommitted Close Button edit is still unstaged and was not included in any commit.

### 3.5 Mobile performance measures

- **Pooling:** collectibles come from a pool (prewarm 16). There is no Instantiate or Destroy for pickups during play.
- **No idle Updates:** effect components, `GameplaySpeedController`, `ComboManager` and the HUD enable themselves only while they have work to do.
- **No physics queries:** near-miss detection walks the existing obstacle list, comparing collider bounds.
- **No scene searches at runtime:** references are wired in the Editor. The one `FindFirstObjectByType` in `PlayerCollision` is a fallback only.
- **No per-frame allocations:** text uses `TMP_Text.SetText` with format arguments. The weight buffer and lane buffer are reused.
- **Clean shutdown:** no coroutines are used. Effects deactivate on scene unload.

## 4. Validation summary

| Check | Result |
|---|---|
| Compilation (game, editor, test assemblies) | ✅ 0 errors, 0 warnings from project code |
| Edit Mode tests (17) | ✅ 17/17 passed. They were run by invoking the test methods inside the Editor, because the MCP bridge blocks the Test Runner API; run them again from **Window ▸ General ▸ Test Runner ▸ EditMode**. |
| Play mode scenarios | ✅ See `POWERUP_SYSTEM.md` §7 and `COLLECTIBLES_SYSTEM.md` §8 (coins, Slow-Time + Nitro, expiry, near miss and combo, shield and invulnerability, magnet, game over during effects, restart, persistence across restart) |
| HUD layout | ✅ Rendered at 1440×3040: no overlap with the score; four timers and a popup visible |
| Not tested | ⏳ Real Android device, touch on device, coin persistence across an **app relaunch**, other Simulator devices for HUD layout (the layout adapts by measurement, but only 1440×3040 was rendered), pause/resume (the game has no pause feature) |

Test note: the Editor was unfocused during testing, so Play mode was kept ticking with a temporary `EditorApplication.update` hook and `Application.runInBackground` for that Play session only. Nothing was saved to project settings.

## 5. Merge of `Difficulty-Setup` (`6dbc1f9` "object pooling", Aquwin11)

Backups made before merging: branch `backup/feature-collectibles-pre-merge`, plus `git stash list` entry "pre-merge backup: uncommitted work before merging Difficulty-Setup" (includes untracked files).

| File | Resolution |
|---|---|
| `ProjectSettings/ProjectVersion.txt` | Kept **6000.3.17f1**. The teammate's commit was made in 2022.3.62f2. |
| `Assets/Spawner.cs` | Merged by hand. Kept: the difficulty curve (unchanged), the obstacle registry, speed scaling and shield hooks. Added: obstacles come from the teammate's `ObjectSpawner` pool (falls back to `Instantiate` if no pool exists). Cars below y −12 are **returned to the pool** instead of destroyed. This fixes a leak in the teammate version, where `RoadEndCollision` is not placed in the scene, so cars were never deactivated except on restart. Reused cars get their record replaced and have player collisions re-enabled (the shield's invulnerability ignores collisions per car). `OnDestroy` hands every car back to the pool, so cars can't drive into the next scene (the pool is `DontDestroyOnLoad`). The teammate's aspect-ratio lane formula is kept behind `adaptLanesToAspect` (default **off**). |
| `Assets/PlayerCollision.cs` | Kept ours. It already contains the teammate's change (serialized `gameManager`), plus the shield and combo hooks. |
| `Assets/Scripts/BackgroundScroller.cs` | Teammate's road-width formula kept behind `adaptWidthToAspect` (default **off**). Restored a `GetComponent` fallback for `collider`: the teammate commented it out, which throws if the scene reference is missing. |
| `Assets/Scripts/PlayerController.cs` | Teammate's changes taken: `AudioManager.instance` replaces the tag lookup, which also fixes a restart bug. Their `laneDistance` formula is kept behind `adaptLaneDistanceToAspect` (default **off**). |
| `Assets/GameManager.cs` | Both changes kept (our `GameOver` event, their `DisableAllObjects()` on restart), plus a null guard for when no pool exists. |
| `Assets/Scripts/VehicleScript.cs` | `rb.velocity` changed to `rb.linearVelocity` (Unity 6 API). |
| `Assets/Prefabs/Obstacle.prefab` | Teammate version: `VehicleScript` added, body **Dynamic** (was Kinematic), gravity 0, rotation frozen. |
| `Assets/Scenes/SampleScene.unity` | Not taken whole from either side. Our scene plus the teammate's exact YAML: the `ObjectPooling` (`ObjectSpawner`) and child `Vehicle` objects (same file IDs, no collisions), the scene-root entry, the road's `collider` and `rb` references, and the disabled `LeftBoundaryCollider`, `RightBoundaryCollider` and `Parent_Road` 3D collider. Their Player `minSwipeDistance` and PlayerCollision `gameManager` were already present. **Not taken:** GameOverUI `m_AdditionalShaderChannelsFlag` 25 to 0 (TextMesh Pro needs 25). |
| New from teammate | `ObjectSpawner.cs`, `VehicleScript.cs`, `RoadEndCollision.cs` (not used in the scene yet) |

**Why the aspect formulas are off:** `ResponsiveCamera` already adapts the view per device, and it was verified on 7 resolutions. Running both scales the game twice. The formulas also put the outer lanes at x = ±2.94 on iPad, off the 2.81-wide road.

## 6. How to revert

- **Everything:** `git switch docs/unity6-analysis`. This feature lives only on `feature/collectibles-powerups`.
- **Just the scene wiring:** revert commit `7637203`.
- **Turn features off without code changes:** set a definition's `spawnWeight` to 0, or disable `GameplaySystems`. The game then behaves as before, apart from the obstacle-cleanup improvement.
