# Mission 03–06 Balance & Economy Tuning

> **Status: Drafted** (pending design pass); **all five rollout slices
> applied** — economy timber (1), mission 04 (2), mission 03 (3),
> mission 06 (4) and mission 05 (5). Every value in this document now
> matches the shipped implementation, so the tables below are a record of
> applied values rather than proposals; the "Proposed" columns read
> "keep" throughout and the derivations are retained as the rationale of
> record. A design pass still graduates this document to Reviewed.
> Written 2026-07-22 against the shipped
> implementation (missions 03–06 as of PR #64), following the
> `pvp-tuning.md` precedent: this document is the knob inventory of
> record for mission 03–06 scenario values and the reward/upgrade
> economy constants. Value changes update the matching table **and this
> status** in the same PR; a design pass graduates it to Reviewed.

> **Measurement basis (2026-08-02):** the sweep table under "Current
> measurements" was re-run under the **D1-A broadside-arc accuracy
> curve** (`docs/design/art-direction.md` §3.1) with the same
> methodology as the original arc (200 seeds, seeds 1–200, canonical
> strategies + passive baseline), now reproducible via
> `npx tsx scripts/balance/sweep-missions.ts`. The per-knob derivation
> narratives in the mission sections below remain the **legacy-curve
> apply-time record** — they are the rationale each slice was applied
> under, kept verbatim as history; the table below is the measurement
> of record for current play.

Motivation (tracked as an open design knob since the mission arc shipped):
in missions 03, 05, and 06 the enemy never ended a mission across the
baseline 200-seed sweeps below — every observed loss is a timeout, and no
passive run was ever wiped. (All three are now addressed by their applied
slices below, but to three different depths. Mission 03 is fully closed:
3 canonical losses to sinking **and** 71/200 passive wipes. Mission 06's
closure is passive-only — 12/200 passive wipes, with its canonical loss
mix still timeout-only, since a competent siege kills the boss before its
output compounds. Mission 05 is closed only in part: passive fleets now
lose at least one ship in 197/200 runs, up from 106/200, but **no passive
run is wiped and the loss mix stays timeout-only**, because pushing the
line close enough to wipe costs more canonical win rate than design
target 1 allows. Mission 05's remaining gap is the named `rangeFalloff`
engine follow-up, not a mission value.)
Mission 04 had
the opposite problem: the canonical boarding line won only a third of
its runs (closed by the applied mission 04 slice below). Reward and upgrade constants are still their original
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
rate, so differences that small are treated as flat. The original arc's
probes were throwaway harness runs; the 2026-08-02 re-sweep committed
the harness as `scripts/balance/sweep-missions.ts` (run with
`npx tsx scripts/balance/sweep-missions.ts`), which copies the canonical
strategy order arrays verbatim from the pinned vitest suites. It is a
measurement tool, not a test; each implementing PR still re-derives its
own pinned fixtures.

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

Four further surfaces are not part of the fingerprint but still carry
copies of these values, and the applied mission 03 slice had to move all
four (two of them found only in review — check them in every slice; the
applied mission 06 slice checked all four and needed one, the synthetic
PlayMode payload, which local review caught after the same surface was
first waved off as hull-only — see the first bullet):

- **derived values in synthetic test payloads** —
  `unity/Assets/Tests/PlayMode/ArmadaPlayModeTests.cs`. Not just hull
  totals: the mission 06 fake client's whole `DamageProfile` was a verbatim
  copy of the canonical seed-1 run, so the **player** damage triple
  (`PlayerHullDamage` / `PlayerHullDamageFraction` / `PlayerRemainingHp`)
  moves with any knob that changes incoming damage, even when every hull
  total is kept. The applied mission 06 slice had to move it 69/0.19/291 →
  94/0.26/266, and pinned those three values in `tests/mission06.test.ts`
  so the synthetic copy now has a source of truth. Its `EnemyHullDamage`
  (468) is a separate, pre-existing inaccuracy: the server reports 576 for
  a cleared field once the reinforcement has spawned. No assertion reads
  any of it, which is exactly why it drifts unnoticed. **Whether this
  surface moves is per-mission and must be measured, not assumed:** mission
  05's fake needed no edit, but the reason has to be stated precisely (local
  review corrected a looser first draft). Its damage triple 110/0.31/250 does
  occur in real runs — old-geometry escorts-first seed 24 and new-geometry
  escorts-first seed 185 both produce it — so "matches no real run" is wrong.
  What is true, and a stronger reason for leaving it alone, is that **no run
  under any of four geometries × nine strategies × seeds 1–600 matches the
  fake's full payload**: it claims a turn-8 flagship-first win *and*
  110/0.31/250, while every turn-8 flagship-first line-break win takes
  exactly 0/0/360 at every spawn distance. The payload is internally
  impossible, so it is invented and tracks nothing. Its `TurnCount` 8 and
  `ChokeBlockedMoves` 2 *are* real seed-1 line-break values (identical old and
  new, hence no edit), and `EnemyHullDamage` 438 is the true fleet total —
  though the flow actually runs seed **505**, whose real blocked count is 0,
  so even the plausible fields do not match the seed the fixture claims. Diff
  the fixture against a live resolve log for the seed it claims to represent,
  and match the whole payload rather than one field group;
