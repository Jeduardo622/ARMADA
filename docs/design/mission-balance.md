# Mission 03–06 Balance & Economy Tuning

> **Status: Drafted** (pending design pass); **economy timber slice
> (rollout slice 1) applied** — all other values remain proposals
> pending their own slices. Written 2026-07-22 against the shipped
> implementation (missions 03–06 as of PR #64), following the
> `pvp-tuning.md` precedent: this document is the knob inventory of
> record for mission 03–06 scenario values and the reward/upgrade
> economy constants. Value changes update the matching table **and this
> status** in the same PR; a design pass graduates it to Reviewed.

Motivation (tracked as an open design knob since the mission arc shipped):
in missions 03, 05, and 06 the enemy never ended a mission across the
200-seed sweeps below — every observed loss is a timeout, and no passive
run was ever wiped. Mission 04 has
the opposite problem: the canonical boarding line wins only a third of
its runs. Reward and upgrade constants are still their original
placeholders, and the campaign's timber income could not pay for the
upgrade tree it is supposed to fund (closed by the applied economy
slice below).

## Method

All empirical numbers below come from deterministic 200-seed sweeps
(seeds 1–200) run against the real mission modules (baseline) and
against `runMissionLoop` re-wired with the proposed constants
(proposals), using:

- the **canonical strategy** for each mission — the scripted order set its
  pinned vitest suite uses (`sloopFirst` for 03, `parallelBoarding` for
  04, `lineBreak` for 05, `swatMid` for 06); and
- a **passive baseline** — every player ship passes every turn — as the
  floor for enemy lethality.

Caveats: the canonical strategies never turn (they advance and heave to
on script, firing along the approach line), so a real player's ceiling
is higher than these win rates; and 200 seeds puts roughly ±3 percentage points of noise on any
rate, so differences that small are treated as flat. The probes are
throwaway harness runs, not committed tests; each implementing PR
re-derives its own pinned fixtures.

**Design targets** used throughout:

1. The canonical scripted strategy wins 55–85% of seeds, higher early in
   the arc (these are teaching missions).
2. Passive play is punished: most passive runs lose at least one ship,
   and full wipes are reachable — the loss-reason mix must not be 100%
   timeout.
3. Turn-count bonus objectives are stretches, hit in roughly a third of
   wins, not near-automatic and not near-impossible. (Conduct bonuses
   like `noShipLost` follow their own logic: they should be at genuine
   risk, not rare.)
4. A full campaign clear (first-completion rewards only) funds the full
   three-component tier-3 upgrade tree with a small (≲10%) buffer in
   the binding material (timber); gold and ore keep larger surpluses as
   future sink budget.
5. Targets are tier-independent as shipped: upgrade tiers currently
   affect only mission 07's simulation (the sole `supportsUpgrades`
   win-proof config), while missions 03–06 accept no tiers and reject
   upgrade proofs with `upgrades_not_supported` — an earned tier-3
   fleet produces exactly tier-0 outcomes in every mission this spec
   covers. Wiring `modifiers.shipUpgrades` into missions 03–06 is a
   resolve-contract and proof-config change, a prerequisite slice for
   any upgraded-fleet retune and out of scope here.

## Where these values live (Unity)

No mission 03–06 value is baked into any Unity scene — the repo's only
scenes are the PvP and Spectator demos, and mission flows are driven by
server payloads. **Scene regeneration is therefore never required for
this spec.** Every scenario knob below is instead mirrored in three
places that must move together:

- `unity/Assets/Armada/Core/Mission0XScenario.cs` — mirrored constants
  plus the `BuildExpectedStart` ship stats;
- the EditMode fingerprint pin in
  `unity/Assets/Tests/EditMode/ArmadaEditModeTests.cs`;
- the vitest fingerprint pin in `tests/mission0X.test.ts`.

## Baseline (current values, 200-seed sweeps)

| Mission | Canonical win rate | Loss reasons observed | Passive wipes | Notes |
| --- | --- | --- | --- | --- |
| 03 | 67.0% | timeout only | 0 / 200 | Passive fleets never lose meaningfully more than one ship's worth of hull (max recorded fleet damage fraction 0.50). |
| 04 | 33.5% (boarding), 16.5% (gunnery) | timeout, sunk, flanked | 86 / 200 | The only mission where enemies already finish fights — and it overshoots into frustration. |
| 05 | 44.0% | timeout only | 0 / 200 | Canonical runs take 6% average fleet damage — enemy guns effectively never bear. |
| 06 | 72.0% (swat-mid), 8.0% (boss-only) | timeout only | 0 / 200 | Passive fleets take 65% average damage but are never wiped in 14 turns. |

