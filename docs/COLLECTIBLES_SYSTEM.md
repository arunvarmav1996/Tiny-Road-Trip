# Collectibles System

Coins, power-up pickups, spawning, pooling and the persistent coin balance.

## 1. Overview

```
CollectibleSpawner ──spawns pooled──▶ Collectible (prefab, kinematic RB2D + trigger)
      │                                   │ OnTriggerEnter2D(Player)
      │◀────────────── Collect(c) ────────┘
      ├── Coin      → CoinManager.AddCoins(value) → CoinWallet → PlayerPrefs
      └── Power-up  → PowerUpManager.Activate(definition)   (see POWERUP_SYSTEM.md)
```

All collectibles share **one prefab** (`Assets/Prefabs/Collectibles/Collectible.prefab`). Its look and behaviour come from a `CollectibleDefinition` asset assigned at spawn time.

## 2. Scripts

| Script | Location | Responsibility |
|---|---|---|
| `CollectibleDefinition` | `Scripts/Gameplay/Collectibles` | ScriptableObject: kind, spawn weight, coin value, duration, magnitude, sprite/icon/tint/size, HUD colour, pickup sound and message. |
| `Collectible` | `Scripts/Gameplay/Collectibles` | Pooled pickup. Applies its definition's visuals, sizes the trigger to the sprite, and calls back to the spawner on contact with the `Player` tag. `TryMarkCollected()` guarantees a single reward. No `Update()`. |
| `CollectibleSpawner` | `Scripts/Gameplay/Collectibles` | Timing, weighted selection, fair lane placement, pooling, road-speed movement, coin magnet, cleanup. Raises `Collected(definition, position)`. |
| `CoinManager` | `Scripts/Gameplay/Collectibles` | Run coins + persistent balance. Raises `CoinsChanged(run, total)`. `TrySpend()` for a future shop. |
| `CoinWallet`, `ICoinStorage`, `PlayerPrefsCoinStorage` | `Scripts/Gameplay/Core` | Pure logic and storage, unit-tested. |

## 3. Spawning rules (`CollectibleSpawner`)

1. **Timer:** a roll every `spawnInterval` (1.6 s) of *gameplay* time. The timer advances by `deltaTime × speed multiplier`, so pickup spacing on the road is constant during Slow-Time and Nitro. No rolls during the first `initialDelay` (3 s).
2. **Weighted pick** over `definitions[].spawnWeight`. Power-ups are excluded from the roll when:
   - `maxPowerUpPickups` (1) power-up pickups are already on screen, or
   - fewer than `minPowerUpInterval` (8 s) have passed since the last power-up spawned.

   The roll then falls back to coins.
3. **Lanes:** the existing obstacle lanes, `Spawner.lanePositions` (5 lanes at y = 5.94, above the top of every screen). Collectibles therefore never spawn outside the playable road.
4. **Fair placement:** a lane is only usable if no live obstacle and no other collectible lies within `obstacleClearance` (1.6) of the spawn area, horizontally within `laneTolerance` (0.4). If every lane is blocked, the roll is skipped instead of spawning unfairly.
5. **Coins** spawn as a vertical row of `coinRowMin…coinRowMax` (3–5) coins, `coinRowSpacing` (0.9) apart. Power-ups spawn singly and pulse gently.
6. **Movement:** collectibles ride the road at `BackgroundScroller.BaseScrollSpeed` (2 u/s) × the speed multiplier. Traffic can move faster than the road, so a car spawned later may drive over a coin row. Coins render under cars, so they are briefly hidden and then reappear. This is expected behaviour and never unfair, because collectibles have no collision with cars.
7. **Cleanup:** below `despawnBelowY` (−12), or on collection, the instance returns to the pool (`prewarmCount` = 16). No `Instantiate` or `Destroy` happens during normal play.

## 4. Collection and duplicate protection

- The pickup is a trigger `CircleCollider2D` (radius = 0.9 × visual size) on a **kinematic** body. Trigger callbacks fire against the dynamic player body only. Kinematic obstacles never interact with collectibles.
- `Collect()` returns early if `TryMarkCollected()` fails, so one pickup can never reward twice, even if a trigger and the magnet fire in the same frame.
- Coin value: `CollectibleDefinition.coinValue` (1).

## 5. Persistent coin balance

| Event | What happens |
|---|---|
| Coin collected | `CoinWallet.Add` → `PlayerPrefs.SetInt` (memory only, cheap) |
| Game over (`GameManager.GameOver`) | `PlayerPrefs.Save()` |
| App paused or sent to background (`OnApplicationPause(true)`) | `PlayerPrefs.Save()` |
| App quit | `PlayerPrefs.Save()` |
| Scene unload (Restart / Close) via `OnDisable` | `PlayerPrefs.Save()` |

- Key: `TinyRoadTrip.TotalCoins` (configurable on `CoinManager`).
- The balance is clamped at `int.MaxValue`, and negative stored values load as 0.
- **Shop hook:** `CoinManager.TrySpend(amount)` deducts, saves and raises `CoinsChanged`.

## 6. Configuration

| What | Where |
|---|---|
| Coin value, spawn weight, sprite and size | `Assets/Data/Collectibles/Collectible_Coin.asset` |
| Spawn rate, row length, limits, fairness, pooling | `GameplaySystems/CollectiblePool` → `CollectibleSpawner` |
| Save key | `GameplaySystems` → `CoinManager` |
| Add a new collectible type | Create a definition (Create ▸ Tiny Road Trip ▸ Collectible Definition) and add it to `CollectibleSpawner.definitions` |

Re-running **Tools ▸ Tiny Road Trip ▸ Set Up Collectibles** only creates what is missing and re-wires references. It never overwrites existing definitions, the prefab or HUD layout.

## 7. Art (placeholders)

| Item | Current sprite | Wanted |
|---|---|---|
| Coin | Kenney `Models/Kenny Models/Previews/item-coin-gold.png` (a preview render of a 3D coin) | Flat top-down coin, 128 px, optional spin animation |

Power-up art is listed in `POWERUP_SYSTEM.md`.

## 8. Testing

| Check | How | Status |
|---|---|---|
| Wallet maths, persistence flushing, overflow, spending | Edit Mode tests `CoinWalletTests` | ✅ 6/6 passed (run in the Editor 2026-10-25) |
| Natural coin pickup in play | Play mode: the car collected coins riding down its lane | ✅ Observed in the Editor: run 2 / bank 2, PlayerPrefs = 2 |
| Run resets, bank persists across Restart | Play mode: `GameManager.RestartGame()` | ✅ run 0, bank kept (2 → 2, later 177 → 177) |
| Bank persists across **app** restart | Quit the Editor/app and relaunch | ⏳ Not performed (requires an app relaunch; PlayerPrefs flush on quit is in code) |
| Pool reuse | Play mode: pooled instances re-used after collection | ✅ Observed (a renamed test coin re-spawned from the pool) |
