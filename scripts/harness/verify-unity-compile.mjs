import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { delimiter, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const COMPILER_ERROR = /error CS\d+|compilation failed|scripts have compiler errors/i;
const SUCCESSFUL_BATCH_EXIT = /Exiting batchmode successfully|Batchmode quit successfully invoked/i;

export function buildUnityCompileArgs(root, logPath) {
  return [
    '-batchmode',
    '-nographics',
    '-quit',
    '-projectPath',
    resolve(root, 'unity'),
    '-logFile',
    logPath
  ];
}

export function classifyUnityCompilation(exitCode, log) {
  return exitCode === 0 && SUCCESSFUL_BATCH_EXIT.test(log) && !COMPILER_ERROR.test(log)
    ? 'passed'
    : 'failed';
}

export function parseUnityProjectVersion(projectVersionText) {
  return /^m_EditorVersion:\s*(\S+)$/m.exec(projectVersionText)?.[1] ?? null;
}

export function parseUnityEditorVersion(output) {
  return /\b(\d+\.\d+\.\d+[a-z]\d+)\b/i.exec(output)?.[1] ?? null;
}

export function resolveUvxExecutable(env = process.env) {
  for (const entry of (env.PATH ?? '').split(delimiter).filter(Boolean)) {
    for (const name of process.platform === 'win32' ? ['uvx.exe', 'uvx.cmd', 'uvx'] : ['uvx']) {
      const candidate = join(entry.replace(/^"|"$/g, ''), name);
      if (existsSync(candidate)) return candidate;
    }
  }
  if (process.platform !== 'win32' || !env.LOCALAPPDATA) return null;
  const packagesRoot = join(env.LOCALAPPDATA, 'Microsoft', 'WinGet', 'Packages');
  if (!existsSync(packagesRoot)) return null;
  const uvPackage = readdirSync(packagesRoot).find((name) => name.startsWith('astral-sh.uv_'));
  if (!uvPackage) return null;
  const candidate = join(packagesRoot, uvPackage, 'uvx.exe');
  return existsSync(candidate) ? candidate : null;
}

export function runUnityCompilation(root = process.cwd(), editorPath = process.env.UNITY_EDITOR_PATH) {
  if (!editorPath || !existsSync(editorPath)) {
    return {
      id: 'unity_compilation',
      executed: false,
      status: 'failed',
      summary: 'UNITY_EDITOR_PATH does not reference an installed Unity Editor',
      details: []
    };
  }

  const projectVersionPath = resolve(root, 'unity/ProjectSettings/ProjectVersion.txt');
  const expectedVersion = existsSync(projectVersionPath)
    ? parseUnityProjectVersion(readFileSync(projectVersionPath, 'utf8'))
    : null;
  const versionResult = spawnSync(editorPath, ['-version'], {
    cwd: root,
    encoding: 'utf8',
    timeout: 30_000,
    windowsHide: true
  });
  const actualVersion = parseUnityEditorVersion(`${versionResult.stdout ?? ''}\n${versionResult.stderr ?? ''}`);
  if (!expectedVersion || versionResult.status !== 0 || actualVersion !== expectedVersion) {
    return {
      id: 'unity_compilation',
      executed: false,
      status: 'failed',
      summary: 'Unity Editor version does not match the project before compilation',
      details: { editorPath, expectedVersion, actualVersion, error: versionResult.error?.message ?? null }
    };
  }

  const uvxPath = resolveUvxExecutable();
  if (!uvxPath) {
    return {
      id: 'unity_compilation',
      executed: false,
      status: 'failed',
      summary: 'uvx is required for the project-scoped Unity MCP server',
      details: { editorPath, expectedVersion, actualVersion, uvxPath: null }
    };
  }

  const logPath = resolve(root, 'reports/harness/unity-compilation.log');
  mkdirSync(dirname(logPath), { recursive: true });
  rmSync(logPath, { force: true });
  const result = spawnSync(editorPath, buildUnityCompileArgs(root, logPath), {
    cwd: root,
    encoding: 'utf8',
    timeout: 300_000,
    windowsHide: true
  });
  const log = existsSync(logPath) ? readFileSync(logPath, 'utf8') : '';
  const exitCode = result.status ?? 1;
  const status = classifyUnityCompilation(exitCode, log);

  return {
    id: 'unity_compilation',
    executed: true,
    status,
    summary: status === 'passed' ? 'Unity batch compilation passed' : 'Unity batch compilation failed',
    details: {
      editorPath,
      expectedVersion,
      actualVersion,
      uvxPath,
      exitCode,
      logPath,
      error: result.error?.message ?? null
    }
  };
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const result = runUnityCompilation();
  const stream = result.status === 'passed' ? process.stdout : process.stderr;
  stream.write(`${JSON.stringify(result)}\n`);
  if (result.status !== 'passed') process.exitCode = 1;
}
