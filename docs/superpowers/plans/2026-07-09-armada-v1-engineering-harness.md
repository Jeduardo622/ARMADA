# Armada V1 Engineering Harness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and verify Armada's Codex-first, repository-native software-delivery harness.

**Architecture:** A short root `AGENTS.md` routes Codex to nested instructions and focused repository skills. Pure ESM classifier and policy modules under `scripts/harness/` provide deterministic routing, safety, structure, dependency, secret, and completion-report enforcement. One `verify:local` command runs the same fail-closed contract locally and in GitHub Actions.

**Tech Stack:** Node.js 20, ESM JavaScript harness scripts, TypeScript/Fastify backend, Vitest, ESLint, npm, GitHub Actions, Unity/C# source metadata.

## Global Constraints

- Codex-first only; do not add Claude Code or provider-neutral adapters.
- Software delivery only; product, design, live-ops, audio, and PM remain reference context.
- Required verification must be secret-free and deterministic.
- Humans retain merge approval; production deployment and production-data mutation are prohibited.
- Class C changes require explicit scope, rollback evidence, focused tests, full verification, and human merge.
- Existing lint and typecheck defects must be fixed, not suppressed.
- Unity compilation must be reported as unavailable until a licensed runner exists and must never be reported as passed.
- Dependency exceptions require advisory ID, package, rationale, owner, introduction date, and an expiration no more than 90 days later.

---

### Task 1: Discoverable Instructions And Skills

**Files:**
- Create: `AGENTS.md`
- Create: `src/AGENTS.md`
- Create: `unity/AGENTS.md`
- Create: `prisma/AGENTS.md`
- Create: `.github/AGENTS.md`
- Create: `tests/AGENTS.md`
- Create: `.codex/skills/backend-delivery/SKILL.md`
- Create: `.codex/skills/unity-delivery/SKILL.md`
- Create: `.codex/skills/qa-verification/SKILL.md`
- Create: `.codex/skills/security-review/SKILL.md`
- Create: `.codex/skills/release-readiness/SKILL.md`
- Create: `tests/harness/structure.test.ts`

**Interfaces:**
- Consumes: commands currently defined in `package.json` and repository documentation under `docs/`.
- Produces: root and nested instruction chain plus five discoverable `SKILL.md` workflows referenced by structure validation.

- [ ] **Step 1: Write the failing structure test**

```ts
import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';

const requiredFiles = [
  'AGENTS.md',
  'src/AGENTS.md',
  'unity/AGENTS.md',
  'prisma/AGENTS.md',
  '.github/AGENTS.md',
  'tests/AGENTS.md',
  '.codex/skills/backend-delivery/SKILL.md',
  '.codex/skills/unity-delivery/SKILL.md',
  '.codex/skills/qa-verification/SKILL.md',
  '.codex/skills/security-review/SKILL.md',
  '.codex/skills/release-readiness/SKILL.md'
];

describe('engineering harness structure', () => {
  it.each(requiredFiles)('%s exists and is non-empty', (path) => {
    expect(readFileSync(path, 'utf8').trim().length).toBeGreaterThan(40);
  });
});
```

- [ ] **Step 2: Run the test and confirm the missing-file failure**

Run: `npx vitest run tests/harness/structure.test.ts`
Expected: FAIL opening `AGENTS.md`.

- [ ] **Step 3: Add the instruction chain and focused skills**

Root instructions must contain the exact lifecycle, Classes A-D, authority limits,
`node scripts/harness/route-task.mjs`, `npm run verify:local`, scope-change
reclassification, and completion-report fields. Nested files add only subtree
rules. Each skill frontmatter must contain `name` and `description`, followed by
triggers, inputs, workflow, verification, and stop conditions.

- [ ] **Step 4: Run the structure test**

Run: `npx vitest run tests/harness/structure.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit the instruction layer**

```powershell
git add -- AGENTS.md src/AGENTS.md unity/AGENTS.md prisma/AGENTS.md .github/AGENTS.md tests/AGENTS.md .codex/skills tests/harness/structure.test.ts
git commit -m "feat: add Codex instruction hierarchy"
```

### Task 2: Deterministic Task Classification

**Files:**
- Create: `scripts/harness/policy.json`
- Create: `scripts/harness/classifier.mjs`
- Create: `scripts/harness/classifier.d.mts`
- Create: `scripts/harness/route-task.mjs`
- Create: `tests/harness/fixtures/routing.json`
- Create: `tests/harness/classifier.test.ts`

**Interfaces:**
- Consumes: `TaskInput = { description: string; changedPaths: string[] }`.
- Produces: `classifyTask(input): ClassificationResult` and CLI JSON with `classification`, `reasons`, `protectedAreas`, `allowedActions`, `requiredReviewers`, and `requiredChecks`.

`classifier.d.mts` declares `TaskInput`, `ClassificationResult`, and
`classifyTask(input: TaskInput): ClassificationResult` so strict TypeScript tests
consume the ESM implementation without implicit `any`.

- [ ] **Step 1: Add routing fixtures and the failing classifier test**

```ts
import { describe, expect, it } from 'vitest';
import fixtures from './fixtures/routing.json';
import { classifyTask } from '../../scripts/harness/classifier.mjs';

