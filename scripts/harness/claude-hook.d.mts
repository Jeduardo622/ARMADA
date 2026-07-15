import type { ClassificationResult } from './classifier.mjs';

export interface ClaudeHookInput {
  hook_event_name?: string;
  prompt?: string;
  tool_name?: string;
  tool_input?: Record<string, unknown>;
  [key: string]: unknown;
}

export interface ClaudeHookOutput {
  decision?: 'block';
  reason?: string;
  suppressOriginalPrompt?: boolean;
  hookSpecificOutput?: {
    hookEventName: 'UserPromptSubmit' | 'PreToolUse';
    additionalContext?: string;
    permissionDecision?: 'deny';
    permissionDecisionReason?: string;
  };
}

export function formatRoutingContext(routing: ClassificationResult): string;
export function evaluateClaudeHook(input: ClaudeHookInput): ClaudeHookOutput;
