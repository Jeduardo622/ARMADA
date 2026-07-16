export declare const DEFAULT_CLAUDE_SHADOW_MODEL: string;
export declare const CLAUDE_SHADOW_TOOL_NAME: string;
export declare const MAX_CLAUDE_SHADOW_RESPONSE_BYTES: number;

export interface ClaudeShadowTool {
  name: string;
  description: string;
  input_schema: Record<string, unknown>;
}

export interface ClaudeShadowRequestBody {
  model: string;
  max_tokens: number;
  messages: Array<{ role: string; content: string }>;
  tools: ClaudeShadowTool[];
  tool_choice: { type: string; name: string };
}

export declare function buildClaudeShadowRequestBody(
  prompt: string,
  schema: Record<string, unknown>,
  model?: string
): ClaudeShadowRequestBody;

export declare function extractClaudeShadowResult(apiResponse: unknown): string;
