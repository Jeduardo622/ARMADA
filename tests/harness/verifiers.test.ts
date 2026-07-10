import { describe, expect, it } from 'vitest';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { runEvaluations } from '../../scripts/harness/run-evals.mjs';
import { verifyContracts } from '../../scripts/harness/verify-contracts.mjs';
import { validateUnityMcpConfig, verifyUnity } from '../../scripts/harness/verify-unity.mjs';
import {
  buildUnityCompileArgs,
  classifyUnityCompilation,
  parseUnityEditorVersion,
  parseUnityProjectVersion
} from '../../scripts/harness/verify-unity-compile.mjs';
import {
  CHECK_DEFINITIONS,
  readChangedPaths,
  requiresUnityCompilation,
  resolveConditionalCheck
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
      fixtureCount: 16
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
      'unity_static',
      'unity_compilation',
      'dependencies',
      'secrets',
      'harness_policy'
    ]);
  });

  it('runs licensed Unity compilation only when an editor is configured', () => {
    const definition = CHECK_DEFINITIONS.find((check) => check.id === 'unity_compilation');
    expect(definition).toMatchObject({
      command: 'npm run verify:unity:compile',
      whenEnv: 'UNITY_EDITOR_PATH',
      requiredWhenEnv: 'UNITY_COMPILATION_REQUIRED'
    });

    expect(resolveConditionalCheck(definition!, {})).toMatchObject({ status: 'not_applicable' });
    expect(resolveConditionalCheck(definition!, { UNITY_COMPILATION_REQUIRED: '1' })).toMatchObject({
      status: 'failed',
      summary: 'UNITY_EDITOR_PATH is required for this verification scope'
    });
    expect(resolveConditionalCheck(definition!, { UNITY_EDITOR_PATH: 'C:/Unity/Unity.exe' })).toBeNull();
    expect(requiresUnityCompilation(['unity/Packages/manifest.json'], {})).toBe(true);
    expect(requiresUnityCompilation(['.codex/config.toml'], {})).toBe(true);
    expect(requiresUnityCompilation(['unity/Assets/Armada/UI/MissionUIController.cs'], {})).toBe(false);

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
