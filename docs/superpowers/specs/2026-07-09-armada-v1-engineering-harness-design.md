# Armada V1 Engineering Harness Design

**Status:** Approved
**Date:** 2026-07-09
**Scope:** Codex-first software delivery only

## Objective

Build a repository-native engineering harness that lets Codex inspect, classify,
implement, verify, and prepare pull requests for Armada while keeping merges,
production changes, secrets, and destructive actions under human control.

The harness must be discoverable from the repository root, progressively disclose
domain-specific guidance, mechanically enforce its policies, and use the same
secret-free verification contract locally and in CI.

## V1 Boundaries

V1 covers backend, Unity/client, QA, security, CI/release, and repository
maintenance work. Product, game-design, live-ops, audio, and project-management
documents remain context rather than executable agent personas.

Codex may inspect, branch, edit, test, commit, push, and open or update pull
requests. Humans retain merge approval. V1 must not deploy production, handle or
extract secrets, mutate production data, disable controls, or make destructive
changes without an explicit separately scoped request.

V1 is Codex-specific. It will not add Claude Code or provider-neutral instruction
adapters. Required CI is deterministic and does not call a hosted model.

## Current-State Problems

- There is no root `AGENTS.md`; `docs/agents.md` is a role charter and is not a
  discoverable engineering entry point.
- Agent safety requirements are prose-only and have no executable enforcement.
- The GitHub Actions workflow has duplicate top-level keys and the latest live
  run failed before creating jobs.
- Replay and contract gates are placeholders.
- Lint and TypeScript compilation currently fail even though unit tests pass.
- There are no task-routing fixtures, protected-path tests, policy tests, or
  machine-readable completion reports.
- Dependency findings are neither gated nor represented as reviewed exceptions.

## Architecture

The harness uses a layered repository design:

1. A concise root `AGENTS.md` defines the universal lifecycle, authority model,
   classification rules, verification contract, and final-report schema.
2. Nested `AGENTS.md` files add rules for `src/`, `unity/`, `prisma/`, `.github/`,
   and `tests/`.
3. Repository skills under `.codex/skills/` describe repeatable backend, Unity,
   QA, security, and release-readiness workflows.
4. `scripts/harness/` contains deterministic classification, structure, policy,
   dependency, secret, and evaluation checks.
5. `tests/harness/fixtures/` stores versioned behavioral cases for routing,
   permissions, reviewer requirements, and verification selection.
6. `npm run verify:local` is the single local and CI verification entry point.

Instructions explain how to work. Scripts decide whether the repository and a
proposed change comply. Neither layer substitutes for the other.

## Repository Layout

```text
AGENTS.md
.codex/
  skills/
    backend-delivery/SKILL.md
    unity-delivery/SKILL.md
    qa-verification/SKILL.md
    security-review/SKILL.md
    release-readiness/SKILL.md
.github/
  AGENTS.md
  workflows/ci.yml
prisma/AGENTS.md
src/AGENTS.md
tests/
  AGENTS.md
  harness/
    classifier.test.ts
    policy.test.ts
    structure.test.ts
    fixtures/
      routing.json
      policy.json
      reports.json
unity/AGENTS.md
scripts/harness/
  policy.json
  classifier.mjs
  route-task.mjs
  verify-structure.mjs
  verify-policy.mjs
  verify-dependencies.mjs
  verify-secrets.mjs
  run-evals.mjs
  verify-local.mjs
  dependency-exceptions.json
reports/harness/
  latest.json
```

`reports/harness/` is generated and ignored by Git except for an optional
`.gitkeep`. CI uploads `latest.json` as an artifact.

## Task Lifecycle

Every task follows this lifecycle:

```text
inspect -> classify -> plan when needed -> implement -> verify -> review -> PR -> human merge
```

The initial inspection reads root and applicable nested instructions, repository
status, recent history, relevant source, and existing tests. Classification is
fresh for every task and must be repeated if scope expands.

Verification starts with the narrowest relevant check and ends with
`npm run verify:local` for non-trivial code, configuration, or behavior changes.
Reports distinguish executed, passed, failed, blocked, and not-applicable checks.
No skipped or placeholder command may be described as passed.

## Classification And Authority

### Class A: Advisory

Analysis, planning, documentation review, and code review. Read-only by default.

### Class B: Standard Delivery

Bounded application or test changes outside protected paths. Codex may branch,
edit, test, commit, push, and manage a pull request. Human merge is required.

### Class C: Protected

Authentication, authorization, API boundaries, runtime configuration, CI,
database schema or migrations, secrets, permissions, deployment, economy, and
production-data paths. Codex may inspect and plan autonomously. Implementation
requires an explicitly bounded request, a minimal diff, named risks, rollback
instructions, applicable focused tests, full local verification, and human merge.

### Class D: Prohibited

Secret extraction, production-data mutation, disabling controls, destructive
cleanup, unapproved deployment, check bypasses, or fabricated evidence. Codex
stops and reports the safe next action.

Mixed-scope tasks take the highest applicable class. Scope growth triggers
reclassification before additional files are edited.

## Classifier Contract

`scripts/harness/classifier.mjs` exports pure functions so Vitest can exercise the
same logic used by the CLI:

```js
classifyTask({ description, changedPaths }) => {
  classification,
  reasons,
  protectedAreas,
  allowedActions,
  requiredReviewers,
  requiredChecks
}
```

`scripts/harness/route-task.mjs` accepts `--description`, repeated `--path`, and
`--json`. It prints a human summary by default and stable JSON for automation.
Classification is conservative: ambiguous protected intent resolves to Class C,
and explicit prohibited intent resolves to Class D.

`scripts/harness/policy.json` is the versioned source for path rules, keywords,
allowed actions, required reviewers, required checks, and report fields.

## Skills

