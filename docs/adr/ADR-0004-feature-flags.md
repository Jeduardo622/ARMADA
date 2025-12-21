# ADR-0004: Feature Flags & Remote Config
- Date: 2025-12-21
- Status: Accepted
- Context: Need safe rollout/rollback for features, balance tweaks, and live-ops without client redeploys.
- Options: Ship-only toggles; third-party flag service; self-hosted config.
- Decision: Use remote config/feature-flag service with audit logging; all new features behind flags; staged rollouts; configs versioned and signed.
- Consequences: Faster iteration and rollback; operational overhead for governance; requires client support for signed/validated configs and cache busting.

