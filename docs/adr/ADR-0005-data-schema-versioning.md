# ADR-0005: Data Schema Versioning & Migrations
- Date: 2025-12-21
- Status: Accepted
- Context: Need safe evolution of DB schema and API contracts without breaking clients; support rollback and phased rollouts.
- Options: Hard breaks with major versions; backward-compatible migrations with deprecation windows; dual-write/dual-read for breaking changes.
- Decision: Prefer backward-compatible migrations with deprecation windows; for breaking changes, use dual-write/dual-read during transition. APIs versioned; DB migrations reversible when feasible.
- Consequences: Slightly more complexity during transitions; safer rollouts; ability to rollback with minimal downtime. Requires disciplined migration scripts, feature flags, and contract tests.

## Rules
- Additive-first: add nullable/optional fields before making them required; avoid destructive changes.
- Deprecation: mark old fields/paths; announce removal window; clean up after window expires.
- Dual-write/dual-read: use for format changes; remove legacy once migration validated.
- Migrations: idempotent where possible; include down scripts when safe; run in stage before prod; monitor errors.
- API: version via path or header; avoid silent behavior changes; contract tests enforce compatibility.
- Data fixes: tracked with tickets; include verification queries; rollback steps documented.

