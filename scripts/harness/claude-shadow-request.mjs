import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { pathToFileURL } from "node:url";

export const DEFAULT_CLAUDE_SHADOW_MODEL = "claude-sonnet-5";
export const CLAUDE_SHADOW_TOOL_NAME = "submit_shadow_response";
export const MAX_CLAUDE_SHADOW_RESPONSE_BYTES = 65536;
const ANTHROPIC_MESSAGES_URL = "https://api.anthropic.com/v1/messages";
const ANTHROPIC_VERSION = "2023-06-01";
const REQUEST_TIMEOUT_MS = 600_000;
const MODEL_PATTERN = /^[A-Za-z0-9._-]{1,128}$/;

export function buildClaudeShadowRequestBody(prompt, schema, model = DEFAULT_CLAUDE_SHADOW_MODEL) {
  if (typeof prompt !== "string" || prompt.length === 0) throw new Error("shadow prompt must be a non-empty string");
  if (schema === null || typeof schema !== "object" || Array.isArray(schema)) throw new Error("shadow response schema must be an object");
  if (typeof model !== "string" || !MODEL_PATTERN.test(model)) throw new Error("shadow model identifier is invalid");
  return {
    model,
    max_tokens: 4096,
    messages: [{ role: "user", content: prompt }],
    tools: [{
      name: CLAUDE_SHADOW_TOOL_NAME,
      description: "Submit the complete structured shadow evaluation response. Provide only schema-defined fields.",
      input_schema: schema,
    }],
    tool_choice: { type: "tool", name: CLAUDE_SHADOW_TOOL_NAME },
  };
}

export function extractClaudeShadowResult(apiResponse) {
  const content = Array.isArray(apiResponse?.content) ? apiResponse.content : [];
  const toolUses = content.filter((block) => block?.type === "tool_use" && block?.name === CLAUDE_SHADOW_TOOL_NAME);
  if (toolUses.length !== 1 || toolUses[0].input === null || typeof toolUses[0].input !== "object") {
    throw new Error("Claude shadow response does not contain exactly one structured tool result");
  }
  const serialized = `${JSON.stringify(toolUses[0].input, null, 2)}\n`;
  const size = Buffer.byteLength(serialized, "utf8");
  if (size < 1 || size > MAX_CLAUDE_SHADOW_RESPONSE_BYTES) {
    throw new Error(`Claude shadow response size ${size} is outside the 1..${MAX_CLAUDE_SHADOW_RESPONSE_BYTES} byte transport bound`);
  }
  return serialized;
}

async function requestClaudeShadowResponse(body, apiKey) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
  try {
    const response = await fetch(ANTHROPIC_MESSAGES_URL, {
      method: "POST",
      headers: {
        "x-api-key": apiKey,
        "anthropic-version": ANTHROPIC_VERSION,
        "content-type": "application/json",
      },
      body: JSON.stringify(body),
      signal: controller.signal,
    });
    if (!response.ok) throw new Error(`Anthropic API request failed with status ${response.status}`);
    return await response.json();
  } finally {
    clearTimeout(timeout);
  }
}

function argument(name, fallback) {
  const index = process.argv.indexOf(name);
  if (index < 0 || !process.argv[index + 1]) {
    if (fallback !== undefined) return fallback;
    throw new Error(`missing ${name}`);
  }
  return process.argv[index + 1];
}

async function main() {
  const promptPath = resolve(argument("--prompt"));
  const schemaPath = resolve(argument("--schema"));
  const outputPath = resolve(argument("--output"));
  const model = argument("--model", DEFAULT_CLAUDE_SHADOW_MODEL);
  const apiKey = process.env.ANTHROPIC_API_KEY;
  if (typeof apiKey !== "string" || apiKey.length === 0) throw new Error("ANTHROPIC_API_KEY is not configured");
  const prompt = await readFile(promptPath, "utf8");
  const schema = JSON.parse(await readFile(schemaPath, "utf8"));
  const body = buildClaudeShadowRequestBody(prompt, schema, model);
  const apiResponse = await requestClaudeShadowResponse(body, apiKey);
  const serialized = extractClaudeShadowResult(apiResponse);
  await mkdir(dirname(outputPath), { recursive: true });
  await writeFile(outputPath, serialized, "utf8");
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch((error) => {
    // Redact everything except the local error message; never echo request or response payloads.
    console.error(error instanceof Error ? error.message : "Claude shadow request failed");
    process.exitCode = 1;
  });
}
