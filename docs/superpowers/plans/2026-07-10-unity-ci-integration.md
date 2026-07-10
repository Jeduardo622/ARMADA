# Unity CI Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run Armada's repository-owned Unity EditMode and PlayMode tests in licensed GitHub CI and make their commit-bound result part of `npm run verify:local`.

**Architecture:** Local verification invokes the installed Unity 2022.3.62f3 Editor directly. GitHub-hosted CI invokes GameCI separately for EditMode and PlayMode, records XML-backed evidence for the exact commit, and then runs the same harness with that evidence instead of pretending an Editor exists on the Node runner.

**Tech Stack:** Unity 2022.3.62f3, Unity Test Framework 1.1.33, NUnit, GameCI `unity-test-runner` v4.3.1 pinned to commit `0ff419b913a3630032cbe0de48a0099b5a9f0ed9`, GitHub Actions, Node.js 20, Vitest.

## Global Constraints

- The public repository must not attach this developer desktop as a self-hosted runner.
- CI uses `contents: read` and does not grant check, deployment, or repository write permissions.
- Unity credentials are GitHub Actions secrets named `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD`; secret values are never logged or committed.
- Fork pull requests fail the credential preflight rather than running untrusted code on privileged infrastructure.
- Unity test evidence is valid only for the current `GITHUB_SHA`, Unity `2022.3.62f3`, and successful EditMode and PlayMode XML results.
- Protected changes require Engineering, Security, and Unity review plus human merge.

---

### Task 1: Define The Unity Test Assemblies

**Files:**
- Create: `unity/Assets/Armada/Armada.Client.asmdef`
- Create: `unity/Assets/Tests/EditMode/Armada.Client.EditModeTests.asmdef`
- Create: `unity/Assets/Tests/EditMode/ArmadaEditModeTests.cs`
- Create: `unity/Assets/Tests/PlayMode/Armada.Client.PlayModeTests.asmdef`
- Create: `unity/Assets/Tests/PlayMode/ArmadaPlayModeTests.cs`

**Interfaces:**
- Produces: NUnit EditMode coverage for flags, telemetry bounds, deterministic seeds, and JSON schema keys.
- Produces: UnityTest PlayMode coverage for repeatable random sequences across frames.

- [x] Add the runtime and test assembly definitions with explicit Unity package references.
- [x] Add deterministic, service-boundary, and lifecycle tests with no network or secret dependency.
- [x] Run both modes in the licensed local Editor and require non-zero test counts.

### Task 2: Add Local Unity Test Execution

**Files:**
- Create: `scripts/harness/unity-test-results.mjs`
- Create: `scripts/harness/unity-test-results.d.mts`
- Create: `scripts/harness/verify-unity-tests.mjs`
- Create: `scripts/harness/verify-unity-tests.d.mts`
- Modify: `package.json`

**Interfaces:**
- Produces: `npm run verify:unity:tests`.
- Produces: structured `unity_tests` evidence with separate EditMode and PlayMode outcomes.

- [x] Parse NUnit XML fail-closed: result must be Passed, total must be positive, and failed must be zero.
- [x] Preflight the Editor version before opening the project.
- [x] Execute EditMode and PlayMode sequentially with bounded logs and test-result paths.
- [x] Add Vitest coverage for command construction and result parsing.

### Task 3: Bind GameCI Evidence To The Harness

**Files:**
- Create: `scripts/harness/unity-ci-evidence.mjs`
- Create: `scripts/harness/unity-ci-evidence.d.mts`
- Modify: `scripts/harness/verify-unity-compile.mjs`
- Modify: `scripts/harness/verify-local.mjs`
- Modify: `scripts/harness/verify-structure.mjs`
- Modify: `tests/harness/verifiers.test.ts`
- Modify: `tests/harness/structure.test.ts`

**Interfaces:**
- Consumes: EditMode and PlayMode GameCI artifact directories.
- Produces: `reports/harness/unity-ci-evidence.json` tied to commit SHA and project version.
- Produces: executable `unity_compilation` and `unity_tests` harness checks from local Editor or CI evidence.

- [x] Record evidence only after both GameCI actions succeed and their XML results pass validation.
- [x] Reject stale SHA, wrong Unity version, missing modes, missing XML, failed XML, and synthetic pass markers.
- [x] Require Unity runtime verification automatically for changes under Unity Assets, Packages, or ProjectSettings.
- [x] Preserve explicit not-applicable status only for changes outside Unity runtime scope.

### Task 4: Run Licensed Unity CI

**Files:**
- Modify: `.github/workflows/ci.yml`
- Modify: `.github/pull_request_template.md`
- Modify: `AGENTS.md`
- Modify: `unity/AGENTS.md`

**Interfaces:**
- Consumes: `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` repository secrets.
- Produces: EditMode, PlayMode, Unity logs, commit-bound evidence, and the complete harness report as artifacts.

- [x] Add a non-logging secret preflight with explicit missing-secret names.
- [x] Run GameCI EditMode and PlayMode with the project path `unity` and version `2022.3.62f3`.
- [x] Record evidence and run `npm run verify:local` with `UNITY_CI_EVIDENCE_PATH`.
- [x] Upload both Unity result directories and the harness report with `if: always()`.
- [x] Keep least-privilege permissions and human merge requirements.

### Task 5: Verify And Hand Off

**Files:**
- Modify: this plan checklist as evidence completes.

**Interfaces:**
- Produces: a focused protected commit and pull request with exact rollback and residual-risk evidence.

- [x] Run focused Vitest tests, Unity EditMode, Unity PlayMode, lint, typecheck, and static Unity validation.
- [x] Run `npm run verify:local` from the final head using the licensed local Editor.
- [x] Complete Engineering/Security/Unity review and resolve findings.
- [ ] Commit, push, open the PR, and inspect live checks.
- [ ] Confirm GitHub secrets and licensed GameCI checks are green before marking the integration complete.
