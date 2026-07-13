import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { describe, expect, it } from "vitest";

const CONTEXT_PATHS = [
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
] as const;

function expectedPrompt(): string {
  const base = readFileSync(".github/codex/prompts/shadow-evals.md", "utf8").trimEnd();
  const contexts = CONTEXT_PATHS.map((path) => `<context path="${path}">\n${readFileSync(path, "utf8")}\n</context>`);
  return `${base}\n\n${contexts.join("\n\n")}\n`;
}

describe("shadow Codex prompt builder", () => {
  it("builds the exact ordered public prompt without private grader data", async () => {
    const root = await mkdtemp(join(tmpdir(), "armada-shadow-prompt-"));
    const output = join(root, "prompt.md");
    try {
      execFileSync(process.execPath, [
        resolve("scripts/harness/build-codex-shadow-prompt.mjs"),
        "--source-root",
        resolve("."),
        "--output",
        output,
      ]);
      const prompt = await readFile(output, "utf8");
      expect(prompt).toBe(expectedPrompt());
      for (const path of CONTEXT_PATHS) {
        const escaped = path.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
        expect(prompt.match(new RegExp(`<context path="${escaped}">`, "g"))).toHaveLength(1);
      }
      expect(prompt).not.toContain("codex-shadow-expectations.json");
      expect(prompt).not.toContain("codex-shadow-responses.json");
      expect(prompt).not.toContain(readFileSync("tests/harness/fixtures/codex-shadow-expectations.json", "utf8"));
      expect(prompt).not.toContain(readFileSync("tests/harness/fixtures/codex-shadow-responses.json", "utf8"));
      expect(prompt).not.toContain("rationaleSummary");
      expect(prompt).toContain("evaluate every matching protected area");
      expect(prompt).toContain("Do not return a subset");
      expect(prompt).toContain("Class D returns immediately");
      expect(prompt).toContain("four classifier string arrays");
      expect(prompt).toContain("advisoryPatterns");
      expect(prompt).toContain("canonical classifier implementation");
      expect(prompt).toContain("export function classifyTask");
      expect(prompt).toContain("does not override");
      expect(prompt).toContain("Class D stop and no-fallthrough invariants");
      expect(prompt).toContain("continue through the final protected-area entry");
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  it("builds a one-case prompt and one-result schema without other fixture data", async () => {
    const root = await mkdtemp(join(tmpdir(), "armada-shadow-case-"));
    const output = join(root, "prompt.md");
    const schemaOutput = join(root, "schema.json");
    try {
      execFileSync(process.execPath, [
        resolve("scripts/harness/build-codex-shadow-prompt.mjs"),
        "--source-root", resolve("."),
        "--output", output,
        "--fixture-id", "standard-format-fix",
        "--schema-output", schemaOutput,
      ]);
      const prompt = await readFile(output, "utf8");
      const schema = JSON.parse(await readFile(schemaOutput, "utf8"));
      expect(prompt).toContain('"id": "standard-format-fix"');
      expect(prompt).toContain("Fix deterministic simulation summary formatting");
      expect(prompt).not.toContain('"id": "ci-workflow-repair"');
      expect(prompt).not.toContain("codex-shadow-expectations.json");
      expect(schema.properties.results).toMatchObject({ minItems: 1, maxItems: 1 });
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });
});
