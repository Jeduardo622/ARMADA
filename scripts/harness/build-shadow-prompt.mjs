import { chmod, mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { pathToFileURL } from "node:url";

export const SHADOW_PROMPT_CONTEXT_PATHS = Object.freeze([
  "AGENTS.md",
  ".github/AGENTS.md",
  "docs/agents.md",
  "src/AGENTS.md",
  "tests/AGENTS.md",
  "prisma/AGENTS.md",
  "unity/AGENTS.md",
  "scripts/harness/policy.json",
  "scripts/harness/classifier.mjs",
  "tests/harness/fixtures/shadow-policy-contract.json",
  "tests/harness/fixtures/shadow-evals.json",
  "scripts/harness/shadow-response.schema.json",
]);

const BASE_PROMPT_PATH = ".github/prompts/shadow-evals.md";
const SUITE_PATH = "tests/harness/fixtures/shadow-evals.json";
const SCHEMA_PATH = "scripts/harness/shadow-response.schema.json";
const PRIVATE_MARKERS = ["shadow-expectations.json", "shadow-responses.json", '"expected"'];

function embeddedContext(path, content) {
  if (content.includes("</context>")) throw new Error(`context delimiter found in ${path}`);
  return `<context path="${path}">\n${content}\n</context>`;
}

async function caseOverrides(root, fixtureId) {
  if (!fixtureId) return new Map();
  if (!/^[a-z0-9-]{1,64}$/.test(fixtureId)) throw new Error("fixture ID is unsafe");
  const suite = JSON.parse(await readFile(resolve(root, SUITE_PATH), "utf8"));
  const selected = suite.cases.filter(({ id }) => id === fixtureId);
  if (selected.length !== 1) throw new Error(`unknown fixture ID: ${fixtureId}`);
  const schema = JSON.parse(await readFile(resolve(root, SCHEMA_PATH), "utf8"));
  schema.properties.results.minItems = 1;
  schema.properties.results.maxItems = 1;
  return new Map([
    [SUITE_PATH, `${JSON.stringify({ ...suite, cases: selected }, null, 2)}\n`],
    [SCHEMA_PATH, `${JSON.stringify(schema, null, 2)}\n`],
  ]);
}

export async function buildCodexShadowPrompt(sourceRoot, fixtureId) {
  const root = resolve(sourceRoot);
  const basePrompt = await readFile(resolve(root, BASE_PROMPT_PATH), "utf8");
  const overrides = await caseOverrides(root, fixtureId);
  const contexts = await Promise.all(SHADOW_PROMPT_CONTEXT_PATHS.map(async (path) =>
    embeddedContext(path, overrides.get(path) ?? await readFile(resolve(root, path), "utf8"))
  ));
  const prompt = `${basePrompt.trimEnd()}\n\n${contexts.join("\n\n")}\n`;
  for (const marker of PRIVATE_MARKERS) {
    if (prompt.includes(marker)) throw new Error(`private shadow marker found in generated prompt: ${marker}`);
  }
  return prompt;
}

export async function writeCodexShadowPrompt(sourceRoot, outputPath, fixtureId, schemaOutputPath) {
  const target = resolve(outputPath);
  const temporary = `${target}.tmp-${process.pid}`;
  await mkdir(dirname(target), { recursive: true });
  try {
    await writeFile(temporary, await buildCodexShadowPrompt(sourceRoot, fixtureId), { encoding: "utf8", mode: 0o444 });
    await rename(temporary, target);
    await chmod(target, 0o444);
    if (fixtureId) {
      if (!schemaOutputPath) throw new Error("--schema-output is required with --fixture-id");
      const overrides = await caseOverrides(resolve(sourceRoot), fixtureId);
      const schemaTarget = resolve(schemaOutputPath);
      await mkdir(dirname(schemaTarget), { recursive: true });
      await writeFile(schemaTarget, overrides.get(SCHEMA_PATH), { encoding: "utf8", mode: 0o444 });
      await chmod(schemaTarget, 0o444);
    }
  } catch (error) {
    await rm(temporary, { force: true });
    throw error;
  }
}

async function main() {
  const sourceIndex = process.argv.indexOf("--source-root");
  const outputIndex = process.argv.indexOf("--output");
  const fixtureIndex = process.argv.indexOf("--fixture-id");
  const schemaIndex = process.argv.indexOf("--schema-output");
  if (sourceIndex < 0 || outputIndex < 0 || !process.argv[sourceIndex + 1] || !process.argv[outputIndex + 1]) {
    throw new Error("usage: build-shadow-prompt.mjs --source-root <path> --output <path>");
  }
  await writeCodexShadowPrompt(
    process.argv[sourceIndex + 1],
    process.argv[outputIndex + 1],
    fixtureIndex >= 0 ? process.argv[fixtureIndex + 1] : undefined,
    schemaIndex >= 0 ? process.argv[schemaIndex + 1] : undefined,
  );
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
