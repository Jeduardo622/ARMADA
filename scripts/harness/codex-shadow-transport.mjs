import { appendFile, mkdir, readFile, rm, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { pathToFileURL } from "node:url";

export const MAX_SHADOW_RESPONSE_BYTES = 65536;
const BASE64_PATTERN = /^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/;

export async function encodeShadowResponse(inputPath, githubOutputPath) {
  const input = resolve(inputPath);
  try {
    const response = await readFile(input);
    if (response.length < 1 || response.length > MAX_SHADOW_RESPONSE_BYTES) {
      throw new Error(`Codex response size ${response.length} is outside the 1..${MAX_SHADOW_RESPONSE_BYTES} byte transport bound`);
    }
    await appendFile(resolve(githubOutputPath), `response-b64=${response.toString("base64")}\n`, "utf8");
  } finally {
    await rm(input, { force: true });
  }
}

export function decodeShadowResponse(encoded) {
  if (typeof encoded !== "string" || encoded.length === 0 || encoded.length % 4 !== 0 || !BASE64_PATTERN.test(encoded)) {
    throw new Error("Codex response transport envelope is missing or malformed");
  }
  const response = Buffer.from(encoded, "base64");
  if (response.length < 1 || response.length > MAX_SHADOW_RESPONSE_BYTES || response.toString("base64") !== encoded) {
    throw new Error(`Decoded Codex response is outside the 1..${MAX_SHADOW_RESPONSE_BYTES} byte transport bound`);
  }
  return response;
}

export async function writeDecodedShadowResponse(encoded, outputPath) {
  const output = resolve(outputPath);
  await mkdir(dirname(output), { recursive: true });
  try {
    await writeFile(output, decodeShadowResponse(encoded));
  } catch (error) {
    await writeFile(output, Buffer.alloc(0));
    throw error;
  }
}

function argument(name) {
  const index = process.argv.indexOf(name);
  if (index < 0 || !process.argv[index + 1]) throw new Error(`missing ${name}`);
  return process.argv[index + 1];
}

async function main() {
  const command = process.argv[2];
  if (command === "encode") {
    await encodeShadowResponse(argument("--input"), argument("--github-output"));
    return;
  }
  if (command === "decode") {
    await writeDecodedShadowResponse(process.env.CODEX_SHADOW_RESPONSE_B64 || "", argument("--output"));
    return;
  }
  throw new Error("usage: codex-shadow-transport.mjs <encode|decode> [options]");
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
