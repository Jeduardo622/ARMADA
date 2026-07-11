# Harness Failure-Injection Design

## Objective

Prove the database and deployment gates fail closed under infrastructure and
tooling faults, and prove disposable database cleanup is attempted after every
successful container start.

## Design

The verifiers retain their existing command-line behavior and result schema.
Optional dependency objects provide command execution, health timing, and file
reads for deterministic tests. Production calls omit those objects and use the
current Docker, Prisma, clock, sleep, and filesystem implementations.

Database tests cover Docker unavailable, container startup failure, unhealthy
or timed-out health checks, port discovery failure, each Prisma command failure,
and cleanup failure. Any path after successful startup must record exactly one
cleanup attempt. Signal cleanup remains idempotent.

Deployment tests cover Compose command failure (including spawn errors), invalid
JSON, missing `.env.example`, missing services, image/health/port violations,
and URL protocol/host/credential/database mismatches.

Tests use no Docker daemon, network, local `.env`, secrets, or clock delay. The
existing live `verify:database` and `verify:deployment` commands remain required
after deterministic tests pass.

## Acceptance Criteria

1. Every injected failure returns `status: failed` with a specific diagnostic.
2. No injected failure is represented as passed, skipped, or unavailable.
3. Database cleanup runs once after every successful injected startup.
4. Real ephemeral PostgreSQL migration verification still passes.
5. Real Compose verification still passes.
6. Harness tests and `verify:local` pass from a clean install.

## Rollback

Revert the verifier refactor, declarations, tests, and this design together.
The public CLI commands and package scripts do not change.
