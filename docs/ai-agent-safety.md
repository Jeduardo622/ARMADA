# AI/Agent Safety & Autonomy Guardrails

- Scope: agents may create/edit docs/config/flags/PRs; production rollouts require human approval (A in RACI).
- Logging: all agent actions logged with rationale; flags/config changes require ticket link.
- Constraints: no direct secret handling; no writing to production DBs; no disabling security controls.
- Reviews: risky changes (economy, monetization, security) need dual sign-off (Product + Security/Backend).
- Testing: agents must attach test evidence (unit/replay/contract) to changes.
- Rollback: agents must supply rollback steps for each change; prefer flags over hard deploys.
- Ethics/Privacy: no new PII collection without explicit approval and ADR.