## Mission 03 "Raking Shot" (`src/sim/mission03.ts`)

| Knob | Current | Proposed | Derivation / effect |
| --- | --- | --- | --- |
| `MISSION_03_TURN_LIMIT` | 10 | **12** | The clock, not the enemy, is the dominant loss: 33% of canonical runs time out with the last enemy at ~36 average hull. Two more turns convert most near-misses (canonical win rate 67% → 81.5%) and give the meaner enemies (below) time to actually finish passive fleets: passive wipes go 0 → 71/200 (35.5%), and 199/200 passive runs lose at least one ship. |
| `MISSION_03_BONUS_TURN_TARGET` | 8 | **9** | Under the proposed values the canonical average win lands on turn ~10.0. A ≤8 target would be hit in only 9 of 163 winning sweeps (≈6%) — effectively unobtainable; ≤9 is hit in 62/163 (≈38%), matching the one-third stretch target. |
| `MISSION_03_RAKE_HIT_TARGET` | 2 | keep | The rake bonus is the mission's teaching objective and already lands in canonical wins. |
| `MISSION_03_ENEMY_DAMAGE_SCALE` | 1.05 | **1.15** | One knob deliberately scales BOTH enemy hull (frigate 180 → `floor(180×1.15)` = 207, sloop 120 → `floor(120×1.15)` = 138) and enemy outgoing damage (engine `damageScale`). +10% each way makes the pincer dangerous without a knob split. Splitting hull from damage would add an objectives field — an API-contract and fingerprint-shape change — and is deliberately out of scope here. |
| `MISSION_03_DEFAULT_SEED` | 303 | keep | Route default only; not part of the fingerprint payload (it is the pin test's argument, not its content). |
| Wind (90° at 2–4), spawn positions | as shipped | keep | The cross-breeze and pincer geometry are the mission's identity; the sweeps show no need to touch them. |

Fingerprint ripple: `turnLimit=12`, `bonusTurns=9`, `enemyScale=1.15`,
`hp207`, `hp138` in all three pins; seed-searched fixtures in
`tests/mission03.test.ts` (win-with-both-bonuses, win-missing-turn-bonus,
timeout, boarding-win seeds) must be re-searched against the new
distribution.

## Mission 04 "Boarding Party" (`src/sim/mission04.ts`)

| Knob | Current | Proposed | Derivation / effect |
| --- | --- | --- | --- |
| `MISSION_04_ENEMY_CREW_SCALE` | 0.9 | **0.8** | Frigate crew `floor(60×0.8)` = 48 (from 54). Crew only enters boarding defense, so this speeds the intended win path without touching enemy gunnery: defender attrition per successful boarding is ≈8–12 crew (`floor(power/6 + rng·4)` at ~50 boarding power), so 48 crew breaks about one boarding-turn sooner per frigate — across two frigates that recovers the runs that currently time out at 90%-boarded. Canonical boarding win rate: 33.5% → 55%. |
| `MISSION_04_PLAYER_BOARDING_BONUS` | 0.1 | **0.15** | At hull-to-hull range the success chance is already capped (60 + 50 power − 27 half-defense + 10 bonus = 93, clamped to 90), so the bonus's effect is at mid range: each 10 range units beyond 30 cost 3 chance points (1 through boarding power, 2 through the penalty term), so +5 bonus points buy back roughly 17 units of grapple envelope. Boarding is the mission's teaching mechanic; it should start paying earlier in the approach. |
| `MISSION_04_TURN_LIMIT` | 10 | keep | Probed at 12 with the knobs above: the win rate stays 55% while canonical-play wipes double (10 → 20/200) — the extra turns only feed the frigates' grind against stalled boarders. 10 is the right pressure. |
| `MISSION_04_DEFAULT_SEED` | 404 | keep | Route default only. |
| Debris field, headwind, spawns | as shipped | keep | — |

Probe consistency check: the passive baseline is byte-identical under
both knob sets (crew scale never enters enemy gunnery), and both sweeps
report exactly 86/200 passive wipes — mission 04's enemy lethality is
already right; only the player's win path was undertuned.

Fingerprint ripple: `crewScale=0.8`, `boardBonus=0.15`, `cw48` in all
three pins; re-search the fixture seeds in `tests/mission04.test.ts`.

## Mission 05 "Line Break" (`src/sim/mission05.ts`)

| Knob | Current | Proposed | Derivation / effect |
| --- | --- | --- | --- |
| Enemy line spawn: flagship `(260, 0)`, escorts `(240, ±60)` | as listed | **flagship `(220, 0)`, escorts `(200, ±60)`** | Root cause of the flat threat: the AI holds fire until `preferredRange` (100 for the flagship's line-advance) while a player broadside from spawn still lands ~70% (the engine's range penalty is only −1 hit chance per 50 units). Opening 40 units closer, the line's guns bear one-to-two turns sooner: canonical win rate 44% → 53%, and passive fleets go from one-third of runs losing a ship to 197/200 (1.3 of 3 ships lost on average). The escorts' station offsets (20 forward, ±60 lateral in the leader frame) are preserved exactly, so `mission05EnemyOrders` needs no change. Probed a further 20 units closer (200/180): canonical drops to 42% — the fight then starts inside the rock choke band — so 220/200 is the chosen point. The proposed 53% sits just below design target 1's 55% floor, inside the sweep's ±3pp noise; accepted, since the scripted line never turns and real play clears the band. |
| `MISSION_05_FLAGSHIP_HP_SCALE` | 1.1 | keep | Probed at 1.0 with the closer line: 52% vs 53% win rate — inside noise. Keep the flagship tanky; minimal diff. |
| `MISSION_05_TURN_LIMIT` / `MISSION_05_BONUS_TURN_TARGET` | 11 / 9 | keep | Proposed canonical average win is turn ~8.5 with the ≤9 bonus reachable; the limit is not the binding constraint here. |
| `MISSION_05_DEFAULT_SEED` | 505 | keep | Route default only. |
| Rock choke `(120, ±70) r35`, tailwind 0° at 4–6 | as shipped | keep | — |

Known residual: even under the proposal, no passive mission 05 run is
fully wiped (wipes 0/200) — the loss mix stays timeout-only. The
limiting factor is the engine's flat range falloff, not mission stats
(see Constraints); this spec moves threat as far as mission values can.

Fingerprint ripple: the three enemy ship position fields in all three
pins; re-search the fixture seeds in `tests/mission05.test.ts`
(flagship-first bonus seeds are geometry-sensitive).

## Mission 06 "Dreadnought Siege" (`src/sim/mission06.ts`)

| Knob | Current | Proposed | Derivation / effect |
| --- | --- | --- | --- |
| `MISSION_06_BOSS_DAMAGE_SCALE` | 1.1 | **1.5** | A boss that never ended a fight across the sweeps is a pushover on a timer: passive fleets take 65% damage but are never wiped in 14 turns. At 1.5 the canonical siege is untouched (71.5% vs 72.0% — flat at this sample size, because a competent siege kills the boss before its output compounds) while sloppy play finally pays: passive wipes 0 → 12/200, passive ships lost average 1.89 of 3, and canonical runs losing at least one ship rise from ~17 to 58/200 — making `noShipLost` a real stake (still kept in 128 of 143 wins). |
| `MISSION_06_ENRAGE_ACCURACY_BONUS` | 10 | **25** | Enrage opens below 30% of 468 hull (< ~140), which the canonical siege burns through in its final two-to-three turns — a +10 accuracy swing changes about one shot before the boss dies. +25 makes the last stand visibly land. Not part of the fingerprint or objectives payload, and `Mission06Scenario.cs` does not carry the constant (verified) — pinned only by the `mission06Modifiers` vitest, so this knob has no Unity ripple at all. |
| `MISSION_06_BOSS_HP_SCALE` | 1.3 | keep | 468 hull already sets the right siege length (canonical wins average turn ~7.9). |
| `MISSION_06_ENRAGE_HULL_FRACTION` | 0.3 | keep | Phase rhythm is fine; only the enrage's bite changes. |
| `MISSION_06_REINFORCEMENT_TURN` / `MISSION_06_REINFORCEMENT_HP_SCALE` | 5 / 0.9 | keep | The swat-mid vs boss-only gap (72% vs 8%) shows the reinforcement already forces the intended target-switch decision. |
| `MISSION_06_TURN_LIMIT` / `MISSION_06_BONUS_TURN_TARGET` | 14 / 12 | keep | Accepted deviation from target 3: with canonical wins averaging turn ~7.9 the ≤12 turn bonus is near-automatic. The boss mission's real stretch is `noShipLost`; tightening the turn target needs its own attainability probe and is deferred rather than guessed here. |
| `MISSION_06_DEFAULT_SEED` | 606 | keep | Route default only. |
| Shifting wind (0°→90° at turn 7), debris field | as shipped | keep | — |

Fingerprint ripple: `bossDmg=1.5` in all three pins (the enrage accuracy
value is not fingerprinted); re-search the fixture seeds in
`tests/mission06.test.ts` (both-bonus, ship-lost, slow-win, timeout).

## Economy: mission rewards (`src/economy/missionRewards.ts`)

Rewards are granted once per mission (first-completion win-proof claim;
no repeat farming), so campaign totals are hard caps on income. Totals
across missions 01–10 against the full three-component tier-3 upgrade
cost, with the applied timber values:

| Currency | Campaign income | Full-tree cost | Balance |
| --- | --- | --- | --- |
| gold | 3500 | 2850 (950 cannon + 780 sail + 1120 hull) | **+650 (≈23% buffer)** |
| timber | 585 (**applied**; was 475) | 555 (225 sail + 330 hull) | **+30 (≈5.4% buffer; was −80, tree not completable)** |
| ore | 400 | 190 (cannon) | **+210 (≈111% buffer)** |

The former timber deficit meant no player could max sail and hull from
campaign income — a dead end, not a choice, since there is no other
player-reachable timber source (the `inventory_grant_api` minting route
exists but is a trusted-service flag seeded disabled; see Constraints).
Fixed on the reward side (mission-linked lever; cutting tier-3 costs
would instead cheapen an unchanged power curve):

| Knob | Current | Proposed | Derivation |
| --- | --- | --- | --- |
| `MISSION_05_TIMBER` | 130 (**applied**; was 100) | keep | Timber income must reach ≥555. Spreading +110 across the three later timber missions keeps the reward curve monotone with difficulty and lands total timber at 585 = cost 555 + ~5.4% buffer, inside the ≲10% target. |
| `MISSION_07_TIMBER` | 160 (**applied**; was 120) | keep | (as above) |
| `MISSION_10_TIMBER` | 170 (**applied**; was 130) | keep | (as above) |
| All gold values (100…600) | as shipped | keep | Monotone with mission index; the ≈23% surplus is the intended budget for future sinks. |
| All ore values | as shipped | keep | The ≈111% ore surplus is large but harmless with no other ore sink; reserved for future sinks (captain XP / repair costs) rather than churned now. |
| `captain_shard` / `cosmetic_token` quantities | as shipped | keep | No sink exists for either yet; retune when one ships. |
| Bonus objectives grant nothing extra | as shipped | keep (future knob) | Bonus-conditional rewards are a natural follow-up once base rates are signed off; out of scope here. |

Ripple: `tests/upgrades.test.ts` is table-driven, and the only
Unity-side reward literal is mission 01's payload-parity pin
(untouched). This section has **no fingerprint or Unity ripple** — the
smallest slice in this spec. The applied slice added one exact pin:
the campaign-closure test in `tests/missionRewards.test.ts` asserts the
per-currency income/cost totals (585/555, 3500/2850, 400/190) and the
covers-the-tree invariant, so any future reward or cost retune updates
that pin and this section together.

## Economy: upgrade costs & effects (`src/economy/upgrades.ts`, `src/sim/upgradeEffects.ts`)

All keep — recorded here to complete the knob inventory of record:

| Knob | Current | Proposed | Note |
| --- | --- | --- | --- |
| `UPGRADE_COST_TABLE` (gold+ore/timber per tier) | cannon 100/250/600 g + 20/50/120 ore; sail 80/200/500 g + 25/60/140 timber; hull 120/300/700 g + 40/90/200 timber | keep | Tier-3 total 2850 g / 555 timber / 190 ore; the timber gap closes on the reward side above. Roughly ×2.5 cost step per tier keeps tier 3 a campaign-length goal. |
| `MAX_UPGRADE_TIER` | 3 | keep | — |
| `CANNON_DAMAGE_BONUS_PCT_PER_TIER` | 10 | keep | Tier 3 = +30% damage. |
| `SAIL_SPEED_BONUS_PER_TIER` | 1 | keep | Tier 3 = +3 effective speed (also feeds hit chance and base damage). |
| `SAIL_SLOW_TURN_RECOVERY_PER_TIER` | 15 | keep | Only bites with `statusEffects` missions. |
| `HULL_HP_BONUS_PCT_PER_TIER` | 10 | keep | Tier 3 = +30% hull at battle start. |

Upgrade tiers do not enter missions 03–06 at all today (see target 5:
only mission 07 is `supportsUpgrades`), so the win-rate targets in this
spec are tier-independent facts, not tier-0 baselines. Revisit effect
magnitudes only if and when a slice wires `modifiers.shipUpgrades` into
these missions and a playtest with earned tiers exists to measure.

## Constraints (do not tune past these)

- **Fingerprint pins move in threes.** Any change to a fingerprinted
  value updates the vitest pin, the EditMode pin, and the
  `Mission0XScenario.cs` mirror (constants AND `BuildExpectedStart`
  stats) in the same commit, then re-derives that mission's seed-searched
  fixtures. Pin exact values, not `<=` bounds, wherever a literal is
  shared across suites.
- **Engine constants are out of scope.** `RAKE_MULTIPLIER`, the hit
  chance formula (base 72, −1 per 50 range units, 15–95 clamp), base
  damage, ram/chain/fire values: flag-off resolution is pinned
  byte-identical, so any engine rebalance must ship behind a new opt-in
  `modifiers.*` flag as its own slice. In particular, the flat range
  falloff is the root cause of mission 05's low threat (players
  outrange every AI hold-fire radius from spawn); a `rangeFalloff`
  modifier flag is the named follow-up if closer spawns prove
  insufficient.
