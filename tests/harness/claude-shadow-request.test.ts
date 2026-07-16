import { describe, expect, it } from 'vitest';
import {
  buildClaudeShadowRequestBody,
  extractClaudeShadowResult,
  CLAUDE_SHADOW_TOOL_NAME,
  DEFAULT_CLAUDE_SHADOW_MODEL,
  MAX_CLAUDE_SHADOW_RESPONSE_BYTES
} from '../../scripts/harness/claude-shadow-request.mjs';

const schema = { type: 'object', properties: { results: { type: 'array' } } };

describe('buildClaudeShadowRequestBody', () => {
  it('builds a forced-tool request pinned to the default model', () => {
    const body = buildClaudeShadowRequestBody('prompt text', schema);
    expect(body.model).toBe(DEFAULT_CLAUDE_SHADOW_MODEL);
    expect(body.messages).toEqual([{ role: 'user', content: 'prompt text' }]);
    expect(body.tools).toHaveLength(1);
    expect(body.tools[0].name).toBe(CLAUDE_SHADOW_TOOL_NAME);
    expect(body.tools[0].input_schema).toBe(schema);
    expect(body.tool_choice).toEqual({ type: 'tool', name: CLAUDE_SHADOW_TOOL_NAME });
  });

  it('rejects empty prompts, non-object schemas, and unsafe model identifiers', () => {
    expect(() => buildClaudeShadowRequestBody('', schema)).toThrow(/prompt/);
    expect(() => buildClaudeShadowRequestBody('p', [] as unknown as Record<string, unknown>)).toThrow(/schema/);
    expect(() => buildClaudeShadowRequestBody('p', schema, 'bad model!')).toThrow(/model/);
  });
});

describe('extractClaudeShadowResult', () => {
  it('returns the serialized input of exactly one matching tool_use block', () => {
    const result = extractClaudeShadowResult({
      content: [
        { type: 'text', text: 'ignored' },
        { type: 'tool_use', name: CLAUDE_SHADOW_TOOL_NAME, input: { schemaVersion: 1, results: [] } }
      ]
    });
    expect(JSON.parse(result)).toEqual({ schemaVersion: 1, results: [] });
    expect(result.endsWith('\n')).toBe(true);
  });

  it('rejects responses without exactly one structured tool result', () => {
    expect(() => extractClaudeShadowResult({ content: [] })).toThrow(/exactly one/);
    expect(() => extractClaudeShadowResult({
      content: [
        { type: 'tool_use', name: CLAUDE_SHADOW_TOOL_NAME, input: {} },
        { type: 'tool_use', name: CLAUDE_SHADOW_TOOL_NAME, input: {} }
      ]
    })).toThrow(/exactly one/);
    expect(() => extractClaudeShadowResult({ content: [{ type: 'tool_use', name: CLAUDE_SHADOW_TOOL_NAME, input: null }] })).toThrow(/exactly one/);
  });

  it('rejects results outside the transport byte bound', () => {
    const oversized = { payload: 'x'.repeat(MAX_CLAUDE_SHADOW_RESPONSE_BYTES) };
    expect(() => extractClaudeShadowResult({
      content: [{ type: 'tool_use', name: CLAUDE_SHADOW_TOOL_NAME, input: oversized }]
    })).toThrow(/transport bound/);
  });
});
