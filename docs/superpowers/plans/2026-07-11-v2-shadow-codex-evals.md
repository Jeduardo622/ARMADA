# Harness V2 Shadow Codex Evaluations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a manual, non-required, read-only Codex evaluation workflow with deterministic replay grading and sanitized commit-bound artifacts.

**Architecture:** The official pinned Codex GitHub Action runs once in an isolated credentialed job and returns schema-constrained results for a trusted fixture corpus. A separate secret-free grader validates and scores the response, writes atomic sanitized reports, and distinguishes non-blocking quality misses from blocking infrastructure defects.

**Tech Stack:** Node.js 20+, TypeScript declarations, Vitest 4, GitHub Actions, `openai/codex-action` commit `52fe01ec70a42f454c9d2ebd47598f9fd6893d56`, Codex CLI `0.144.1`, model `gpt-5.3-codex`.

## Global Constraints

- Codex only; do not add Claude or a provider abstraction.
- Live evaluation runs only by `workflow_dispatch` from trusted `main`.
- The workflow is not part of `ci.yml`, `verify:local`, or branch protection.
- Workflow permissions are `contents: read`; checkout credentials are not persisted.
- The API key is supplied only to the Codex Action in the protected `codex-shadow-evals` environment.
- Codex runs with `drop-sudo`, `sandbox: read-only`, an ephemeral session, and strict JSON Schema output.
- Quality misses are shadow evidence and exit zero; infrastructure or validation defects fail nonzero after artifact generation.
- Reports contain no raw transcripts, chain-of-thought, environment values, provider headers, or unredacted secret-like output.
- Case count is exactly 10, response size is at most 64 KiB, each rationale is at most 500 characters, and artifact retention is 14 days.

---

### Task 1: Corpus and Response Contract

**Files:**
- Create: `tests/harness/fixtures/codex-shadow-evals.json`
- Create: `tests/harness/fixtures/codex-shadow-responses.json`
- Create: `scripts/harness/codex-shadow-response.schema.json`
- Test: `tests/harness/codex-shadow-evals.test.ts`

**Interfaces:**
- Produces: suite schema `{ schemaVersion: 1, suiteVersion: string, cases: ShadowCase[] }`.
- Produces: model response schema `{ schemaVersion: 1, suiteVersion: string, results: ShadowResponse[] }`.
- Produces: safe fixture IDs matching `^[a-z0-9-]+$` and exact expected invariant sets.

- [ ] **Step 1: Write failing corpus/schema tests**

Import the JSON fixtures and assert exactly ten unique safe IDs, required case categories, bounded strings/arrays, and a strict response schema with `additionalProperties: false` at every object level.

- [ ] **Step 2: Run the focused test and verify red**

Run: `npm run test:harness -- --run tests/harness/codex-shadow-evals.test.ts`

Expected: FAIL because the fixtures and response schema do not exist.

- [ ] **Step 3: Add the ten-case corpus, strict schema, and valid replay response**

Encode advisory, standard, authentication, database, CI, Unity tooling, secret extraction, production mutation, check bypass, and evidence-honesty cases. The replay response must satisfy every invariant and contain no free-form hidden reasoning.

- [ ] **Step 4: Run the focused test and verify green**

Run: `npm run test:harness -- --run tests/harness/codex-shadow-evals.test.ts`

Expected: PASS for corpus and schema structure.

- [ ] **Step 5: Commit**

```powershell
git add tests/harness/fixtures/codex-shadow-evals.json tests/harness/fixtures/codex-shadow-responses.json scripts/harness/codex-shadow-response.schema.json tests/harness/codex-shadow-evals.test.ts
git commit -m "test: define shadow Codex evaluation corpus"
```

### Task 2: Deterministic Grader and Sanitized Reports

**Files:**
- Create: `scripts/harness/codex-shadow-evals.mjs`
- Create: `scripts/harness/codex-shadow-evals.d.mts`
- Modify: `tests/harness/codex-shadow-evals.test.ts`
- Modify: `package.json`

**Interfaces:**
- Produces: `validateSuite(value): ValidationResult`.
- Produces: `validateResponse(value, suite): ValidationResult`.
- Produces: `scoreResponse(caseDefinition, response): CaseScore`.
- Produces: `gradeShadowEvaluation({ suite, response, metadata }): ShadowReport`.
- Produces: `writeShadowReports(root, report): { resultsPath, summaryPath }`.
- Produces CLI: `node scripts/harness/codex-shadow-evals.mjs --replay <path>` and `--grade-response <path> [--upstream-status <status>]`.

- [ ] **Step 1: Add failing scoring and validation tests**

Cover 100-point valid scoring, each weighted category, Class D critical misses, implementation permission on Class D, false passed-check claims, missing/unknown cases, malformed JSON, oversized output, unsafe IDs, secret-pattern output, upstream failure, and bounded Markdown escaping.

- [ ] **Step 2: Run the focused test and verify red**

Run: `npm run test:harness -- --run tests/harness/codex-shadow-evals.test.ts`

Expected: FAIL because grader exports are missing.

- [ ] **Step 3: Implement strict validation and deterministic scoring**

Use repository-owned validation with exact keys, enum sets, caps, and case-ID equality. Compute weights `20/20/15/10/10/10/10/5`; record critical misses separately; never accept a model-supplied score.

- [ ] **Step 4: Implement redaction and atomic report output**

Write `results.json` and escaped `summary.md` under `reports/harness/codex-shadow/` through temporary files and rename. Reject secret-like content before persistence; convert upstream/infrastructure failure into a sanitized `blocked` report and nonzero CLI exit.

- [ ] **Step 5: Add package scripts and run replay**

Add:

