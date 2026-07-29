# Art Direction & Art-Readiness Spec

> **Status: Draft — D1 and D2 decided by @Jeduardo622 on 2026-07-28;
> D3 open pending the W5 evidence brief.** Authored following the
> `docs/design/spectator-tuning.md` precedent (spec drafted alongside the
> work, reviewed via PR merge; base content merged via PR #79).
> **D1 firing arcs → option A**: rebalance the sim to reward broadsides
> (Class C implementation arc, see §3.1). **D2 platform → option B**:
> mobile-first per the GDD (see §3.3). The spec reaches **Reviewed** once
> D3 is decided and the D1 rebalance lands its re-derived constraints.

This spec carries every design decision, seam, and convention that must
exist before 2D/3D art can be applied to Armada — without adding any art.
It reconciles the Game Design Document (`Armada – Game Design Document.pdf`,
art direction pp. 16–18, platform notes p. 24) against the game actually
built (missions 01–10, PvP hot-seat and netplay, spectator demo).

## 1. Style brief (GDD-derived)

From GDD pp. 16–18, the intended look:

- **Painterly Age-of-Sail**: classic naval oil-painting reference;
  brushstroke texture detail, rich saturated color; "a living painting".
- **Ocean as a character**: stylized water with hand-painted wave strokes
  and whitecaps — artistic impression over photorealism, explicitly for
  mobile performance headroom.
- **Ships**: realistic proportions and rigging, slight stylization
  (exaggerated sails/figureheads) so silhouettes read on small screens.
  Faction identity at a glance via sail colors, flags, hull motifs.
- **UI**: nautical motif — parchment panels, rope/wood frames, compass
  iconography; deep blues and golds (navy and brass). Clarity beats
  ornament. Health bars styled as segmented ship hulls; ability icons in
  cannonball frames with radial cooldown sweeps. In-world overlays
  (movement arrows, firing cones) drawn chalk/ink style.
- **Effects**: stylized cannon smoke ("ink in water"), wood-splinter hit
  particles, minimal gore. Camera: locked top-down orientation for
  fairness, optional slight zoom, rare skippable cinematic cuts.
- **Palette shifts by region/mood**: bright turquoise tropics vs stormy
  gray-black boss seas.

## 2. GDD-vs-build divergence table

Each row records the divergence and its **proposed resolution** for the
purposes of art readiness. Resolutions marked *(D#)* depend on an open
decision.

| # | GDD | Built | Proposed resolution |
| --- | --- | --- | --- |
| 1 | Mobile iOS/Android, touch, 30fps | Desktop 1920×1080, mouse, generated scenes | **(D2: DECIDED — mobile-first)** Scenes rebuilt around touch/aspect ranges before art lands; desktop stays as the Editor dev harness (§3.3). |
| 2 | 3v3 battles | 2v2 PvP, 1v2–3v3 missions | Accept as-built. Fleet size is scenario data, not a rendering constraint; art must simply not assume a fixed ship count. No engineering work. |
| 3 | Waypoint movement on a grid | Heading + speed deltas (WEGO retained) | Accept as-built and document as a deliberate divergence. The heading/speed model is deeply pinned (schema, fixtures, missions). Art consequence: ships need readable **absolute heading** (see §3.2). |
| 4 | Port/starboard firing arcs, cone UI, vulnerable bow/stern | No firing arc; accuracy maximised bow-on (`engine.ts` angle penalty) | **(D1: DECIDED — rebalance the sim)** The engine will reward beam angles per the GDD; firing-model-dependent art waits for the new curve (§3.1). |
| 5 | Reload timers | None; broadside every turn | Document gap; do not build. No art blocker — "radial cooldown" UI slots designed in the HUD IA (W4) so a future reload system has a home. |
| 6 | Boarding actions | `boarding` is live in missions 03/04 (orders, cooldowns, telemetry-counted) and rendered as a generic flash + HUD line; PvP defers it | Treat as an **active** mechanic in the art pass: it needs its own visual identity in W2 (today it reuses the ram flash color, so boarding and ramming are indistinguishable on the board). |
| 7 | Captains, abilities, portraits | Do not exist | Out of scope; document gap. No binding point required — captains attach to ships, and the ship view seam (W2) is sufficient. |
| 8 | Harbor hub | Does not exist | Out of scope; document gap. |
| 9 | Wind as core tactic, compass UI | Wind fully mechanical (`windMovement`, `windTurnRate`) but **never rendered** | Close the gap before art: wind direction/speed needs a world or HUD representation (W1 audit row; W2/W4 binding points). |
| 10 | Critical hits, subsystem damage | Sail/crew damage exists; no crit system | Accept as-built; sail/crew bars already visualize the subsystem idea. |

## 3. W0 — Design contradictions

### 3.1 Firing arcs vs the accuracy model (Decision D1 — HARD STOP)

**Current math** (`src/sim/engine.ts:328`):

```
angleDiff    = |attacker.heading − bearingToTarget|  (normalized to 0–180)
anglePenalty = floor(angleDiff / 15)                  (0–12 points)
hitChance    = clamp(72 − floor(range/50) − anglePenalty
               + floor(effectiveSpeed/2) + accuracyBonus, 15, 95)
```

Accuracy is **maximised when the bow points directly at the target** and
degrades 1 point per 15° off the bow. There is **no hard firing arc**: a
ship can fire at any bearing, including dead astern, at most −12 points.
The order schema requires `side: 'port' | 'starboard'` on every broadside
and the AI computes it from bearing sign (`ai.ts:80`), but resolution
ignores it entirely — it is a cosmetic label on the event.

**GDD intent** (p. 7): broadsides fire from port/starboard arcs shown as
cone overlays; bow/stern carry only weak chasers; optimal play is crossing
the enemy's path to deliver full broadsides while hiding your bow/stern.
Raking fire *is* implemented (`RAKE_ARC = 20`, ×1.5, keyed off the
*target's* keel line), so half of the GDD's positional game already
exists — the attacker-side half does not.

**Options:**

- **A. Change the sim to reward broadsides.** Invert the angle term to
  peak at ±90° off the bow (or add a hard arc gate using the already-typed
  `side` field). *Cost:* this reshapes the accuracy curve every pinned
  fixture stands on — all ten mission suites, the mission playable-seed
  selections (e.g. mission 10's seed 872 sweep), the PvP seed-11 empirical
  fixtures, both scenario fingerprints, and the reviewed
  `pvp-tuning.md` design analysis (v2's "closing raises hit chance"
  reasoning assumes bow-on approach is also firing posture). It re-runs
  the entire mission-balance arc (PRs #26–#78). Weeks of rebalance, and
  `pvp-tuning.md` review reopens.
- **B. Change the GDD (document divergence).** Keep the bow-on model as
  the shipped rule; art then draws a forward accuracy cone, not broadside
  cones; the `side` field stays cosmetic (which gun deck fires). *Cost:*
  gives up the most distinctive Age-of-Sail tactic the GDD promises;
  "broadside" naming throughout the game becomes flavor, not geometry.
  Zero engineering cost.
- **C. Deliberate divergence, staged.** Ship art against the current
  model (per B) but pin the decision that a broadside-arc rework is a
  future, separately-balanced Class C effort behind a new opt-in modifier
  (`broadsideArcs`), following the exact precedent of `windMovement` /
  `mutualRamming`: flag-off resolution stays byte-identical, missions keep
  legacy rules, PvP adopts it only via a new reviewed scenario version.
  Art consequence now: ship models and view seams must not bake in either
  answer — heading must be readable (needed under every option), and the
  firing-feedback visual (flash/tracer) originates from the event's
  `side` field so it is already correct if arcs later become real.

**DECIDED: option A** (@Jeduardo622, 2026-07-28) — the sim is rebalanced
so broadside (beam) firing angles are rewarded, per the GDD, **before**
art constraints are finalized. Consequences accepted with the decision:
every pinned fixture re-derives (all ten mission suites, playable-seed
selections, PvP seed-11 empirical fixtures, both scenario fingerprints,
C# mirrors), and `docs/design/pvp-tuning.md` review reopens in the
implementing PR. The rebalance is a Class C arc executed slice-by-slice
with human merges; ship-view art work that depends on the firing model
(fire-feedback origin, arc overlays) waits for the new curve, while
heading visibility (§3.2) proceeds — it is required under any curve.

### 3.2 Heading visibility (engineering plan — no decision needed)

Absolute heading is mechanically load-bearing (accuracy, raking, point of
sail, ram geometry) and **invisible today**: the renderer applies heading
as a Y rotation (`SpectatorRenderer.cs:451,576`), but a cube is 4-fold
symmetric about Y and a Unity capsule's long axis *is* Y — from the
top-down camera neither shape shows orientation at all. The PvP order
panel shows only the delta being dialled (`PvpOrderSession.cs:183`),
never absolute heading. Plan (all under W2/W4, no human gate):

1. The default primitive ship view gains an **asymmetric bow cue** (e.g. a
   flattened cone/quad "prow" child) so heading reads before any model
   exists — and every future model must satisfy the same contract:
   *silhouette must be directional from top-down at gameplay zoom*.
2. The view-provider contract (W2) exposes heading as a binding point
   (marker orientation already flows; the contract makes it mandatory).
3. The HUD IA (W4) adds absolute heading + wind readout (compass rose per
   GDD) to the order-entry surface.

### 3.3 Platform fork (Decision D2 — HARD STOP)

The GDD targets mobile touch at 30fps (p. 24: iOS/Android, min iPhone 8 /
3GB Android, tablets via anchored Canvas). The build is desktop
1920×1080 mouse-driven; the Unity install has Windows standalone and
Android modules (no WebGL, no iOS on this machine). Everything in W3–W6
forks on this: layout anchoring, font minimums, hit-target sizes
(~44pt touch vs mouse), safe-area handling, aspect range, texture budgets,
and whether `perf-budgets.md`'s "mid-tier device" means a phone.

- **A. Desktop-first, mobile-later:** art specs authored at 1920×1080
  landscape with a documented mobile-portability constraint set (relative
  anchoring, no hover-only affordances, hit targets ≥ 44pt-equivalent).
- **B. Mobile-first per GDD:** demo scenes rebuilt around touch and
  aspect-ratio ranges now; desktop becomes the dev harness. Larger
  engineering slice before any art lands.

**DECIDED: option B** (@Jeduardo622, 2026-07-28) — art specs are
authored mobile-first per GDD p. 24: landscape-first battle scenes with
defensive reflow across aspect ranges (button strips wrap on narrow
aspects; the battle-scene sensor-landscape orientation lock was approved
by @Jeduardo622 on 2026-07-29 and applied in PlayerSettings, and
portrait-optimized HUD layout remains W4 scope — the capture matrix
keeps its portrait frames as defensive-reflow evidence),
touch hit targets ≥ 44 pt **on the minimum supported device** (iPhone 8:
scaler-aware math, not reference-pixel claims), safe-area handling,
30 fps mid-tier budget,
Android as the locally-buildable reference platform (the WebGL module is
absent; iOS builds happen off this machine). The demo scenes are rebuilt
around touch before art lands; keyboard/mouse remain as the Editor dev
harness. Sequenced **after** the D1-A rebalance (rules first, then the
view/UI layer that displays them).

### 3.4 Render pipeline (Decision D3 — HARD STOP, owned by W5)

Built-in pipeline today; TMP is the only visual package. The GDD's
painterly water and mobile 30fps target realistically want **URP**
(Shader Graph water, SRP batcher, mobile-tuned lighting), and
`com.unity.addressables` 1.22.3 is already installed. Conversion is
project-wide (every material, every scene builder's
`LoadOrCreateBoardMaterial()` path, the capture harness's pinned ambient
settings). Full evidence and recommendation land with the W5 slice;
logged now because ship/water material authoring conventions (W6) and any
art-side shader work fork on it. **Awaiting human decision after the W5
brief.**

## 4. W1 — Visual-state audit

Every piece of simulation state, classified. "Client data" = reaches the
Unity client today (scenario/state/event stream). "Rendered" = has any
visual today. "Must render" = required before art (binding point in W2/W4).

| State item | Source | Client data? | Rendered today? | Must be renderable? | Notes |
| --- | --- | --- | --- | --- | --- |
| Position | `ship.position` → movement events | yes | yes (marker lerp) | yes | The one fully-served item. |
| Heading | `ship.heading`, maneuver events | yes | applied but invisible (§3.2) | **yes** | Bow cue + model silhouette contract. |
| Speed | `ship.speed`, maneuver/movement events | yes | no | **yes** | Sail-trim visual hook; HUD numeric at minimum. Movement events carry `effectiveSpeed` (wind-adjusted). |
| Sail trim (ordered speed delta) | order surface | yes (local) | text blob only | yes (HUD) | W4 order panel. |
| Wind direction/speed | `state.wind` | yes | **no** | **yes** | GDD compass; world-space indicator + HUD. Mechanically live under `windMovement`/`windTurnRate`. |
| Hull (hp) | ship state, `targetRemaining` | yes | yes (green bar) | yes | Bars re-skin later (GDD hull-shaped bars). |
| Sail (rigging) | ship state | yes | yes (yellow bar) | yes | |
| Crew | ship state | yes | **no** | yes (HUD) | Live for boarding math; cosmetic in PvP v1. Bar or HUD stat; binding point required. |
| Ammo selection (round/chain) | order + broadside event `ammo` | yes | flash color only (cyan/orange) | yes | Order panel icon slot (W4); flash stays the world cue. |
| Current target | order surface | yes (local) | text blob only | **yes** | Target line/reticle overlay slot in W2 board view. |
| Broadside side (port/starboard) | order + event `side` | yes | no | yes | Fire feedback must originate from the correct side of the view so D1-C stays honest (§3.1). |
| Hit / miss + roll/chance | broadside events | yes | flash regardless of hit | **yes** | GDD: splashes on miss. Distinct hit-vs-miss cue is a binding point. |
| Rake state (`bow`/`stern`) | broadside event `rake` | **no — discarded at the wire**: the C# `SimEvent` model has no `Rake` property, so deserialization drops the server field and `TurnPlayback` never sees it | no | yes | The showcase tactic; W2 must extend the C# wire model + playback mapping before a flourish slot can exist. |
| Ram / collision | ram events | yes | white flash | yes | Keep; needs contact-point binding. |
| Boarding | boarding events + `cooldowns.boarding` | yes | partial (ram-colored flash + HUD line) | **yes** | Active in missions 03/04 (divergence row 6); needs a visual distinct from ramming. |
| Status: fire | `status.onFire` + counters, status events | yes | **no** | **yes** | Mechanically live (`statusEffects` missions). Particle/tint slot. |
| Status: slow | `status.slowed` + counters | yes | **no** | **yes** | Shredded-sail visual pairs with sail bar. |
| Sinking / death | hp = 0, summary `sunk` | yes | marker vanishes | **yes** | Needs a real sink state (GDD: stylized sinking); today ships pop out of existence. |
| Turn number / limit | state.turn, scenario | yes | HUD narration text | yes | W4 IA slot. |
| Obstacles (islands) | `state.obstacles` | yes | **no** | **yes** | Impassable terrain that is invisible — actively misleading once ships route around nothing. Board-view binding point. |
| Slow zones (debris) | `state.slowZones` | yes | **no** | **yes** | Same; hazard readability. |
| Range bands | derived (range/50 penalty) | derivable | no | yes (overlay) | Chalk/ink range rings on the order surface (GDD cone/overlay language). |
| Upgrades (cannon/sail/hull tiers) | mission requests | yes (missions) | no | no | Meta-progression; not a battle visual. Documented gap. |
| Damage scale / accuracy bonus (boss tuning) | modifiers | yes | no | no | Balance plumbing, not player-facing state. |

## 5. Ship & faction inventory needing art

What the *existing* game needs, per scenario data (Deliverable 4 will
carry per-asset technical constraints):

| Class | Where used | Sides | Today |
| --- | --- | --- | --- |
| Sloop (light) | Missions (player, e.g. mission 10) | player | green cube |
| Frigate (medium) | PvP both sides; mission enemies | both | cube/capsule |
| Clipper (light-fast) | Mission 10 enemies | enemy | red capsule |
| Boss/man-of-war (heavy) | Mission 06 boss | enemy | red capsule |
| Sea/board | all four scenes | — | stretched cube + flat material |
| Islands / obstacles | missions with `obstacles` | — | nothing |
| Debris / slow zones | missions with `slowZones` | — | nothing |
| Effects: round shot, chain shot, ram, rake, fire, slow, sink, splash-miss | all scenes | — | material color swap (0.45s) or nothing |
| UI: order buttons, bars, banner, compass, code entry | all scenes | — | default TMP + flat uGUI |

Faction identity (GDD: Aurorian Empire vs Crimson Republic pirates as the
obvious player/enemy read) replaces the green-vs-red color coding; color
remains the accessibility fallback.

## 6. Open decisions

| ID | Decision | Options | Recommendation | Status |
| --- | --- | --- | --- | --- |
| D1 | Firing arcs: reconcile engine accuracy vs GDD broadsides | A rebalance sim / B document divergence / C staged opt-in modifier | C | **DECIDED: A** — @Jeduardo622, 2026-07-28 (rebalance chosen over the recommendation; blast radius accepted) |
| D2 | Platform target for art specs | A desktop-first + portability constraints / B mobile-first rebuild | A | **DECIDED: B** — @Jeduardo622, 2026-07-28 (mobile-first per GDD chosen over the recommendation) |
| D3 | Render pipeline | URP vs built-in (evidence lands with W5 slice) | URP (provisional) | **OPEN — human, after W5 brief** |

Decisions already taken inside this spec (reversible, engineering-level,
no shipped-behaviour change): heading gets a bow cue + silhouette
contract (§3.2); wind, obstacles, slow zones, hit/miss, and sinking are
promoted to must-render (§4); divergence rows 2, 5–8, 10 are documented
gaps, not build work.

## 7. Downstream workstream index

- **W2** view abstraction: every "must render" row above becomes a
  binding point; bar clearance derives from view bounds (replaces the
  hardcoded 1.5 enemy-capsule top noted in `spectator-tuning.md`).
- **W3** camera/composition: fix the ~90%-empty opening frame
  (`LINE_SEPARATION` 220 at 0.1 world scale vs ortho size 8.5); preserve
  the follow camera's never-tighter-than-authored invariant.
- **W4** HUD information architecture: structure only, skinnable to §1.
- **W5** pipeline + measurable perf budgets + Addressables policy.
- **W6** asset pipeline conventions (folders, naming, import presets,
  GUID discipline for generated scenes, licensing ledger).
- **W7** visual-regression capture harness (promoted from
  `SpectatorCaptureSpike.cs`).
