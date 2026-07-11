import { describe, expect, it } from 'vitest';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { runEvaluations } from '../../scripts/harness/run-evals.mjs';
import { verifyContracts } from '../../scripts/harness/verify-contracts.mjs';
import { validateUnityMcpConfig, verifyUnity } from '../../scripts/harness/verify-unity.mjs';
import { parseUnityTestResults } from '../../scripts/harness/unity-test-results.mjs';
import {
  createUnityCiEvidence,
  validateUnityCiEvidence,
  verifyUnityCiEvidence
} from '../../scripts/harness/unity-ci-evidence.mjs';
import { buildUnityTestArgs } from '../../scripts/harness/verify-unity-tests.mjs';
import { createUnityProjectSandbox } from '../../scripts/harness/unity-project-sandbox.mjs';
import { buildPostgresRunArgs, installSignalCleanup } from '../../scripts/harness/verify-database.mjs';
import { validateDeploymentConfig } from '../../scripts/harness/verify-deployment.mjs';
import {
  buildUnityCompileArgs,
  classifyUnityCompilation,
  parseUnityEditorVersion,
  parseUnityProjectVersion
} from '../../scripts/harness/verify-unity-compile.mjs';
import {
  CHECK_DEFINITIONS,
  appendMissingRequiredChecks,
  readChangedPaths,
  requiresUnityCompilation,
  requiresUnityTests,
  resolveConditionalCheck,
  resolveVerificationMetadata
} from '../../scripts/harness/verify-local.mjs';

