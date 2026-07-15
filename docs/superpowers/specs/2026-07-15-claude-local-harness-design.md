# Claude Code Local Harness Integration Design

## Goal

Make Armada's existing AI engineering harness complete and useful in local
Claude Code sessions without changing required CI or adding Anthropic secrets.
Claude Code must consume the same task classification, protected-path policy,
verification contract, and human-merge rules as Codex.

## Selected Approach

Use native Claude Code project instructions, skills, subagents, and hooks as
adapters over Armada's existing provider-neutral harness. Keep `AGENTS.md`,
`scripts/harness/policy.json`, and the scripts under `scripts/harness/` as the
canonical policy and verification implementation.

Alternatives rejected:

1. A `CLAUDE.md` file alone would make the repository readable in Claude Code
   but would not provide native workflows or deterministic enforcement.
2. Generalizing the Codex shadow evaluator into a multi-provider CI framework
   would exceed the requested local-only scope and introduce credentials,
   workflows, and cost controls.

## Architecture

### Shared instructions

The root `CLAUDE.md` imports `AGENTS.md` and adds only Claude-specific navigation.
It tells Claude Code to use the repository skills and agents, explains the
automatic hook behavior, and preserves the existing Class A-D authority model.
It does not duplicate the engineering policy.

### Native skills

Project skills under `.claude/skills/` expose three user-facing workflows:

- `/route-task`: classify a proposed task and intended paths before edits;
- `/verify-change`: derive the required checks from the final changed paths,
  run focused checks first, and run `npm run verify:local` when required;
- `/harness-help`: explain the lifecycle, available specialists, commands, and
  evidence expectations.

Skills call or direct Claude to the canonical scripts. They must not restate or
fork classifier policy.

### Specialist agents

Project agents under `.claude/agents/` provide the roles referenced by the root
guide:

- `tester` for regression design and verification evidence;
- `reviewer` for independent diff review;
- `ui-hardener` for Unity UI resilience and accessibility;
- `test-isolation` for flaky or nondeterministic tests;
- `security-reviewer` for protected trust-boundary and rollback review.

Each agent starts by reading the applicable `AGENTS.md` and canonical `.codex`
skill where one exists. Agents remain read-only unless their delegated task and
the routed class permit edits. They report findings and executed evidence back
to the main Claude session.

### Deterministic hooks

`.claude/settings.json` registers a cross-platform Node hook adapter:

- `UserPromptSubmit` classifies the submitted prompt using the canonical
  classifier and injects the exact routing result into Claude's context.
- A Class D prompt exits with Claude Code's blocking status and returns the safe
  stop reason. The user's prompt is not executed.
- Class A-C prompts continue. Class C context explicitly identifies reviewers,
  required checks, and the need for bounded approval and rollback evidence.
- `PreToolUse` inspects Bash commands with the same prohibited-intent policy and
  blocks commands classified as Class D. Normal Claude permissions still apply
  to every other command.

The hook fails closed for malformed input or classifier failures: it blocks the
affected prompt/tool call and reports that routing was unavailable. It never
reads secrets, writes repository state, runs tests, or grants permissions.

### Structure verification

Claude integration files and hook configuration become required harness
structure. Focused tests validate:

- required files and valid frontmatter;
- `CLAUDE.md` imports `AGENTS.md` rather than copying policy;
- settings use only the intended hook events and the repository Node adapter;
- hook output for Class A-C routing;
- Class D prompt and Bash blocking;
- fail-closed malformed input;
- no automatic verification, secret access, or permission escalation.

## Local Workflow

1. Start Claude Code at the repository root.
2. Claude loads `CLAUDE.md`, which imports the root guide.
3. Every submitted task is classified automatically.
4. Claude may use `/route-task` to reclassify with intended paths before scope
   expands.
5. Claude delegates bounded test, review, UI, isolation, or security work to the
   matching project agent when useful.
6. `/verify-change` selects focused checks and the full local contract.
7. Claude hands protected work off with exact evidence, rollback instructions,
   required reviewers, and human merge.

## Security and Failure Handling

- Class D is always blocked by the hook.
- Class C is not silently converted into standard delivery. It is surfaced with
  the canonical restrictions and may proceed only after explicit bounded user
  approval, such as the approval for this integration.
- Hooks do not auto-approve tools or use `dangerously-skip-permissions`.
- No API keys, Claude GitHub Action, network calls, or CI changes are included.
- Hook errors block instead of allowing an unclassified operation.
- Repository scripts receive untrusted prompt/tool values as data arguments;
  the hook never evaluates them as shell code.

## Verification

Required evidence for this protected change:

1. A failing focused test before implementation.
2. Passing focused Claude harness tests.
3. `npm run verify:structure`.
4. `npm run verify:policy`.
5. `npm run verify:secrets`.
6. `npm run test:harness`.
7. `npm run lint` and `npm run typecheck`.
8. `npm run verify:local` with the Class C task description and rollback
   metadata required by the existing verifier.
9. Independent Engineering and Security review of the final diff.

## Rollback

Revert the integration commit, which removes `CLAUDE.md`, `.claude/`, the hook
adapter, its tests, and the related structure/policy entries. Then rerun
`npm run verify:local` to confirm the pre-integration harness remains intact.

## Non-goals

- Claude Code GitHub Actions or CI execution;
- Anthropic API keys, billing, or model configuration;
- Claude shadow evaluations or Codex evaluator generalization;
- changing existing Codex behavior;
- production deployment or data access.
