import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { describe, expect, it } from "vitest";
import suite from "./fixtures/shadow-evals.json";
import replay from "./fixtures/shadow-responses.json";

const SCRIPT = resolve("scripts/harness/shadow-transport.mjs");

function run(args: string[], env: NodeJS.ProcessEnv = {}) {
  return spawnSync(process.execPath, [SCRIPT, ...args], {
    encoding: "utf8",
    env: { ...process.env, ...env },
  });
}

describe("shadow Shadow response transport", () => {
  it.each([1, 65536])("round-trips a %i-byte response and deletes the raw input", async (size) => {
    const root = await mkdtemp(join(tmpdir(), "armada-shadow-transport-"));
    try {
      const input = join(root, "response.json");
      const githubOutput = join(root, "github-output.txt");
      const decoded = join(root, "decoded.json");
      const response = Buffer.alloc(size, 0x61);
      await writeFile(input, response);
      await writeFile(githubOutput, "");

      expect(run(["encode", "--input", input, "--github-output", githubOutput]).status).toBe(0);
      expect(existsSync(input)).toBe(false);
      const envelope = (await readFile(githubOutput, "utf8")).trim().replace(/^response-b64=/, "");
      expect(run(["decode", "--output", decoded], { SHADOW_RESPONSE_B64: envelope }).status).toBe(0);
      expect(await readFile(decoded)).toEqual(response);
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  it.each([0, 65537])("rejects a %i-byte raw response and still deletes it", async (size) => {
    const root = await mkdtemp(join(tmpdir(), "armada-shadow-transport-"));
    try {
      const input = join(root, "response.json");
      const githubOutput = join(root, "github-output.txt");
      await writeFile(input, Buffer.alloc(size, 0x61));
      await writeFile(githubOutput, "");
      expect(run(["encode", "--input", input, "--github-output", githubOutput]).status).toBe(1);
      expect(existsSync(input)).toBe(false);
      expect(await readFile(githubOutput, "utf8")).toBe("");
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  it("deletes the raw response when GitHub output append fails", async () => {
    const root = await mkdtemp(join(tmpdir(), "armada-shadow-transport-"));
    try {
      const input = join(root, "response.json");
      await writeFile(input, "{}");
      expect(run(["encode", "--input", input, "--github-output", root]).status).toBe(1);
      expect(existsSync(input)).toBe(false);
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  it.each(["", "not-base64", Buffer.alloc(65537, 0x61).toString("base64")])("fails closed for a malformed or oversized envelope", async (envelope) => {
    const root = await mkdtemp(join(tmpdir(), "armada-shadow-transport-"));
    try {
      const decoded = join(root, "decoded.json");
      expect(run(["decode", "--output", decoded], { SHADOW_RESPONSE_B64: envelope }).status).toBe(1);
      expect(await readFile(decoded)).toEqual(Buffer.alloc(0));
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  it("combines exactly one response per trusted fixture in suite order and deletes raw files", async () => {
    const root = await mkdtemp(join(tmpdir(), "armada-shadow-combine-"));
    const input = join(root, "responses");
    const output = join(root, "combined.json");
    try {
      await mkdir(input);
      await Promise.all(replay.results.map(async (result) => {
        await writeFile(join(input, `${result.fixtureId}.json`), JSON.stringify({ schemaVersion: 1, suiteVersion: replay.suiteVersion, results: [result] }));
      }));
      expect(run(["combine", "--input-dir", input, "--suite", resolve("tests/harness/fixtures/shadow-evals.json"), "--output", output]).status).toBe(0);
      expect(JSON.parse(await readFile(output, "utf8"))).toEqual(replay);
      expect(existsSync(input)).toBe(true);
      for (const result of replay.results) expect(existsSync(join(input, `${result.fixtureId}.json`))).toBe(false);
      expect(JSON.parse(await readFile(resolve("tests/harness/fixtures/shadow-evals.json"), "utf8")).cases.map(({ id }: { id: string }) => id)).toEqual(suite.cases.map(({ id }) => id));
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  it("fails closed on a mismatched one-case envelope and deletes all raw files", async () => {
    const root = await mkdtemp(join(tmpdir(), "armada-shadow-combine-"));
    const input = join(root, "responses");
    const output = join(root, "combined.json");
    try {
      await mkdir(input);
      await Promise.all(replay.results.map(async (result, index) => {
        const actual = index === 0 ? { ...result, fixtureId: "wrong-fixture" } : result;
        await writeFile(join(input, `${result.fixtureId}.json`), JSON.stringify({ schemaVersion: 1, suiteVersion: replay.suiteVersion, results: [actual] }));
      }));
      expect(run(["combine", "--input-dir", input, "--suite", resolve("tests/harness/fixtures/shadow-evals.json"), "--output", output]).status).toBe(1);
      expect(existsSync(input)).toBe(true);
      for (const result of replay.results) expect(existsSync(join(input, `${result.fixtureId}.json`))).toBe(false);
      expect(existsSync(output)).toBe(false);
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  it("preserves unrelated files in the caller-supplied input directory", async () => {
    const root = await mkdtemp(join(tmpdir(), "armada-shadow-combine-"));
    const input = join(root, "responses");
    const output = join(root, "combined.json");
    const sentinel = join(input, "unrelated.txt");
    try {
      await mkdir(input);
      await writeFile(sentinel, "preserve me");
      await Promise.all(replay.results.map(async (result) => {
        await writeFile(join(input, `${result.fixtureId}.json`), JSON.stringify({ schemaVersion: 1, suiteVersion: replay.suiteVersion, results: [result] }));
      }));
      expect(run(["combine", "--input-dir", input, "--suite", resolve("tests/harness/fixtures/shadow-evals.json"), "--output", output]).status).toBe(0);
      expect(await readFile(sentinel, "utf8")).toBe("preserve me");
      expect(existsSync(input)).toBe(true);
      for (const result of replay.results) expect(existsSync(join(input, `${result.fixtureId}.json`))).toBe(false);
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });

  it("does not delete outside the input directory when the trusted suite has an invalid fixture ID", async () => {
    const root = await mkdtemp(join(tmpdir(), "armada-shadow-combine-"));
    const input = join(root, "responses");
    const suitePath = join(root, "invalid-suite.json");
    const output = join(root, "combined.json");
    const sentinel = join(root, "victim.json");
    try {
      await mkdir(input);
      await writeFile(sentinel, "preserve me");
      const invalidSuite = { ...suite, cases: suite.cases.map((entry, index) => index === 0 ? { ...entry, id: "../victim" } : entry) };
      await writeFile(suitePath, JSON.stringify(invalidSuite));
      expect(run(["combine", "--input-dir", input, "--suite", suitePath, "--output", output]).status).toBe(1);
      expect(await readFile(sentinel, "utf8")).toBe("preserve me");
      expect(existsSync(output)).toBe(false);
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });
});
