# API Contracts (Guidelines)

- Versioned schemas; backward-compatible changes preferred.
- Use OpenAPI/JSON schemas; share examples for client stubs.
- Core surfaces: auth, profiles, inventory/economy, missions, telemetry ingest, flags/config.
- Validation: server-side schema validation; reject unknown/extra fields in strict modes.
- Deprecation: mark old fields; sunset plan with dates; dual-write/dual-read when needed.
- Testing: contract tests in CI; mock servers for client integration.