describe('classifyTask', () => {
  it.each(fixtures)('$id', (fixture) => {
    expect(classifyTask(fixture.input)).toEqual(fixture.expected);
  });
});
```

Fixtures must cover advisory docs, backend delivery, Unity delivery, auth, Prisma
migrations, CI, runtime configuration, mixed protected scope, secret extraction,
production mutation, destructive cleanup, deployment, and check bypass.

- [ ] **Step 2: Run the classifier test and confirm the missing-module failure**

Run: `npx vitest run tests/harness/classifier.test.ts`
Expected: FAIL resolving `scripts/harness/classifier.mjs`.

- [ ] **Step 3: Implement policy-driven classification**

```js
export function classifyTask({ description = '', changedPaths = [] }) {
  // Normalize paths and text, collect path and intent matches, select the highest
  // class, then return sorted unique policy-derived arrays.
}
```

Class D intent always wins. Protected intent or protected paths produce Class C.
Documentation-only intent produces Class A when no higher rule matches. All other
bounded delivery defaults to Class B. A malformed policy import throws so callers
fail closed.

- [ ] **Step 4: Implement the CLI**

Support `--description <text>`, repeated `--path <repo/path>`, and `--json`.
Invalid arguments exit 2. Successful routing exits 0. Class D is a successful
classification and is represented by `allowedActions: ["report_safe_next_action"]`.

- [ ] **Step 5: Run classifier tests and CLI smoke cases**

Run: `npx vitest run tests/harness/classifier.test.ts`
Expected: PASS.

Run: `node scripts/harness/route-task.mjs --description "change auth middleware" --path src/plugins/auth.ts --json`
Expected: JSON containing `"classification":"C"`.

Run: `node scripts/harness/route-task.mjs --description "print production secrets" --json`
Expected: JSON containing `"classification":"D"` and no edit action.

- [ ] **Step 6: Commit routing**

```powershell
git add -- scripts/harness/policy.json scripts/harness/classifier.mjs scripts/harness/classifier.d.mts scripts/harness/route-task.mjs tests/harness/fixtures/routing.json tests/harness/classifier.test.ts
git commit -m "feat: add deterministic task routing"
```

### Task 3: Harness Policy, Structure, Secret, And Dependency Checks

**Files:**
- Create: `scripts/harness/verify-structure.mjs`
- Create: `scripts/harness/verify-policy.mjs`
- Create: `scripts/harness/verify-policy.d.mts`
- Create: `scripts/harness/verify-secrets.mjs`
- Create: `scripts/harness/verify-dependencies.mjs`
- Create: `scripts/harness/dependency-exceptions.json`
- Create: `tests/harness/fixtures/policy.json`
- Create: `tests/harness/fixtures/reports.json`
- Create: `tests/harness/policy.test.ts`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: repository root, policy JSON, tracked files, staged diff, `npm audit --json`, and completion-report objects.
- Produces: `{ id, status, summary, details }` check results where status is `passed`, `failed`, `blocked`, or `not_applicable`.

`verify-policy.d.mts` declares the completion-report and dependency-exception
validators imported by strict TypeScript tests.

- [ ] **Step 1: Write failing policy tests**

```ts
import { describe, expect, it } from 'vitest';
import { validateCompletionReport, validateDependencyExceptions } from '../../scripts/harness/verify-policy.mjs';

