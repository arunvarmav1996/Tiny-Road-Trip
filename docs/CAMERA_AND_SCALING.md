# Camera and World Scaling

All values come from `SampleScene.unity`, the road texture and `ResponsiveCamera.cs` [CONFIRMED], unless marked otherwise.

## 1. The fixed facts

| Item | Value |
|---|---|
| Main Camera | Orthographic, serialized size **R = 4.976419**, position y = **−1.24**, background **navy (0.192, 0.302, 0.475)** |
| Reference aspect | 1080 / 1920 = **0.5625** (9:16) |
| Road texture | `Assets/Prefabs/Road2 - 09-11-2025 00-08-25.png`, **1024 × 2890 px**, PPU 100, Max Size 2048 (downscaled on import), **fully opaque edge-to-edge**, white edge lines at x ≈ 0–30 and 990–1023 px, asphalt between. There is **no roadside art** in the texture. |
| Road world width | 10.24 × 0.44838 (child) × 1.225 (Parent_Road) = **5.625 units** (x ∈ ±2.81) |
| Road world height | 28.9 × 0.537 = **15.53 units** [ASSUMPTION: Unity keeps the sprite world size when Max Size downsizes the texture. Verify via the road's Sprite Renderer bounds. The serialized `m_Size`/collider say 10.24 × 2.89, which looks stale.] |
| Road vertical travel | `BackgroundScroller` keeps the centre y in (−2.89, 0], so the road always covers **y ≤ 4.875 at the top and y ≥ −7.765 at the bottom** |
| Gameplay geometry (world, camera-independent) | Lanes: player x ∈ {−2.16 … 2.24}, spawn x ∈ {−2.15 … 2.15}; car half-width ≈ 0.33 → outermost car edge ≈ **±2.57**. Player y ∈ [−5.49, 2.21], car half-height ≈ 0.83. Spawn y = 5.94. Walls at ±3.24. |

**Key observation:** at 9:16 the visible width is 2 × R × 0.5625 = **5.599**, which matches the 5.625-unit road almost exactly. The original game was tuned for exactly one aspect ratio.

## 2. What each camera mode shows

Visible half-width = size × aspect; visible top/bottom = −1.24 ± size.

### 2a. Fixed size 4.976 (original 2022 behaviour; also what you see in **Edit mode** today)

| Device (portrait) | Aspect | Visible width | Result |
|---|---|---|---|
| 9:16 phone (1080×1920) | 0.5625 | 5.60 | Correct (design target) |
| iPhone 12 mini (1080×2340) | 0.4615 | **4.59** | Road cropped 0.52 per side; outer lane cars partly off-screen → **"narrower / cropped"** |
| 20:9 Android (1080×2400) | 0.45 | 4.48 | Worse cropping |
| iPad (3:4) / iPad Pro 12.9 | 0.75 | **7.46** | Road fills only 5.63 → **0.92 units of navy each side** |

These are **exactly the reported symptoms**. [CONFIRMED by calculation]

### 2b. Current `ResponsiveCamera` ("fit width": size = R × 0.5625 / aspect), active **only in Play mode**

| Device | Size | Visible y range | Result |
|---|---|---|---|
| 9:16 | 4.976 | −6.22 … 3.74 | Unchanged |
| iPhone 12 mini | 6.07 | −7.31 … 4.83 | Road fills; top margin to road edge is only 0.05 |
| 20:9 | 6.22 | −7.46 … **4.98** | **Navy strip at the top** (road ends at 4.875) for part of each scroll cycle |
| iPad | **3.73** | **−4.97 … 2.49** | No navy, but **vertical world heavily cropped**: player at its lowest y (−5.49) is fully **off-screen**, at its highest (2.21) half off-screen; obstacles appear ~1.2 units later, so there is **less reaction time** (difficulty effectively rises on iPad) |

So the current script swaps one problem for another. It fixes the side gaps but breaks gameplay visibility on wide screens and shows a top gap on very tall phones.

## 3. Root causes of the screen-size issue

1. **[CONFIRMED]** The game world was authored for exactly 9:16: the road texture has no surplus width and the camera size is fixed.
2. **[CONFIRMED]** `ResponsiveCamera` uses pure width-fit. That is correct for tall phones but wrong for wider-than-9:16 screens (iPad), where it crops the vertical play area.
3. **[CONFIRMED]** `ResponsiveCamera` only runs at runtime (no `[ExecuteAlways]`). In Edit mode the Simulator and Game view still show the fixed size, which is exactly the reported symptoms. [ASSUMPTION] Some of the screenshots or observations were taken in Edit mode, or while the script failed to compile (the Editor log shows an earlier version with CS1513/CS1529/CS1022 errors; a component whose script fails to compile does not run).
4. **[CONFIRMED]** The camera clear colour is navy, so any area not covered by the road shows navy.
5. **[CONFIRMED]** Player Settings allow **auto-rotation to landscape** (`defaultScreenOrientation: 4`, all four orientations allowed). Rotating the Simulator or a device gives aspect ≈ 1.78, so width-fit shrinks the size to ~1.57. That breaks the game completely. The game is designed for portrait only.

Not causes: Sprite import settings, the Canvas/UI (already fixed), the Unity 6 sprite renderer, or Pixel Perfect (not used).

## 4. Recommended approach (minimal, keeps gameplay identical)

Correct the existing `ResponsiveCamera` into a **"contain a safe gameplay rectangle"** camera:

```
size = max( R , safeHalfWidth / aspect )      // R = 4.976419, safeHalfWidth = 2.75
```

- **9:16:** size = 4.976, identical to the original. Gameplay and feel are unchanged.
- **Tall phones:** width-fit to ±2.75 (all lanes, car edges at ±2.57, and most of the white edge lines at 2.65–2.81 stay visible). iPhone 12 mini → size 5.96, y −7.20 … 4.72. 1080×2400 → size 6.11, top 4.871 (road top 4.875), no gap. (21:9 → top 5.18, a 0.30 strip above the road for part of each scroll cycle, filled with the road-coloured background; see note.)
- **Value chosen by rendered tests** (2026-10-25, Main Camera rendered at 1080×2340, 1080×2400, 1536×2048, 1080×1920, 1170×2532, 1080×2520 and 1668×2388, with the road at both scroll extremes). 2.65 cropped the white edge lines on tall phones. Anything above ~2.75 opens a gap at the top on 1080×2400.
- The road sprite size is now **confirmed** in the Editor: 10.24 × 28.9 units (texture imported at 726×2048, effective PPU 70.87), rendering 5.62 × 15.53 world units.
- **iPad / wide:** size stays 4.976, so the **vertical play area matches the reference exactly** and the player and obstacles stay fully visible. The extra width (0.92 per side) is filled by **side fill** (pick one):
  - **S1 (minimal):** set the camera Background colour to a roadside colour instead of navy. One property change.
  - **S2 (nicer):** add a wide roadside/grass sprite behind the road (child of `road (4)` so it scrolls with it). This is a scene addition, not a new script.
- Recompute only when `Screen.width/height` change, not every frame (cheap either way).
- Keep it runtime-only (don't add `[ExecuteAlways]`, which would rewrite the serialized camera size). **Evaluate scaling in Play mode.**
- **Lock orientation to Portrait** in Player Settings.

Because lanes, limits, spawn points, walls and speeds are world-space and untouched, **gameplay, difficulty and collisions are unchanged**. Only the visible frame changes.

Note: for ultra-tall screens (≤ 0.43 aspect, e.g. 21:9) a small top gap can remain. If it matters, give `road (4)`'s scroll a larger cover margin or raise `safeHalfWidth` slightly. That's a tuning value, not a redesign.

## 5. Other scaling observations (pre-existing, low priority)

- The scroll wrap step (2.89 world units) does not match the texture's dash period (~289 px ≈ 1.55 world units). This may cause a small visible "jump" of lane dashes every ~1.4 s. [ASSUMPTION: check visually.] It predates the migration.
- `Destroy(obs, 6f)` can remove an obstacle while it is still visible at the very bottom on tall screens in the first seconds of a run. Cosmetic.
- Spawn y = 5.94 is above the visible top on all devices (max top ≈ 4.9), so obstacles never pop in on screen.
