# Manual PR head check

Immediately before human merge, verify that the intended, reviewed and tested
commit is still the current GitHub PR head:

```text
node scripts/harness/verify-pr-head.mjs --repo Jeduardo622/ARMADA --pr <PR-number> --sha <reviewed-full-40-character-SHA>
```

Requires Node.js and GitHub CLI (`gh`) authenticated to github.com with read access
to the repository. Supply the SHA from the review/verification evidence. Do not
derive it from the current remote head just to make this check pass. The explicit
repository and PR identify the target independently of the local branch or cwd.

Exit 0 means the open PR's current GitHub head matches. Exit 1 means stop: input
is invalid, GitHub could not be queried, its response is invalid, the PR is no
longer open, or its head changed. A mismatch prints both expected and observed
SHAs. Review and verify the new commit before changing the intended SHA.
Lookup failures deliberately omit raw CLI output, which could contain sensitive
diagnostic data. The read-only lookup times out after 30 seconds.

This is a snapshot, not an atomic merge guard, CI attestation, approval, or merge
authorization. A push can occur after success. Recheck immediately before human
merge; use an expected-head constraint in the human's merge operation when
available. Existing review, branch protection and exact-commit verification
requirements still apply.

The live lookup stays local/manual. `.github/workflows/ci.yml` runs on PR updates
and main pushes and has no human merge-time integration point. An earlier CI
snapshot would not establish freshness at merge time. Deterministic regression
tests run in the existing `test:harness` and full test suites without GitHub access.

## Protected review and rollback

Class C (`engineering_harness`); required reviewers: Engineering and Security.
Scope: this document, `scripts/harness/verify-pr-head.mjs`, its `.d.mts`
declaration and `tests/harness/verify-pr-head.test.ts`. No workflow, permissions,
dependencies, production state or merge settings change.

Verification: run `npx vitest run tests/harness/verify-pr-head.test.ts`, then
`npm run verify:local` with `HARNESS_TASK_DESCRIPTION` describing this task and
`HARNESS_ROLLBACK` describing the following rollback. Inspect
`reports/harness/latest.json`; report skipped or unavailable checks explicitly.

Rollback: revert the commit adding these four files through a reviewed PR, then
rerun `npm run verify:local`. Since the command has no automatic integration or
side effects, reverting it requires no infrastructure or data restoration.
Residual risk: the manual check can be omitted and the remote head can advance
after it succeeds. Human merge remains required.
