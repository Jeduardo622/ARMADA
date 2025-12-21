# Live Ops & Flags Handbook

## Flag Governance
- All new features behind flags; default off until QA passes.
- Changes require: owner, intent, rollout % plan, rollback plan, monitoring links.
- Audit log kept for every flag/config change.
- Approval matrix:
  - Backend/Config changes: Live Ops (A), Backend (R), QA (C), PM (I).
  - Gameplay/balance flags: Design (A), Client (R), QA (C), Live Ops (C), PM (I).
  - Monetization/store flags: Product/Monetization (A), Backend (R), QA (C), Compliance (C), PM (I).
  - Emergency rollback: any on-call Eng (R) with immediate notification to PM/Product; follow with postmortem.

## Experiments
- Hypothesis, success metric, guardrails, sample size, duration.
- Predefined stop/rollback criteria; postmortem within 48h of end.

## Content Drops (e.g., cosmetics)
- Checklist: assets uploaded/CDN cached; configs validated; store copy/compliance checked; perf sanity on target devices.
- Staged rollout: internal → stage region → broader.

## Soft Launch Ops
- Cohort dashboards: retention, mission funnels, economy sinks/sources, crash/perf.
- Weekly review: keep/kill/iterate decisions; backlog updates.

## Rollback
- Flags/config revert first; client hotfix only if required.
- Announce internally; track impact in dashboards.

## Incident/On-Call (Live Ops)
- On-call: rotate weekly; contact in ops channel; escalation to Product/PM for player-facing impacts.
- Incident template: summary, impact, timeline, actions, owners, rollback, follow-up tickets.

