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

function exactKeys(value, keys) {
  return value !== null && typeof value === "object" && !Array.isArray(value) &&
    JSON.stringify(Object.keys(value).sort()) === JSON.stringify([...keys].sort());
}

export async function combineShadowResponses(inputDir, suitePath, outputPath) {
  const input = resolve(inputDir);
  const output = resolve(outputPath);
  let fixtureIds = [];
  try {
    const suite = JSON.parse(await readFile(resolve(suitePath), "utf8"));
    if (!Array.isArray(suite?.cases) || suite.cases.length !== 10 || typeof suite.suiteVersion !== "string") {
      throw new Error("trusted suite is invalid for response aggregation");
    }
    const candidateFixtureIds = suite.cases.map(({ id }) => id);
    if (new Set(candidateFixtureIds).size !== candidateFixtureIds.length || candidateFixtureIds.some((id) => !/^[a-z0-9-]{1,64}$/.test(id))) {
      throw new Error("trusted suite fixture IDs are invalid for response aggregation");
    }
    fixtureIds = candidateFixtureIds;
    const results = [];
    for (const fixtureId of fixtureIds) {
      const raw = await readFile(resolve(input, `${fixtureId}.json`));
      if (raw.length < 1 || raw.length > MAX_SHADOW_RESPONSE_BYTES) throw new Error(`response for ${fixtureId} is outside the transport bound`);
      const response = JSON.parse(raw.toString("utf8"));
      if (!exactKeys(response, ["results", "schemaVersion", "suiteVersion"]) || response.schemaVersion !== 1 ||
          response.suiteVersion !== suite.suiteVersion || !Array.isArray(response.results) || response.results.length !== 1 ||
          response.results[0]?.fixtureId !== fixtureId) {
        throw new Error(`response envelope for ${fixtureId} is invalid`);
      }
      results.push(response.results[0]);
    }
    const combined = Buffer.from(`${JSON.stringify({ schemaVersion: 1, suiteVersion: suite.suiteVersion, results }, null, 2)}\n`);
    if (combined.length > MAX_SHADOW_RESPONSE_BYTES) throw new Error("combined response exceeds the transport bound");
    await mkdir(dirname(output), { recursive: true });
    await writeFile(output, combined);
  } catch (error) {
    await rm(output, { force: true });
    throw error;
  } finally {
    await Promise.all(fixtureIds.map((fixtureId) => rm(resolve(input, `${fixtureId}.json`), { force: true })));
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
  if (command === "combine") {
    await combineShadowResponses(argument("--input-dir"), argument("--suite"), argument("--output"));
    return;
  }
  throw new Error("usage: codex-shadow-transport.mjs <encode|decode|combine> [options]");
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
