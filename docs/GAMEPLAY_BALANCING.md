# Gameplay Balancing

Every value below can be changed in the Inspector without code changes. The defaults are the initial tuning, not final balance.

## 1. Unchanged original difficulty

| Setting (on `ObstacleSpawner` → `Spawner`) | Value | Note |
|---|---|---|
| `spawnInterval` / `obstacleSpawnTimeFactor` | 1.5 s / 0.1 | interval = 1.5 / t^0.1 |
| `obstacleSpeed` / `obstacleSpeedFactor` | 2 / 0.2 | speed = 2 × t^0.2 |

With no power-up active, the speed multiplier is 1 and obstacle spawning and speed are identical to before. The only change is that obstacles are now removed below y = −12 instead of after 6 s. That is purely cosmetic: previously cars could vanish while still visible on tall phones.

## 2. Collectible spawning (`GameplaySystems/CollectiblePool` → `CollectibleSpawner`)

| Field | Default | Effect |
|---|---|---|
| `initialDelay` | 3 s | Calm start |
| `spawnInterval` | 1.6 s (gameplay time) | One roll per interval |
| `coinRowMin` / `coinRowMax` | 3 / 5 | Coins per row |
| `coinRowSpacing` | 0.9 | Vertical gap inside a row |
| `maxPowerUpPickups` | 1 | Power-up pickups on screen at once |
| `minPowerUpInterval` | 8 s | Minimum gap between power-up spawns |
| `obstacleClearance` | 1.6 | Free road required around a spawn |

## 3. Spawn weights (`Assets/Data/Collectibles/*.asset`)

| Collectible | Weight | Rarity | Duration | Magnitude |
|---|---|---|---|---|
| Coin | 70 | Common | — | value 1 |
| Nitro | 12 | Uncommon | 4 s | ×1.5 world speed |
| Slow-Time | 6 | Rare | 5 s | ×0.5 world speed |
| Shield | 6 | Rare | 1 hit | — (invulnerability 1 s on `ShieldEffect`) |
| Coin Magnet | 6 | Rare | 8 s | radius 3.0 |

**Approximate frequency:**
- A roll every 1.6 s gives about 37 rolls per minute.
- When power-ups are allowed, 30% of rolls pick one, but the 8 s gap and the one-on-screen limit cap them at **≤ 7 per minute**, in practice about 4–6.
- Of those, Nitro is about 40% and each rare type about 20%.
- Coins: roughly 25 rows per minute × 4 coins ≈ 100 coins on the road per minute, before lanes the player can't reach.

## 4. Power-up tuning (components on `GameplaySystems`)

| Component | Field | Default |
|---|---|---|
| `GameplaySpeedController` | `rampDuration` | 0.25 s |
| `NitroEffect` | `bonusScorePerSecond` | 10 (the base graph adds 10/s, so Nitro doubles score gain) |
| `ShieldEffect` | `invulnerabilityDuration` | 1 s |
| `CoinMagnetEffect` | `pullSpeed` / `collectDistance` | 10 u/s / 0.35 |

## 5. Near-miss combo

| Component | Field | Default |
|---|---|---|
| `NearMissDetector` | `nearMissDistance` | 0.3 world units (adjacent-lane cruising is 0.36–0.48, so it never counts) |
| `ComboManager` | `basePoints` | 25 |
| | `maxMultiplier` | 5 (points 25 → 50 → 75 → 100 → 125, then capped) |
| | `comboTimeout` | 3 s |

## 6. Balance notes and future difficulty scaling

- **Nitro makes the game harder** (×1.5 world speed) in exchange for double score. Lower `magnitude` (e.g. 1.3) if it feels unfair. Spacing between cars stays the same; only reaction time shrinks.
- **Slow-Time** also slows how often obstacles arrive, because spawn timing follows world speed. That keeps car spacing constant on the road.
- **Future difficulty scaling:**
  - Spawn weights and intervals live on assets and serialized fields, so a difficulty controller can adjust them at runtime: weights through the definitions, intervals through `CollectibleSpawner`.
  - `GameplaySpeedController.SetModifier` can host a new `SpeedSource` (for example, a global difficulty multiplier) without touching existing effects.
- **Shop:** `CoinManager.TrySpend()` is ready for a car shop or upgrades. A shop could raise effect durations by editing the definition values before a run starts.

## 7. Suggested balancing playtest

1. Play 5 runs per device size (phone 20:9, 9:16, tablet). Record the score, coins per run, power-ups seen and near misses.
2. If coins per run exceed what the planned shop prices can absorb, lower the coin weight or `coinRowMax`.
3. If near misses are rare (< 1 per 30 s for a skilled player), raise `nearMissDistance` towards 0.33. Never go to 0.36 or above, or normal adjacent-lane cruising will start counting.
