import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const REQUIRED_UNITY_FILES = [
  '.codex/config.toml',
  'unity/ProjectSettings/ProjectVersion.txt',
  'unity/Packages/manifest.json',
  'unity/Assets/Armada/Core/ApiClient.cs',
  'unity/Assets/Armada/Core/DeterministicSimHooks.cs',
  'unity/Assets/Armada/Services/SimService.cs'
];

const UNITY_MCP_PACKAGE = 'https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.0.0';
const UNITY_MCP_COMMIT = 'd49ae2953580f3481beb1e084a1da2682f0b5610';

export function validateUnityMcpConfig(config) {
  const details = [];
  const lines = config.split(/\r?\n/);
  const sectionIndexes = lines
    .map((line, index) => (/^\s*\[mcp_servers\.unityMCP\]\s*(?:#.*)?$/.test(line) ? index : -1))
    .filter((index) => index >= 0);
  if (sectionIndexes.length !== 1) return ['Unity MCP Codex server must have exactly one active section'];

  const assignments = new Map();
  for (let index = sectionIndexes[0] + 1; index < lines.length; index += 1) {
    const line = lines[index];
    if (/^\s*\[/.test(line)) break;
    if (/^\s*(?:#|$)/.test(line)) continue;
    const match = /^\s*([A-Za-z0-9_]+)\s*=\s*(.*?)\s*(?:#.*)?$/.exec(line);
    if (!match) continue;
    const [, key, value] = match;
    if (assignments.has(key)) details.push(`Unity MCP setting ${key} must not be duplicated`);
    assignments.set(key, value);
  }

  if (assignments.get('command') !== '"node"') details.push('Unity MCP command must use the repository Node launcher');
  let args;
  try {
    args = JSON.parse(assignments.get('args') ?? 'null');
  } catch {
    args = null;
  }
  if (
    JSON.stringify(args) !== JSON.stringify(['scripts/harness/launch-unity-mcp.mjs'])
  ) {
    details.push('Unity MCP must use the repository launcher');
  }
  if (assignments.get('required') !== 'false') details.push('Unity MCP must remain optional when the Editor is closed');
  if (assignments.get('default_tools_approval_mode') !== '"writes"') {
    details.push('Unity MCP write tools must require approval');
  }
  return details;
}

export function verifyUnity(root) {
  const details = REQUIRED_UNITY_FILES
    .filter((path) => !existsSync(resolve(root, path)))
    .map((path) => `missing ${path}`);
  const manifestPath = resolve(root, 'unity/Packages/manifest.json');
  if (existsSync(manifestPath)) {
    try {
      const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
      if (!manifest.dependencies?.['com.unity.addressables']) details.push('Addressables package is missing');
      if (!manifest.dependencies?.['com.unity.test-framework']) details.push('Unity test framework is missing');
      if (manifest.dependencies?.['com.coplaydev.unity-mcp'] !== UNITY_MCP_PACKAGE) {
        details.push('Unity MCP package must be pinned to v10.0.0');
      }
    } catch {
      details.push('Unity package manifest is invalid JSON');
    }
  }
  const packageLockPath = resolve(root, 'unity/Packages/packages-lock.json');
  if (existsSync(packageLockPath)) {
    try {
      const packageLock = JSON.parse(readFileSync(packageLockPath, 'utf8'));
      if (packageLock.dependencies?.['com.coplaydev.unity-mcp']?.hash !== UNITY_MCP_COMMIT) {
        details.push('Unity MCP package lock must resolve v10.0.0 to the reviewed commit');
      }
    } catch {
      details.push('Unity package lock is invalid JSON');
    }
  }
  const mcpConfigPath = resolve(root, '.codex/config.toml');
  if (existsSync(mcpConfigPath)) {
    const config = readFileSync(mcpConfigPath, 'utf8');
    details.push(...validateUnityMcpConfig(config));
  }
  const mcpLauncherPath = resolve(root, 'scripts/harness/launch-unity-mcp.mjs');
  if (existsSync(mcpLauncherPath)) {
    const launcher = readFileSync(mcpLauncherPath, 'utf8');
    if (!launcher.includes("'mcpforunityserver==10.0.0'") || !launcher.includes("'--transport', 'stdio'")) {
      details.push('Unity MCP launcher must pin server 10.0.0 over stdio');
    }
  } else {
    details.push('Unity MCP repository launcher is missing');
  }
  const hooksPath = resolve(root, 'unity/Assets/Armada/Core/DeterministicSimHooks.cs');
  if (existsSync(hooksPath)) {
    const hooks = readFileSync(hooksPath, 'utf8');
    if (!hooks.includes('Random.InitState') || !hooks.includes('fixedDeltaTime')) {
      details.push('Deterministic hooks must seed randomness and set fixedDeltaTime');
    }
  }
  return {
    id: 'unity_static',
    status: details.length === 0 ? 'passed' : 'failed',
    summary: details.length === 0 ? `${REQUIRED_UNITY_FILES.length} Unity metadata boundaries validated` : `${details.length} Unity violations`,
    details,
    compilation: {
      id: 'unity_compilation',
      executed: false,
      status: 'not_applicable',
      summary: 'set UNITY_EDITOR_PATH to execute licensed Unity compilation'
    }
  };
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const result = verifyUnity(process.cwd());
  const stream = result.status === 'passed' ? process.stdout : process.stderr;
  stream.write(`${JSON.stringify(result)}\n`);
  if (result.status !== 'passed') process.exitCode = 1;
}
