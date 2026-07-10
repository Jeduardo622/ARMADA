#!/usr/bin/env node
import { spawnSync } from 'node:child_process';
import { mkdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { validateCompletionReport } from './verify-policy.mjs';

export const CHECK_DEFINITIONS = [
  { id: 'harness_structure', command: 'npm run verify:structure' },
  { id: 'harness_tests', command: 'npm run test:harness', dependsOn: ['harness_structure'] },
  { id: 'lint', command: 'npm run lint' },
  { id: 'typecheck', command: 'npm run typecheck' },
  { id: 'test', command: 'npm test' },
  { id: 'build', command: 'npm run build', dependsOn: ['typecheck'] },
  { id: 'contracts', command: 'npm run verify:contracts' },
  { id: 'unity_static', command: 'npm run verify:unity' },
  { id: 'unity_compilation', notApplicable: 'licensed Unity runner unavailable' },
  { id: 'dependencies', command: 'npm run verify:dependencies' },
  { id: 'secrets', command: 'npm run verify:secrets' },
  { id: 'harness_policy', command: 'npm run verify:policy' }
];

function boundedOutput(value) {
  const text = String(value ?? '').trim();
  return text.length <= 4000 ? text : text.slice(-4000);
}

function runCommand(root, definition) {
  const started = Date.now();
  const result = spawnSync(definition.command, {
    cwd: root,
    encoding: 'utf8',
    env: process.env,
    shell: true,
    timeout: 180_000
  });
  const durationMs = Date.now() - started;
  const exitCode = result.status ?? 1;
  return {
    id: definition.id,
    executed: true,
    status: exitCode === 0 ? 'passed' : 'failed',
    summary: exitCode === 0 ? `${definition.command} passed` : `${definition.command} failed with exit ${exitCode}`,
    durationMs,
    details: {
      exitCode,
      stdout: boundedOutput(result.stdout),
      stderr: boundedOutput(result.stderr || result.error?.message)
    }
  };
}

function writeReport(root, report) {
  const reportPath = resolve(root, 'reports/harness/latest.json');
  const temporaryPath = `${reportPath}.${process.pid}.tmp`;
  mkdirSync(dirname(reportPath), { recursive: true });
  writeFileSync(temporaryPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
  rmSync(reportPath, { force: true });
  renameSync(temporaryPath, reportPath);
}

export function runVerification(root = process.cwd()) {
  const checks = [];
  const byId = new Map();
  for (const definition of CHECK_DEFINITIONS) {
    let result;
    if (definition.notApplicable) {
      result = {
        id: definition.id,
        executed: false,
        status: 'not_applicable',
        summary: definition.notApplicable,
        durationMs: 0,
        details: {}
      };
    } else {
      const failedDependency = definition.dependsOn?.find((id) => byId.get(id)?.status !== 'passed');
      if (failedDependency) {
        result = {
          id: definition.id,
          executed: false,
          status: 'blocked',
          summary: `blocked by ${failedDependency}`,
          durationMs: 0,
          details: {}
        };
      } else {
        process.stdout.write(`[verify] ${definition.id}\n`);
        result = runCommand(root, definition);
      }
    }
    checks.push(result);
    byId.set(result.id, result);
  }
  validateCompletionReport({ classification: 'B', checks });
  const overallStatus = checks.some((check) => check.status === 'failed' || check.status === 'blocked')
    ? 'failed'
    : 'passed';
  const report = {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    overallStatus,
    checks
  };
  writeReport(root, report);
  return report;
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const report = runVerification();
  process.stdout.write(`[verify] overall ${report.overallStatus}\n`);
  if (report.overallStatus !== 'passed') process.exitCode = 1;
}
