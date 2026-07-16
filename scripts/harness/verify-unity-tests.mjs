import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, rmSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resultFromUnityCiEvidence } from './unity-ci-evidence.mjs';
import { parseUnityTestResults } from './unity-test-results.mjs';
import { preflightUnityEditor } from './verify-unity-compile.mjs';
import { createUnityProjectSandbox } from './unity-project-sandbox.mjs';

const MODES = ['EditMode', 'PlayMode'];
const ASSEMBLIES = {
  EditMode: 'Armada.Client.EditModeTests',
  PlayMode: 'Armada.Client.PlayModeTests'
};

export function buildUnityTestArgs(root, mode, resultPath, logPath, projectPath = resolve(root, 'unity')) {
  return [
    '-batchmode',
    '-nographics',
    '-projectPath',
    projectPath,
    '-runTests',
    '-testPlatform',
    mode,
    '-assemblyNames',
    ASSEMBLIES[mode],
    '-testResults',
    resultPath,
    '-logFile',
    logPath
  ];
}

function runMode(root, editorPath, mode, projectPath) {
  const key = mode.toLowerCase();
  const resultPath = resolve(root, `reports/harness/unity-${key}-results.xml`);
  const logPath = resolve(root, `reports/harness/unity-${key}.log`);
  mkdirSync(dirname(resultPath), { recursive: true });
  rmSync(resultPath, { force: true });
  rmSync(logPath, { force: true });
  const processResult = spawnSync(editorPath, buildUnityTestArgs(root, mode, resultPath, logPath, projectPath), {
    cwd: root,
    encoding: 'utf8',
    timeout: 600_000,
    windowsHide: true
  });
  const summary = existsSync(resultPath)
    ? parseUnityTestResults(readFileSync(resultPath, 'utf8'))
    : { status: 'failed', result: null, total: 0, passed: 0, failed: 0, skipped: 0 };
  return {
    mode,
    status: processResult.status === 0 && summary.status === 'passed' ? 'passed' : 'failed',
    exitCode: processResult.status ?? 1,
    resultPath,
    logPath,
    summary,
    error: processResult.error?.message ?? null
  };
}

export function runUnityTests(root = process.cwd(), editorPath = process.env.UNITY_EDITOR_PATH, env = process.env) {
  if (env.UNITY_CI_EVIDENCE_PATH) return resultFromUnityCiEvidence(root, 'unity_tests', env);
  const preflight = preflightUnityEditor(root, editorPath);
  if (preflight.status !== 'passed') {
    return {
      id: 'unity_tests', executed: false, status: 'failed', summary: preflight.summary, details: preflight.details
    };
  }
  const sandbox = createUnityProjectSandbox(root);
  let modes;
  try {
    modes = MODES.map((mode) => runMode(root, editorPath, mode, sandbox.projectPath));
  } finally {
    sandbox.cleanup();
  }
  const status = modes.every((mode) => mode.status === 'passed') ? 'passed' : 'failed';
  return {
    id: 'unity_tests',
    executed: true,
    status,
    summary: status === 'passed'
      ? `${modes.reduce((sum, mode) => sum + mode.summary.total, 0)} Unity tests passed across EditMode and PlayMode`
      : 'Unity EditMode or PlayMode tests failed',
    details: { editorPath, expectedVersion: preflight.details.expectedVersion, modes }
  };
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const result = runUnityTests();
  const stream = result.status === 'passed' ? process.stdout : process.stderr;
  stream.write(`${JSON.stringify(result)}\n`);
  if (result.status !== 'passed') process.exitCode = 1;
}
