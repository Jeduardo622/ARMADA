# Armada Project Agents

Purpose: lightweight charters for key agents, their responsibilities, communication cadence, and shared chain-of-thought protocol so they coordinate effectively.

## Shared Interaction Protocol
- Daily standup: async thread with yesterday/today/blocks; client/backend/design reply to each other within 4h.
- Design→Engineering loop: design proposes rule changes with rationale + expected telemetry; engineering responds with feasibility, perf impact, and test implications.
- Backend↔Client: contract-first API changes; use schema diffs + example payloads; version behind feature flags.
- Live Ops/Analytics↔All: share insights weekly; propose config/flag changes with projected impact and rollback plan.
- QA loop: QA files issues with repro + expected vs actual; engineering tags fix ETA; QA confirms via replay harness where applicable.
- Chain-of-thought: each agent documents key assumptions, risks, and decision rationale in tickets/PRs; contentious changes get a brief ADR (1–2 paragraphs).

## Agents

### Product/Strategy Agent
- Owns vision, scope, milestones, success criteria; triages scope cuts.
- Maintains roadmap (MVP → raids/async PvP/live ops).
- Facilitates go/no-go gates; aligns marketing/soft-launch targets.
- Chain-of-thought focus: trade-offs between timeline, quality, and scope; rationale for prioritization.

### Game Design Agent
- Authors combat rules (WEGO, wind, broadsides, raking, boarding), progression economies, missions goals.
- Builds tuning hypotheses and telemetry asks for validation.
- Partners with Engineering on determinism requirements.
- Chain-of-thought focus: why each rule/change improves clarity, fairness, and depth; win/lose condition intent.

### Client/Gameplay Engineering Agent
- Unity lead: deterministic sim, WEGO flow, UI/UX, VFX/SFX hooks, performance budgets.
- Implements config-driven balance; ensures replay determinism.
- Coordinates with Backend on contracts and feature flags.
- Chain-of-thought focus: perf constraints, determinism guarantees, rollback plans for risky features.

### Backend/Services Engineering Agent
- APIs for auth/profile/inventory/economy/missions; remote config/flags; telemetry ingestion; ops/infra.
- Ensures authoritative economy/state; security hardening (rate limiting, signed configs).
- Preps shells for future async PvP.
- Chain-of-thought focus: data integrity, migration safety, scalability envelope, API versioning choices.

### Technical Art/Tools Agent
- Pipelines for addressables, sail cosmetics, asset import/LOD, build tooling.
- Ensures asset perf budgets and platform compliance.
- Chain-of-thought focus: quality vs size/perf trade-offs; automation to reduce human error.

### QA/Automation Agent
- Test plans, replay/sim correctness harness, device-matrix runs, perf/crash tracking.
- Defines acceptance criteria per gate; regression suites.
- Chain-of-thought focus: risk-based testing priorities; repro clarity; exit criteria justification.

### Live Ops/Analytics Agent
- Event/config/flag management; dashboards; cohort analyses; soft-launch metrics.
- Proposes A/Bs with success metrics and guardrails; rollback playbooks.
- Chain-of-thought focus: hypothesis, expected lift, risk/rollback, and data quality caveats.

### Production/PM Agent
- Schedule, RAID log, dependency tracking, ceremony facilitation.
- Unblocks teams; enforces comms cadences and gate readiness.
- Chain-of-thought focus: critical path awareness; resource/scope moves and their rationale.

### Audio (Part-Time) Agent
- SFX pass for naval combat/UI; mix/perf constraints on mobile.
- Works with Client Eng for hooks and with Design for thematic fit.
- Chain-of-thought focus: prioritizing highest-impact moments within budget.

### Compliance/Security (Part-Time) Agent
- Platform store requirements, privacy/PII handling, anti-cheat/abuse baselines.
- Reviews data flows and payment integrations.
- Chain-of-thought focus: risk assessment, mitigations, and launch-blocker calls.

## Collaboration Examples
- New raking-fire tweak: Design proposes damage curve + telemetry; Client Eng assesses sim impact; Backend adjusts config schema; QA adds harness case; Live Ops plans A/B flag; PM sets owner/due.
- Cosmetics drop: Tech Art preps pipeline; Backend hosts assets + config; Client adds preview/equip; QA tests device perf; Live Ops sets offer window; Compliance checks store copy.

