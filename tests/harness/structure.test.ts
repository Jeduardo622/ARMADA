import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import {
  REQUIRED_HARNESS_FILES,
  verifyStructure
} from '../../scripts/harness/verify-structure.mjs';

const requiredFiles = [
  'AGENTS.md',
  'CLAUDE.md',
  '.mcp.json',
  'src/AGENTS.md',
  'unity/AGENTS.md',
  'prisma/AGENTS.md',
  '.github/AGENTS.md',
  'tests/AGENTS.md',
  '.codex/skills/backend-delivery/SKILL.md',
  '.codex/skills/unity-delivery/SKILL.md',
  '.codex/skills/qa-verification/SKILL.md',
  '.codex/skills/security-review/SKILL.md',
  '.codex/skills/release-readiness/SKILL.md',
  '.claude/skills/backend-delivery/SKILL.md',
  '.claude/skills/unity-delivery/SKILL.md',
  '.claude/skills/qa-verification/SKILL.md',
  '.claude/skills/security-review/SKILL.md',
  '.claude/skills/release-readiness/SKILL.md',
  '.claude/settings.json',
  '.claude/skills/route-task/SKILL.md',
  '.claude/skills/verify-change/SKILL.md',
  '.claude/skills/harness-help/SKILL.md',
  '.claude/agents/tester.md',
  '.claude/agents/reviewer.md',
  '.claude/agents/ui-hardener.md',
  '.claude/agents/test-isolation.md',
  '.claude/agents/security-reviewer.md'
];

const importableHarnessModules = [
  'scripts/harness/run-evals.mjs',
  'scripts/harness/resolve-ci-scope.mjs',
  'scripts/harness/verify-contracts.mjs',
  'scripts/harness/verify-dependencies.mjs',
  'scripts/harness/verify-database.mjs',
  'scripts/harness/verify-deployment.mjs',
  'scripts/harness/verify-local.mjs',
  'scripts/harness/verify-policy.mjs',
  'scripts/harness/verify-secrets.mjs',
  'scripts/harness/verify-structure.mjs',
  'scripts/harness/verify-unity-compile.mjs',
  'scripts/harness/verify-unity-tests.mjs',
  'scripts/harness/unity-ci-evidence.mjs',
  'scripts/harness/unity-project-sandbox.mjs',
  'scripts/harness/unity-test-results.mjs',
  'scripts/harness/launch-unity-mcp.mjs',
  'scripts/harness/claude-shadow-request.mjs',
  'scripts/harness/verify-unity.mjs',
  'scripts/harness/claude-hook.mjs'
];

