# Telemetry Dashboards & Alerts (MVP)

- Crash/Perf: crash rate (<1% target), ANR, FPS p50/p95, memory; alerts on thresholds.
- Mission funnel: session → mission_start → mission_end (success/fail reasons); turn count distribution; rake/boarding usage.
- Economy: sources/sinks by currency; upgrade costs vs earnings; suspicious spikes.
- Store: impressions → clicks → purchase attempts → success; fail reasons.
- Flags/Experiments: rollout %, key metrics vs control, guardrails (crash/perf, funnel drop).
- Cohorts: retention D1/D7; region/device segmentation.

Alerting:
- Crash rate spike, perf degradation on any matrix device, event drop/drift, payment failures spike.

