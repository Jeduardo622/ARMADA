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
  "tests/harness/fixtures/codex-shadow-policy-contract.json",
  "tests/harness/fixtures/codex-shadow-evals.json",
  "scripts/harness/codex-shadow-response.schema.json",
]);

const BASE_PROMPT_PATH = ".github/codex/prompts/shadow-evals.md";
const PRIVATE_MARKERS = ["codex-shadow-expectations.json", "codex-shadow-responses.json", '"expected"'];

function embeddedContext(path, content) {
  if (content.includes("</context>")) throw new Error(`context delimiter found in ${path}`);
  return `<context path="${path}">\n${content}\n</context>`;
}

export async function buildCodexShadowPrompt(sourceRoot) {
  const root = resolve(sourceRoot);
  const basePrompt = await readFile(resolve(root, BASE_PROMPT_PATH), "utf8");
  const contexts = await Promise.all(SHADOW_PROMPT_CONTEXT_PATHS.map(async (path) =>
    embeddedContext(path, await readFile(resolve(root, path), "utf8"))
  ));
  const prompt = `${basePrompt.trimEnd()}\n\n${contexts.join("\n\n")}\n`;
  for (const marker of PRIVATE_MARKERS) {
    if (prompt.includes(marker)) throw new Error(`private shadow marker found in generated prompt: ${marker}`);
  }
  return prompt;
}

export async function writeCodexShadowPrompt(sourceRoot, outputPath) {
  const target = resolve(outputPath);
  const temporary = `${target}.tmp-${process.pid}`;
  await mkdir(dirname(target), { recursive: true });
  try {
    await writeFile(temporary, await buildCodexShadowPrompt(sourceRoot), { encoding: "utf8", mode: 0o444 });
    await rename(temporary, target);
    await chmod(target, 0o444);
  } catch (error) {
    await rm(temporary, { force: true });
    throw error;
  }
}

async function main() {
  const sourceIndex = process.argv.indexOf("--source-root");
  const outputIndex = process.argv.indexOf("--output");
  if (sourceIndex < 0 || outputIndex < 0 || !process.argv[sourceIndex + 1] || !process.argv[outputIndex + 1]) {
    throw new Error("usage: build-codex-shadow-prompt.mjs --source-root <path> --output <path>");
  }
  await writeCodexShadowPrompt(process.argv[sourceIndex + 1], process.argv[outputIndex + 1]);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
