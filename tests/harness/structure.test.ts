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
  'scripts/harness/verify-unity.mjs'
];

describe('engineering harness structure', () => {
  it.each(requiredFiles)('%s exists and is non-empty', (path) => {
    expect(readFileSync(path, 'utf8').trim().length).toBeGreaterThan(40);
  });

  it('validates instruction and skill structure from the repository root', () => {
    expect(verifyStructure(process.cwd())).toMatchObject({ status: 'passed' });
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

  it('isolates manual shadow Codex evaluations from required CI', () => {
    const workflow = readFileSync('.github/workflows/codex-shadow-evals.yml', 'utf8').replace(/\r\n/g, '\n');
    const ciWorkflow = readFileSync('.github/workflows/ci.yml', 'utf8');
    const prompt = readFileSync('.github/codex/prompts/shadow-evals.md', 'utf8');

    expect(workflow).toContain('on:\n  workflow_dispatch:');
    expect(workflow).not.toMatch(/\b(?:pull_request|push):/);
    expect(workflow).toContain('permissions:\n  contents: read');
    expect(workflow).toContain('group: codex-shadow-evals-${{ github.ref }}');
    expect(workflow).toContain('timeout-minutes: 15');
    expect(workflow).toContain('actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0 # v7.0.0');
    expect(workflow).toContain('openai/codex-action@52fe01ec70a42f454c9d2ebd47598f9fd6893d56');
    expect(workflow).toContain('actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1');
    expect(workflow).toContain('codex-version: 0.144.1');
    expect(workflow).toContain('model: gpt-5.3-codex');
    expect(workflow).toContain('effort: medium');
    expect(workflow).toContain('safety-strategy: drop-sudo');
    expect(workflow).toContain('permission-profile: ":read-only"');
    expect(workflow).toContain('working-directory: ${{ runner.temp }}/codex-shadow-workspace');
    expect(workflow).toContain('prompt-file: ${{ runner.temp }}/codex-shadow-workspace/shadow-evaluation-prompt.md');
    expect(workflow).toContain('output-file: ${{ runner.temp }}/codex-shadow-workspace/codex-shadow-response.json');
    expect(workflow).toContain('output-schema-file: ${{ runner.temp }}/codex-shadow-workspace/scripts/harness/codex-shadow-response.schema.json');
    expect(workflow).toContain("codex-args: '[\"--ephemeral\"]'");
    expect(workflow).toContain('if: always()');
    expect(workflow).toContain('if-no-files-found: error');
    expect(workflow).toContain('retention-days: 14');
    expect(workflow).toContain('name: codex-shadow-eval-${{ github.sha }}');
    expect(workflow).not.toMatch(/\b(?:issues|pull-requests|actions|checks|statuses|deployments|packages):\s*write\b/);
    expect(workflow).not.toContain('continue-on-error');
    expect(ciWorkflow).not.toContain('codex-shadow-evals');

    const evaluateJob = workflow.slice(workflow.indexOf('  evaluate:'), workflow.indexOf('  grade:'));
    const gradeJob = workflow.slice(workflow.indexOf('  grade:'));
    expect(evaluateJob).toContain("if: github.ref == 'refs/heads/main' && github.sha != ''");
    expect(evaluateJob).toContain('environment: codex-shadow-evals');
    expect(evaluateJob.match(/ref: \$\{\{ github\.sha \}\}/g)).toHaveLength(1);
    expect(evaluateJob.match(/persist-credentials: false/g)).toHaveLength(1);
    expect(evaluateJob).toContain('path: source');
    expect(evaluateJob).toContain('rm -rf "$GITHUB_WORKSPACE/source"');
    expect(evaluateJob).toContain('test ! -e "$eval_root/.git"');
    for (const guide of ['.github/AGENTS.md', 'docs/agents.md', 'src/AGENTS.md', 'tests/AGENTS.md', 'prisma/AGENTS.md', 'unity/AGENTS.md']) {
      expect(evaluateJob).toContain(`source/${guide} "$eval_root/${guide}"`);
    }
    expect(evaluateJob).toContain('find "$eval_root" -iname agents.md -type f | wc -l)" -eq 7');
    expect(evaluateJob).toContain('test ! -e "$eval_root/tests/harness/fixtures/codex-shadow-expectations.json"');
    expect(evaluateJob).toContain('test ! -e "$eval_root/tests/harness/fixtures/codex-shadow-responses.json"');
    expect(evaluateJob).toContain('node source/scripts/harness/build-codex-shadow-prompt.mjs');
    expect(evaluateJob.indexOf('node source/scripts/harness/build-codex-shadow-prompt.mjs')).toBeLessThan(evaluateJob.indexOf('rm -rf "$GITHUB_WORKSPACE/source"'));
    expect(prompt).toContain('complete authoritative public context below');
    expect(prompt).toContain('Preserve the exact suite version and fixture IDs');
    expect(prompt).not.toContain('`rationaleSummary`');
    expect(prompt).toContain('Return only schema-defined structured fields');
    const actionStep = evaluateJob.indexOf('name: Run read-only shadow evaluation');
    const transportStep = evaluateJob.indexOf('name: Encode bounded response for secret-safe transport');
    expect(actionStep).toBeGreaterThan(-1);
    expect(transportStep).toBeGreaterThan(actionStep);
    expect(evaluateJob.slice(transportStep)).toContain('codex-shadow-transport.mjs" encode');
    expect(evaluateJob.slice(transportStep)).not.toContain('secrets.');
    expect(evaluateJob.trimEnd()).toMatch(/--github-output "\$GITHUB_OUTPUT"$/);
    expect(evaluateJob.match(/secrets\.OPENAI_API_KEY/g)).toHaveLength(1);
    expect(gradeJob).toContain("if: ${{ always() && github.ref == 'refs/heads/main' }}");
    expect(gradeJob.match(/ref: \$\{\{ github\.sha \}\}/g)).toHaveLength(1);
    expect(gradeJob.match(/persist-credentials: false/g)).toHaveLength(1);
    expect(gradeJob).not.toMatch(/^ {4}environment:/m);
    expect(gradeJob).not.toContain('secrets.');
    expect(gradeJob).toContain('CODEX_SHADOW_RESPONSE_B64: ${{ needs.evaluate.outputs.response-b64 }}');
    expect(gradeJob).toContain('codex-shadow-transport.mjs decode');
    expect(gradeJob).not.toContain('needs.evaluate.outputs.response }}');

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