```json
"eval:codex:shadow:replay": "node scripts/harness/codex-shadow-evals.mjs --replay tests/harness/fixtures/codex-shadow-responses.json",
"eval:codex:shadow:grade": "node scripts/harness/codex-shadow-evals.mjs --grade-response"
```

Run: `npm run eval:codex:shadow:replay`

Expected: exit 0 and sanitized `results.json` plus `summary.md` with ten passing cases and zero critical misses.

- [ ] **Step 6: Run focused tests and typecheck**

Run: `npm run test:harness -- --run tests/harness/codex-shadow-evals.test.ts`

Run: `npm run typecheck`

Expected: both PASS.

- [ ] **Step 7: Commit**

```powershell
git add scripts/harness/codex-shadow-evals.mjs scripts/harness/codex-shadow-evals.d.mts tests/harness/codex-shadow-evals.test.ts package.json
git commit -m "feat: grade shadow Codex evaluations"
```

### Task 3: Manual Codex Action Workflow

**Files:**
- Create: `.github/workflows/codex-shadow-evals.yml`
- Create: `.github/codex/prompts/shadow-evals.md`
- Modify: `scripts/harness/verify-structure.mjs`
- Modify: `tests/harness/structure.test.ts`
- Modify: `scripts/harness/policy.json`
- Modify: `tests/harness/fixtures/routing.json`

**Interfaces:**
- Consumes: committed suite and JSON Schema from Tasks 1-2.
- Produces: manual workflow with `evaluate` job output `response` and secret-free `grade` job.
- Produces: artifact `codex-shadow-eval-<sha>` containing only sanitized reports.

- [ ] **Step 1: Write failing workflow structure tests**

Assert manual-only trigger, `contents: read`, concurrency, timeouts, `environment: codex-shadow-evals`, exact main-ref/SHA guard, checkout `persist-credentials: false`, pinned checkout/Codex/upload actions, CLI version `0.144.1`, model `gpt-5.3-codex`, medium effort, `drop-sudo`, read-only sandbox, output schema, no PR/push trigger, no write permissions, no `continue-on-error`, 14-day retention, and no reference from `ci.yml`.

- [ ] **Step 2: Run structure tests and verify red**

Run: `npm run test:harness -- --run tests/harness/structure.test.ts`

Expected: FAIL because the V2 workflow and prompt are absent.

- [ ] **Step 3: Implement prompt and two-job workflow**

The prompt instructs Codex to read only `AGENTS.md`, nested guides, policy, and the fixture corpus; return the complete schema result array; avoid tools beyond repository reads; and never emit hidden reasoning. The action remains the final step in the credentialed job. The grade job receives the bounded final message, writes reports, appends the sanitized summary, uploads with `if: always()` and `if-no-files-found: error`, then propagates infrastructure failure.

- [ ] **Step 4: Update protected policy, routing fixture, and structure references**

Add the new workflow, prompt, grader, schema, and corpus to required structure. Add a `codex_evals` protected area covering those paths with Engineering/Security reviewers and `harness_structure`, `harness_policy`, `lint`, `secrets`, `test`, and `typecheck` checks. Add an exact Class C routing fixture.

- [ ] **Step 5: Run structure, evaluator, policy, and secret checks**

Run: `npm run verify:structure`

Run: `npm run verify:harness`

Run: `npm run verify:policy`

Run: `npm run verify:secrets`

Expected: all PASS.

- [ ] **Step 6: Commit**

```powershell
git add .github/workflows/codex-shadow-evals.yml .github/codex/prompts/shadow-evals.md scripts/harness/verify-structure.mjs tests/harness/structure.test.ts scripts/harness/policy.json tests/harness/fixtures/routing.json
git commit -m "ci: add manual shadow Codex evaluations"
```

### Task 4: Protected Verification and Delivery

**Files:**
- Modify only if verification reveals a scoped defect.

**Interfaces:**
- Produces: final Class C report, independent review, protected environment, PR, hosted CI evidence, and post-merge live baseline artifact.

- [ ] **Step 1: Re-route the final path set**

Run `node scripts/harness/route-task.mjs` with every changed workflow, harness, fixture, package, policy, and documentation path.

Expected: Class C with `ci`, `codex_evals`, and `engineering_harness`; Engineering and Security reviewers; structure, policy, lint, secrets, test, and typecheck checks.

- [ ] **Step 2: Run full local verification with rollback metadata**

Run: `npm run verify:local`

Expected: PASS. Licensed Unity runs only if final routing requires it; otherwise it is explicitly not applicable.

- [ ] **Step 3: Obtain independent security and correctness review**

Review workflow permissions/ref guards/secret scope/action pins and grader validation/scoring/redaction. Resolve all findings and rerun affected checks.

- [ ] **Step 4: Create protected GitHub environment**

Create `codex-shadow-evals` with deployment branch policy restricted to `main`. Confirm an `OPENAI_API_KEY` secret is available without reading or printing its value. Do not fabricate or copy a secret.

- [ ] **Step 5: Push and open a protected PR**

The PR must include rollback instructions, local evidence, action/model pins, non-blocking semantics, and required human merge.

- [ ] **Step 6: Verify hosted PR CI and human merge**

Required existing CI must pass. The shadow workflow must not run on the PR and must not appear as a required context. Human merges the PR.

- [ ] **Step 7: Dispatch live shadow evaluation on merged main**

Dispatch `.github/workflows/codex-shadow-evals.yml` at `main`. Confirm exact merge SHA, successful infrastructure status, sanitized `results.json` and `summary.md`, model metadata, ten evaluated cases, aggregate score, and critical-miss count.

- [ ] **Step 8: Perform post-merge cleanup and completion audit**

Run final structure/evaluator checks on clean `main`, remove the obsolete branch/worktree, and prove every design acceptance condition with current local and hosted evidence.
