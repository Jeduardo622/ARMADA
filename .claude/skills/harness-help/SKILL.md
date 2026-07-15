---
name: harness-help
description: Explain how to navigate and use Armada's local AI engineering harness in Claude Code.
---

# Armada Harness Help

Explain the canonical lifecycle from `AGENTS.md`: inspect, classify, plan when
needed, implement, verify, review, prepare a PR, and hand off for human merge.

Point users to:

- `scripts/harness/policy.json` for task classes and protected scopes;
- `node scripts/harness/route-task.mjs --description <text> --path <path> --json`
  for routing;
- `/route-task` for guided path-aware classification;
- `/verify-change` and `npm run verify:local` for final evidence;
- `.codex/skills/` for canonical delivery and review procedures;
- `tester`, `reviewer`, `ui-hardener`, `test-isolation`, and
  `security-reviewer` for bounded specialist work.

Keep executed, blocked, not-applicable, and not-run checks separate. Explain
that local Claude integration does not add Claude CI or shadow evaluations.
