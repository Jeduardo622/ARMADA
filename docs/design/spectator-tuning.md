# Spectator Demo Design Tuning

> **Status: Reviewed.** Applied values approved by @Jeduardo622 via the
> PR #51 merge on 2026-07-21. Originally drafted alongside the tuning
> slice, following the Mission 07 precedent of authoring the missing
> design spec with the implementation (see the QA notes in
> `docs/content/missions/mission-07-burning-seas.md`). Future value
> changes reopen review: update the table and this status in the same PR.

Consolidates every design-tunable placeholder in the spectator demo —
`SpectatorRenderer` serialized defaults and `SpectatorDemoSceneBuilder`
scene values — into one reviewable table. "keep" means the current
placeholder is retained deliberately, with the reason recorded. Applied
values match the **Proposed** column; the scene is regenerated after any
change here (see Regeneration below).

## SpectatorRenderer (`unity/Assets/Armada/Playback/SpectatorRenderer.cs`)

### Board scale

| Knob | Current | Proposed | Rationale |
| --- | --- | --- | --- |
| `worldUnitsPerSimUnit` | 0.1 | keep | Test-pinned: PlayMode asserts marker world x/z derived from it. |
| `markerHeight` | 0.5 | keep | Markers sit half a unit above the board; bar lift math builds on it. |

### Step timings (seconds)

| Knob | Current | Proposed | Rationale |
| --- | --- | --- | --- |
| `turnBannerSeconds` | 0.4 | 0.5 | The banner is the only reading pause between turns; still completes in one 0.5s test tick. |
| `maneuverSeconds` | 0.2 | keep | Rotation applies instantly at step start; the hold only needs to register. |
| `moveSeconds` | 0.35 | keep | Test-pinned ceiling: one 0.1s tick at x4 speed must finish a move step, so it must stay ≤ 0.4. |
| `flashSeconds` | 0.45 | keep | Longest single beat already; ≥ 0.5 would double the test ticks a flash step consumes. |

### Colors

| Knob | Current | Proposed | Rationale |
| --- | --- | --- | --- |
| `playerColor` | (0.20, 0.75, 0.35) | keep | Green reads well on the dark sea; shape (cube vs capsule) is the primary side cue. |
| `enemyColor` | (0.85, 0.25, 0.20) | keep | Strong contrast against both sea and player green. |
| `roundShotFlashColor` | (1.00, 0.60, 0.10) | (1.00, 0.72, 0.05) | More separation from enemy red when an enemy attacker flashes, while staying warm-family so chain cyan stays the distinct showcase. |
| `chainShotFlashColor` | (0.20, 0.90, 1.00) | keep | Mission 10 showcase; cyan must stay visually distinct from round shot. |
| `ramFlashColor` | white | keep | Neutral, distinct from both shot flashes. |

### Playback control bindings

| Knob | Current | Proposed | Rationale |
| --- | --- | --- | --- |
| `pauseKey` | Space | keep | Universal media convention. |
| `stepKey` | RightArrow | keep | Matches frame-step convention; documented in `docs/demo.md`. |
| `speedUpKey` / `speedDownKey` | Equals / Minus | keep | +/− convention; keypad variants already hardcoded alongside. |
| `speedPresets` | 0.5 / 1 / 2 / 4 | keep | Covers slow-motion review through fast skim; keys 1–4 documented. |
| `SetSpeed` clamp | 0.25–8 | keep | Bounds the presets with headroom for inspector experiments. |

### Readout bars

| Knob | Current | Proposed | Rationale |
| --- | --- | --- | --- |
| `barWidth` | 1.2 | keep | Slightly wider than the 1-unit markers; fractions divide by it, so no test coupling. |
| `barLift` | 1.2 | 1.4 | Doubles the clearance margin over the enemy capsule top (y = 1.5) flagged in the PR #50 review; bars now sit at y = 1.9. |
| `hullBarColor` | (0.40, 0.95, 0.40) | keep | Bright green reads against the dark sea the bars actually render over. |
| `sailBarColor` | (0.95, 0.90, 0.55) | keep | Yellow distinct from hull green at a glance. |
| Hull bar z-offset (`PositionBars`, hard-coded) | 0.45 | keep | Screen-up for the top-down camera; hull sits above sail. |
| Sail bar z-offset (`PositionBars`, hard-coded) | 0.30 | keep | 0.15 spacing from the hull bar exceeds the 0.12 bar depth, so the bars never overlap. |
| Bar cross-section (`SpawnBar`, hard-coded) | 0.08 × 0.12 (h × d) | keep | Thin enough to read as an overlay, thick enough to survive the orthographic zoom. |

## SpectatorDemoSceneBuilder (`unity/Assets/Editor/SpectatorDemoSceneBuilder.cs`)

| Knob | Current | Proposed | Rationale |
| --- | --- | --- | --- |
| Camera `orthographicSize` | 9 | 8.5 | Tighter framing: combat stays within z ±6 and the board (z ±8) is still fully framed with a 0.5-unit border. |
| Camera position | (12.5, 20, 0) | keep | Centered over the board with ample clip distance. |
| Camera background | (0.03, 0.08, 0.15) | keep | Near-black navy makes the sea board read as the play surface. |
| Board cube | 30×1×16 @ (12.5, −0.55, 0) | keep | Covers sim space (x 0–250, y ±60 at 0.1 world units per sim unit). |
| Sea material color | (0.07, 0.22, 0.36) | keep | Dark sea keeps all five flash/side colors legible. |
| Label font size | 20 | keep | Single HUD line fits comfortably at default game-view resolutions. |
| Label rect (hard-coded) | ±10 edge offset, −40 width inset, 60 height | keep | Two-line safety height with a small margin off the screen edges. |
| Directional light rotation (hard-coded) | (50, −30, 0) | keep | Unity's default key-light angle; markers are flat-tinted so lighting is non-critical. |
| Initial HUD hint | "Waiting for run... (Space pause, Right Arrow step, 1-4 speed)" | append "+/- cycle" | The +/− preset-cycle bindings shipped in PR #50 but were missing from the hint. |

## Constraints (do not tune past these)

- **Test-pinned:** `worldUnitsPerSimUnit` = 0.1 (marker position asserts);
  `moveSeconds` ≤ 0.4 (one 0.1s tick at x4 must finish a move step); HUD
  format strings ("PAUSED", "speed x4", damage totals) are asserted verbatim.
- **Tick budget:** both spectator PlayMode tests bound playback at 100 ticks
  of 0.5s — keep per-step timings the same order of magnitude, and treat
  0.5s as a per-step boundary (crossing it doubles ticks consumed).
- **Bar clearance:** marker y + `barLift` must exceed 1.5 (enemy capsule top
  under the top-down camera).
- **Showcase distinctness:** `chainShotFlashColor` must stay visually
  distinct from `roundShotFlashColor`.

## Regeneration

The scene is generated. After changing any serialized default or builder
value, rebuild `Assets/Scenes/SpectatorDemo.unity` via
`Assets → Armada → Build Spectator Demo Scene` (or batchmode
`-executeMethod SpectatorDemoSceneBuilder.Build`) and commit the scene
plus any new `.meta` files.
