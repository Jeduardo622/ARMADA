# Release & Gate Checklists

## Gates (examples)
- G1 (Foundations): sim skeleton, build/CI green, crash <1% smoke.
- G2 (Playable Loop): missions 1–6, progression loop, telemetry validated, perf within budget.
- G3 (Pre-Soft Launch): stability/perf thresholds, store/flags tested, rollback ready.
- Launch: soft-launch region approved, store builds submitted, dashboards live, support routing set.

## Pre-Release Checklist
- Tests: unit/integration/replay passed; device-farm smoke.
- Telemetry: schemas validated; dashboards updated.
- Flags/config: defaults verified; rollback path documented.
- Store: metadata, screenshots, age ratings, privacy policy.
- Security: secrets scan clean; PII review done.
- Notes: release notes, known issues, owner on-call.

## Post-Release
- Monitor: crash/perf, funnel, economy anomalies.
- Rapid response: flag rollback or hotfix if thresholds breached.
- Postmortem: within 72h for major issues; record in playbooks/ADR if needed.