- **AI profile defaults are shared.** `AI_PROFILE_DEFAULTS`,
  `ESCORT_DEFAULTS`, and `BossParams` shapes feed missions 01–10 and
  telemetry; retuning them is a cross-mission change outside this spec.
- **Objectives payload shape is a contract.** Adding/splitting knobs
  that surface in `objectives` (e.g. separating mission 03's hull vs
  damage scale) changes the mission start contract and OpenAPI schema —
  protected-path work, deliberately not proposed here.
- **Rewards stay fail-closed** (`missionRewardsForCode` returns `[]`
  for unknown codes) and server-authoritative; value changes never touch
  the grant flow or the win-proof re-simulation.
- **Economy income math assumes `inventory_grant_api` stays disabled.**
  The grant route can mint any item for the calling player once that
  flag is enabled; the campaign-total "hard cap" arithmetic above holds
  only under the shipped seeded-disabled state. Enabling the flag is a
  separate protected decision, not a tuning knob.
- **Classifier divergence, documented deliberately:** `AGENTS.md` lists
  economy as a Class C protected area, but the harness classifier
  currently has no protected-area entry for `src/economy/`, so
  `route-task` returns Class B for the reward-value slices above.
  Until that gap is closed (tracked as its own follow-up), economy
  slices from this spec apply Class C discipline anyway — named
  reviewer, in-PR rollback, named risk — regardless of the mechanical
  classification.
