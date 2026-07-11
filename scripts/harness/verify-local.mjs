import { spawnSync } from 'node:child_process';
import { mkdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { validateCompletionReport } from './verify-policy.mjs';
import { classifyTask } from './classifier.mjs';

export const CHECK_DEFINITIONS = [
  { id: 'harness_structure', command: 'npm run verify:structure' },
  { id: 'harness_tests', command: 'npm run test:harness', dependsOn: ['harness_structure'] },
  { id: 'lint', command: 'npm run lint' },
  { id: 'typecheck', command: 'npm run typecheck' },
  { id: 'test', command: 'npm test' },
  { id: 'build', command: 'npm run build', dependsOn: ['typecheck'] },
  { id: 'contracts', command: 'npm run verify:contracts' },
  { id: 'database', command: 'npm run verify:database' },
  { id: 'deployment', command: 'npm run verify:deployment' },
  { id: 'unity_static', command: 'npm run verify:unity' },
  {
    id: 'unity_compilation',
    command: 'npm run verify:unity:compile',
    whenEnv: 'UNITY_EDITOR_PATH',
    alternativeEnv: 'UNITY_CI_EVIDENCE_PATH',
    requiredWhenEnv: 'UNITY_COMPILATION_REQUIRED',
    notApplicable: 'UNITY_EDITOR_PATH not configured',
    timeoutMs: 750_000
  },
  {
    id: 'unity_tests',
    command: 'npm run verify:unity:tests',
    whenEnv: 'UNITY_EDITOR_PATH',
    alternativeEnv: 'UNITY_CI_EVIDENCE_PATH',
    requiredWhenEnv: 'UNITY_TESTS_REQUIRED',
    notApplicable: 'UNITY_EDITOR_PATH and UNITY_CI_EVIDENCE_PATH not configured',
    timeoutMs: 1_350_000
  },
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
    timeout: definition.timeoutMs ?? 180_000
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

const UNITY_RUNTIME_PATH = /^(?:unity\/(?:Assets|Packages|ProjectSettings)\/|\.codex\/config\.toml$)/;

export function requiresUnityCompilation(changedPaths, env = process.env) {
  return Boolean(env.UNITY_COMPILATION_REQUIRED) || changedPaths.some((path) => UNITY_RUNTIME_PATH.test(path));
}

export function requiresUnityTests(changedPaths, env = process.env) {
  return Boolean(env.UNITY_TESTS_REQUIRED) || changedPaths.some((path) => UNITY_RUNTIME_PATH.test(path));
}

export function readChangedPaths(root, env = process.env) {
  const status = spawnSync('git', ['status', '--porcelain=v1'], { cwd: root, encoding: 'utf8', windowsHide: true });
  const workingPaths = String(status.stdout ?? '')
    .split(/\r?\n/)
    .filter(Boolean)
    .map((line) => line.slice(3).split(' -> ').at(-1));
  const configuredBase = env.HARNESS_BASE_REF || (env.GITHUB_BASE_REF ? `origin/${env.GITHUB_BASE_REF}` : 'origin/main');
  const baseExists = spawnSync('git', ['rev-parse', '--verify', configuredBase], {
    cwd: root,
    encoding: 'utf8',
    windowsHide: true
  }).status === 0;
  const baseArgs = baseExists
    ? ['diff', '--name-only', `${configuredBase}...HEAD`]
    : ['diff-tree', '--no-commit-id', '--name-only', '-r', 'HEAD'];
  const diff = spawnSync('git', baseArgs, { cwd: root, encoding: 'utf8', windowsHide: true });
  const committedPaths = String(diff.stdout ?? '').split(/\r?\n/).filter(Boolean);
  return [...new Set([...workingPaths, ...committedPaths])];
}

export function resolveConditionalCheck(definition, env = process.env) {
  if (!definition.whenEnv || env[definition.whenEnv] || (definition.alternativeEnv && env[definition.alternativeEnv])) {
    return null;
  }
  if (definition.requiredWhenEnv && env[definition.requiredWhenEnv]) {
    const requirement = definition.alternativeEnv
      ? `${definition.whenEnv} or ${definition.alternativeEnv}`
      : definition.whenEnv;
    return {
      id: definition.id,
      executed: false,
      status: 'failed',
      summary: `${requirement} is required for this verification scope`,
      durationMs: 0,
      details: {}
    };
  }
  return {
    id: definition.id,
    executed: false,
    status: 'not_applicable',
    summary: definition.notApplicable,
    durationMs: 0,
    details: {}
  };
}

export function resolveVerificationMetadata(changedPaths, env = process.env) {
  const routing = classifyTask({
    description: env.HARNESS_TASK_DESCRIPTION ?? 'Verify current repository changes',
    changedPaths
  });
  const rollback = routing.classification === 'C'
    ? env.HARNESS_ROLLBACK ?? 'Restore the protected paths from the reviewed base commit and rerun npm run verify:local.'
    : undefined;
  return { routing, rollback };
}

export function appendMissingRequiredChecks(checks, routing) {
  const present = new Set(checks.map((check) => check.id));
  for (const id of routing.requiredChecks) {
    if (present.has(id)) continue;
    checks.push({
      id,
      executed: false,
      status: 'failed',
      summary: `required check ${id} has no verification definition`,
      durationMs: 0,
      details: {}
    });
  }
  return checks;
}

export function runVerification(root = process.cwd()) {
  const checks = [];
  const byId = new Map();
  const changedPaths = readChangedPaths(root);
  const metadata = resolveVerificationMetadata(changedPaths);
  const verificationEnv = { ...process.env };
  if (requiresUnityCompilation(changedPaths)) verificationEnv.UNITY_COMPILATION_REQUIRED = '1';
  if (requiresUnityTests(changedPaths)) verificationEnv.UNITY_TESTS_REQUIRED = '1';
  for (const definition of CHECK_DEFINITIONS) {
    let result = resolveConditionalCheck(definition, verificationEnv);
    if (!result) {
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
  appendMissingRequiredChecks(checks, metadata.routing);
  validateCompletionReport({ classification: metadata.routing.classification, rollback: metadata.rollback, checks });
  const overallStatus = checks.some((check) => check.status === 'failed' || check.status === 'blocked')
    ? 'failed'
    : 'passed';
  const report = {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    classification: metadata.routing.classification,
    routing: metadata.routing,
    changedPaths,
    rollback: metadata.rollback ?? null,
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
