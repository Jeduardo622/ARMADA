# Art Needs — The Handoff (Deliverable 4)

> **Status: Drafted with the art-readiness arc; graduates alongside
> `art-direction.md` via this PR's human merge.** This is the document
> an artist starts from: every listed asset drops into a seam that
> already exists, is validated by harnesses that already run, and obeys
> conventions that are already enforced. Style: the GDD brief in
> `art-direction.md` §1 (painterly Age-of-Sail, deep blues and golds).

## 1. How art drops in (read first)

- **Ships**: build a prefab per class/livery, add the `ShipView`
  component, implement a `ShipViewProvider` that instantiates it —
  the renderer needs **zero changes** (`art-direction.md` §7 W2).
- **Files**: everything under `Assets/Art/` per
  `docs/design/asset-pipeline.md` (naming, enforced import floors,
  ledger row for anything third-party).
- **Validation**: run `node scripts/visual/capture.mjs` and
  `node scripts/visual/hud-capture.mjs` — your asset appears in 24
  deterministic frames across four aspects; re-baseline deliberately
  when it looks right (`docs/visual-regression.md`).

## 2. Global technical contract (applies to every ship)

| Constraint | Value | Why |
| --- | --- | --- |
| World scale | hull length ≈ **1.0–2.2 world units** by class (sloop 1.0, frigate 1.4, clipper/brig 1.2, capital 2.2) | Primitives are 1-unit; the board is 140×120 units; camera ortho size 5–8.5 |
| Pivot/origin | **waterline center**; renderer places the root at y = 0.5 | `SpectatorRenderer.ToWorld` and the sink submersion (−0.35) assume it |
| Bow | **local +z** | The renderer yaws roots by 90 − heading so +z tracks motion |
| Silhouette | **directional from top-down** at ortho size 5–8.5 | Heading is gameplay-critical (D1-A beam arcs); the bow cue precedent |
| `TopClearance` | honest height of the model's highest point (masts!) above origin | HP/sail bars derive their lift from it |
| Tint surface | main hull material = `TintRenderer`: URP/Lit with `[MainColor] _BaseColor` | Side tint, flashes, status warms, sink tint all recolor it |
| Accent surface | sails/trim as the accent renderer (never flashed) | Faction livery + stable heading cue mid-flash |
| Poly budget | ≤ 8k tris (capital ≤ 15k) | 6+ ships × 30 fps mid-tier (`perf-budgets.md`) |
| Textures | ≤ 1024² per ship (2048 hard floor via `ArtImportDefaults`); atlas per class | Same budget; atlasing mandated by `perf-budgets.md` |

## 3. Prioritized inventory

### P1 — Ship classes × faction liveries (blocks everything visual)

Factions replace the green/red coding (color stays as accessibility
fallback): **Aurorian Empire** (player/side A: navy hulls, brass trim,
white-gold sails) vs **Crimson Republic** (enemy/side B: dark hulls,
crimson sails). Livery = sail/trim textures on the shared class model.

| Class | Scenes/roles | Scale | Notes |
| --- | --- | --- | --- |
| Sloop | Missions 01–10 player fleet | 1.0 | The player's identity; most on-screen time |
| Frigate | PvP both sides (2v2), mission escorts | 1.4 | Needs BOTH liveries at launch (mirror match) |
| Clipper | Mission 10 enemies | 1.2 | Tall-rigged silhouette (sail-cutter fiction) |
| Brig | Mission 09 enemies | 1.2 | Reinforced bow reads the ram mechanic |
| Man-of-war / flagship | Mission 05 flagship, mission 06 dreadnought (scaled variant acceptable) | 2.2 | Boss presence; also the m06 reinforcement at 1.2 |

### P2 — The sea and the board (the URP payoff)

- **Water**: Shader Graph painterly sea replacing the flat board tint —
  the single highest-impact asset (GDD "ocean as a character"). Must
  keep a flat-color fallback param for the capture determinism check.
- **Islands/rocks**: replaces the obstacle cylinders (radius-scaled;
  spawned by the board context — 2–3 variants suffice).
- **Debris field**: replaces the slow-zone discs (translucent decal).

### P3 — Effects (each replaces a named pre-art cue)

| Effect | Replaces | Binding |
| --- | --- | --- |
| Round-shot muzzle + tracer + hull impact | orange hull flash | `PlaybackStep.Side` anchors the firing battery |
| Chain-shot variant | cyan flash | same; visually distinct (GDD showcase) |
| Splash (miss) | muted flash | miss branch of the broadside step |
| Rake flourish | victim co-flash + HUD callout | `PlaybackStep.Rake` |
| Ram impact | white flash | ram step, contact point between the two views |
| Boarding | violet flash | boarding step |
| Fire status | ember hull tint | looping while `ShipView` status on |
| Slow status | gray hull tint | shredded-sail visual on the accent |
| Sinking | submerge + deep-sea tint | replaces `ShipView.SetSunk` presentation |
| Wake | none (new) | motion readability during move lerps |

### P4 — UI skin (structure exists; skin the slots)

Per the HUD IA zone map (`art-direction.md` §7 W4): parchment narration
banner + status strip; per-row order cards with ammo icons and the
reserved radial-cooldown slot; rope-framed buttons (190×140 grid
cells); hull-shaped HP bars + sail bars (swap via the view seam);
wind compass rose (replaces the arrow); match-code input; turn banner.
All at 1920×1080 reference, safe-area aware, 4-aspect validated by the
HUD matrix.

### P5 — Audio (banks per `asset-pipeline.md` §1)

Cannon (round/chain distinct), ram, boarding, fire, sink, ambient sea
and gulls, UI ticks, one battle theme + one verdict sting (GDD p. 18).

## 4. Acceptance, per asset

1. Import floors pass (automatic — `ArtImportDefaults`).
2. Ship prefabs: PlayMode view-contract tests stay green (bow cue
   direction, `TopClearance`-derived bars, provider discovery).
3. Both capture harnesses re-baselined deliberately; determinism holds
   on a second run (the 24-frame review is the art critique surface).
4. Third-party anything: ledger row + license file in the same PR.
