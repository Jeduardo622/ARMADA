# Risk & Compliance

- RAID log: track risks, assumptions, issues, dependencies with owners and due dates.
- Seed items:
  - Risk: Determinism regressions → Owner: Eng Lead; Mitigation: replay CI + fixed-step guidelines; Due: ongoing.
  - Risk: Device perf on low-tier Android → Owner: Client Eng; Mitigation: weekly perf runs, LOD budgets; Due: G2.
  - Risk: Economy exploits → Owner: Backend; Mitigation: server validation, signed configs; Due: G2.
  - Assumption: Soft-launch region allows feature-flag store tests → Owner: Product; Verify by: G2.
  - Issue: None logged yet.
  - Dependency: Flag service availability → Owner: Live Ops; Due: G2.
- Compliance: store guidelines (Apple/Google), age ratings, privacy policy alignment.
- Data: PII inventory, retention, access controls; review changes that touch data flows.
- Launch blockers: unresolved P1 risks, failing gates, non-compliant store assets, unverified payments.
- Escalation: PM/Compliance owner decides block/ship with sign-off from Product + Security.