Each skill has a narrow trigger and a checklist that references executable
commands rather than duplicating policy:

- `backend-delivery`: Fastify routes, services, contracts, telemetry, and backend
  tests.
- `unity-delivery`: C# client code, deterministic simulation, API parity,
  Addressables, and device constraints.
- `qa-verification`: test selection, regression design, isolation, replay, and
  evidence reporting.
- `security-review`: authentication, authorization, input boundaries,
  dependencies, secrets, and threat-focused review.
- `release-readiness`: CI results, artifacts, rollback evidence, PR readiness,
  and human-merge handoff.

Skills do not create autonomous product, design, PM, audio, or live-ops roles.

## Verification Contract

`npm run verify:local` runs these checks in fixed order and writes
`reports/harness/latest.json` even when a check fails:

1. Harness structure and instruction lint.
2. Harness unit tests and deterministic fixtures.
3. ESLint.
4. TypeScript typecheck without emit.
5. Backend unit and integration tests.
6. Backend production build.
7. API-contract and deterministic-simulation checks.
8. Dependency policy.
9. Secret scanning.
10. Harness policy and evidence validation.

The runner stops launching dependent checks after a prerequisite failure but
records each remaining check as blocked. Independent checks continue so one run
provides a useful failure inventory.

The current lint and typecheck failures are baseline defects and must be fixed as
part of harness implementation. They must not be suppressed or grandfathered.

## Deterministic Evaluations

JSON fixtures cover:

- Backend feature and bug-fix routing.
- Unity client and deterministic-simulation work.
- Test-only and documentation work.
- Authentication, schema, migration, CI, runtime, and deployment work.
- Mixed-scope escalation.
- Secret access, production mutation, destructive operations, and check bypasses.
- Required checks and reviewers for every class and protected area.
- Required final-report fields and status vocabulary.

The eval runner compares exact classifier output with expected classification,
allowed actions, reviewer roles, and check IDs. Failures show a fixture ID and a
field-level diff. Required evals use no network, API key, or model call.

## Dependency Policy

`verify-dependencies.mjs` consumes `npm audit --json`. New critical or high
findings fail verification. Existing findings may pass only when listed in
`dependency-exceptions.json` with advisory ID, affected package, rationale,
owner, and ISO expiration date no more than 90 days after the exception is
introduced. Missing, malformed, overlong, or expired exceptions fail.

Exceptions document debt; they do not mark findings as fixed. The generated
report includes active exceptions and remaining severity counts. Dependency
upgrades that change runtime behavior remain separately reviewable protected
work.

## Secret Policy

`verify-secrets.mjs` scans tracked files and the staged diff for private-key
headers, provider-token patterns, high-confidence credential assignments, and
tracked environment files. `.env.example` may contain documented placeholder
values but not live credentials. Findings include file and rule ID without
printing the suspected secret value.

V1 does not read secret stores or require credentials to run verification.

## CI Design

`.github/workflows/ci.yml` becomes one syntactically valid workflow. It uses
`actions/checkout@v4`, `actions/setup-node@v4`, Node 20, `npm ci`, and
`npm run verify:local`. It uploads the machine-readable harness report and test
artifacts even on failure.

The workflow has least-privilege read permissions and no production credentials.
No check is represented by an echo placeholder. A required but unavailable check
returns a named blocked status and fails for affected changes.

Unity compilation is not a required v1 gate because the repository has no
licensed Unity runner. V1 validates Unity source conventions, project metadata,
API contracts, and deterministic fixtures. The report explicitly names Unity
compilation as unavailable rather than passed.

## Completion Report

Every agent completion report contains:

- chosen task and issue key, if any;
- route-task classification and reasons;
- delegated contributions, if any;
- files changed;
- checks run with executed status and outcome;
- PR identifier and live checks, when applicable;
- merge blockers and merge result, when applicable;
- rollback instructions for protected changes;
- residual risk;
- recommended next slice, when useful.

Raw chain-of-thought is excluded. Reports contain concise assumptions, decisions,
evidence, and risks only.

## Failure Handling

- A classifier configuration error fails closed as Class C.
- Invalid fixture or policy data fails the harness before application checks.
- A verification command timeout is recorded as failed, not skipped.
- Missing tools are blocked locally and failed in CI when required.
- Generated reports are written atomically to avoid partial success artifacts.
- Scope changes invalidate the prior classification and require a fresh route.
- Protected verification that needs unavailable infrastructure remains blocked;
  Codex reports the exact human or environment action needed.

## Maintenance

Harness policy changes are protected CI changes. They require fixture updates,
focused harness tests, full verification, rollback notes, and human merge.

Documentation and policy drift are checked mechanically: all referenced files,
commands, check IDs, skills, and report fields must exist. Dependency exceptions
expire. New protected directories must be added to policy and fixtures in the
same change.

V1 does not add scheduled hosted-model evaluations. A future version may add
optional Codex evaluation tasks after deterministic fixtures establish a stable
baseline.

## Acceptance Criteria

The harness is complete when:

1. Codex discovers root and nested instructions from all protected subtrees.
2. Every software-delivery task fixture receives the expected classification,
   permissions, reviewers, and checks.
3. Class D fixtures cannot produce implementation permissions.
4. All instruction, skill, command, check, and policy references validate.
5. `npm run verify:local` executes successfully from a clean checkout and writes
   a complete machine-readable report.
6. Lint, typecheck, backend tests, build, contract checks, deterministic checks,
   dependency policy, secret scan, and harness evals are real commands.
7. CI runs the same verification contract with no secret and uploads artifacts.
8. Existing dependency exceptions are explicit, owned, justified, and expiring.
9. Unity compilation is reported as unavailable and never represented as passed.
10. The current branch has a minimal, reviewable diff and no unrelated changes.
