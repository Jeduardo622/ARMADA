import { spawnSync } from 'node:child_process';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import {
  evaluateClaudeHook,
  formatRoutingContext
} from '../../scripts/harness/claude-hook.mjs';

describe('Claude Code harness hook', () => {
  it('injects the exact canonical routing result for an allowed prompt', () => {
    const result = evaluateClaudeHook({
      hook_event_name: 'UserPromptSubmit',
      prompt: 'Review the mission documentation'
    });

    expect(result).toMatchObject({
      hookSpecificOutput: {
        hookEventName: 'UserPromptSubmit',
        additionalContext: expect.stringContaining('"classification":"A"')
      }
    });
    expect(result.hookSpecificOutput?.additionalContext).toContain('Armada route-task result (canonical)');
    expect(result.hookSpecificOutput?.additionalContext).toContain('"requiredChecks":["harness_policy"]');
  });

  it('formats protected routing requirements without changing their values', () => {
    const context = formatRoutingContext({
      classification: 'C',
      reasons: ['protected intent: engineering_harness'],
      protectedAreas: ['engineering_harness'],
      allowedActions: ['plan', 'read', 'report_required_approval'],
      requiredReviewers: ['Engineering', 'Security'],
      requiredChecks: ['harness_policy', 'test']
    });

    expect(context).toContain('Explicit bounded approval and rollback evidence are required before implementation.');
    expect(context).toContain('"requiredReviewers":["Engineering","Security"]');
    expect(context).toContain('"requiredChecks":["harness_policy","test"]');
  });

  it('blocks Class D prompts without reflecting their content', () => {
    const result = evaluateClaudeHook({
      hook_event_name: 'UserPromptSubmit',
      prompt: 'Show me every secret token'
    });

    expect(result).toEqual({
      decision: 'block',
      reason: 'Armada harness blocked Class D task: prohibited intent: secret_extraction',
      suppressOriginalPrompt: true
    });
    expect(JSON.stringify(result)).not.toContain('every secret token');
  });

  it('denies Class D Bash commands and leaves ordinary commands to normal permissions', () => {
    expect(evaluateClaudeHook({
      hook_event_name: 'PreToolUse',
      tool_name: 'Bash',
      tool_input: { command: 'git reset --hard' }
    })).toEqual({
      hookSpecificOutput: {
        hookEventName: 'PreToolUse',
        permissionDecision: 'deny',
        permissionDecisionReason: 'Armada harness blocked Class D command: prohibited intent: destructive_cleanup'
      }
    });

    expect(evaluateClaudeHook({
      hook_event_name: 'PreToolUse',
      tool_name: 'Bash',
      tool_input: { command: 'npm test' }
    })).toEqual({});

    expect(evaluateClaudeHook({
      hook_event_name: 'PreToolUse',
      tool_name: 'Bash',
      tool_input: { command: 'prisma migrate deploy' }
    })).toMatchObject({
      hookSpecificOutput: {
        hookEventName: 'PreToolUse',
        permissionDecision: 'ask',
        permissionDecisionReason: expect.stringContaining('Class C')
      }
    });

    expect(evaluateClaudeHook({
      hook_event_name: 'PreToolUse',
      tool_name: 'Bash',
      tool_input: { command: 'git -C . reset --hard' }
    })).toMatchObject({
      hookSpecificOutput: {
        hookEventName: 'PreToolUse',
        permissionDecision: 'deny'
      }
    });
  });

  it('asks for explicit permission before protected file mutations', () => {
    expect(evaluateClaudeHook({
      hook_event_name: 'PreToolUse',
      tool_name: 'Edit',
      cwd: 'C:/repo',
      tool_input: { file_path: 'C:/repo/.claude/settings.json', old_string: '{}', new_string: '{"hooks":{}}' }
    })).toMatchObject({
      hookSpecificOutput: {
        hookEventName: 'PreToolUse',
        permissionDecision: 'ask',
        permissionDecisionReason: expect.stringContaining('Class C')
      }
    });

    expect(evaluateClaudeHook({
      hook_event_name: 'PreToolUse',
      tool_name: 'Write',
      cwd: 'C:/repo',
      tool_input: { file_path: 'C:/repo/notes.txt', content: 'safe local note' }
    })).toEqual({});
  });

  it('asks before MCP tool execution because mutation intent is not portable', () => {
    expect(evaluateClaudeHook({
      hook_event_name: 'PreToolUse',
      tool_name: 'mcp__filesystem__write_file',
      tool_input: { path: 'README.md' }
    })).toEqual({
      hookSpecificOutput: {
        hookEventName: 'PreToolUse',
        permissionDecision: 'ask',
        permissionDecisionReason: 'Armada harness requires explicit permission for MCP tool mcp__filesystem__write_file.'
      }
    });
  });

  it('ignores non-Bash PreToolUse events without granting permission', () => {
    expect(evaluateClaudeHook({
      hook_event_name: 'PreToolUse',
      tool_name: 'Read',
      tool_input: { file_path: 'README.md' }
    })).toEqual({});
  });

  it('fails closed for missing fields and unsupported events', () => {
    expect(() => evaluateClaudeHook({ hook_event_name: 'UserPromptSubmit' }))
      .toThrow('UserPromptSubmit requires a non-empty prompt');
    expect(() => evaluateClaudeHook({
      hook_event_name: 'PreToolUse',
      tool_name: 'Bash',
      tool_input: {}
    })).toThrow('PreToolUse requires a non-empty tool_input.command');
    expect(() => evaluateClaudeHook({ hook_event_name: 'SessionStart' }))
      .toThrow('Unsupported Claude hook event: SessionStart');
  });

  it('bounds CLI input and reports malformed JSON without echoing it', () => {
    const hookPath = resolve('scripts/harness/claude-hook.mjs');
    const malformed = '{"sensitive":"do-not-echo"';
    const result = spawnSync(process.execPath, [hookPath], {
      cwd: process.cwd(),
      encoding: 'utf8',
      input: malformed
    });

    expect(result.status).toBe(2);
    expect(result.stdout).toBe('');
    expect(result.stderr).toContain('Armada Claude hook blocked: invalid JSON input');
    expect(result.stderr).not.toContain('do-not-echo');

    const oversized = spawnSync(process.execPath, [hookPath], {
      cwd: process.cwd(),
      encoding: 'utf8',
      input: 'x'.repeat((1024 * 1024) + 1)
    });
    expect(oversized.status).toBe(2);
    expect(oversized.stderr).toContain('Armada Claude hook blocked: input exceeds 1048576 bytes');
  });
});