- **the documented resolve bound** — `Mission0XResolveRequest.turns.maxItems`
  in `docs/api/openapi.yaml` equals that mission's turn limit, because the
  route derives its Zod cap from the constant. `verify-contracts` compares
  operation lists and `schemaVersion` only, so nothing in CI catches this
  drift; any turn-limit change owes this edit (mission 03 pins it in
  `tests/mission03.test.ts`);
- **player-facing objective strings** —
  `docs/content/strings/missions.json` (`mission_0X_obj_bonus_*` spell out
  turn targets); and
- **the mission content doc** — `docs/content/missions/mission-0X-*.md`
  repeats the values in its objectives, player-constraints, and
  "Tuning knobs" lines.

## Current measurements (D1-A broadside curve, 200-seed sweeps, 2026-08-02)

Re-measured under the D1-A broadside-arc accuracy curve with all applied
slice values in place (no mission value changed for this re-sweep). The
"legacy" figures in each Notes cell are the post-slice numbers under the
old bow-on curve, kept for comparability; the pre-slice baselines and the
apply-time derivation record live in the mission sections below.

| Mission | Canonical win rate | Loss reasons observed | Passive wipes | Notes |
| --- | --- | --- | --- | --- |
| 03 | 80.0% (160/200) | timeout 37, sunk 3 | 75 / 200 | Average win turn 9.91 (histogram 8:9, 9:55, 10:53, 11:28, 12:15); ≤9 turn bonus hit in 64/160 wins (40%), matching the one-third stretch target. Passive: 200/200 runs lose at least one ship (average 1.38 of 2), average passive fleet damage 85%. Nearly unchanged from legacy (81.5%, timeout 34 + sunk 3, wipes 71/200) — the mission 03 geometry survives the curve inversion, and passive wipes actually rose. |
| 04 | 46.0% (boarding, 92/200), 15.5% (gunnery, 31/200) | boarding: timeout 90, sunk 18; passive adds flanked | 69 / 200 | Boarding wins average turn 7.04; every boarding win lands a successful boarding and keeps `noShipLost`. Legacy: 55.0% / 16.5%, wipes 86/200. The boarding win rate fell 9 points below design target 1's 55% floor — see the D1-A deviations note below. |
| 05 | 43.5% (87/200) | timeout only (113) | 0 / 200 | Average win turn 8.61; `sankFlagshipFirst` in all 87 wins; ≤9 bonus in 79/87 (91%, still near-automatic). Passive: 198/200 runs lose at least one ship (average 1.23 of 3), average passive fleet damage 57%, wipes still 0/200 with a timeout-only loss mix — the known residual is unchanged in kind. Legacy: 53.0%, 197/200 passive losing a ship, 59% passive damage. Fell 11.5 points below the 55% floor — see the D1-A deviations note below. |
| 06 | 53.0% (swat-mid, 106/200), 2.0% (boss-only, 4/200) | timeout only (94) | 8 / 200 | Average win turn 9.08; `noShipLost` kept in 85/106 wins; canonical runs losing at least one ship 97/200. Passive: average 1.78 of 3 ships lost, average passive fleet damage 75%, loss mix timeout 192 + sunk 8. Legacy: 71.5% / 8.0%, wipes 12/200, `noShipLost` in 128/143. The largest D1-A shift in the arc (−18.5 points) — see the D1-A deviations note below. |

