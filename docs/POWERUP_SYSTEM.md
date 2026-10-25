# Power-Up System

Slow-Time, Shield, Nitro, Coin Magnet, the gameplay speed multiplier, and the near-miss combo.

## 1. Architecture

```
CollectibleSpawner.Collect(powerUp) → PowerUpManager.Activate(definition)
                                         └─▶ PowerUpEffect (per kind, component on GameplaySystems)
                                               ├─ SlowTimeEffect   → GameplaySpeedController.SetModifier(SlowTime, 0.5)
                                               ├─ NitroEffect      → SetModifier(Nitro, 1.5) + bonus score + flame
                                               ├─ ShieldEffect     → bubble; PlayerCollision asks it before game over
                                               └─ CoinMagnetEffect → CollectibleSpawner pulls coins within radius

GameplaySpeedController.MultiplierChanged ─▶ Spawner (obstacles), BackgroundScroller (road), CollectibleSpawner
```

### `PowerUpEffect` (base class)
- **Lifecycle:** `Activate(definition)`, then a timer from `definition.duration`, then `Deactivate()`.
- **Cost when idle:** the component is enabled (and its `Update` runs) only while active, so idle power-ups cost nothing.
- **Re-collecting:** picking up the same power-up again **refreshes** it to full duration; effects do not stack.
- **Time base:** timers use scaled `Time.deltaTime`, so they **freeze on game over** (`timeScale = 0`) and in any future pause.
- **Clean shutdown:** `OnDisable` deactivates a running effect when the scene unloads (Restart / Close), so no speed modifier or visual can linger.
- **Events:** `Activated` and `Expired`, re-raised by `PowerUpManager` for the HUD.

### Extending
To add a new power-up:
1. Add a value to `CollectibleKind`.
2. Subclass `PowerUpEffect`.
3. Add the component to `GameplaySystems` and list it in `PowerUpManager.effects`.
4. Create a `CollectibleDefinition` and add it to `CollectibleSpawner.definitions`.
5. Optionally add a HUD slot.

## 2. Gameplay speed multiplier

`GameplaySpeedController` multiplies all active modifiers and blends to the new value over `rampDuration` (0.25 s). It ticks only while blending.

| Uses the multiplier | Not affected |
|---|---|
| Road scroll velocity (`BackgroundScroller`) | Player steering (`PlayerController` lerp) |
| Obstacle velocities (each keeps its base spawn speed × multiplier) | `Time.timeScale` (still used only for game over) |
| Obstacle spawn **timer**, so spacing between cars stays constant | Difficulty clock `timeAlive`, so the difficulty curve is unchanged |
| Collectible velocity and spawn timer | Score rate of the Visual Scripting graph (10/s) |

**Combined effects multiply:** Slow-Time 0.5 × Nitro 1.5 = **0.75**. This was verified in Play mode (road −1.5 u/s; obstacle = base × 0.75).

## 3. The four power-ups

| | Slow-Time | Shield | Nitro | Coin Magnet |
|---|---|---|---|---|
| Definition | `Collectible_SlowTime` | `Collectible_Shield` | `Collectible_Nitro` | `Collectible_CoinMagnet` |
| Duration | 5 s | Until it absorbs a hit | 4 s | 8 s |
| Magnitude | 0.5 (speed ×) | — | 1.5 (speed ×) | 3.0 (radius, world units) |
| Effect | Road, cars and pickups at half speed; steering unchanged | Absorbs one obstacle hit, then 1 s invulnerability | World ×1.5, +10 score/s (doubles the base rate), exhaust flame | Coins within radius fly to the car (10 u/s) and auto-collect at 0.35 |
| Visual | HUD timer | Green bubble around the car; car blinks during invulnerability | Flickering orange flame behind the car | Spinning pink ring around the car |
| Popup | `SLOW-MO!` | `SHIELD!`, then `SHIELD SAVED YOU!` | `NITRO!` | `MAGNET!` |

### Shield details (`ShieldEffect`)
- `PlayerCollision.OnCollisionEnter2D` (tag `Obstacle`) first resets the combo, then calls `PowerUpManager.TryAbsorbObstacleHit()`. Only if that returns false does the original `GameManager.OnPlayerHitObstacle()` run, so with no shield the game over path is exactly as before.
- **On absorb:**
  1. The car that was hit is removed (`Spawner.DespawnObstacle`).
  2. The player's physics velocity is zeroed. The player body is dynamic, with Y frozen.
  3. The shield turns off.
  4. A `invulnerabilityDuration` (1 s) window starts.
