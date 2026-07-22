# PvP Demo Design Tuning

> **Status: Reviewed.** Design pass run 2026-07-21; applied values
> approved by @Jeduardo622 via the human merge of the design-pass PR.
> Originally drafted against the shipped implementation (PRs #53–#56,
> corrected in #58), following the Mission 07 / spectator-tuning
> precedent. The pass applied one change (in-progress idle TTL, below),
> confirmed every other value as a deliberate keep, and recorded the v1
> dominant-strategy analysis. Future value changes reopen review: update
> the table and this status in the same PR.

Consolidates every design-tunable knob and placeholder constant in the
PvP demo — scenario stats, match lifecycle policy, order-entry surface,
netplay polling, and the two generated scenes. Spectator playback knobs
(shared renderer) are owned by `docs/design/spectator-tuning.md` and are
deliberately not duplicated here.

## Scenario (`src/sim/pvpScenario.ts`, mirrored in `unity/.../Core/PvpScenario.cs`)

> ⚠ Everything in this section **except `PVP_DEFAULT_SEED`** is
> **fingerprint-pinned** in `tests/pvpScenario.test.ts` AND the Unity
> EditMode suite. Changing a pinned value is a design change: update both
> fingerprint constants, the C# mirror, and re-derive the empirical
> fixtures (see Constraints). The default seed is not part of the
> fingerprint — it is pinned separately by the seed-11 fixtures and the
> C# `PvpScenario.DefaultSeed` mirror, so tuning only the seed touches
> those but neither fingerprint constant.

| Knob | Current | Proposed | Rationale / derived effect |
| --- | --- | --- | --- |
| `PVP_TURN_LIMIT` | 20 | keep | Focus-fire wins land around turn 6–8; 20 gives maneuver-heavy play headroom while keeping stalemates bounded. Timeout = draw. |
| `FRIGATE_HP` | 120 | keep | 4–5 round-shot hits to sink (25–30 hull per hit at opening stats: base 25 + 0–5 variance; 4 hits only at maximum variance); sets match length together with the hit chance below. |
| `FRIGATE_SAIL` | 80 | keep | Feeds base damage (`18 + floor(sail/25)` = +3) and is the chain-shot target pool. |
| `FRIGATE_CREW` | 50 | keep | Only cosmetic in v1 (boarding deferred); becomes live if boarding joins the modifier set. |
| `FRIGATE_SPEED` | 3 | keep | +1 hit chance (`floor(v/2)`), +4 base damage (`floor(v·1.5)`); speedDelta orders swing damage ±3 without a movement phase. |
| `LINE_SEPARATION` | 220 | keep | Opening range 220 → range penalty 4 → ~69% hit chance at start. The single biggest lethality lever: −50 range ≈ +1% hit per 50 units. |
| `LINE_SPREAD` | 30 | keep | ±30 y keeps the four markers visually separated at the shared 0.1 world-scale framing. |
| `WIND_DIRECTION` / `WIND_SPEED` | 90 / 0 | keep | Cosmetic in v1 (no `windMovement`); speed 0 keeps it inert even if flags flip accidentally. |
| `PVP_DEFAULT_SEED` | 11 | keep | Hot-seat only (netplay seeds server-side). **Not in the fingerprint**; pinned by the seed-11 focus-fire-vs-split fixtures (vitest + server full-match test), the C# `DefaultSeed` mirror, AND the serialized `seed` baked into `PvPHotseatDemo.unity` (regenerate the scene when tuning). |
| Modifier set | `{ chainShot: true }` | keep | **Product pin, not a tuning knob.** No movement phase by consequence; flipping `windMovement`/`ramming` in is scenario v2 and needs explicit sign-off. See the dominant-strategy note below for why v2 is the real balance lever. |

### Design note: the v1 dominant strategy (accepted for the demo)

With no movement phase, range and bearing never change, which collapses
the order surface. Heading matters only through your own broadside angle
penalty (`floor(diff/15)` per 15° off the target bearing; nothing else
reads heading), so the entire depth of maneuvering is **one free
alignment press**: same-row targets sit at bearing 0° (already aligned),
cross-row targets at ±15° rounded (one 15° press removes one penalty
point, e.g. 68% → 69% hit), and any turn beyond alignment only hurts.
Speed ramping strictly helps (+`floor(v/2)` hit chance and
+`floor(v·1.5)` base damage, with no positional cost since nothing
moves). Optimal v1 play is therefore **align to your target's bearing
once (re-align one press when switching rows), speed +2 every turn,
focus fire** — beyond that single alignment the maneuver buttons are
trap options, and chain-vs-round plus target selection are the only
live decisions. This is accepted for the demo: it keeps matches legible
and short. It is also the headline motivation for scenario v2
(`windMovement` + `ramming`), where heading and speed buy position
instead of only modifying gunnery. Do not "fix" this by tuning the v1
numbers — no constant in this spec changes the dominance structure.

## Match lifecycle policy (`src/routes/pvp.ts`)

| Knob | Current | Proposed | Rationale |
| --- | --- | --- | --- |
| `MATCH_WAITING_TTL_MS` | 30 min | keep | How long a join code stays live. Generous for share-a-code-over-chat; not so long that dead lobbies pile up. |
| `MATCH_IN_PROGRESS_TTL_MS` | 60 min | 15 min (**applied**) | Idle time (no submission) before an in-progress match expires. Authoring a turn takes 1–2 minutes, so 15 idle minutes is unambiguous abandonment — and with no resume flow, a longer TTL only extends how long the stranded opponent stares at the waiting HUD (worst case was an hour). Still an order of magnitude above real authoring time; polling never refreshes it by design. |
| `MAX_OPEN_MATCHES_PER_PLAYER` | 3 | keep | Soft cap, enforced on create AND join. Bounds storage abuse; 3 lets a playtest juggle a stuck match plus a fresh one. |
| Join-code length / alphabet | 8 chars, `A–Z2–9` minus `0/O/1/I/L` (31 glyphs) | keep | Read-aloud safe; 31⁸ ≈ 8.5e11 codes keeps collisions negligible for the 3-attempt create loop. |
| `CODE_CREATE_ATTEMPTS` | 3 | keep | Collision retries before giving up; at the code space above this should never be observed. |
| Orders per submission (zod cap, hard-coded) | 8 | keep | Must stay ≥ ships per side (2); headroom for larger scenario variants without a contract change. |
| Server seed range (hard-coded) | `randomInt(0, 2147483647)` → 0…2³¹−2 | keep | `crypto.randomInt`'s maximum is exclusive, so 2³¹−1 itself is unreachable; effectively the full non-negative int32 space minus one value, with no gameplay effect beyond variety. |

## Order-entry surface (`unity/.../Services/PvpOrderSession.cs`)

| Knob | Current | Proposed | Rationale |
| --- | --- | --- | --- |
| `TurnDeltaStep` | 15° per press | keep | Matches the engine's 15°-per-point angle penalty granularity, so every press has a legible effect. |
| `TurnDeltaLimit` | ±90° | keep | **Schema-pinned** (`simOrderSchema`); the UI clamp must mirror the server bound. |
| `SpeedDeltaLimit` | ±2 | keep | **Schema-pinned**; same mirroring requirement. |

## Netplay client (`unity/.../UI/PvpNetplayUIController.cs`)

| Knob | Current | Proposed | Rationale |
| --- | --- | --- | --- |
| `pollIntervalSeconds` (serialized) | 2 | keep | Sets worst-case "opponent resolved → I see it" latency. PlayMode tests drive `Advance(2.5f)` ticks and polling fires at `_pollDueIn <= 0`, so intervals up to and including 2.5 still poll; above 2.5 update the tests with it. |
| Verdict / status copy (hard-coded) | "VICTORY — your side wins", "DEFEAT…", "DRAW", "MATCH EXPIRED — abandoned…", "Connection hiccup… retrying", "Turn N: broadsides fly..." | keep | Placeholder voice; a copy pass can retune freely — none of these strings are test-pinned verbatim except via `Does.Contain("VICTORY")` in one PlayMode assert. |

## Generated scenes (`PvPHotseatDemoSceneBuilder` / `PvPNetplayDemoSceneBuilder`)

Both builders share the spectator demo's board material and framing
conventions; values below are hard-coded in the builders.

| Knob | Current | Proposed | Rationale |
| --- | --- | --- | --- |
| Camera `orthographicSize` / position | 8.5 @ (11, 20, 0) | keep | Battle midline sits at sim x = 110 → world x = 11 (vs 12.5 in the mission scene); same 8.5 zoom as the reviewed spectator framing. |
| Board cube | 30×1×16 @ (11, −0.55, 0) | keep | Covers the mirrored sim space at 0.1 world units per sim unit. |
| Button size / spacing / margin | 130×40, 8 gap, 20 edge | keep | Eight order buttons fit one row at default game-view widths. |
| Button fill color | (0.15, 0.25, 0.4, 0.9) | keep | Muted navy; readable TMP white labels without competing with the board. |
| Order/HUD label layout | hud 60h @ −10; status 40h @ −75; order panel 140h @ 70 (hot-seat) / 116 (netplay) | keep | Netplay lifts the order panel above its second (menu) button row at y = 66. |
| Label / button font sizes | 18 / 16 | keep | Denser than the mission scene's 20 — the PvP HUD carries three text blocks. |
| Join-code input (netplay) | 8-char limit, LegacyRuntime 18pt, white field (0.9), gray italic "MATCH CODE" placeholder | keep | Legacy uGUI InputField keeps the interactive path off TMP; limit mirrors the server code length. |
| Hot-seat handoff copy | "Side A locked in. Hand the seat to Side B…" | keep | The interstitial is a review-mandated fairness gate; copy is free to change, the extra confirm press is not. |

## Constraints (do not tune past these)

- **Fingerprint pins:** any Scenario-section change other than
  `PVP_DEFAULT_SEED` must update the fingerprint constant in
  `tests/pvpScenario.test.ts`, the same constant in the Unity EditMode
  suite, and `PvpScenario.cs` — and re-derive the empirical fixtures:
  the seed-11 focus-fire win (vitest + the server full-match test) and
  the hold-fire turn-limit draw. Tuning only the default seed skips both
  fingerprint constants but still re-derives the fixtures, updates the
  C# `DefaultSeed` mirror, **and regenerates `PvPHotseatDemo.unity`** —
  the scene stores the bootstrap's serialized `seed`, which does not
  follow the C# default (netplay is unaffected: its seed is
  server-picked).
