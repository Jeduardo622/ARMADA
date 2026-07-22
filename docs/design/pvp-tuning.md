# PvP Demo Design Tuning

> **Status: Reviewed** (scenario v2). Design pass run 2026-07-21 (PR #60);
> **scenario v2 — `windMovement` + `ramming` with a live cross-breeze —
> explicitly signed off by @Jeduardo622 and applied via the human merge of
> the v2 PR** on 2026-07-21. Originally drafted against the shipped
> implementation (PRs #53–#56, corrected in #58), following the
> Mission 07 / spectator-tuning precedent. Future value changes reopen
> review: update the table and this status in the same PR.

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
| `FRIGATE_SPEED` | 3 | keep | Under v2 this is real motion: 15 sim units per turn (`v × MOVEMENT_SCALE 5`), so head-on fleets close at 30/turn from 220 apart — contact band (`RAM_CONTACT_RANGE` 25) around turn 6–7. Still feeds hit chance (`floor(v/2)`) and base damage (`floor(v·1.5)`), now via wind-adjusted effective speed. |
| `LINE_SEPARATION` | 220 | keep | Opening range 220 → range penalty 4 → ~69% hit chance at start; under v2 range collapses as the lines close, so hit chance and damage climb turn over turn. |
| `LINE_SPREAD` | 30 | keep | ±30 y keeps the four markers visually separated at the shared 0.1 world-scale framing. |
| `WIND_DIRECTION` / `WIND_SPEED` | 90 / 4 (**applied, v2**) | keep | Live cross-breeze: direction 90 is perpendicular to the battle axis, so the mirror stays perfectly fair (a maneuver and its mirrored counterpart sit at the same point of sail); speed 4 gives ±2 effective speed on the tailwind/headwind arcs (mission convention), and both fleets open at a neutral beam reach. |
| `PVP_DEFAULT_SEED` | 11 | keep | Hot-seat only (netplay seeds server-side). **Not in the fingerprint**; pinned by the seed-11 focus-fire-vs-split fixtures (vitest + server full-match test), the C# `DefaultSeed` mirror, AND the serialized `seed` baked into `PvPHotseatDemo.unity` (regenerate the scene when tuning). |
| Modifier set | `{ chainShot, ramming, windMovement }` (**applied, v2**) | keep | **Product pin, not a tuning knob** — v2 signed off by @Jeduardo622 (this PR). `windMovement` makes heading and speed buy real position; `ramming` makes contact within 25 units dangerous (`10 + 4×effectiveSpeed` hull to the target, half to the rammer). The two flags travel together: ram contact happens in the movement phase. |

### Design note: v1's dominant strategy and what v2 changed

**v1 (historical, chain shot only):** with no movement phase, range and
bearing never changed, collapsing maneuvering to one free alignment
press (same-row targets at bearing 0°, cross-row at rounded ±15°; any
turn beyond alignment only hurt) — optimal play was align once, speed +2
every turn, focus fire, and the maneuver buttons were otherwise trap
options. That analysis motivated v2 and no v1 constant could change it.

**v2 (current):** heading and speed now buy position, which breaks the
dominance: closing raises both sides' hit chance and damage and courts
the ram band, holding range keeps the duel long, and the wind arcs make
some headings faster than others. Empirically pinned properties
(`tests/pvpScenario.test.ts`, seed 11): focus-fire-vs-split is a side A
win at turn 7 (bloodier than v1's static duel — three ships sink);
straight-ahead hold-fire fleets collide near midfield, exchange exactly
4 rams, sail through, and never re-engage (draw at the limit); turning
away on turn 1 avoids contact entirely. **Known asymmetry, accepted and
pinned:** resolution order is by ship id, so alpha-side ships move first
and strike first in the ram band — in the head-on fixture side A ends
98/98 hull vs side B's 76/76. This first-mover ram initiative is
inherent to sequential resolution (called out since slice 1) and is now
the top open balance question for a future pass; candidate levers live
in the engine (ram damage split, contact range), not in this scenario's
constants.

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
| Camera `orthographicSize` / position | 8.5 @ (11, 20, 0) | keep | The *opening* frame only under v2: battle midline sits at sim x = 110 → world x = 11; the follow behavior below takes over once ships move. |
| Follow camera (`SpectatorRenderer.followCamera`, wired by both PvP builders) | `followPadding` 2, `followMinSize` 8.5 | keep (**new, v2**) | Re-frames the orthographic camera every tick to keep all markers (and their bars, via the padding) in view; never zooms tighter than the authored 8.5. Mission scenes leave the field null and keep their fixed framing. |
| Board cube | 140×1×120 @ (11, −0.55, 0) (**applied, v2**) | keep | Sea under any realistic 20-turn line once ships actually sail; extreme max-speed runs can still reach open void past the edge — cosmetic only, the follow camera keeps the ships themselves in view. |
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
  the seed-11 focus-fire win at turn 7 (vitest + the server full-match
  test), the head-on 4-ram exchange with its 98/76 hull split, and the
  turn-1-turn-away clean stall. Tuning only the default seed skips both
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