- **During invulnerability:** further contacts are ignored with `Physics2D.IgnoreCollision` for that pair, so the car passes through, and the obstacle is marked as hit so it can't count as a near miss.
- Obstacle colliders are **not** modified.

### Magnet details
- Only `CollectibleKind.Coin` is attracted; power-ups and obstacles are never pulled.
- A coin is captured when it enters the radius. Captured coins keep homing even if the magnet expires, so none are left hanging mid-road.
- Collection goes through the same single-reward `Collect()` path as a normal pickup.

## 4. Near-miss combo

| Script | Responsibility |
|---|---|
| `NearMissDetector` | Each frame, walks `Spawner.ActiveObstacles` (no physics queries). When an obstacle's collider centre crosses the player's centre line, it computes the horizontal gap between the two colliders. 0 ≤ gap ≤ `nearMissDistance` (0.3) counts as a near miss. Each obstacle is evaluated **once** (`NearMissResolved`), and obstacles that hit the player are excluded (`WasHit`). |
| `ComboManager` | `ComboTracker`: points = `basePoints` (25) × min(combo, `maxMultiplier` 5). Resets after `comboTimeout` (3 s) without a near miss, on any obstacle collision (including one the shield absorbed) and on game over. Points are added to the existing score through `ScoreBridge.AddPoints()`. |

**Why 0.3:** cars settled in adjacent lanes pass the player 0.36–0.48 apart (measured from the lane and collider sizes). A threshold of 0.3 rewards only last-moment dodges, avoiding a near miss every few seconds from normal driving. Obstacle collision boundaries are not changed.

## 5. Interaction matrix

| Combination | Result |
|---|---|
| Slow-Time + Nitro | Multiplier 0.75; both timers run independently; whichever expires first removes only its own modifier |
| Shield + anything | The shield is independent of speed; Nitro does not grant invulnerability |
| Magnet + Nitro or Slow-Time | Captured coins fly at magnet speed; uncaptured coins follow the world multiplier |
| Same power-up twice | Refreshes the duration (no stacking) |
| Game over while effects are active | Timers freeze with timeScale 0; the combo resets; coins are saved |
| Restart / Close | The scene reloads; effects deactivate in `OnDisable`; all state is new; the multiplier is back to 1 |

## 6. Placeholder art (needed)

| Item | Current | Wanted |
|---|---|---|
| Slow-Time orb | Kenney blue `button_round_gloss` + `icon_repeat_light` | Clock or hourglass orb |
| Shield orb | Kenney green orb + grey `icon_checkmark` | Shield-emblem orb |
| Nitro orb | Kenney red orb + `icon_arrow_up_light` | Nitro bottle or flame orb |
| Magnet orb | Kenney grey orb tinted pink + `icon_arrow_down_light` | Horseshoe-magnet orb |
| Shield bubble / magnet ring / nitro flame | Unity built-in `Knob` sprite and Kenney `button_round_line`, tinted | Dedicated bubble, ring and flame sprites (or a particle system for the flame) |

To replace any of these, swap the sprite on the definition asset or on the player child object; no code changes are needed.

## 7. Testing (performed 2026-10-25 in the Unity Editor, Play mode, Device Simulator at 1440×3040)

| Scenario | Result |
|---|---|
| Slow-Time + Nitro activation, combined speed | ✅ Multiplier 0.75; road −1.5; obstacle = base × 0.75; collectibles −1.5; HUD timers visible; flame on |
| Expiry restores speed | ✅ Multiplier 1.0, road −2, effect components disabled, HUD slots hidden, flame off |
| Near misses (gaps 0.15 and 0.10; 0.45 control) | ✅ Exactly +75 bonus (25 + 2× combo 50); the 0.45 pass scored nothing |
| Shield absorbs a head-on hit | ✅ No game over; the car hit was removed; shield off; player velocity 0; popup shown |
| Invulnerability against a car overlapping the player | ✅ `Physics2D.GetIgnoreCollision` = true; no game over; ends after 1 s; sprite visible again |
| Coin magnet | ✅ Coin outside the radius and a Nitro orb inside it were *not* pulled; run coins rose 114 → 141 in ~7.5 s with the magnet on |
| Game over with Nitro + Slow-Time active | ✅ timeScale 0, Game Over UI on, timers frozen (identical in two reads), combo 0, coins flushed |
| Restart after that | ✅ Multiplier 1, all effects off, all visuals hidden, run 0, bank kept |
| Console | ✅ 0 errors / 0 warnings during the session |
| Pause / resume | ⏳ The game has **no pause feature**; timers use scaled time, so a timeScale-based pause will freeze them (inferred, not tested) |
| Real Android device, touch | ⏳ Not performed |
