# Config Source of Truth

- Formats: JSON configs for balance tables, missions, flags; signed and versioned.
- Ownership: Backend/Live Ops maintain; Client consumes via cached endpoints with signature validation and cache-busting.
- Versioning: semantic; include config_version in telemetry; keep N-1 compatibility where possible.
- Deployment: stage → prod with audit logs; flags/configs gated with approval matrix.
- Rollback: retain last-good version; flip via config service; document in ops channel.

