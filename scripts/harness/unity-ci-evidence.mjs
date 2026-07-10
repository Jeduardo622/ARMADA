import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { dirname, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseUnityTestResults, summarizeUnityResultDirectory } from './unity-test-results.mjs';

const MODES = ['editmode', 'playmode'];

function sha256(path) {
  return createHash('sha256').update(readFileSync(path)).digest('hex');
}

function projectVersion(root) {
  const path = resolve(root, 'unity/ProjectSettings/ProjectVersion.txt');
  if (!existsSync(path)) return null;
  return /^m_EditorVersion:\s*(\S+)$/m.exec(readFileSync(path, 'utf8'))?.[1] ?? null;
}

export function createUnityCiEvidence({ root, commitSha, editmodePath, playmodePath }) {
  if (!commitSha) throw new Error('GITHUB_SHA is required for Unity CI evidence');
  const paths = { editmode: resolve(root, editmodePath), playmode: resolve(root, playmodePath) };
  const modes = Object.fromEntries(MODES.map((mode) => {
    const summary = summarizeUnityResultDirectory(paths[mode]);
    if (summary.status !== 'passed') throw new Error(`${mode} Unity test results did not pass`);
    return [mode, {
      status: summary.status,
      total: summary.total,
      passed: summary.passed,
      failed: summary.failed,
      files: summary.files.map((path) => ({
        path: relative(root, path).replaceAll('\\', '/'),
        sha256: sha256(path)
      }))
    }];
  }));
  return {
    schemaVersion: 1,
    commitSha,
    unityVersion: projectVersion(root),
    compilation: { status: 'passed' },
    modes
  };
}

export function validateUnityCiEvidence(evidence, { commitSha, unityVersion }) {
  const violations = [];
  if (evidence?.schemaVersion !== 1) violations.push('Unity CI evidence schemaVersion must be 1');
  if (!commitSha || evidence?.commitSha !== commitSha) violations.push('Unity CI evidence commit does not match GITHUB_SHA');
  if (!unityVersion || evidence?.unityVersion !== unityVersion) violations.push('Unity CI evidence Unity version does not match the project');
  if (evidence?.compilation?.status !== 'passed') violations.push('Unity CI evidence does not prove compilation passed');
  for (const mode of MODES) {
    const result = evidence?.modes?.[mode];
    if (
      result?.status !== 'passed' ||
      !Number.isInteger(result.total) ||
      result.total <= 0 ||
      result.passed !== result.total ||
      result.failed !== 0
    ) {
      violations.push(`Unity CI evidence does not prove ${mode} tests passed`);
    }
    if (
      !Array.isArray(result?.files) ||
      result.files.length === 0 ||
      result.files.some((file) => typeof file?.path !== 'string' || !/^[a-f0-9]{64}$/.test(file?.sha256))
    ) {
      violations.push(`Unity CI evidence has no ${mode} result files`);
    }
  }
  return violations;
}

export function verifyUnityCiEvidence(root, evidencePath, env = process.env) {
  let evidence;
  try {
    evidence = JSON.parse(readFileSync(resolve(root, evidencePath), 'utf8'));
  } catch (error) {
    return { evidence: null, violations: [`Unity CI evidence is unavailable: ${error.message}`] };
  }
  const violations = validateUnityCiEvidence(evidence, {
      commitSha: env.GITHUB_SHA,
      unityVersion: projectVersion(root)
    });
  for (const mode of MODES) {
    for (const file of evidence?.modes?.[mode]?.files ?? []) {
      const absolutePath = resolve(root, file.path);
      const relativePath = relative(root, absolutePath);
      if (relativePath.startsWith('..') || relativePath === '') {
        violations.push(`Unity CI evidence ${mode} result path escapes the repository`);
        continue;
      }
      if (!existsSync(absolutePath)) {
        violations.push(`Unity CI evidence ${mode} result file is missing: ${file.path}`);
        continue;
      }
      if (sha256(absolutePath) !== file.sha256) {
        violations.push(`Unity CI evidence ${mode} result hash does not match: ${file.path}`);
        continue;
      }
      if (parseUnityTestResults(readFileSync(absolutePath, 'utf8')).status !== 'passed') {
        violations.push(`Unity CI evidence ${mode} result XML did not pass: ${file.path}`);
      }
    }
  }
  return { evidence, violations };
}

export function resultFromUnityCiEvidence(root, checkId, env = process.env) {
  const { evidence, violations } = verifyUnityCiEvidence(root, env.UNITY_CI_EVIDENCE_PATH, env);
  return {
    id: checkId,
    executed: true,
    status: violations.length === 0 ? 'passed' : 'failed',
    summary: violations.length === 0
      ? `${checkId} passed from commit-bound Unity CI evidence`
      : `${checkId} failed Unity CI evidence validation`,
    details: { evidence, violations, evidencePath: env.UNITY_CI_EVIDENCE_PATH }
  };
}

function writeEvidence(root = process.cwd(), env = process.env) {
  const outputPath = resolve(root, env.UNITY_CI_EVIDENCE_PATH ?? 'reports/harness/unity-ci-evidence.json');
  const evidence = createUnityCiEvidence({
    root,
    commitSha: env.GITHUB_SHA,
    editmodePath: env.UNITY_EDITMODE_RESULTS,
    playmodePath: env.UNITY_PLAYMODE_RESULTS
  });
  mkdirSync(dirname(outputPath), { recursive: true });
  writeFileSync(outputPath, `${JSON.stringify(evidence, null, 2)}\n`, 'utf8');
  return outputPath;
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  try {
    process.stdout.write(`${JSON.stringify({ status: 'passed', evidencePath: writeEvidence() })}\n`);
  } catch (error) {
    process.stderr.write(`${JSON.stringify({ status: 'failed', error: error.message })}\n`);
    process.exitCode = 1;
  }
}