- **Mission docs stay in sync.** Each implementing PR updates the
  "Tuning knobs" line of its `docs/content/missions/mission-0X-*.md`
  alongside this spec's table.
- **Ownership boundaries.** PvP knobs belong to `pvp-tuning.md`,
  spectator playback to `spectator-tuning.md`; missions 01–02 (intro,
  intentionally gentle) and the mission 07–10 engine-flag showcase
  constants are outside this spec except for the two timber reward
  values pulled in by the economy closure above.

## Rollout

Implement as one bounded PR per section, each with constants + fixture
re-derivation + all three fingerprint pins + the spec table/status
update, in this order:

1. **Economy: timber rewards** (**applied**) — smallest slice, zero
   fingerprint/Unity ripple; validates the process. Named risk for this slice: a
   `/complete` in flight across the deploy grants the new quantities
   for a pre-deploy win — benign (one-time, server-verified, small
   delta) and accepted; there is no other behavioral surface.
2. **Mission 04** — two constants, biggest player-facing pain.
3. **Mission 03** — three constants including a turn-limit change.
4. **Mission 06** — two constants, one outside the fingerprint.
5. **Mission 05** — position changes, geometry-sensitive fixtures.

Named deployment risk (applies to slices 2–5): a client that fetched
`/start` before a deploy and resolves after it is re-simulated under
the **new** constants with no signal — resolve requests carry no
scenario identifier (only `seed` + `turns`, plus a fixed
`schemaVersion: 1`), so the server cannot tell the orders were authored
against the old scenario. The client's local preview and the server
outcome can silently diverge, and a previously valid win proof can be
rejected. This is an **unsignaled compatibility window**, accepted
because deploys are atomic, mission sessions live minutes, and mission
resolves are stateless single requests. Carrying a scenario
fingerprint/version in resolve requests would close the window but is a
mission-API contract change — an optional hardening follow-up, not
proposed here. Rollback for every slice is a plain revert of the
constants commit (no schema, no migration, no scene).