describe('repository verifiers', () => {
  it('matches every documented API operation to backend route source', () => {
    expect(verifyContracts(process.cwd())).toMatchObject({
      id: 'contracts',
      status: 'passed'
    });
  });

  it('validates Unity metadata without claiming compilation passed', () => {
    expect(verifyUnity(process.cwd())).toMatchObject({
      id: 'unity_static',
      status: 'passed',
      compilation: {
        id: 'unity_compilation',
        executed: false,
        status: 'not_applicable',
        summary: 'set UNITY_EDITOR_PATH to execute licensed Unity compilation'
      }
    });
  });

  it('evaluates all routing fixtures deterministically', () => {
    expect(runEvaluations(process.cwd())).toMatchObject({
      id: 'harness_evals',
      status: 'passed',
      fixtureCount: 18
    });
  });

  it('keeps the local verification contract ordered and explicit', () => {
    expect(CHECK_DEFINITIONS.map((check) => check.id)).toEqual([
      'harness_structure',
      'harness_tests',
      'lint',
      'typecheck',
      'test',
      'build',
      'contracts',
      'database',
      'deployment',
      'unity_static',
      'unity_compilation',
      'unity_tests',
      'dependencies',
      'secrets',
      'harness_policy'
    ]);
  });

  it('uses an isolated PostgreSQL container for migration verification', () => {
    expect(buildPostgresRunArgs('armada-db-verify-123')).toEqual([
      'run',
      '--detach',
      '--rm',
      '--name',
      'armada-db-verify-123',
      '--env',
      'POSTGRES_USER=armada_verify',
      '--env',
      'POSTGRES_PASSWORD=armada_verify',
      '--env',
      'POSTGRES_DB=armada_verify',
      '--health-cmd',
      'pg_isready -U armada_verify -d armada_verify',
      '--health-interval',
      '1s',
      '--health-timeout',
      '5s',
      '--health-retries',
      '30',
      '--publish',
      '127.0.0.1::5432',
      'postgres:16-alpine'
    ]);
  });

  it('requires the documented database URL to match the Compose host port', () => {
    const compose = {
      services: {
        postgres: {
          image: 'postgres:16-alpine',
          environment: { POSTGRES_USER: 'postgres', POSTGRES_PASSWORD: 'postgres', POSTGRES_DB: 'armada' },
          ports: [{ target: 5432, published: '15432' }],
          healthcheck: { test: ['CMD-SHELL', 'pg_isready -U postgres'] }
        }
      }
    };
    expect(validateDeploymentConfig(compose, 'DATABASE_URL=postgres://postgres:postgres@localhost:5432/armada\n'))
      .toContain('DATABASE_URL port 5432 does not match Compose PostgreSQL host port 15432');
    expect(validateDeploymentConfig(compose, 'DATABASE_URL=postgres://postgres:postgres@localhost:15432/armada\n'))
      .toEqual([]);
    expect(validateDeploymentConfig(compose, 'DATABASE_URL=mysql://wrong:wrong@localhost:15432/not_armada\n'))
      .toEqual(expect.arrayContaining([
        '.env.example DATABASE_URL must use PostgreSQL',
        'DATABASE_URL username does not match Compose POSTGRES_USER',
        'DATABASE_URL password does not match Compose POSTGRES_PASSWORD',
        'DATABASE_URL database does not match Compose POSTGRES_DB'
      ]));
  });

  it('makes interruption cleanup idempotent', () => {
    let cleanupCount = 0;
    const signalCleanup = installSignalCleanup('armada-db-verify-test', () => cleanupCount++);
    try {
      signalCleanup.cleanup();
      signalCleanup.cleanup();
      expect(cleanupCount).toBe(1);
    } finally {
      signalCleanup.dispose();
    }
  });

  it('preserves protected classification and rollback metadata in verification reports', () => {
    expect(resolveVerificationMetadata(['.github/workflows/ci.yml'], {
      HARNESS_TASK_DESCRIPTION: 'Change the GameCI Unity test workflow'
    })).toMatchObject({
      routing: {
        classification: 'C',
        protectedAreas: ['ci', 'unity_ci'],
        requiredReviewers: ['Engineering', 'Security', 'Unity']
      },
      rollback: 'Restore the protected paths from the reviewed base commit and rerun npm run verify:local.'
    });
    expect(resolveVerificationMetadata(['.github/workflows/ci.yml'], {})).toMatchObject({
      routing: {
        classification: 'C',
        protectedAreas: ['ci', 'unity_ci'],
        requiredReviewers: ['Engineering', 'Security', 'Unity']
      }
    });
    expect(appendMissingRequiredChecks([], {
      classification: 'C',
      reasons: [],
      protectedAreas: [],
      allowedActions: [],
      requiredReviewers: [],
      requiredChecks: ['secrets']
    })).toContainEqual(expect.objectContaining({
      id: 'secrets', executed: false, status: 'failed'
    }));
  });

  it('runs licensed Unity compilation only when an editor is configured', () => {
    const definition = CHECK_DEFINITIONS.find((check) => check.id === 'unity_compilation');
    expect(definition).toMatchObject({
      command: 'npm run verify:unity:compile',
      whenEnv: 'UNITY_EDITOR_PATH',
      alternativeEnv: 'UNITY_CI_EVIDENCE_PATH',
      requiredWhenEnv: 'UNITY_COMPILATION_REQUIRED'
    });

    expect(resolveConditionalCheck(definition!, {})).toMatchObject({ status: 'not_applicable' });
    expect(resolveConditionalCheck(definition!, { UNITY_COMPILATION_REQUIRED: '1' })).toMatchObject({
      status: 'failed',
      summary: 'UNITY_EDITOR_PATH or UNITY_CI_EVIDENCE_PATH is required for this verification scope'
    });
    expect(resolveConditionalCheck(definition!, { UNITY_EDITOR_PATH: 'C:/Unity/Unity.exe' })).toBeNull();
    expect(resolveConditionalCheck(definition!, { UNITY_CI_EVIDENCE_PATH: 'evidence.json' })).toBeNull();
    expect(requiresUnityCompilation(['unity/Packages/manifest.json'], {})).toBe(true);
    expect(requiresUnityCompilation(['.codex/config.toml'], {})).toBe(true);
    expect(requiresUnityCompilation(['unity/Assets/Armada/UI/MissionUIController.cs'], {})).toBe(true);
    expect(requiresUnityTests(['unity/Assets/Armada/UI/MissionUIController.cs'], {})).toBe(true);

    expect(buildUnityCompileArgs('C:/repo', 'C:/logs/unity.log')).toEqual([
      '-batchmode',
      '-nographics',
      '-quit',
      '-projectPath',
      resolve('C:/repo', 'unity'),
      '-logFile',
      'C:/logs/unity.log'
    ]);
    expect(classifyUnityCompilation(0, 'Exiting batchmode successfully now!')).toBe('passed');
    expect(classifyUnityCompilation(0, '')).toBe('failed');
    expect(
      classifyUnityCompilation(
        0,
        'Assets/Test.cs(1,1): error CS1002: ; expected\nExiting batchmode successfully now!'
      )
    ).toBe('failed');
    expect(classifyUnityCompilation(1, 'Batch mode aborted')).toBe('failed');
    expect(parseUnityProjectVersion('m_EditorVersion: 2022.3.62f3\n')).toBe('2022.3.62f3');
    expect(parseUnityEditorVersion('Unity Editor 2022.3.62f3')).toBe('2022.3.62f3');
    expect(buildUnityTestArgs('C:/repo', 'EditMode', 'results.xml', 'unity.log')).toEqual([
      '-batchmode',
      '-nographics',
      '-projectPath',
      resolve('C:/repo', 'unity'),
      '-runTests',
      '-testPlatform',
      'EditMode',
      '-assemblyNames',
      'Armada.Client.EditModeTests',
      '-testResults',
      'results.xml',
      '-logFile',
      'unity.log'
    ]);
  });

  it('accepts only non-empty passing Unity XML and commit-bound CI evidence', () => {
    expect(parseUnityTestResults('<test-run testcasecount="5" result="Passed" passed="5" failed="0" skipped="0"></test-run>'))
      .toMatchObject({ status: 'passed', total: 5, passed: 5, failed: 0 });
    expect(parseUnityTestResults('<test-run testcasecount="1" result="Failed" passed="0" failed="1" skipped="0"></test-run>'))
      .toMatchObject({ status: 'failed', total: 1, failed: 1 });
    expect(parseUnityTestResults('<test-run testcasecount="0" result="Passed" passed="0" failed="0" skipped="0"></test-run>'))
      .toMatchObject({ status: 'failed', total: 0 });
    expect(parseUnityTestResults('<test-run testcasecount="1" result="Passed" passed="1" failed="0" skipped="0">'))
      .toMatchObject({ status: 'failed' });
    expect(parseUnityTestResults('<test-run testcasecount="2" result="Passed" passed="1" failed="0" skipped="0"></test-run>'))
      .toMatchObject({ status: 'failed' });
    expect(parseUnityTestResults('<test-run testcasecount="1" result="Passed" passed="1" failed="0" skipped="0"></test-run><test-run></test-run>'))
      .toMatchObject({ status: 'failed' });
    expect(parseUnityTestResults('<test-run testcasecount="1" result="Passed" passed="1" failed="0" skipped="0"><test-suite></test-run>'))
      .toMatchObject({ status: 'failed' });

    const evidence = {
      schemaVersion: 1,
      commitSha: 'abc123',
      unityVersion: '2022.3.62f3',
      compilation: { status: 'passed' },
      modes: {
        editmode: {
          status: 'passed', total: 4, passed: 4, failed: 0,
          files: [{ path: 'edit.xml', sha256: 'a'.repeat(64) }]
        },
        playmode: {
          status: 'passed', total: 1, passed: 1, failed: 0,
          files: [{ path: 'play.xml', sha256: 'b'.repeat(64) }]
        }
      }
    };
    expect(validateUnityCiEvidence(evidence, { commitSha: 'abc123', unityVersion: '2022.3.62f3' })).toEqual([]);
    expect(validateUnityCiEvidence(evidence, { commitSha: 'different', unityVersion: '2022.3.62f3' }))
      .toContain('Unity CI evidence commit does not match GITHUB_SHA');
  });

  it('rejects Unity CI result files that change after evidence is recorded', () => {
    const root = mkdtempSync(resolve(tmpdir(), 'armada-unity-evidence-'));
    try {
      mkdirSync(resolve(root, 'unity/ProjectSettings'), { recursive: true });
      mkdirSync(resolve(root, 'reports/unity/editmode'), { recursive: true });
      mkdirSync(resolve(root, 'reports/unity/playmode'), { recursive: true });
      writeFileSync(resolve(root, 'unity/ProjectSettings/ProjectVersion.txt'), 'm_EditorVersion: 2022.3.62f3\n');
      const passingXml = '<test-run testcasecount="1" result="Passed" passed="1" failed="0" skipped="0"></test-run>';
      writeFileSync(resolve(root, 'reports/unity/editmode/results.xml'), passingXml);
      writeFileSync(resolve(root, 'reports/unity/playmode/results.xml'), passingXml);
      const evidence = createUnityCiEvidence({
        root,
        commitSha: 'abc123',
        editmodePath: 'reports/unity/editmode',
        playmodePath: 'reports/unity/playmode'
      });
      writeFileSync(resolve(root, 'evidence.json'), JSON.stringify(evidence));
      expect(verifyUnityCiEvidence(root, 'evidence.json', { GITHUB_SHA: 'abc123' }).violations).toEqual([]);

      writeFileSync(resolve(root, 'reports/unity/playmode/results.xml'), `${passingXml}\nchanged`);
      expect(verifyUnityCiEvidence(root, 'evidence.json', { GITHUB_SHA: 'abc123' }).violations)
        .toContain('Unity CI evidence playmode result hash does not match: reports/unity/playmode/results.xml');
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });

  it('isolates Unity batch execution from the working project', () => {
    const root = mkdtempSync(resolve(tmpdir(), 'armada-unity-state-'));
    const settingsPath = resolve(root, 'unity/ProjectSettings/ProjectSettings.asset');
    try {
      mkdirSync(resolve(root, 'unity/Assets'), { recursive: true });
      mkdirSync(resolve(root, 'unity/Packages'), { recursive: true });
      mkdirSync(resolve(root, 'unity/ProjectSettings'), { recursive: true });
      writeFileSync(resolve(root, 'unity/Assets/source.cs'), 'original\n');
      writeFileSync(resolve(root, 'unity/Packages/manifest.json'), '{}\n');
      writeFileSync(settingsPath, 'runInBackground: 0\n');
      const sandbox = createUnityProjectSandbox(root);
      expect(sandbox.projectPath).not.toBe(resolve(root, 'unity'));
      writeFileSync(resolve(sandbox.projectPath, 'ProjectSettings/ProjectSettings.asset'), 'runInBackground: 1\n');
      expect(readFileSync(settingsPath, 'utf8')).toBe('runInBackground: 0\n');
      sandbox.cleanup();
      expect(existsSync(sandbox.projectPath)).toBe(false);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });

  it('rejects commented MCP safeguards and active unsafe settings', () => {
    expect(
      validateUnityMcpConfig(`
[mcp_servers.unityMCP]
# command = "node"
command = "other"
# args = ["scripts/harness/launch-unity-mcp.mjs"]
args = ["latest"]
# required = false
required = true
# default_tools_approval_mode = "writes"
default_tools_approval_mode = "never"
`)
    ).toEqual([
      'Unity MCP command must use the repository Node launcher',
      'Unity MCP must use the repository launcher',
      'Unity MCP must remain optional when the Editor is closed',
      'Unity MCP write tools must require approval'
    ]);
  });

  it('detects protected Unity tooling on a clean committed feature branch', () => {
    const root = mkdtempSync(resolve(tmpdir(), 'armada-harness-git-'));
    const git = (...args: string[]) => {
      const result = spawnSync('git', args, { cwd: root, encoding: 'utf8' });
      expect(result.status, result.stderr).toBe(0);
    };
    try {
      git('init', '-b', 'main');
      git('config', 'user.email', 'harness@example.invalid');
      git('config', 'user.name', 'Harness Test');
      writeFileSync(resolve(root, 'README.md'), 'baseline\n');
      git('add', '.');
      git('commit', '-m', 'baseline');
      git('switch', '-c', 'feature');
      mkdirSync(resolve(root, 'unity/Packages'), { recursive: true });
      writeFileSync(resolve(root, 'unity/Packages/manifest.json'), '{}\n');
      git('add', '.');
      git('commit', '-m', 'add unity tooling');

      const changedPaths = readChangedPaths(root, { HARNESS_BASE_REF: 'main' });
      expect(changedPaths).toContain('unity/Packages/manifest.json');
      expect(requiresUnityCompilation(changedPaths, {})).toBe(true);
    } finally {
      rmSync(root, { recursive: true, force: true });
    }
  });
});
