import { readSync } from 'node:fs';
import { isAbsolute, relative, resolve } from 'node:path';
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

function toolDecision(permissionDecision, permissionDecisionReason) {
  return {
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      permissionDecision,
      permissionDecisionReason
    }
  };
}

function projectRelativePath(input, filePath) {
  const root = process.env.CLAUDE_PROJECT_DIR || input.cwd || process.cwd();
  const absolute = isAbsolute(filePath) ? filePath : resolve(root, filePath);
  return relative(root, absolute).replaceAll('\\', '/');
}

function hasRecursiveForceRm(command) {
  const invocations = command.matchAll(
    /(?:^|[;&|(\r\n]\s*)(?:(?:sudo(?:\s+-[a-z]+)*|command)\s+|\/(?:usr\/)?bin\/)?rm\b([^;&|\r\n)]*)/gi
  );
  for (const invocation of invocations) {
    const tokens = invocation[1].trim().split(/\s+/);
    const optionBoundary = tokens.indexOf('--');
    const flags = (optionBoundary === -1 ? tokens : tokens.slice(0, optionBoundary))
      .map((token) => token.match(/^(['"])(-[a-z-]+)\1$/i)?.[2] ?? token)
      .map((token) => token.replace(/^\\(?=-)/, ''))
      .filter((token) => /^-(?:-[a-z-]+|[a-z]+)$/i.test(token));
    const hasRecursive = flags.some((flag) => flag.toLowerCase() === '--recursive' ||
      (!flag.startsWith('--') && /r/i.test(flag)));
    const hasForce = flags.some((flag) => flag.toLowerCase() === '--force' ||
      (!flag.startsWith('--') && /f/i.test(flag)));
    if (hasRecursive && hasForce) return true;
  }
  return false;
}

function isDestructiveShellVariant(command) {
  return /\bgit(?:\s+-C\s+\S+)?\s+reset\s+--hard\b/i.test(command) ||
    hasRecursiveForceRm(command) ||
    /\bRemove-Item\b(?=[^\r\n]*(?:-Recurse[^\r\n]*-Force|-Force[^\r\n]*-Recurse))/i.test(command);
}

function commandPathReferences(input, command) {
  const normalizedCommand = command.replace(
    /\$(?:PWD|\{PWD\}|CLAUDE_PROJECT_DIR|\{CLAUDE_PROJECT_DIR\})(?=[\\/])/g,
    '.'
  );
  const nested = normalizedCommand.match(/(?:[A-Za-z]:[\\/])?(?:\.{0,2}[\\/])?(?:[A-Za-z0-9_.-]+[\\/])+[A-Za-z0-9_.-]+/g) ?? [];
  const rootFiles = normalizedCommand.match(/(?:^|[\s'"`><=,(])(?:CLAUDE\.md|AGENTS\.md|package\.json|docker-compose\.yml|\.env(?:\.[A-Za-z0-9_.-]+)?)(?=$|[\s'"`<>,;)])/g) ?? [];
  const cleanedRootFiles = rootFiles.map((value) => value.replace(/^[\s'"`><=,(]+/, ''));
  const normalized = [...nested, ...cleanedRootFiles].map((path) => projectRelativePath(input, path));
  return [...new Set(normalized.flatMap((path) => [path, `${path}/`]))];
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
    const toolName = requireText(input.tool_name, 'tool_name');
    const toolInput = requireObject(input.tool_input, 'tool_input');

    if (toolName === 'Bash') {
      let command;
      try {
        command = requireText(toolInput.command, 'tool_input.command');
      } catch {
        throw new Error('PreToolUse requires a non-empty tool_input.command');
      }
      const routing = classifyTask({
        description: command,
        changedPaths: commandPathReferences(input, command)
      });
      if (routing.classification === 'D') {
        return toolDecision('deny', `Armada harness blocked Class D command: ${routing.reasons.join('; ')}`);
      }
      if (isDestructiveShellVariant(command)) {
        return toolDecision('deny', 'Armada harness blocked a destructive Bash command variant.');
      }
      if (routing.classification === 'C') {
        return toolDecision('ask', `Armada harness requires explicit permission for Class C command: ${routing.reasons.join('; ')}`);
      }
      return {};
    }

    if (toolName.startsWith('mcp__')) {
      return toolDecision('ask', `Armada harness requires explicit permission for MCP tool ${toolName}.`);
    }

    const pathField = toolName === 'NotebookEdit' ? 'notebook_path' : 'file_path';
    if (!['Edit', 'Write', 'NotebookEdit'].includes(toolName)) return {};
    const filePath = requireText(toolInput[pathField], `tool_input.${pathField}`);
    const changedPath = projectRelativePath(input, filePath);
    const routing = classifyTask({ description: `${toolName} repository file`, changedPaths: [changedPath] });
    if (routing.classification === 'D') {
      return toolDecision('deny', `Armada harness blocked Class D file mutation: ${routing.reasons.join('; ')}`);
    }
    if (routing.classification === 'C') {
      return toolDecision('ask', `Armada harness requires explicit permission for Class C path ${changedPath}: ${routing.reasons.join('; ')}`);
    }
    return {};
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
