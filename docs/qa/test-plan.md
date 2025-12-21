# Test Plan (MVP)

## Scope
- Deterministic combat sim (WEGO flow, wind, broadsides, raking, boarding).
- Progression (ships/captains/components), economy integrity.
- Missions (5–8), boss encounter, cosmetics pipeline.
- Telemetry correctness, flags/config behavior, perf/crash.

## Strategies
- Unit: sim math, damage calc, wind/movement, ability effects.
- Integration: full turn resolution, AI behaviors, mission scripts.
- Replay/determinism: record/replay parity checks in CI.
- Contract tests: client/backend payloads, schema validation.
- Device matrix: target mid-tier Android/iOS; perf budgets enforced.
- Regression: per-milestone suites; smoke on every PR to main.

## Gates (examples)
- G1: core sim passing unit/replay suite; crash <1% in smoke.
- G2: full loop playable; mission goals verified; perf within budget.
- G3: stability/perf threshold met; telemetry validated; flags tested.

## Reporting
- Defects with repro steps + expected vs actual + logs/replays.
- Daily status: pass/fail counts, top risks, blockers.

