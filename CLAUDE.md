@AGENTS.md

## Claude Code

Use the repository harness through these native project skills:

- `/harness-help` explains the lifecycle and available entry points.
- `/route-task` classifies a task with its complete intended path set.
- `/verify-change` runs focused evidence and the final local contract.

Automatic prompt routing does not replace path-aware reclassification before
edits or whenever scope expands. Class D requests stop. Class C work requires an
explicitly bounded request, required reviewers and checks, rollback evidence,
and human merge exactly as defined by the imported guide.

Claude Code permissions remain the security boundary. Hooks add routing context,
deny recognized Class D operations, and request confirmation for protected paths
and commands when they run; they do not replace workspace trust or tool approval.

Delegate bounded work when useful to `tester`, `reviewer`, `ui-hardener`,
`test-isolation`, or `security-reviewer`. Treat their reports as evidence, not as
authority to widen scope. Invoke `/verify-change` before claiming completion.

## Quick Reference

- Classify every task: `node scripts/harness/route-task.mjs --description "<task>" --path <repo/path> --json`
- Full verification (local and CI source of truth): `npm run verify:local`
- Focused checks: `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`
- Humans approve all merges. Class C requires rollback evidence. Class D means
  stop and report the safe next action.

## Delivery Skills

Delivery skills in `.claude/skills/` (backend-delivery, unity-delivery,
qa-verification, security-review, release-readiness) are discovery pointers;
each one delegates to its canonical workflow in `.codex/skills/<name>/SKILL.md`.
Follow the canonical file. Skills cannot weaken classification, verification,
or human-merge rules.

## MCP Servers

`.mcp.json` registers the project-scoped Unity MCP server (started via
`scripts/harness/launch-unity-mcp.mjs`, requires `uvx`). Unity may be closed;
protected Unity verification still requires a licensed Editor separately and
is never satisfied by MCP availability.

## Boundaries

Do not deploy production, read or print secrets, mutate production data,
weaken controls, bypass checks, or describe unexecuted checks as passed.
These rules are enforced mechanically by `scripts/harness/` and CI.
