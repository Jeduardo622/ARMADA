# Path-Aware CI Design

## Objective

Make ordinary pull-request verification secret-free without weakening Unity
protection. The required GitHub check remains `Verify local contract`, and
`npm run verify:local` remains the final local and CI authority.

## Architecture

The workflow has four jobs:

1. `scope` checks out the exact workflow SHA and runs a repository-owned scope
   resolver. It publishes whether licensed Unity evidence is required.
2. `core` runs the secret-free checks on every pull request and main push. It
   receives no Unity credentials.
3. `unity` runs EditMode and PlayMode only when a Unity runtime/tooling path
   changed or the event is a main push. It uploads test XML and commit-bound
   evidence as one artifact.
4. `verify` is named `Verify local contract`. It always runs after the other
   jobs, restores Unity evidence when available, and executes
   `npm run verify:local` against the original base SHA.

The final job intentionally repeats the repository contract after the parallel
checks. This keeps one authoritative report and prevents job-level success from
being mistaken for repository-level success.

## Scope Resolution

`scripts/harness/resolve-ci-scope.mjs` uses the same changed-path and Unity-path
rules as `verify-local.mjs`. It writes `unity_required=true|false` to the GitHub
output file and emits a JSON summary for diagnostics. Main pushes set
`FORCE_UNITY=1`, so every merged commit receives licensed Unity evidence.

## Failure Behavior

- A backend, documentation, dependency, database, or deployment pull request
  can pass without Unity secrets.
- An internal Unity pull request must produce passing EditMode and PlayMode
  evidence bound to the workflow SHA and project version.
- A fork Unity pull request receives no credentials. Its Unity job fails the
  credential preflight, and the aggregate also fails because required evidence
  is absent.
- Failed or skipped core verification makes the aggregate fail.
- Missing, stale, modified, or wrong-version Unity evidence makes the aggregate
  fail when Unity is required.
- Artifacts are uploaded on failure without exposing credentials.

No `pull_request_target`, self-hosted runner, write permission, production
credential, or check bypass is introduced.

## Verification

Repository tests must prove:

- backend/docs paths do not require Unity;
- Unity assets, packages, project settings, and project MCP config do;
- `FORCE_UNITY=1` requires Unity on main;
- the workflow contains all four jobs and keeps least-privilege permissions;
- Unity secrets appear only in the Unity job;
- the final job retains the exact required-check name and runs
  `npm run verify:local` with restored commit-bound evidence;
- fork Unity changes fail closed rather than silently skipping verification.

## Acceptance Criteria

1. A non-Unity pull request reaches a passing `Verify local contract` without
   reading Unity secrets or running GameCI.
2. A Unity pull request runs both licensed suites and the aggregate validates
   their evidence.
3. Every main push runs both licensed suites.
4. The required branch-protection context remains unchanged.
5. Local `npm run verify:local` behavior remains unchanged.
6. Focused harness tests and the complete local verification contract pass.

## Rollback

Revert the workflow, resolver, declarations, and tests together. Restore the
single-job workflow and rerun `npm run verify:local`. No production state or
credentials are changed by this design.