**D1-A deviations (open knob, measured 2026-08-02, no values changed
here):** under the broadside-arc curve the canonical scripted strategies —
which advance and fire along the approach line, i.e. mostly bow-on —
lose accuracy by construction, so missions 04 (46.0%), 05 (43.5%) and 06
(53.0%) now sit below design target 1's 55% floor while mission 03
(80.0%) holds. Two readings are possible and deciding between them is a
design call, not a sweep artifact: (a) the canonical scripts understate
real play more than before, because a real player can now turn to
present a beam and claw the lost accuracy back — the very behavior D1-A
exists to teach — so the floor should be judged against beam-aware
scripts; or (b) the missions genuinely got harder and want a value
retune. Any retune is a new rollout slice with its own apply-time
re-derivation; nothing in this re-sweep changes an applied value.

## Mission 03 "Raking Shot" (`src/sim/mission03.ts`)

| Knob | Current | Proposed | Derivation / effect |
| --- | --- | --- | --- |
| `MISSION_03_TURN_LIMIT` | 12 (**applied**; was 10) | keep | The clock, not the enemy, is the dominant loss: 33% of canonical runs time out with the last enemy at ~36 average hull. Two more turns convert most near-misses (canonical win rate 67% → 81.5%) and give the meaner enemies (below) time to actually finish passive fleets: passive wipes go 0 → 71/200 (35.5%), and 199/200 passive runs lose at least one ship. |
| `MISSION_03_BONUS_TURN_TARGET` | 9 (**applied**; was 8) | keep | Under the applied values the canonical average win lands on turn ~10.0. A ≤8 target would be hit in only 8 of 163 winning sweeps (≈5%) — effectively unobtainable; ≤9 is hit in 62/163 (≈38%), matching the one-third stretch target. (The drafted proposal put the ≤8 count at 9 of 163 (≈6%); the applied slice's re-derivation measured 8 — win-turn histogram 8:8, 9:54, 10:52, 11:26, 12:23. The ≤9 figure reproduced exactly and the conclusion is unchanged.) |
| `MISSION_03_RAKE_HIT_TARGET` | 2 | keep | The rake bonus is the mission's teaching objective and already lands in canonical wins. |
| `MISSION_03_ENEMY_DAMAGE_SCALE` | 1.15 (**applied**; was 1.05) | keep | One knob deliberately scales BOTH enemy hull (frigate 180 → 207, sloop 120 → 138, decimal `floor(base×1.15)`) and enemy outgoing damage (engine `damageScale`). +10% each way makes the pincer dangerous without a knob split. Implementation note from the applied slice: `1.15` has no exact binary representation, so `Math.floor(180 * 1.15)` evaluates to **206** — the module rounds the product to whole units of 1e-6 before flooring (`scaleHull`) so the derived hull is the decimal 207 this table specifies, and the test pins are literal 207/138 rather than recomputed expressions. The sweep numbers in this section are unaffected: 206 and 207 produce identical win rates, loss mixes, bonus rates and fixture categories. Splitting hull from damage would add an objectives field — an API-contract and fingerprint-shape change — and is deliberately out of scope here. |
| `MISSION_03_DEFAULT_SEED` | 303 | keep | Route default only; not part of the fingerprint payload (it is the pin test's argument, not its content). |
| Wind (90° at 2–4), spawn positions | as shipped | keep | The cross-breeze and pincer geometry are the mission's identity; the sweeps show no need to touch them. |

Fingerprint ripple (**applied**): `turnLimit=12`, `bonusTurns=9`,
`enemyScale=1.15`, `hp207`, `hp138` in all three pins. Because the
canonical order arrays are sized by `MISSION_03_TURN_LIMIT`, the longer
clock changed both strategies' shape and their rng consumption, so every
seed-pinned fixture in `tests/mission03.test.ts` was re-searched from
scratch: win-with-both-bonuses stayed on seed 1 (now turn 9),
win-missing-turn-bonus moved 2 → 8 (turn 10 — seed 2 now wins inside the
≤9 bonus), timeout moved 21 → 39 (seed 21 now wins on turn 11), and
boarding-win moved 5 → 2 (seed 5 now times out); the boarding seed is
shared by the engine and route suites.

## Mission 04 "Boarding Party" (`src/sim/mission04.ts`)

| Knob | Current | Proposed | Derivation / effect |
| --- | --- | --- | --- |
| `MISSION_04_ENEMY_CREW_SCALE` | 0.8 (**applied**; was 0.9) | keep | Frigate crew `floor(60×0.8)` = 48 (from 54). Crew only enters boarding defense, so this speeds the intended win path without touching enemy gunnery: defender attrition per successful boarding is ≈8–12 crew (`floor(power/6 + rng·4)` at ~50 boarding power), so 48 crew breaks about one boarding-turn sooner per frigate — across two frigates that recovers the runs that currently time out at 90%-boarded. Canonical boarding win rate: 33.5% → 55%. |
| `MISSION_04_PLAYER_BOARDING_BONUS` | 0.15 (**applied**; was 0.1) | keep | At hull-to-hull range the success chance is already capped (60 + 50 power − 27 half-defense + 10 bonus = 93, clamped to 90), so the bonus's effect is at mid range: each 10 range units beyond 30 cost 3 chance points (1 through boarding power, 2 through the penalty term), so +5 bonus points buy back roughly 17 units of grapple envelope. Boarding is the mission's teaching mechanic; it should start paying earlier in the approach. |
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
| Enemy line spawn | flagship `(220, 0)`, escorts `(200, ±60)` (**applied**; was `(260, 0)` / `(240, ±60)`) | keep | Root cause of the flat threat: the AI holds fire until `preferredRange` (100 for the flagship's line-advance) while a player broadside from spawn still lands ~70% (the engine's range penalty is only −1 hit chance per 50 units). Opening 40 units closer, the line's guns bear one-to-two turns sooner: canonical win rate 44% → 53%, and passive fleets go from **106/200** runs losing a ship (**correction from the applied slice's re-derivation: this row drafted "one-third"; measured 106/200 = 53%**) to 197/200 (1.3 of 3 ships lost on average, and average passive fleet damage 33% → 59%). The escorts' station offsets (20 forward, ±60 lateral in the leader frame) are preserved exactly, so `mission05EnemyOrders` needs no change — verified at apply time: because the whole line translates by −40 on x, the escort-to-leader offsets are byte-identical and only the module's explanatory comment (which quoted the old coordinates) had to move. Probed a further 20 units closer (200/180): canonical drops to 42% — the fight then starts inside the rock choke band — so 220/200 is the chosen point; both figures reproduced exactly at apply time. The applied 53% sits just below design target 1's 55% floor, inside the sweep's ±3pp noise; accepted, since the scripted line never turns and real play clears the band. |
| `MISSION_05_FLAGSHIP_HP_SCALE` | 1.1 | keep | Probed at 1.0 with the closer line: 52% vs 53% win rate — inside noise. Keep the flagship tanky; minimal diff. (Reproduced exactly at apply time: 104/200 at 1.0 vs 106/200 at 1.1.) |
| `MISSION_05_TURN_LIMIT` / `MISSION_05_BONUS_TURN_TARGET` | 11 / 9 | keep | Applied canonical average win is turn **8.52**; the limit is not the binding constraint here. Finding from the apply-time re-derivation, unchanged by this slice: the ≤9 bonus is hit in **100 of 106 wins (94%)** — near-automatic, the same deviation from design target 3 that mission 06's row records, and equally pre-existing (83 of 88 wins, also 94%, before the closer line). Mission 05's real stretch is `sankFlagshipFirst`, which the canonical line-break strategy earns in every win it gets but escorts-first play forfeits. Tightening the turn target needs its own attainability probe and is deferred rather than guessed, exactly as for mission 06. |
| `MISSION_05_DEFAULT_SEED` | 505 | keep | Route default only. |
| Rock choke `(120, ±70) r35`, tailwind 0° at 4–6 | as shipped | keep | — |

Known residual (**applied slice, measured**): no passive mission 05 run is
fully wiped (wipes 0/200) and the loss mix stays timeout-only, so mission
05's closure of the "enemy never ends a fight" knob is the weakest of the
three — passive fleets now lose ships in 197/200 runs, but never all three.
**Correction to this paragraph's original claim:** the ceiling is not that
"this spec moves threat as far as mission values can". Position values *can*
produce wipes — the rejected 200/180 point measures 9 passive wipes and a
`sunk` entry in the loss mix — but it costs canonical win rate (42%, below
the old 44%). So the limit is a **trade-off against design target 1**, not an
absolute floor set by the engine's flat range falloff. Closing the wipe gap
without paying win rate does need an engine change (a `rangeFalloff`
modifier flag, see Constraints), which stays the named follow-up.

Fingerprint ripple (**applied**): the three enemy ship position fields in
all three pins (`enemy-flagship:...:220,0`, `enemy-escort-a:...:200,60`,
`enemy-escort-b:...:200,-60`). No hull literal moved — the flagship HP scale
is kept, so `hp198` and the escorts' `hp120` are byte-identical, as is the
enemy fleet total 438 in the PlayMode fake client. The kept turn limit leaves
`Mission05ResolveRequest.turns.maxItems` at 11 (now pinned against the
constant, completing the pattern across missions 03, 05 and 06) and the
`mission_05_obj_bonus_*` strings untouched.

**All four seed-pinned fixtures kept the seed and the category their test
asserts, and no fixture had to be re-searched** — but "nothing moved" would
overstate it, and local review caught this document doing exactly that. The
change is a uniform translation (the whole line shifts −40 on x), so relative
geometry, the escort station offsets and the strategies' target order are all
preserved, and the order arrays are not reshaped the way a turn-limit change
reshapes them. Outcomes still move, though: closer spawns change hit chances
from turn 1, so the draw stream diverges in effect even at identical array
length. Measured, old → applied:

- both-bonus **seed 1** (line-break): turn 8, flagship first, zero hull damage
  — **byte-identical** at both spawn distances;
- slow-flagship **seed 1**: turn 10, flagship first, outside the ≤9 bonus,
  zero hull damage — also identical;
- timeout **seed 9** (slow-flagship): still a turn-11 timeout, but player hull
  damage **30 → 107** — same category, different fight (now pinned: it is the
  strongest canonical-strategy geometry guard in the suite);
- escorts-first **seed 14**: win with escort-a sunk first either way, but
  turn **9 → 10**, so `withinTurnTarget` flips **true → false**, and the fleet
  now pays a sloop (`[120, 24, 120]` → `[120, 0, 120]`). The pre-slice test
  pinned neither the turn count nor that flag, which is why the fixture still
  "held" — the run underneath it changed. This slice pins both.

So the retune is visible in a canonical fixture after all, in the one case
where the line gets to shoot for an extra turn. The line-break fixtures are
the geometry-insensitive ones (zero hull damage at either distance), so the
slice also adds a **passive-baseline fixture** — seed 1, all ships passing:
297 hull damage (83% of the fleet), 63 hp left, a player sloop as the first
sink, the enemy line untouched, 19 blocked moves — so the threat change this
slice exists to make is pinned by behaviour rather than only by a position
string. That fixture fails under the old geometry (which gave 141/0.39/219 and
16 blocked), so it is a real guard; note seed 1's 83% fleet damage is a harsh
sample against the 59% sweep mean, chosen because it is the low seed, not the
typical one. Four masks became exact pins in the process (`turnCount` ≤ 9 → 8;
a `chokeBlockedMoves >= 0` + `typeof` tautology → exact 2 and 19;
`firstSinkTarget not.toBe(flagship)` → the named escort; and a route-level
`toHaveProperty('chokeBlockedMoves')` → exact 2). One caution for future
slices: `chokeBlockedMoves` counts **both** sides' blocked moves, so it is a
navigation-pressure metric, not a player-difficulty one — the passive 16 → 19
is +4 enemy against −1 player.

## Mission 06 "Dreadnought Siege" (`src/sim/mission06.ts`)

| Knob | Current | Proposed | Derivation / effect |
| --- | --- | --- | --- |
| `MISSION_06_BOSS_DAMAGE_SCALE` | 1.5 (**applied**; was 1.1) | keep | A boss that never ended a fight across the sweeps is a pushover on a timer: passive fleets take 65% damage but are never wiped in 14 turns. At 1.5 the canonical siege is untouched (71.5% vs 72.0% — flat at this sample size, because a competent siege kills the boss before its output compounds) while sloppy play finally pays: passive wipes 0 → 12/200, passive ships lost average 1.89 of 3 (average fleet damage 65% → 78%), and canonical runs losing at least one ship rise to 58/200 — making `noShipLost` a real stake (still kept in 128 of 143 wins). Two corrections from the applied slice's re-derivation, neither changing the conclusion. (1) The pre-slice count of canonical runs losing at least one ship is **35/200**, not the ~17 this row drafted; the post-slice 58/200 reproduced exactly, so the rise is 35 → 58 (+66%) rather than the drafted tripling. (2) The 58/200 is a **two-knob** figure, credited here to 1.5 alone: an attribution sweep over the 2×2 of (1.1\|1.5) × (10\|25) measures canonical runs losing a ship at 35 (1.1/10) → 45 (1.5/10) → 58 (1.5/25), so the damage scale contributes 10 of the 23 and the enrage bonus the other 13 (average canonical fleet damage 0.141 → 0.172 → 0.191). The **passive** numbers in this row are pure boss damage and correctly attributed — passive fleets never push the boss below the enrage threshold, so enrage 10 and 25 produce byte-identical passive results (12 wipes, 1.89 ships lost either way). |
| `MISSION_06_ENRAGE_ACCURACY_BONUS` | 25 (**applied**; was 10) | keep | Enrage opens below 30% of 468 hull (< ~140), which the canonical siege burns through in its final two-to-three turns — a +10 accuracy swing changes about one shot before the boss dies. +25 makes the last stand visibly land. Not part of the fingerprint or objectives payload, and `Mission06Scenario.cs` does not carry the constant (verified again at apply time) — pinned only by the `mission06Modifiers` vitest, so this knob has no Unity ripple at all. Measured support from the applied slice's attribution sweep (this row drafted none): holding the damage scale at 1.5, raising the bonus 10 → 25 moves canonical runs losing a ship 45 → 58 of 200 and average canonical fleet damage 0.172 → 0.191, and it is this knob — not the damage scale — that flips the slow-win fixture seed to a ship-losing win. The last stand does land. |
| `MISSION_06_BOSS_HP_SCALE` | 1.3 | keep | 468 hull already sets the right siege length (canonical wins average turn ~7.9). |
| `MISSION_06_ENRAGE_HULL_FRACTION` | 0.3 | keep | Phase rhythm is fine; only the enrage's bite changes. |
| `MISSION_06_REINFORCEMENT_TURN` / `MISSION_06_REINFORCEMENT_HP_SCALE` | 5 / 0.9 | keep | The swat-mid vs boss-only gap (72% vs 8%) shows the reinforcement already forces the intended target-switch decision. |
| `MISSION_06_TURN_LIMIT` / `MISSION_06_BONUS_TURN_TARGET` | 14 / 12 | keep | Accepted deviation from target 3: with canonical wins averaging turn ~7.9 the ≤12 turn bonus is near-automatic. The boss mission's real stretch is `noShipLost`; tightening the turn target needs its own attainability probe and is deferred rather than guessed here. |
| `MISSION_06_DEFAULT_SEED` | 606 | keep | Route default only. |
| Shifting wind (0°→90° at turn 7), debris field | as shipped | keep | — |

Fingerprint ripple (**applied**): `bossDmg=1.5` in all three pins (the
enrage accuracy value is not fingerprinted). No hull literal moved —
`MISSION_06_BOSS_HP_SCALE` is kept, so the boss's 468 hull is byte-identical
in every pin. The PlayMode fake client's **damage** values did move, though,
and no hull scale was involved — see the first "further surfaces" bullet
above. The
kept turn limit also leaves `Mission06ResolveRequest.turns.maxItems` at 14
(now pinned against the constant in `tests/mission06.test.ts`, following the
mission 03 precedent) and the `mission_06_obj_bonus_*` strings untouched.

Because the turn limit is unchanged the canonical order arrays keep their
shape and rng consumption, so three of the four seed-pinned fixtures held
their category outright: both-bonus **seed 1** (still turn 9, phases
1→2 at turn 5, enrage turn 6, boss at 0 hull), ship-lost **seed 106**
(turn 12), timeout **seed 1 / boss-only** (14 turns, 101 reinforcement
damage). The exception is the slow-win fixture: under the retune a
**no-ship-lost** win that misses the ≤12 bonus no longer exists on the
swat-late line at all (0 of 3000 searched seeds; 2 of 3000 on swat-mid).
The attribution sweep places that on the enrage bonus rather than the
damage scale — seed 68 already flips at 1.1/25. The fixture therefore keeps
**seed 68** and its strategy, preserving the swat-late line's only
coverage, and now pins the surviving late-swat failure mode: a turn-14 win
that pays a ship (`noShipLost` false, `withinTurnTarget` false). That
extinction is itself the retune landing.

To keep the bonus-flag matrix complete rather than merely as broad as
before (local review finding), the slice adds a fifth fixture for the
combination the swat-late line lost — **seed 2706** on swat-mid, a turn-13
win that stays clean but overruns the ≤12 target — so all four
`noShipLost` × `withinTurnTarget` combinations are pinned. Seed 2706 is a
deliberately rare configuration (2 of 3000) and is labelled as such in the
test; a later slice that extinguishes it should re-search rather than
relax it. All five fixtures pin exact turn counts, and the two
telemetry-bearing fixtures (seed 1 both-bonus, seed 1 boss-only) pin exact
telemetry and damage values rather than `<=` / `> 0` bounds.

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
- **Win proofs prove an achievable line, not that it was played.**
  `/missions/:code/start` does not issue a seed — it accepts one from
  the caller (`seed` defaulted to `MISSION_0X_DEFAULT_SEED` under a
  `.strict()` body, no `playerId`) and echoes it back; all ten Unity
  flows assert that echo (`seed_mismatch`). Nothing binds the `seed` in
  a `/complete` win proof to any server-dealt run, so a caller can
  search `(seed, turns)` offline and submit a favourable RNG line. The
  re-simulation still enforces real mission rules — valid player ships,
  valid targets, `allowBoarding`, schema turn caps, and owned upgrade
  tiers (`upgrade_tiers_exceed_owned`) — so a forged proof must be a
  line the player *could* have played, and first-completion-only grants
  cap the take at one campaign's reward table per player rather than
  anything farmable. **Decision (2026-07-27): accepted, unbound.** There
  is no monetization and no PvP stake on mission rewards, and the
  canonical scripted strategy wins a majority of seeds throughout, so
  the offline search is one or two tries. **The retunes moved that
  payoff slightly the other way**, which corrects the premise this was
  raised under (that difficulty retunes raise the value of
  seed-shopping): expected attempts to find a winning line go as `1/p`,
  and every applied slice raised the canonical win rate or left it flat
  — 03 `67.0→81.5%`, 04 `33.5→55.0%`, 05 `44.0→53.0%`, 06
  `72.0→71.5%` — so attacker cost *fell*, from an arc worst case of
  ~3.0 expected attempts (mission 04 baseline) to ~1.8, with the
  current worst mission 05's ~1.9 against mission 03's ~1.2. Difficulty
  is therefore a real but very weak lever in either direction, and
  nothing in this arc's range is an attacker-facing difference. Run
  that arithmetic on a future retune rather than assuming the sign.
  Binding a server-issued seed would mean issuing and persisting one
  across all ten `/start` routes — which carry no player identity
  today — and breaking the echo assertion in all ten client
  flows: a route + schema + Unity change (PlayMode evidence, migration,
  Database review) whose cost the exposure does not justify, and which
  still would not prove play while `/start` re-rolls are free. Revisit
  if mission rewards gain monetary or PvP value, if grants become
  repeatable rather than first-completion, or if a real player base
  exists at rollout. The remedy then is a **live server-held run**: the
  server generates and retains the seed, keeps the run state, and
  advances it one turn at a time from orders submitted against RNG it
  has not yet revealed, so there is no offline oracle to search. Note
  what does *not* qualify — "re-simulate it server-side" is already
  what `/complete` does (`proofConfig.run(seed, turns, upgrades)`) and
  is the very thing being bypassed, so any future slice must be
  specified as server-held state and server-controlled RNG rather than
  server-side replay of a caller-supplied `(seed, turns)` pair.
  Distinct from the unsignaled compatibility window under Rollout, which
  is about *which* constants a run was authored against.
- **Economy income math assumes `inventory_grant_api` stays disabled.**
  The grant route can mint any item for the calling player once that
  flag is enabled; the campaign-total "hard cap" arithmetic above holds
  only under the shipped seeded-disabled state. Enabling the flag is a
  separate protected decision, not a tuning knob.
- **Classifier alignment (gap closed):** the harness now carries an
  `economy` protected area (`^src/economy/` by path; word-bounded
  `economy` / `reward(s)` / `upgrade cost(s)` by intent), so
  `route-task` routes the economy value slices Class C mechanically —
  matching the `AGENTS.md` prose and the Class C discipline the applied
  timber slice already followed by convention. (Mission difficulty
  slices 2–5 are outside `src/economy/` and still route B on path;
  their protection remains the fingerprint-pin/review discipline
  above.) Deliberately outside the economy path set: the reward-flow
  routes (`src/routes/missions.ts` grant flow,
  `src/routes/inventory.ts` minting, `src/routes/upgrades.ts`
  purchases) and the upgrade power-curve constants in
  `src/sim/upgradeEffects.ts` — each guarded by its own review
  surface; widening is a separate policy decision.
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
   Named scope limit (Codex P1 on the applied PR): the closure holds
   only for campaigns completed **after** this slice deploys. A player
   who first-completed missions 05/07/10 before it stays capped at 475
   lifetime timber (grants are first-completion-only; repeat
   completions grant `[]` by design) and still cannot finish the tree.
   **Decision (2026-07-23): accepted without backfill.** There is no
   production player base to strand pre-launch, so no compensation runs.
   Revisit only if completed campaigns exist at rollout time — the
   remedy would then be an idempotent backfill grant (+30/+40/+40 for
   prior 05/07/10 completions), which is persisted-player data mutation
   and therefore its own explicitly-authorized protected slice, never
   part of a value retune.
2. **Mission 04** (**applied**) — two constants, biggest player-facing
   pain. The apply-time re-derivation reproduced the proposal's numbers
   exactly (110/200 canonical wins, 86/200 passive wipes); the only
   fixture that moved was the sunk-loss seed (3 → 41; the other pinned
   seeds still land in their categories under the new distribution).
3. **Mission 03** (**applied**) — three constants including a turn-limit
   change. The apply-time re-derivation reproduced every proposal number
   that gates a design target (163/200 canonical wins, average win turn
   10.0, 62/163 turn bonuses, 71/200 passive wipes, 199/200 passive runs
   losing a ship) and corrected one that gates nothing (the ≤8 count was
   8 of 163, not 9 — see the bonus-target row);
   three of the four pinned fixtures moved because the turn limit resizes
   the canonical order arrays (see the ripple note above).
4. **Mission 06** (**applied**) — two constants, one outside the
   fingerprint. The apply-time re-derivation reproduced every proposal
   number that gates a design target (71.5% canonical wins, 12/200
   passive wipes, 1.89 average passive ships lost, 58/200 canonical runs
   losing a ship, `noShipLost` kept in 128/143 wins) and corrected the
   baseline it was measured against (35/200, not ~17); one of the four
   pinned fixtures moved a category rather than a seed (see the ripple
   note above).
5. **Mission 05** (**applied**) — position changes. The apply-time
   re-derivation reproduced every proposal number that gates a design
   target (106/200 canonical wins, average win turn 8.52, 197/200 passive
   runs losing a ship at 1.3 of 3) plus both rejected-alternative probes
   (42% at 200/180, 52% at flagship HP 1.0), and corrected the baseline it
   was measured against (106/200 passive runs losing a ship, not
   "one-third"). No pinned seed had to be re-searched — a uniform −40 x
   translation preserves relative geometry and the strategies' target
   order, so this item's geometry-sensitivity warning did not bite the way
   a turn-limit change does. It is not true that nothing moved, though
   (local review corrected an earlier draft of this line): escorts-first
   seed 14 slipped turn 9 → 10, forfeiting `withinTurnTarget` and one
   sloop, and the seed-9 timeout's player damage went 30 → 107. Both are
   now pinned. The slice also added a passive-baseline fixture because the
   line-break fixtures proved geometry-insensitive, and corrected this
   section's claim that mission values had reached their threat ceiling
   (see the Known residual note).

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
