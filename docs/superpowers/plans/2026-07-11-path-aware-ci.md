# Path-Aware CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make non-Unity pull-request verification secret-free while keeping Unity changes and main pushes fail-closed on licensed, commit-bound evidence.

**Architecture:** A repository-owned resolver feeds four least-privilege GitHub jobs: scope, core, conditional Unity, and aggregate. The aggregate retains the protected check name and is the only job that publishes the authoritative `verify:local` report.

**Tech Stack:** Node.js 20+, GitHub Actions, GameCI, Vitest 4, existing Armada harness modules.

## Global Constraints

- Keep `npm run verify:local` as the final local and CI authority.
- Keep the required check name exactly `Verify local contract`.
- Do not use `pull_request_target`, self-hosted runners, write permissions, or production credentials.
- Unity evidence must match `GITHUB_SHA` and Unity `2022.3.62f3`.
- Main pushes always run EditMode and PlayMode.

---

### Task 1: CI Scope Resolver

**Files:**
- Create: `scripts/harness/resolve-ci-scope.mjs`
- Create: `scripts/harness/resolve-ci-scope.d.mts`
- Modify: `tests/harness/verifiers.test.ts`
- Modify: `tests/harness/structure.test.ts`
- Modify: `scripts/harness/verify-structure.mjs`

**Interfaces:**
- Consumes: `readChangedPaths(root, env)`, `requiresUnityCompilation(paths, env)`, and `requiresUnityTests(paths, env)` from `verify-local.mjs`.
- Produces: `resolveCiScope(root?, env?) -> { changedPaths, unityRequired }` and CLI output `unity_required=true|false` through `GITHUB_OUTPUT`.

- [ ] **Step 1: Write failing resolver tests**

Assert backend paths return `false`, Unity assets and `.codex/config.toml` return
`true`, and `FORCE_UNITY=1` returns `true` without changed paths.

- [ ] **Step 2: Run the focused tests**

Run: `npm run test:harness -- --run tests/harness/verifiers.test.ts tests/harness/structure.test.ts`

Expected: FAIL because `resolve-ci-scope.mjs` does not exist.

- [ ] **Step 3: Implement the resolver**

Use the existing path predicates, write exactly one output line when
`GITHUB_OUTPUT` is configured, and print a JSON diagnostic without secrets.

- [ ] **Step 4: Re-run the focused tests**

Expected: PASS with the new resolver included in portable/required module lists.

- [ ] **Step 5: Commit**

```powershell
git add scripts/harness/resolve-ci-scope.* scripts/harness/verify-structure.mjs tests/harness
git commit -m "feat: add CI scope resolver"
```

### Task 2: Four-Job Workflow

**Files:**
- Modify: `.github/workflows/ci.yml`
- Modify: `tests/harness/structure.test.ts`

**Interfaces:**
- Consumes: `scope.outputs.unity_required` and artifact `unity-verification`.
- Produces: jobs `scope`, `core`, `unity`, and `verify`; final job display name `Verify local contract`.

- [ ] **Step 1: Replace monolithic workflow expectations with failing four-job assertions**

Assert least-privilege workflow permissions, pinned actions, exact job names,
conditional Unity execution, Unity secrets confined to the Unity job, artifact
upload/download, `if: always()` aggregation, and final `verify:local` execution.

- [ ] **Step 2: Run the structure test**

Run: `npm run test:harness -- --run tests/harness/structure.test.ts`

Expected: FAIL against the current single-job workflow.

- [ ] **Step 3: Implement the workflow**

The core job runs all secret-free package scripts. The Unity job runs credential
preflight, both GameCI modes, records evidence, and uploads results plus evidence.
The verify job downloads the artifact with non-fatal absence handling and runs
`verify:local`; required Unity paths make absent evidence fail in the harness.

- [ ] **Step 4: Run structure and harness tests**

Run: `npm run test:harness`

Expected: all workflow and resolver contracts pass.

- [ ] **Step 5: Commit**

```powershell
git add .github/workflows/ci.yml tests/harness/structure.test.ts
git commit -m "ci: split core and licensed Unity verification"
```

### Task 3: Protected Verification And Hosted Proof

**Files:**
- Modify only if review finds a concrete defect in Task 1 or Task 2 files.

**Interfaces:**
- Consumes: final workflow and resolver.
- Produces: passing local report and GitHub PR checks for non-Unity and Unity-required scope behavior.

- [ ] **Step 1: Run focused protected checks**

Run: `npm run verify:structure`, `npm run verify:secrets`, `npm run verify:policy`, and `npm run test:harness`.

Expected: all pass.

- [ ] **Step 2: Run the complete local contract**

Run: `npm run verify:local` with Class C rollback metadata.

Expected: overall passed; local Unity compilation/tests are not applicable because no Unity runtime path changed.

- [ ] **Step 3: Review final routing and workflow security**

Confirm `.github/workflows/ci.yml` remains Class C with Engineering, Security,
and Unity reviewers; confirm no credential appears outside the Unity job.

- [ ] **Step 4: Push and open a human-merge PR**

The PR records rollback, fork behavior, local evidence, and required reviewers.

- [ ] **Step 5: Verify hosted behavior**

Confirm the workflow contains separate scope/core/unity/aggregate jobs, the final
required context is unchanged, main protection still requires it, and the exact
PR SHA has a passing aggregate report. If this CI-only change requires Unity by
policy, verify both licensed suites run and their evidence is accepted.

## Rollback

Revert both implementation commits and the design/plan commits through a
reviewed PR, then run `npm run verify:local`. Branch protection does not need a
context change because the final display name remains stable.
