# Harness Failure-Injection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add deterministic fail-closed tests for database and deployment verifier faults without changing their CLI contracts.

**Architecture:** Refactor each verifier around an optional dependency object with real defaults. Unit tests supply scripted command/file results and assert diagnostics plus cleanup; live commands remain the final integration proof.

**Tech Stack:** Node.js, Vitest 4, Docker CLI, Prisma CLI, existing Armada result schema.

## Global Constraints

- Never contact production or persistent databases.
- Keep `verify:database` and `verify:deployment` command/result contracts unchanged.
- Cleanup must be attempted exactly once after a successful container start.
- Tests must not require Docker, network, secrets, `.env`, or real waiting.

---

### Task 1: Database Failure Injection

**Files:**
- Modify: `scripts/harness/verify-database.mjs`
- Modify: `scripts/harness/verify-database.d.mts`
- Create: `tests/harness/failure-injection.test.ts`

**Interfaces:**
- Produces: `verifyDatabase(root?, dependencies?)` where dependencies may supply `runCommand`, `now`, and `sleep`.

- [ ] Write failing tests for Docker unavailable, start failure, health failure,
  port failure, Prisma failure, cleanup failure, and one cleanup per started run.
- [ ] Run `npm run test:harness -- --run tests/harness/failure-injection.test.ts`
  and confirm failures occur before the dependency interface exists.
- [ ] Refactor the verifier to use injected dependencies with current real
  defaults, preserving CLI output.
- [ ] Re-run the focused test and `npm run verify:database`; both must pass.
- [ ] Commit as `test: add database verifier failure injection`.

### Task 2: Deployment Failure Injection

**Files:**
- Modify: `scripts/harness/verify-deployment.mjs`
- Modify: `scripts/harness/verify-deployment.d.mts`
- Modify: `tests/harness/failure-injection.test.ts`

**Interfaces:**
- Produces: `verifyDeployment(root?, dependencies?)` where dependencies may supply `runCommand` and `readTextFile`.

- [ ] Add failing tests for Compose spawn/stderr failure, malformed JSON,
  missing environment file, missing service, and connection mismatches.
- [ ] Run the focused test and confirm the new cases fail first.
- [ ] Refactor deployment verification around the injected dependencies and
  include spawn error messages in diagnostics.
- [ ] Run the focused test and `npm run verify:deployment`; both must pass.
- [ ] Commit as `test: add deployment verifier failure injection`.

### Task 3: Full Protected Verification

**Files:**
- Modify only if review identifies a concrete defect in the files above.

- [ ] Run `npm ci`, `npm run test:harness`, `npm run verify:database`,
  `npm run verify:deployment`, and `npm run verify:local` with Class C rollback.
- [ ] Confirm no `armada-db-verify-*` container remains.
- [ ] Obtain Backend, Database, Engineering, and Security review.
- [ ] Push and open a stacked PR targeting `codex/path-aware-ci`; after PR #7
  merges, retarget to `main`, rerun hosted verification, and require human merge.

## Rollback

Revert the implementation commits and rerun both live verifiers plus
`npm run verify:local`. No schema, migration, or persistent data changes occur.
