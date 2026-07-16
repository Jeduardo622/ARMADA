# Armada

Backend services, Unity client source, and documentation for the Armada MVP,
plus a repository-native engineering harness that governs how coding agents
(Codex and Claude Code) deliver software here.

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
- Dev server: `npm run dev` (Fastify on `PORT`, default 4500). All non-health routes require a Bearer JWT from `/auth/guest`; player-specific routes enforce ownership.
- Tests: `npm test` (Vitest, uses mocked deps)
- Lint: `npm run lint`

### API surface (v0)

- `GET /healthz`, `GET /readyz`
- `POST /auth/guest` → creates/fetches player + returns signed placeholder JWT
- `POST /players` / `GET /players/:id`
- `GET /inventory/:playerId`, `POST /inventory/:playerId/grant` (server-authoritative grants)
- `GET /missions` (flag: `missions_api`), `POST /missions/:code/complete`
- `POST /sim/preview` deterministic hash stub (flag: `sim_stub`)
- `POST /telemetry/ingest` schema-validated, stored only (flag: `telemetry_ingest`)
- `GET /config/:namespace[?version=]` serves versioned snapshots (flag: `config_api`)

Flags are backed by Unleash; seeds set them on by default. Storage is S3-compatible via MinIO (`ASSET_BUCKET`).

## Engineering Harness And Agents

The repository governs agent-driven software delivery mechanically. Rules live
in instructions; scripts decide compliance.

- `AGENTS.md` is the canonical agent guide (lifecycle, task classes A-D,
  authority, verification, completion report). Nested `AGENTS.md` files add
  subtree rules for `src/`, `unity/`, `prisma/`, `.github/`, and `tests/`.
- `CLAUDE.md` is the Claude Code entry point; `.claude/skills/` mirrors the
  canonical skills in `.codex/skills/`; `.mcp.json` registers the project
  Unity MCP server (Codex uses `.codex/config.toml` for the same server).
- Classify any task: `node scripts/harness/route-task.mjs --description "<task>" --path <repo/path> --json`
- Full verification (same contract locally and in CI): `npm run verify:local`
  writes `reports/harness/latest.json` and never reports an unexecuted check
  as passed.
- Manual shadow evaluations benchmark agent policy compliance against a
  committed public corpus: `.github/workflows/shadow-evals.yml`
  (`workflow_dispatch`, provider `claude` (default) or `codex`; repository
  secrets `ANTHROPIC_API_KEY` / `OPENAI_API_KEY` gated by the `shadow-evals`
  environment).

Humans retain merge approval. Protected paths (auth, database, CI, runtime
config, deployment, harness policy, MCP config) require bounded requests,
focused tests, rollback evidence, and human review.

## Docs Map

- Exec/specs: `docs/exec-spec.md`, `docs/agents.md`, `docs/adr/`
- Harness design/plans: `docs/superpowers/specs/`, `docs/superpowers/plans/`
- Ops/CI: `docs/ops/`, `docs/ci/`, `docker-compose.yml`, `.env.example`
- Content: `docs/content/` (missions, AI profiles, balance tables)

## Local Claude Code Harness

Install a current Claude Code release and start `claude` from the repository
root with Node 20 or newer available. `CLAUDE.md` imports the canonical
`AGENTS.md`, and project hooks classify prompts, deny recognized Class D or
destructive Bash operations, and request confirmation for protected file,
command, notebook, and MCP mutations.

Claude Code permissions remain the security boundary. Keep workspace trust and
normal tool approval enabled because hook process errors and timeouts are
non-blocking in Claude Code.

- `/harness-help` explains the engineering lifecycle and navigation.
- `/route-task` performs path-aware classification before edits or scope growth.
- `/verify-change` runs focused checks and `npm run verify:local` before handoff.
- `tester`, `reviewer`, `ui-hardener`, `test-isolation`, and
  `security-reviewer` provide bounded specialist workflows.

This integration is local only. It does not add Claude CI, shadow evaluations,
Anthropic credentials, or model configuration.
