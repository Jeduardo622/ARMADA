# Task 3 Report

- Scope: manual, protected-environment shadow Codex evaluation workflow, prompt, structure enforcement, and protected routing policy.
- Classification: Class C (`ci`, `codex_evals`, and `engineering_harness`); required reviewers are Engineering and Security.
- TDD red: `npm run test:harness -- --run tests/harness/structure.test.ts` failed because `.github/workflows/codex-shadow-evals.yml` was absent. The run also observed the expected parallel Task 2 missing-module failure before Task 2 landed.
- TDD green: the same focused command passed after implementation (7 files, 106 tests).
- Workflow boundary: `workflow_dispatch` only; trusted `refs/heads/main` evaluation; exact `${{ github.sha }}` checkout; read-only contents permission; credentials not persisted; protected `codex-shadow-evals` environment.
- Codex boundary: official action pinned to `52fe01ec70a42f454c9d2ebd47598f9fd6893d56`, CLI `0.144.1`, `gpt-5.3-codex`, medium effort, `drop-sudo`, `:read-only`, strict committed schema, ephemeral execution, and action-last placement.
- Evidence boundary: the secret-free grade job runs under `always()`, uploads only sanitized reports for 14 days, and propagates grader infrastructure failure after artifact upload.
- Verification passed: `npm run verify:structure`, `npm run verify:harness`, `npm run verify:policy`, and `npm run verify:secrets`.
- Rollback: revert the Task 3 commit; after the workflow is removed, delete the `codex-shadow-evals` GitHub environment if it was created.
- Residual risk: hosted behavior remains unproven until human merge, protected-environment configuration, and a manual dispatch from merged `main`.
