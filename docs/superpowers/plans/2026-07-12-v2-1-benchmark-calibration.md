# Harness V2.1 Benchmark Calibration Implementation Plan

**Goal:** Calibrate only free-form scorer fields, lock all safety invariants,
add sanitized diagnostics, and clarify the model-visible general policy.

**Classification:** Class C (`engineering_harness`, `codex_evals`). Engineering
and Security review, full local verification, rollback evidence, and human merge
are required.

### Task 1: Benchmark lock and public contract

**Files:**
- Create `tests/harness/fixtures/codex-shadow-benchmark-lock.json`
- Create `tests/harness/fixtures/codex-shadow-policy-contract.json`
- Modify `tests/harness/codex-shadow-evals.test.ts`
- Modify `tests/harness/codex-shadow-prompt.test.ts`
- Modify `scripts/harness/build-codex-shadow-prompt.mjs`
- Modify `scripts/harness/verify-structure.mjs`
- Modify `scripts/harness/policy.json`

1. Add failing tests that call `buildGradingSuite` with a lock, mutate corpus,
   safety-core fields, weights, threshold, and critical rules, and expect each
   mutation to fail. Assert the public contract has no fixture IDs/prompts or
   private expectation values and appears exactly once in the built prompt.
2. Run:
   `npx vitest run tests/harness/codex-shadow-evals.test.ts tests/harness/codex-shadow-prompt.test.ts`
   Expected: fail because lock validation and public contract are absent.
3. Add canonical stable JSON hashing, lock validation, the committed manifest,
   public contract, prompt allowlist entry, structure entry, and protected path.
4. Rerun the focused tests. Expected: pass.

### Task 2: Semantic rollback and evidence scoring

**Files:**
- Modify `tests/harness/codex-shadow-evals.test.ts`
- Modify `scripts/harness/codex-shadow-evals.mjs`
- Modify `scripts/harness/codex-shadow-evals.d.mts`

1. Add failing tests for safe rollback paraphrases, vague and unsafe rollback,
   exact claim tuples with paraphrased status-consistent evidence, missing and
   extra claims, status/executed mismatch, inconsistent prose, and unsupported
   passed claims. Snapshot the fixed weights and every Class D critical rule.
2. Run `npx vitest run tests/harness/codex-shadow-evals.test.ts`.
   Expected: semantic paraphrases fail under exact JSON equality.
3. Implement semantic rollback/evidence predicates and reason-code generation.
   Keep all exact classification/decision/set scoring and critical gates.
4. Rerun the focused test. Expected: pass with replay 100.

### Task 3: Sanitized report v2

**Files:**
- Modify `tests/harness/codex-shadow-evals.test.ts`
- Modify `scripts/harness/codex-shadow-evals.mjs`
- Modify `scripts/harness/codex-shadow-evals.d.mts`

1. Add failing tests that require schema version 2, bounded unique reason codes,
   reason counts in Markdown, and absence of breakdown, expected/actual values,
   prompts, rollback, evidence, rationale, and secrets from persisted reports.
2. Run `npx vitest run tests/harness/codex-shadow-evals.test.ts`.
   Expected: fail because reports persist `breakdown` and schema version 1.
3. Persist only sanitized case fields and update declarations/summary columns.
4. Rerun focused tests and replay. Expected: 100, zero critical misses.

### Task 4: Protected verification and delivery

1. Run `npm run verify:structure`, `npm run verify:harness`,
   `npm run verify:policy`, `npm run verify:secrets`, and
   `npm run eval:codex:shadow:replay` with bundled Node 24.
2. Run `npm run verify:local` with rollback instructions.
3. Independently review benchmark integrity, answer-key isolation, report
   sanitization, Class D gates, and false-pass behavior.
4. Commit, push `codex/v2-1-benchmark-calibration`, open a Class C PR, and wait
   for all required GitHub checks. Human merge is required.

### Task 5: Hosted stability proof

1. After merge, dispatch `codex-shadow-evals.yml` three times on the exact
   merged `main` SHA.
2. Download every artifact and verify job success, SHA/run binding, ten unique
   fixtures, schema version 2, bounded reason codes, and no private/raw fields.
3. Require zero honesty critical misses in all runs and at least two suite
   passes. If not met, preserve the benchmark and recommend prompt/skill
   hardening rather than further scorer calibration.
