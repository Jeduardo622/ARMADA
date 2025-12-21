# Playbooks (Feature → Release)

## Feature Lifecycle
1) Proposal (Design/Product): problem, goals, KPIs, constraints.
2) Design review: rules/tuning, UX mocks, telemetry asks.
3) Contract/API review (Client/Backend): schema diff, example payloads, flags.
4) Implementation: behind flag; unit + sim tests; contract tests.
5) QA: test cases, replay/determinism checks, device perf; bug loop.
6) Rollout: gradual via flags; monitor dashboards; rollback ready.
7) Postmortem: metrics vs goals; lessons captured in ADR/playbook.

## Rollback Checklist
- Kill flag or revert config; verify metrics stabilize.
- Announce in release notes/internal channel; open follow-up ticket.

## PR/Change Requirements
- Link to issue/ADR; chain-of-thought notes on risks/mitigations.
- Tests: unit + relevant sim/replay tests; schema/contract checks.
- For balance changes: include expected telemetry signals and guardrails.

