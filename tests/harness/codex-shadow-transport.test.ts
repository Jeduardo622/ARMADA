import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { describe, expect, it } from "vitest";

const SCRIPT = resolve("scripts/harness/codex-shadow-transport.mjs");

function run(args: string[], env: NodeJS.ProcessEnv = {}) {
  return spawnSync(process.execPath, [SCRIPT, ...args], {
    encoding: "utf8",
    env: { ...process.env, ...env },
  });
}

describe("shadow Codex response transport", () => {
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
      expect(run(["decode", "--output", decoded], { CODEX_SHADOW_RESPONSE_B64: envelope }).status).toBe(0);
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
      expect(run(["decode", "--output", decoded], { CODEX_SHADOW_RESPONSE_B64: envelope }).status).toBe(1);
      expect(await readFile(decoded)).toEqual(Buffer.alloc(0));
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });
});
