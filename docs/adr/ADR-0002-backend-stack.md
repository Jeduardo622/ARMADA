# ADR-0002: Backend Stack
- Date: 2025-12-21
- Status: Accepted
- Context: Need fast iteration for MVP, simple services (auth, profiles, inventory/economy, missions, flags/config, telemetry ingest).
- Options: Node/TypeScript; Go; Java/Kotlin; .NET.
- Decision: Node/TypeScript services behind API gateway; Postgres primary DB; Redis cache; object storage + CDN for assets; feature-flag/remote-config service.
- Consequences: Fast dev, rich ecosystem; must manage TypeScript discipline, runtime monitoring; plan for Go carve-outs later if specific services need more perf.

