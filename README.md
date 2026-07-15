# Armada Docs & Local Stack

## Quick Start
- Install tooling: Node 20 + npm; TypeScript (`npm i -g typescript ts-node`); psql/redis-cli; MinIO CLI (`mc`); jq.
- Copy `.env.example` to `.env`.
- Start local services: `docker-compose up -d` (Postgres, Redis, MinIO, Unleash).
- MinIO console: http://localhost:9100 (minio-access/minio-secret). Unleash: http://localhost:4242 (admin/admin).
- (Optional) Init MinIO bucket: `mc alias set local http://localhost:9000 minio-access minio-secret` then `mc mb local/armada-dev`.

## Backend (Armada MVP services)
- Install deps: `npm install`
- Run migrations: `npm run migrate` (uses Prisma migrations under `prisma/migrations`)
- Seed baseline data/flags/missions: `npm run seed`
- Dev server: `npm run dev` (Fastify on `PORT`, default 4500). All non-health routes now require a Bearer JWT from `/auth/guest`; player-specific routes enforce ownership.
- Tests: `npm test` (Vitest, uses mocked deps)
- Lint: `npm run lint`

### API surface (v0 stubs)
- `GET /healthz`, `GET /readyz`
- `POST /auth/guest` → creates/fetches player + returns signed placeholder JWT
- `POST /players` / `GET /players/:id`
- `GET /inventory/:playerId`, `POST /inventory/:playerId/grant` (server-authoritative grants)
- `GET /missions` (flag: `missions_api`), `POST /missions/:code/complete`
- `POST /sim/preview` deterministic hash stub (flag: `sim_stub`)
- `POST /telemetry/ingest` schema-validated, stored only (flag: `telemetry_ingest`)
- `GET /config/:namespace[?version=]` serves versioned snapshots (flag: `config_api`)

Flags are backed by Unleash; seeds set them on by default. Storage is S3-compatible via MinIO (`ASSET_BUCKET`).

## Docs Map
- Exec/specs: `docs/exec-spec.md`, `docs/agents.md`, `docs/adr/`
- Ops/CI: `docs/ops/`, `docs/ci/`, `docker-compose.yml`, `.env.example`
- Content: `docs/content/` (missions, AI profiles, balance tables)

## Local Claude Code Harness

Install a current Claude Code release and start `claude` from the repository
root with Node 20 or newer available. `CLAUDE.md` imports the canonical
`AGENTS.md`, and project hooks automatically classify prompts and deny Class D
Bash operations. Normal Claude Code permissions continue to apply.

- `/harness-help` explains the engineering lifecycle and navigation.
- `/route-task` performs path-aware classification before edits or scope growth.
- `/verify-change` runs focused checks and `npm run verify:local` before handoff.
- `tester`, `reviewer`, `ui-hardener`, `test-isolation`, and
  `security-reviewer` provide bounded specialist workflows.

This integration is local only. It does not add Claude CI, shadow evaluations,
Anthropic credentials, or model configuration.

## Next Steps
- Configure git creds to push.
- Enable CI checks when code lands (see `.github/workflows/ci.yml`).
- Use PR template for changes.
# ARMADA
