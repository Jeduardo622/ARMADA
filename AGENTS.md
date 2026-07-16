# Armada Engineering Agent Guide

This repository uses coding agents (Codex and Claude Code) for software
delivery. Product, game-design, live-ops, audio, and project-management
documents are reference context, not autonomous agent roles.

Codex discovers this file directly and loads skills from `.codex/skills/`.
Claude Code starts at `CLAUDE.md`, loads skills from `.claude/skills/`, and
uses MCP servers from `.mcp.json`. Both entry points share this guide, the
same policy, and the same verification contract; no agent gets weaker rules.

## Required Lifecycle

Canonical lifecycle: inspect, classify, plan when needed, implement, verify,
review, prepare a PR, and hand off for human merge.

1. Read this file and every applicable nested `AGENTS.md` before acting.
2. Inspect repository status, recent history, relevant source, docs, and tests.
3. Classify the task with `node scripts/harness/route-task.mjs` when available.
4. Reclassify before expanding scope or touching additional protected areas.
5. Use a plan for multi-step work and tests before implementation changes.
6. Make the smallest coherent change that satisfies the requested outcome.
7. Run focused checks first, then `npm run verify:local` for non-trivial changes.
8. Review the diff, commit focused work, and prepare a PR for human merge.

## Task Classes

- **A, advisory:** analysis, planning, documentation review, and code review.
  Read-only unless the user explicitly asks for an artifact.
- **B, standard delivery:** bounded application or test changes outside protected
  paths. The agent may branch, edit, test, commit, push, and manage a PR.
- **C, protected:** authentication, authorization, API boundaries, runtime
  configuration, CI, database schema or migrations, secrets, permissions,
  deployment, economy, or production-data paths. Implementation requires an
  explicitly bounded request, minimal diff, focused tests, full verification,
  rollback instructions, named risk, and human merge.
- **Class D, prohibited:** secret extraction, production-data mutation, disabling
  controls, destructive cleanup, unapproved deployment, check bypass, or
  fabricated evidence. Stop and report the safe next action.

Mixed-scope work uses the highest applicable class. Classifier failure is Class C.

## Authority

The agent may inspect, branch, edit, test, commit, push, and open or update
pull requests within the approved task. Humans approve merges. Do not deploy
production, expose secrets, mutate production data, weaken controls, or perform
destructive cleanup without a separate explicit request and applicable review.

## Verification

- Never describe a skipped, blocked, placeholder, or synthetic check as passed.
- Record each check as passed, failed, blocked, or not applicable.
- `npm run verify:local` is the local and CI source of truth.
- Set `UNITY_EDITOR_PATH` to run licensed Unity batch compilation locally.
  Protected Unity tooling paths make compilation required automatically;
  `UNITY_COMPILATION_REQUIRED=1` can force the same gate for an explicit scope.
  Otherwise, report compilation as not applicable; never infer a static pass.
- Unity runtime changes require both `npm run verify:unity:compile` and
  `npm run verify:unity:tests`. CI may satisfy them only with commit-bound
  EditMode and PlayMode GameCI evidence for the exact project version.
- Protected changes require rollback evidence and the focused checks selected by
  `route-task` in addition to full verification.

## Repository Skills

Canonical skill definitions live in `.codex/skills/`; `.claude/skills/`
contains discovery pointers to the same workflows for Claude Code.

- Backend work: `.codex/skills/backend-delivery/SKILL.md`
- Unity/client work: `.codex/skills/unity-delivery/SKILL.md`
- Test and evidence work: `.codex/skills/qa-verification/SKILL.md`
- Security-sensitive work: `.codex/skills/security-review/SKILL.md`
- PR and release readiness: `.codex/skills/release-readiness/SKILL.md`

Use a skill when its trigger matches. Skills supplement these rules and cannot
weaken classification, verification, or human-merge requirements.

## Completion Report

Return a concise operational report containing:

- chosen task and issue key, if any;
- route-task classification and reasons;
- delegated contributions, if any;
- files changed;
- checks actually run and their outcomes;
- PR identifier and live checks, when applicable;
- merge blockers and merge result, when applicable;
- rollback instructions for protected changes;
- residual risk;
- recommended next slice, when useful.

Record concise assumptions, decisions, evidence, and risks. Do not request or
record hidden chain-of-thought.