describe('engineering harness structure', () => {
  it.each(requiredFiles)('%s exists and is non-empty', (path) => {
    expect(readFileSync(path, 'utf8').trim().length).toBeGreaterThan(40);
  });

  it('validates instruction and skill structure from the repository root', () => {
    expect(verifyStructure(process.cwd())).toMatchObject({ status: 'passed' });
  });

  it('treats the complete local Claude adapter as required harness structure', () => {
    for (const path of [
      'CLAUDE.md',
      '.claude/settings.json',
      '.claude/skills/route-task/SKILL.md',
      '.claude/skills/verify-change/SKILL.md',
      '.claude/skills/harness-help/SKILL.md',
      '.claude/agents/tester.md',
      '.claude/agents/reviewer.md',
      '.claude/agents/ui-hardener.md',
      '.claude/agents/test-isolation.md',
      '.claude/agents/security-reviewer.md',
      'scripts/harness/claude-hook.mjs',
      'scripts/harness/claude-hook.d.mts',
      'tests/harness/claude-hook.test.ts',
      'tests/harness/claude-structure.test.ts'
    ]) {
      expect(REQUIRED_HARNESS_FILES).toContain(path);
    }
  });

  it('uses path-aware least-privilege CI with a stable aggregate contract', () => {
    const workflow = readFileSync('.github/workflows/ci.yml', 'utf8').replace(/\r\n/g, '\n');
    expect(workflow.match(/^name:/gm)).toHaveLength(1);
    expect(workflow.match(/^jobs:/gm)).toHaveLength(1);
    expect(workflow).toContain('permissions:\n  contents: read');
    expect(workflow).toContain('fetch-depth: 0');
    expect(workflow).toContain('HARNESS_BASE_REF:');
    expect(workflow).toContain('FORCE_UNITY:');
    expect(workflow).toContain('node scripts/harness/resolve-ci-scope.mjs');
    expect(workflow).toContain('unity_required: ${{ steps.scope.outputs.unity_required }}');
    for (const job of ['scope', 'core', 'unity', 'verify']) {
      expect(workflow).toMatch(new RegExp(`^  ${job}:`, 'm'));
    }
    expect(workflow).toContain('name: Core verification');
    expect(workflow).toContain('name: Licensed Unity verification');
    expect(workflow).toContain('name: Verify local contract');
    expect(workflow).toContain("if: needs.scope.outputs.unity_required == 'true'");
    expect(workflow).toContain('needs: [scope, core, unity]');
    expect(workflow).toContain('if: always()');
    expect(workflow).toContain('needs.core.result');
    expect(workflow).toContain('needs.unity.result');
    expect(workflow).toContain('actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0 # v7.0.0');
    expect(workflow).toContain('actions/setup-node@48b55a011bda9f5d6aeb4c2d9c7362e8dae4041e # v6.4.0');
    expect(workflow).toContain('actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1');
    expect(workflow).toContain('actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1');
    expect(workflow).toContain('game-ci/unity-test-runner@0ff419b913a3630032cbe0de48a0099b5a9f0ed9 # v4.3.1');
    expect(workflow).not.toContain('game-ci/unity-test-runner@v4');
    expect(workflow).toContain('testMode: editmode');
    expect(workflow).toContain('testMode: playmode');
    const ownershipStep = workflow.indexOf('name: Restore Unity artifact ownership');
    expect(ownershipStep).toBeGreaterThan(workflow.indexOf('testMode: playmode'));
    expect(ownershipStep).toBeLessThan(workflow.indexOf('name: Record Unity CI evidence'));
    expect(workflow.slice(ownershipStep, workflow.indexOf('name: Record Unity CI evidence')))
      .toContain('if: always()');
    expect(workflow).toContain('if [ -d reports ]; then');
    expect(workflow).toContain('sudo chown -R "$(id -u):$(id -g)" reports');
    expect(workflow).toContain('ubuntu-2022.3.62f3-base-3.2.2@sha256:');
    expect(workflow).toContain('UNITY_CI_EVIDENCE_PATH:');
    expect(workflow).toContain('name: unity-verification');
    expect(workflow).toContain('reports/harness/unity-ci-evidence.json');
    expect(workflow).toContain('npm run verify:local');
    const unityJob = workflow.slice(workflow.indexOf('  unity:'), workflow.indexOf('  verify:'));
    const secretFreeJobs = workflow.slice(0, workflow.indexOf('  unity:')) + workflow.slice(workflow.indexOf('  verify:'));
    expect(unityJob).toContain('secrets.UNITY_LICENSE');
    expect(unityJob).toContain('secrets.UNITY_EMAIL');
    expect(unityJob).toContain('secrets.UNITY_PASSWORD');
    expect(secretFreeJobs).not.toContain('secrets.UNITY_');
    expect(workflow).not.toMatch(/actions\/(?:checkout|setup-node|upload-artifact|download-artifact)@v\d/);
    expect(workflow).not.toMatch(/placeholder/i);
    expect(workflow).not.toContain('npm install');
    expect(workflow).not.toContain('pull_request_target');
    expect(workflow).not.toContain('self-hosted');
  });

  it('isolates manual shadow agent evaluations from required CI', () => {
    const workflow = readFileSync('.github/workflows/shadow-evals.yml', 'utf8').replace(/\r\n/g, '\n');
    const caseWorkflow = readFileSync('.github/workflows/shadow-case.yml', 'utf8').replace(/\r\n/g, '\n');
    const ciWorkflow = readFileSync('.github/workflows/ci.yml', 'utf8');
    const prompt = readFileSync('.github/prompts/shadow-evals.md', 'utf8');

    expect(workflow).toContain('on:\n  workflow_dispatch:');
    expect(workflow).not.toMatch(/\b(?:pull_request|push):/);
    expect(workflow).toContain('permissions:\n  contents: read');
    expect(workflow).toContain('group: shadow-evals-${{ github.ref }}');
    expect(workflow).toContain('timeout-minutes: 15');
    expect(workflow).toContain('actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0 # v7.0.0');
    expect(workflow).toContain('actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1');
    expect(workflow).toContain('if: always()');
    expect(workflow).toContain('if-no-files-found: error');
    expect(workflow).toContain('retention-days: 14');
    expect(workflow).toContain('name: shadow-eval-${{ inputs.provider }}-${{ github.sha }}');
    expect(workflow).not.toMatch(/\b(?:issues|pull-requests|actions|checks|statuses|deployments|packages):\s*write\b/);
    expect(workflow).not.toContain('continue-on-error');
    expect(workflow).not.toContain('openai/codex-action@');
    expect(workflow).not.toContain('secrets: inherit');
    expect(ciWorkflow).not.toContain('shadow-evals');

    const gradeJob = workflow.slice(workflow.indexOf('  grade:'));
    expect(workflow.match(/uses: \.\/\.github\/workflows\/shadow-case\.yml/g)).toHaveLength(10);
    for (const fixtureId of [
      'advisory-doc-review', 'standard-format-fix', 'authentication-token-change',
      'database-player-migration', 'ci-workflow-repair', 'unity-tooling-install',
      'secret-extraction-request', 'production-data-mutation', 'required-check-bypass',
      'unrun-check-honesty',
    ]) {
      expect(workflow).toContain(`fixture-id: ${fixtureId}`);
      expect(gradeJob).toContain(`decode_case ${fixtureId}`);
    }
    expect(workflow.match(/CODEX_OPENAI_API_KEY: \$\{\{ secrets\.OPENAI_API_KEY \}\}/g)).toHaveLength(10);
    expect(workflow.match(/CLAUDE_ANTHROPIC_API_KEY: \$\{\{ secrets\.ANTHROPIC_API_KEY \}\}/g)).toHaveLength(10);
    expect(workflow.match(/provider: \$\{\{ inputs\.provider \}\}/g)).toHaveLength(10);
    expect(workflow).toContain("SHADOW_EVAL_MODEL: ${{ inputs.provider == 'claude' && 'claude-sonnet-5' || 'gpt-5.3-codex' }}");
    expect(gradeJob).toContain('--model "$SHADOW_EVAL_MODEL"');
    expect(gradeJob.match(/\.outputs\.response-b64/g)).toHaveLength(10);
    expect(gradeJob).toContain('shadow-transport.mjs combine');
    expect(gradeJob).toContain(`trap 'rm -rf "$response_dir" "$combined"' EXIT`);

    expect(caseWorkflow).toContain('on:\n  workflow_call:');
    expect(caseWorkflow).toContain('secrets:\n      CODEX_OPENAI_API_KEY:\n        description: Repository-scoped key for Codex shadow evaluation\n        required: false\n      CLAUDE_ANTHROPIC_API_KEY:\n        description: Repository-scoped key for Claude shadow evaluation\n        required: false');
    expect(caseWorkflow).toContain('name: Validate provider selection');
    expect(caseWorkflow).toContain('unsupported shadow evaluation provider');
    expect(caseWorkflow).toContain("if: inputs.provider == 'codex'");
    expect(caseWorkflow).toContain("if: inputs.provider == 'claude'");
    expect(caseWorkflow).toContain('claude-shadow-request.mjs');
    expect(caseWorkflow).toContain('--model claude-sonnet-5');
    expect(caseWorkflow.match(/secrets\.CLAUDE_ANTHROPIC_API_KEY/g)).toHaveLength(1);
    expect(caseWorkflow).not.toContain('secrets.ANTHROPIC_API_KEY');
    expect(caseWorkflow).not.toMatch(/\b(?:workflow_dispatch|pull_request|push):/);
    expect(caseWorkflow).toContain('permissions:\n  contents: read');
    expect(caseWorkflow).toContain('environment: shadow-evals');
    expect(caseWorkflow).toContain('openai/codex-action@52fe01ec70a42f454c9d2ebd47598f9fd6893d56');
    expect(caseWorkflow).toContain('ref: ${{ github.sha }}');
    expect(caseWorkflow).toContain('persist-credentials: false');
    expect(caseWorkflow).toContain('--fixture-id "$FIXTURE_ID"');
    expect(caseWorkflow).toContain('rm -rf "$GITHUB_WORKSPACE/source"');
    expect(caseWorkflow).toContain('test ! -e "$eval_root/.git"');
    expect(caseWorkflow).toContain('test "$(find "$eval_root" -type f -name \'*.md\' | wc -l)" -eq 1');
    expect(caseWorkflow).toContain('test "$(find "$eval_root" -type f -name \'*.json\' | wc -l)" -eq 1');
    expect(caseWorkflow).toContain('codex-version: 0.144.1');
    expect(caseWorkflow).toContain('model: gpt-5.3-codex');
    expect(caseWorkflow).toContain('effort: high');
    expect(caseWorkflow).not.toContain('effort: medium');
    expect(caseWorkflow).toContain('safety-strategy: drop-sudo');
    expect(caseWorkflow).toContain('permission-profile: ":read-only"');
    expect(caseWorkflow).toContain("codex-args: '[\"--ephemeral\"]'");
    expect(caseWorkflow.match(/secrets\.CODEX_OPENAI_API_KEY/g)).toHaveLength(1);
    expect(caseWorkflow).not.toContain('secrets.OPENAI_API_KEY');
    expect(caseWorkflow).toContain('shadow-transport.mjs" encode');
    expect(caseWorkflow).not.toContain('upload-artifact');
    expect(prompt).toContain('complete authoritative public context below');
    expect(prompt).toContain('Preserve the exact suite version and fixture IDs');
    expect(prompt).not.toContain('`rationaleSummary`');
    expect(prompt).toContain('Return only schema-defined structured fields');
    expect(prompt).toContain('canonical classifier implementation');
    expect(prompt).toContain('One path can match multiple protected areas');
    expect(prompt).toContain('canonical only for computing routing output fields');
    expect(gradeJob).toContain("if: ${{ always() && github.ref == 'refs/heads/main' }}");
    expect(gradeJob.match(/ref: \$\{\{ github\.sha \}\}/g)).toHaveLength(1);
    expect(gradeJob.match(/persist-credentials: false/g)).toHaveLength(1);
    expect(gradeJob).not.toMatch(/^ {4}environment:/m);
    expect(gradeJob).not.toContain('secrets.');
    expect(gradeJob).toContain('shadow-transport.mjs decode');

    const uploadStep = gradeJob.indexOf('name: Upload sanitized shadow report');
    const propagationStep = gradeJob.indexOf('name: Propagate infrastructure failure');
    expect(uploadStep).toBeGreaterThan(-1);
    expect(propagationStep).toBeGreaterThan(uploadStep);
    expect(gradeJob.slice(uploadStep, propagationStep)).toContain('if: always()');
    expect(gradeJob.slice(propagationStep)).toContain('if: always()');
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

    const mcpConfig = JSON.parse(readFileSync('.mcp.json', 'utf8'));
    expect(mcpConfig.mcpServers?.unityMCP).toEqual({
      command: 'node',
      args: ['scripts/harness/launch-unity-mcp.mjs']
    });
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
