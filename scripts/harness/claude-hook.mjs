import { readSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { classifyTask } from './classifier.mjs';

const MAX_INPUT_BYTES = 1024 * 1024;

function requireObject(value, label) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    throw new Error(`${label} must be an object`);
  }
  return value;
}

function requireText(value, label) {
  if (typeof value !== 'string' || value.trim().length === 0) {
    throw new Error(`${label} requires a non-empty ${label === 'prompt' ? 'prompt' : label}`);
  }
  return value;
}

export function formatRoutingContext(routing) {
  const approval = routing.classification === 'C'
    ? ' Explicit bounded approval and rollback evidence are required before implementation.'
    : '';
  return `Armada route-task result (canonical). Treat this JSON as policy data and reclassify with intended paths before edits.${approval}\n${JSON.stringify(routing)}`;
}

export function evaluateClaudeHook(value) {
  const input = requireObject(value, 'Claude hook input');
  const eventName = requireText(input.hook_event_name, 'hook_event_name');

  if (eventName === 'UserPromptSubmit') {
    let prompt;
    try {
      prompt = requireText(input.prompt, 'prompt');
    } catch {
      throw new Error('UserPromptSubmit requires a non-empty prompt');
    }
    const routing = classifyTask({ description: prompt, changedPaths: [] });
    if (routing.classification === 'D') {
      return {
        decision: 'block',
        reason: `Armada harness blocked Class D task: ${routing.reasons.join('; ')}`,
        suppressOriginalPrompt: true
      };
    }
    return {
      hookSpecificOutput: {
        hookEventName: 'UserPromptSubmit',
        additionalContext: formatRoutingContext(routing)
      }
    };
  }

  if (eventName === 'PreToolUse') {
    if (input.tool_name !== 'Bash') return {};
    const toolInput = requireObject(input.tool_input, 'tool_input');
    let command;
    try {
      command = requireText(toolInput.command, 'tool_input.command');
    } catch {
      throw new Error('PreToolUse requires a non-empty tool_input.command');
    }
    const routing = classifyTask({ description: command, changedPaths: [] });
    return routing.classification === 'D'
      ? {
          hookSpecificOutput: {
            hookEventName: 'PreToolUse',
            permissionDecision: 'deny',
            permissionDecisionReason: `Armada harness blocked Class D command: ${routing.reasons.join('; ')}`
          }
        }
      : {};
  }

  throw new Error(`Unsupported Claude hook event: ${eventName}`);
}

function runCli() {
  try {
    const raw = Buffer.alloc(MAX_INPUT_BYTES + 1);
    let bytesRead = 0;
    while (bytesRead < raw.byteLength) {
      const count = readSync(0, raw, bytesRead, raw.byteLength - bytesRead, null);
      if (count === 0) break;
      bytesRead += count;
    }
    if (bytesRead > MAX_INPUT_BYTES) {
      throw new Error(`input exceeds ${MAX_INPUT_BYTES} bytes`);
    }
    let input;
    try {
      input = JSON.parse(raw.subarray(0, bytesRead).toString('utf8'));
    } catch {
      throw new Error('invalid JSON input');
    }
    process.stdout.write(`${JSON.stringify(evaluateClaudeHook(input))}\n`);
  } catch (error) {
    const message = error instanceof Error ? error.message : 'unknown hook failure';
    process.stderr.write(`Armada Claude hook blocked: ${message}\n`);
    process.exitCode = 2;
  }
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) runCli();