describe('harness policy', () => {
  it('rejects completion reports that claim an unexecuted check passed', () => {
    expect(() => validateCompletionReport({ checks: [{ id: 'lint', executed: false, status: 'passed' }] })).toThrow(/executed/);
  });

  it('rejects dependency exceptions longer than 90 days', () => {
    expect(() => validateDependencyExceptions([{ introduced: '2026-07-09', expires: '2026-10-08' }])).toThrow(/90 days/);
  });
});
```

- [ ] **Step 2: Run tests and confirm missing exports**

Run: `npx vitest run tests/harness/policy.test.ts`
Expected: FAIL resolving `verify-policy.mjs`.

- [ ] **Step 3: Implement structure and completion-report validation**

Validate every referenced instruction, skill, script, npm command, check ID, and
required report field. Reject duplicate check IDs, unknown status values,
`executed: false` with `passed`, and protected changes without rollback evidence.

- [ ] **Step 4: Implement redacting secret checks**

Scan `git ls-files` and `git diff --cached --unified=0` for private-key headers,
known provider token prefixes, non-placeholder credential assignments, and tracked
`.env` variants other than `.env.example`. Report rule ID and file only; never emit
the matching value.

- [ ] **Step 5: Implement dependency checks and baseline exceptions**

Parse the audit JSON even when `npm audit` exits non-zero. Critical and high
advisories fail unless an exact active exception exists. Record current advisory
IDs with package, rationale `pre-existing finding from 2026-07-09 harness audit`,
owner `Armada engineering`, introduced `2026-07-09`, and expiration no later than
`2026-10-07`. Moderate and lower findings remain visible but non-blocking in v1.

- [ ] **Step 6: Ignore generated harness reports**

Add `reports/harness/` to `.gitignore`.

- [ ] **Step 7: Run focused tests and verifier smokes**

Run: `npx vitest run tests/harness/policy.test.ts tests/harness/structure.test.ts`
Expected: PASS.

Run: `node scripts/harness/verify-structure.mjs`
Expected: PASS result JSON.

Run: `node scripts/harness/verify-secrets.mjs`
Expected: PASS without printing environment values.

Run: `node scripts/harness/verify-dependencies.mjs`
Expected: PASS with active exception counts and visible remaining severity counts.

- [ ] **Step 8: Commit policy verifiers**

```powershell
git add -- .gitignore scripts/harness tests/harness/fixtures tests/harness/policy.test.ts
git commit -m "feat: enforce harness policy and safety checks"
```

### Task 4: Evaluation Runner And Unified Verification

**Files:**
- Create: `scripts/harness/run-evals.mjs`
- Create: `scripts/harness/verify-local.mjs`
- Create: `scripts/harness/verify-contracts.mjs`
- Create: `scripts/harness/verify-unity.mjs`
- Modify: `package.json`
- Modify: `src/plugins/auth.ts`
- Modify: `src/plugins/flags.ts`
- Modify: `src/types.d.ts`
- Modify: `src/app.ts`
- Test: `tests/app.test.ts`
- Test: `tests/harness/*.test.ts`

**Interfaces:**
- Consumes: child-process commands and fixture suites.
- Produces: `reports/harness/latest.json` containing timestamps, overall status, and ordered check results with execution state, duration, summary, and redacted details.

- [ ] **Step 1: Add package scripts**

```json
{
  "typecheck": "tsc -p tsconfig.json --noEmit",
  "test:harness": "vitest run tests/harness",
  "verify:contracts": "node scripts/harness/verify-contracts.mjs",
  "verify:unity": "node scripts/harness/verify-unity.mjs",
  "verify:harness": "node scripts/harness/run-evals.mjs",
  "verify:local": "node scripts/harness/verify-local.mjs"
}
```

- [ ] **Step 2: Implement fixture evaluation**

`run-evals.mjs` loads all routing fixtures, calls `classifyTask`, reports field-level
diffs by fixture ID, invokes structure and policy validation, and exits 1 on any
failure.

- [ ] **Step 3: Add real contract and Unity metadata checks**

Contract verification must confirm documented OpenAPI route/method pairs exist in
the route source and that schema version 1 is represented in both sim types and
OpenAPI. Unity verification must confirm `ProjectVersion.txt`, `manifest.json`,
determinism hooks, and API client files exist, then return `unity_compilation` as
`not_applicable` with reason `licensed Unity runner unavailable`.

- [ ] **Step 4: Repair lint without suppression**

Replace `any` in the auth guard with Fastify's `FastifyRequest` and `FastifyReply`
types and preserve the existing 401 behavior.

- [ ] **Step 5: Repair FlagClient type drift**

Use `Context` from `unleash-client`, add `ready(): boolean` to the single exported
`FlagClient` interface, remove the duplicate interface from `src/types.d.ts`, and
call the supported synchronous `client.destroy()` during close.

- [ ] **Step 6: Repair Fastify logger inference**

Pass `loggerInstance: logger` rather than `logger` into `Fastify()` so the Pino
instance is not reinterpreted as logger configuration and route registration keeps
the default raw-server generic.

- [ ] **Step 7: Verify the baseline repairs**

Run: `npm run lint`
Expected: PASS with zero errors.

Run: `npm run typecheck`
Expected: PASS with zero errors.

Run: `npm test`
Expected: all backend and harness tests pass.

Run: `npm run build`
Expected: PASS and emit `dist/`.

- [ ] **Step 8: Implement the unified runner**

Run structure, harness tests, lint, typecheck, backend tests, build, contracts,
Unity metadata, dependencies, secrets, and policy in the design-specified order.
Continue independent checks after failures, mark dependent checks blocked, write
the report through a temporary file plus rename, and exit 1 unless all required
checks pass.

- [ ] **Step 9: Run the full contract**

Run: `npm run verify:local`
Expected: exit 0 and `reports/harness/latest.json` with overall status `passed`,
all required checks passed, and Unity compilation explicitly not applicable.

- [ ] **Step 10: Commit unified verification**

```powershell
git add -- package.json src scripts/harness tests
git commit -m "feat: add unified local verification"
```

### Task 5: GitHub Actions And Pull-Request Contract

**Files:**
- Replace: `.github/workflows/ci.yml`
- Modify: `.github/pull_request_template.md`
- Test: `tests/harness/structure.test.ts`

**Interfaces:**
- Consumes: clean checkout and `package-lock.json`.
- Produces: one valid least-privilege CI workflow and uploaded `harness-report` artifact.

- [ ] **Step 1: Extend structure tests for workflow invariants**

Assert the workflow contains exactly one top-level `name:`, one top-level `jobs:`,
`permissions:\n  contents: read`, `npm ci`, `npm run verify:local`, and
`actions/upload-artifact@v4`, and contains neither placeholder text nor
`npm install`.

- [ ] **Step 2: Run the focused test and confirm failure against old CI**

Run: `npx vitest run tests/harness/structure.test.ts`
Expected: FAIL because `.github/workflows/ci.yml` has duplicate top-level keys and placeholders.

- [ ] **Step 3: Replace CI with the valid workflow**

Use Node 20, npm cache, `npm ci`, `npm run verify:local`, and an `if: always()`
artifact upload for `reports/harness/latest.json`. Set workflow permissions to
read-only contents and do not add secrets or deployment permissions.

- [ ] **Step 4: Update the PR template**

Require classification, changed paths, executed checks, blocked checks, protected
reviewers, rollback evidence, dependency exceptions, Unity compilation status,
and residual risk.

- [ ] **Step 5: Run structure and full verification**

Run: `npx vitest run tests/harness/structure.test.ts`
Expected: PASS.

Run: `npm run verify:local`
Expected: PASS.

- [ ] **Step 6: Commit CI integration**

```powershell
git add -- .github tests/harness/structure.test.ts
git commit -m "ci: enforce the engineering harness"
```

### Task 6: Completion Audit And Handoff

**Files:**
- Modify only files required by failures found in this task.

**Interfaces:**
- Consumes: design acceptance criteria and current branch state.
- Produces: requirement-by-requirement evidence, a clean worktree, and a reviewable branch.

- [ ] **Step 1: Run clean-install verification**

Run: `npm ci`
Expected: successful deterministic install from `package-lock.json`.

Run: `npm run verify:local`
Expected: exit 0 with a complete report.

- [ ] **Step 2: Audit every acceptance criterion**

For each of the ten criteria in the design, record the authoritative file or
command output proving it. Treat missing or indirect evidence as incomplete and
fix the gap before continuing.

- [ ] **Step 3: Inspect the final diff and repository state**

Run: `git diff --check origin/main...HEAD`
Expected: no output.

Run: `git status --short --branch`
Expected: clean branch ahead of `origin/main` only by harness commits.

Run: `git diff --stat origin/main...HEAD`
Expected: only design, plan, instructions, skills, harness, focused baseline fixes,
tests, package scripts, workflow, PR template, and ignore rules.

- [ ] **Step 4: Review commit history**

Run: `git log --oneline origin/main..HEAD`
Expected: focused commits for design, instructions, routing, policy, verification,
and CI.

- [ ] **Step 5: Prepare the operational report**

Report chosen task, issue key, route classification, files changed, exact executed
checks, PR state, merge blockers, residual risk, Unity runner limitation, active
dependency exceptions, and the next recommended slice.
