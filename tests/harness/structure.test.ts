import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { verifyStructure } from '../../scripts/harness/verify-structure.mjs';

const requiredFiles = [
  'AGENTS.md',
  'src/AGENTS.md',
  'unity/AGENTS.md',
  'prisma/AGENTS.md',
  '.github/AGENTS.md',
  'tests/AGENTS.md',
  '.codex/skills/backend-delivery/SKILL.md',
  '.codex/skills/unity-delivery/SKILL.md',
  '.codex/skills/qa-verification/SKILL.md',
  '.codex/skills/security-review/SKILL.md',
  '.codex/skills/release-readiness/SKILL.md'
];

const importableHarnessModules = [
  'scripts/harness/run-evals.mjs',
  'scripts/harness/verify-contracts.mjs',
  'scripts/harness/verify-dependencies.mjs',
  'scripts/harness/verify-local.mjs',
  'scripts/harness/verify-policy.mjs',
  'scripts/harness/verify-secrets.mjs',
  'scripts/harness/verify-structure.mjs',
  'scripts/harness/verify-unity-compile.mjs',
  'scripts/harness/launch-unity-mcp.mjs',
  'scripts/harness/verify-unity.mjs'
];

describe('engineering harness structure', () => {
  it.each(requiredFiles)('%s exists and is non-empty', (path) => {
    expect(readFileSync(path, 'utf8').trim().length).toBeGreaterThan(40);
  });

  it('validates instruction and skill structure from the repository root', () => {
    expect(verifyStructure(process.cwd())).toMatchObject({ status: 'passed' });
  });

  it('uses one least-privilege CI workflow for the local verification contract', () => {
    const workflow = readFileSync('.github/workflows/ci.yml', 'utf8').replace(/\r\n/g, '\n');
    expect(workflow.match(/^name:/gm)).toHaveLength(1);
    expect(workflow.match(/^jobs:/gm)).toHaveLength(1);
    expect(workflow).toContain('permissions:\n  contents: read');
    expect(workflow).toContain('npm ci');
    expect(workflow).toContain('fetch-depth: 0');
    expect(workflow).toContain('HARNESS_BASE_REF:');
    expect(workflow).toContain('npm run verify:local');
    expect(workflow).toContain('actions/upload-artifact@v4');
    expect(workflow).not.toMatch(/placeholder/i);
    expect(workflow).not.toContain('npm install');
  });

  it.each(importableHarnessModules)('%s is portable when imported after CRLF checkout', (path) => {
    expect(readFileSync(path, 'utf8')).not.toMatch(/^#!/);
  });

  it('excludes local worktrees from Vitest discovery', () => {
    expect(readFileSync('vitest.config.ts', 'utf8')).toContain('.worktrees');
  });

  it('pins the project-scoped Unity MCP integration with write approvals', () => {
    const manifest = JSON.parse(readFileSync('unity/Packages/manifest.json', 'utf8'));
    expect(manifest.dependencies?.['com.coplaydev.unity-mcp']).toBe(
      'https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.0.0'
    );
    const packageLock = JSON.parse(readFileSync('unity/Packages/packages-lock.json', 'utf8'));
    expect(packageLock.dependencies?.['com.coplaydev.unity-mcp']?.hash).toBe(
      'd49ae2953580f3481beb1e084a1da2682f0b5610'
    );

    const config = readFileSync('.codex/config.toml', 'utf8');
    expect(config).toContain('[mcp_servers.unityMCP]');
    expect(config).toContain('command = "node"');
    expect(config).toContain('args = ["scripts/harness/launch-unity-mcp.mjs"]');
    expect(config).toContain('default_tools_approval_mode = "writes"');
    expect(config).toContain('required = false');
  });

  it('requires harness evidence in the pull request template', () => {
    const template = readFileSync('.github/pull_request_template.md', 'utf8');
    for (const field of [
      'Task classification',
      'Changed paths',
      'Executed checks',
      'Blocked checks',
      'Required reviewers',
      'Rollback evidence',
      'Dependency exceptions',
      'Unity compilation',
      'Residual risk'
    ]) {
      expect(template).toContain(field);
    }
  });
});
