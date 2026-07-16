---
name: qa-verification
description: Design Armada regressions, deterministic fixtures, test isolation, verification selection, and evidence reports.
---

# QA Verification

## Trigger

Use for tests, regressions, flaky behavior, harness fixtures, or release evidence.

## Workflow

1. Identify the user-visible or policy behavior and its smallest reliable test.
2. Reproduce the failure and preserve exact command output.
3. Write the failing test before implementation and verify the expected failure.
4. Keep fixtures independent of local `.env`, secrets, clock drift, and services.
5. Run focused checks, then `npm run verify:local`.
6. Separate executed proof from blocked, unavailable, and not-applicable checks.

## Stop Conditions

Stop when the failure cannot be reproduced, required infrastructure is absent, or
the proposed test would conceal nondeterminism instead of controlling it.
