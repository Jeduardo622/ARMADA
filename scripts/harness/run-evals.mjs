#!/usr/bin/env node
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { isDeepStrictEqual } from 'node:util';
import { fileURLToPath } from 'node:url';
import { classifyTask } from './classifier.mjs';
import { validateCompletionReport } from './verify-policy.mjs';
import { verifyStructure } from './verify-structure.mjs';

export function runEvaluations(root) {
  const routing = JSON.parse(readFileSync(resolve(root, 'tests/harness/fixtures/routing.json'), 'utf8'));
  const reports = JSON.parse(readFileSync(resolve(root, 'tests/harness/fixtures/reports.json'), 'utf8'));
  const details = [];
  for (const fixture of routing) {
    const actual = classifyTask(fixture.input);
    if (!isDeepStrictEqual(actual, fixture.expected)) {
      details.push({ fixtureId: fixture.id, expected: fixture.expected, actual });
    }
  }
  for (const fixture of reports) {
    let valid = true;
    try {
      validateCompletionReport(fixture.report);
    } catch {
      valid = false;
    }
    if (valid !== fixture.valid) details.push({ fixtureId: fixture.id, expectedValid: fixture.valid, actualValid: valid });
  }
  const structure = verifyStructure(root);
  if (structure.status !== 'passed') details.push({ fixtureId: 'repository-structure', actual: structure });
  return {
    id: 'harness_evals',
    status: details.length === 0 ? 'passed' : 'failed',
    summary: details.length === 0 ? `${routing.length} routing fixtures and ${reports.length} report fixtures passed` : `${details.length} evaluation failures`,
    fixtureCount: routing.length,
    reportFixtureCount: reports.length,
    details
  };
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const result = runEvaluations(process.cwd());
  const stream = result.status === 'passed' ? process.stdout : process.stderr;
  stream.write(`${JSON.stringify(result)}\n`);
  if (result.status !== 'passed') process.exitCode = 1;
}
