# Armada Docs & Local Stack

## Quick Start
- Install tooling: Node 20 + npm; TypeScript (`npm i -g typescript ts-node`); psql/redis-cli; MinIO CLI (`mc`); jq.
- Copy `.env.example` to `.env`.
- Start local services: `docker-compose up -d` (Postgres, Redis, MinIO, Unleash).
- MinIO console: http://localhost:9100 (minio-access/minio-secret). Unleash: http://localhost:4242 (admin/admin).
- (Optional) Init MinIO bucket: `mc alias set local http://localhost:9000 minio-access minio-secret` then `mc mb local/armada-dev`.

## Docs Map
- Exec/specs: `docs/exec-spec.md`, `docs/agents.md`, `docs/adr/`
- Ops/CI: `docs/ops/`, `docs/ci/`, `docker-compose.yml`, `.env.example`
- Content: `docs/content/` (missions, AI profiles, balance tables)

## Next Steps
- Configure git creds to push.
- Enable CI checks when code lands (see `.github/workflows/ci.yml`).
- Use PR template for changes.
# ARMADA
