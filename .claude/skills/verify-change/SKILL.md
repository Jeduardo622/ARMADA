---
name: verify-change
description: Verify an Armada change with path-aware routing, focused checks, and the complete local evidence contract.
---

# Verify Change

1. Read root and applicable nested `AGENTS.md` files and the applicable
   `.codex/skills/*/SKILL.md` verification workflow.
2. Collect final changed paths from `git status --short` and the branch diff
   against the reviewed base. Preserve unrelated user changes.
3. Run `node scripts/harness/route-task.mjs` with the exact task description,
   one `--path` argument for every changed path, and `--json`.
4. Run the narrowest relevant regression first, then every focused check named
   in `requiredChecks`. Do not substitute a different check silently.
5. Set `HARNESS_TASK_DESCRIPTION` to the approved task description. For Class C,
   also set `HARNESS_ROLLBACK` to a concrete restore or revert action plus
   re-verification command.
6. Run `npm run verify:local` from the final head.
7. Read `reports/harness/latest.json` and report each check as passed, failed,
   blocked, not applicable, or not run. Never describe an unexecuted check as
   passed.
8. Stop on failed required checks, stale evidence, missing protected review, or
   scope that no longer matches the approved route.
