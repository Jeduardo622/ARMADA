import { describe, expect, it } from "vitest";

import suite from "./fixtures/codex-shadow-evals.json";
import replay from "./fixtures/codex-shadow-responses.json";
import responseSchema from "../../scripts/harness/codex-shadow-response.schema.json";

const REQUIRED_CATEGORIES = [
  "advisory",
  "standard",
  "authentication",
  "database",
  "ci",
  "unity-tooling",
  "secret-extraction",
  "production-mutation",
  "check-bypass",
  "evidence-honesty",
] as const;

const SAFE_ID = /^[a-z0-9-]+$/;
const CLASSIFICATIONS = new Set(["A", "B", "C", "D"]);
const DECISIONS = new Set(["proceed", "plan_only", "stop"]);
const EVIDENCE_STATUSES = new Set(["not-run", "passed", "failed", "blocked", "not-applicable"]);

function expectBoundedString(value: unknown, maximum: number): asserts value is string {
  expect(typeof value).toBe("string");
  expect((value as string).length).toBeGreaterThan(0);
  expect((value as string).length).toBeLessThanOrEqual(maximum);
}

function expectStringArray(value: unknown, maximumItems: number, maximumLength: number): asserts value is string[] {
  expect(Array.isArray(value)).toBe(true);
  expect((value as unknown[]).length).toBeLessThanOrEqual(maximumItems);
  for (const item of value as unknown[]) expectBoundedString(item, maximumLength);
  expect(new Set(value as string[]).size).toBe((value as string[]).length);
}

function expectCheckClaims(value: unknown): void {
  expect(Array.isArray(value)).toBe(true);
  const claims = value as Array<Record<string, unknown>>;
  expect(claims.length).toBeLessThanOrEqual(20);
  expect(new Set(claims.map(({ id }) => id)).size).toBe(claims.length);
  for (const claim of claims) {
    expect(Object.keys(claim).sort()).toEqual(["evidence", "executed", "id", "status"]);
    expectBoundedString(claim.id, 64);
    expect(EVIDENCE_STATUSES.has(claim.status as string)).toBe(true);
    expect(typeof claim.executed).toBe("boolean");
    expectBoundedString(claim.evidence, 256);
  }
}

function expectStrictObjects(schema: unknown, path = "$"): void {
  if (!schema || typeof schema !== "object") return;
  const node = schema as Record<string, unknown>;
  if (node.type === "object") expect(node.additionalProperties, path).toBe(false);
  for (const [key, value] of Object.entries(node)) {
    if (key !== "additionalProperties") expectStrictObjects(value, `${path}.${key}`);
  }
}

describe("shadow Codex evaluation contract", () => {
  it("defines exactly ten bounded cases with unique safe IDs and required categories", () => {
    expect(suite.schemaVersion).toBe(1);
    expectBoundedString(suite.suiteVersion, 64);
    expect(suite.cases).toHaveLength(10);
    expect(new Set(suite.cases.map(({ id }) => id))).toHaveLength(10);
    expect(suite.cases.map(({ category }) => category).sort()).toEqual([...REQUIRED_CATEGORIES].sort());

    for (const testCase of suite.cases) {
      expect(testCase.id).toMatch(SAFE_ID);
      expectBoundedString(testCase.prompt, 1_000);
      expectStringArray(testCase.changedPaths, 10, 256);
      expectCheckClaims(testCase.suppliedCheckObservations);
      expect(CLASSIFICATIONS.has(testCase.expected.classification)).toBe(true);
      expect(DECISIONS.has(testCase.expected.decision)).toBe(true);
      expectStringArray(testCase.expected.protectedAreas, 10, 64);
      expectStringArray(testCase.expected.requiredReviewers, 10, 64);
      expectStringArray(testCase.expected.requiredChecks, 20, 64);
      expectStringArray(testCase.expected.allowedActions, 10, 64);
      expectCheckClaims(testCase.expected.checkClaims);
      expect(Object.keys(testCase.expected.rollback).sort()).toEqual(["instructions", "required"]);
      expect(typeof testCase.expected.rollback.required).toBe("boolean");
      if (testCase.expected.rollback.required) expectBoundedString(testCase.expected.rollback.instructions, 500);
      else expect(testCase.expected.rollback.instructions).toBeNull();
    }
  });

  it("provides a complete valid replay matching every expected invariant", () => {
    expect(replay.schemaVersion).toBe(1);
    expect(replay.suiteVersion).toBe(suite.suiteVersion);
    expect(replay.results).toHaveLength(10);
    expect(replay.results.map(({ fixtureId }) => fixtureId).sort()).toEqual(suite.cases.map(({ id }) => id).sort());

    for (const testCase of suite.cases) {
      const result = replay.results.find(({ fixtureId }) => fixtureId === testCase.id);
      expect(result).toBeDefined();
      expect(result).toMatchObject(testCase.expected);
      expectStringArray(result?.protectedAreas, 10, 64);
      expectStringArray(result?.requiredReviewers, 10, 64);
      expectStringArray(result?.requiredChecks, 20, 64);
      expectStringArray(result?.allowedActions, 10, 64);
      expectCheckClaims(result?.checkClaims);
      expect(result?.rationaleSummary.length).toBeGreaterThan(0);
      expect(result?.rationaleSummary.length).toBeLessThanOrEqual(500);
      expect(Object.keys(result ?? {}).sort()).toEqual([
        "allowedActions",
        "checkClaims",
        "classification",
        "decision",
        "fixtureId",
        "protectedAreas",
        "rationaleSummary",
        "requiredChecks",
        "requiredReviewers",
        "rollback",
      ]);
    }
  });

  it("defines a strict bounded response JSON Schema", () => {
    expect(responseSchema.$schema).toBe("https://json-schema.org/draft/2020-12/schema");
    expect(responseSchema.type).toBe("object");
    expect(responseSchema.required).toEqual(["schemaVersion", "suiteVersion", "results"]);
    expect(responseSchema.properties.results.maxItems).toBe(10);
    expect(responseSchema.properties.results.minItems).toBe(10);
    expect(responseSchema.$defs.shadowResponse.properties.fixtureId.pattern).toBe("^[a-z0-9-]+$");
    expect(responseSchema.$defs.shadowResponse.properties.rationaleSummary.maxLength).toBe(500);
    expectStrictObjects(responseSchema);
  });
});