- **Schema mirrors:** `TurnDeltaLimit` (±90) and `SpeedDeltaLimit` (±2)
  must equal `simOrderSchema`'s bounds; the join-code input's character
  limit must equal `CODE_LENGTH`.
- **No upper bound on `turnNumber`** in the submit schema — deliberate
  (post-draw replays must reach the transaction to earn `409 match_over`;
  Codex finding on PR #54). Do not "tidy" a max back in.
- **TTL semantics:** polling must never bump `updatedAt` (idle polling
  keeping a match alive would defeat abandonment detection). The
  in-progress TTL must exceed any plausible order-authoring session.
- **Orders cap ≥ fleet size** per side, or legal submissions start
  failing validation.
- **Poll interval vs tests:** PlayMode drives 2.5s ticks and the poll
  condition is `<= 0`, so the boundary is exactly 2.5s — a poll interval
  above 2.5s silently stops polling in those tests.
- **Modifier set is a product pin**, not a knob (see Scenario table).
- Spectator playback knobs (timings, colors, bars, camera in the mission
  scene) are owned by `spectator-tuning.md`; PvP scenes inherit the
  renderer defaults automatically, and per-turn bar baselines derive from
  `PvpScenario.BuildInitialState()` so hull/sail stat changes flow into
  the bars with no extra wiring.

## Regeneration

Both PvP scenes are generated. After changing any builder value or
serialized default, rebuild via `Assets → Armada → Build PvP Hotseat
Demo Scene` / `Build PvP Netplay Demo Scene` (or batchmode
`-executeMethod PvPHotseatDemoSceneBuilder.Build` /
`PvPNetplayDemoSceneBuilder.Build`) and commit the scenes plus any new
`.meta` files. Scenario or lifecycle constant changes need no scene
rebuild — with one exception: `PVP_DEFAULT_SEED` is baked into the
hot-seat scene as the bootstrap's serialized `seed`, so seed tuning
regenerates `PvPHotseatDemo.unity` too. Everything else ripples only
through the fingerprint/fixture pins above.
