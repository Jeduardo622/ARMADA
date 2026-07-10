---
name: release-readiness
description: Validate Armada branch, CI, artifact, rollback, pull request, and human-merge readiness.
---

# Release Readiness

## Trigger

Use before opening, updating, or handing off a pull request.

## Workflow

1. Confirm branch, base, diff scope, and live PR state when present.
2. Run `npm run verify:local` from the final head.
3. Inspect the generated harness report and distinguish all blocked checks.
4. Confirm protected reviewers, rollback evidence, and dependency exceptions.
5. Push and update the PR only after the evidence matches the final diff.
6. Leave merge to a human and report exact live blockers.

## Stop Conditions

Stop for a dirty unrelated diff, stale verification, failed required checks,
unapproved protected scope, missing rollback evidence, or unavailable credentials.
