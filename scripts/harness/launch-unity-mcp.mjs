import { spawnSync } from 'node:child_process';
import { resolveUvxExecutable } from './verify-unity-compile.mjs';

const uvxPath = resolveUvxExecutable();
if (!uvxPath) {
  process.stderr.write('Unity MCP startup failed: uvx is not installed or discoverable.\n');
  process.exit(1);
}

const result = spawnSync(
  uvxPath,
  ['--from', 'mcpforunityserver==10.0.0', 'mcp-for-unity', '--transport', 'stdio'],
  { stdio: 'inherit', windowsHide: true }
);
process.exit(result.status ?? 1);
