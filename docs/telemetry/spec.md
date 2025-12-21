# Telemetry Spec (MVP)

## Principles
- Minimal PII; consistent IDs; schema versioned.
- Validate client events; drop malformed; alert on drift.
- Tie events to experiments/flags for readouts.

## Core Events (fields abbreviated)
- session_start/end: player_id, device, app_version, build_hash.
- mission_start/end: mission_id, result, stars, duration, fail_reason, captain_ids, ship_ids, config_version.
- combat_tick (sampled): turn_id, actions_summary, damage_by_type, wind_angle, rng_seed.
- economy_tx: tx_id, type, currency, amount, balance_after, source/sink.
- store_view/ click/ purchase_attempt/ purchase_result: sku_id, price, currency, success/fail_reason.
- perf: fps_avg/p5/p95, memory, thermal, device_tier.
- error/crash: stack, build_hash, device, breadcrumbs.

## Validation & QA
- Schema JSON + contract tests; CI blocks on breaking changes.
- Canary alerting: event drop, spike in fail_reason, crash rate.
- Dashboards: funnel (sessions → mission start → mission success), retention, economy sinks/sources, perf, store CTR → purchase.

