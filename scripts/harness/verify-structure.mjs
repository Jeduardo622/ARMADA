import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const REQUIRED_HARNESS_FILES = [
  'AGENTS.md',
  'src/AGENTS.md',
  'unity/AGENTS.md',
  'prisma/AGENTS.md',
  '.github/AGENTS.md',
  'tests/AGENTS.md',
  '.github/workflows/codex-shadow-evals.yml',
  '.github/codex/prompts/shadow-evals.md',
  '.codex/skills/backend-delivery/SKILL.md',
  '.codex/skills/unity-delivery/SKILL.md',
  '.codex/skills/qa-verification/SKILL.md',
  '.codex/skills/security-review/SKILL.md',
  '.codex/skills/release-readiness/SKILL.md',
  'scripts/harness/policy.json',
  'scripts/harness/codex-shadow-evals.mjs',
  'scripts/harness/codex-shadow-evals.d.mts',
  'scripts/harness/codex-shadow-response.schema.json',
  'scripts/harness/resolve-ci-scope.mjs',
  'scripts/harness/launch-unity-mcp.mjs',
  'scripts/harness/unity-ci-evidence.mjs',
  'scripts/harness/unity-project-sandbox.mjs',
  'scripts/harness/unity-test-results.mjs',
  'scripts/harness/verify-database.mjs',
  'scripts/harness/verify-deployment.mjs',
  'scripts/harness/verify-unity-tests.mjs',
  'tests/harness/fixtures/codex-shadow-evals.json',
  'tests/harness/fixtures/codex-shadow-responses.json'
];

const ROOT_REQUIREMENTS = [
  'inspect, classify, plan when needed',
  'npm run verify:local',
  'Class D',
  'human merge',
  'Completion Report'
];

const REQUIRED_PACKAGE_SCRIPTS = [
  'lint',
  'typecheck',
  'test',
  'build',
  'test:harness',
  'verify:structure',
  'verify:contracts',
  'verify:database',
  'verify:deployment',
  'verify:unity',
  'verify:unity:compile',
  'verify:unity:tests',
  'verify:dependencies',
  'verify:secrets',
  'verify:policy',
  'verify:harness',
  'verify:local'
];

export function verifyStructure(root) {
  const failures = [];
  for (const path of REQUIRED_HARNESS_FILES) {
    const absolutePath = resolve(root, path);
    if (!existsSync(absolutePath)) {
      failures.push(`missing ${path}`);
      continue;
    }
    if (readFileSync(absolutePath, 'utf8').trim().length < 40) failures.push(`empty ${path}`);
  }

  const rootGuide = existsSync(resolve(root, 'AGENTS.md'))
    ? readFileSync(resolve(root, 'AGENTS.md'), 'utf8')
    : '';
  for (const requirement of ROOT_REQUIREMENTS) {
    if (!rootGuide.toLowerCase().includes(requirement.toLowerCase())) {
      failures.push(`AGENTS.md missing: ${requirement}`);
    }
  }

  for (const path of REQUIRED_HARNESS_FILES.filter((item) => item.endsWith('/SKILL.md'))) {
    const absolutePath = resolve(root, path);
    if (!existsSync(absolutePath)) continue;
    const content = readFileSync(absolutePath, 'utf8');
    if (!/^---\r?\nname: [a-z0-9-]+\r?\ndescription: .+\r?\n---/m.test(content)) {
      failures.push(`invalid skill frontmatter: ${path}`);
    }
  }

  const packagePath = resolve(root, 'package.json');
  if (!existsSync(packagePath)) {
    failures.push('missing package.json');
  } else {
    const packageJson = JSON.parse(readFileSync(packagePath, 'utf8'));
    for (const script of REQUIRED_PACKAGE_SCRIPTS) {
      if (typeof packageJson.scripts?.[script] !== 'string') failures.push(`missing npm script: ${script}`);
    }
  }

  return failures.length === 0
    ? { id: 'harness_structure', status: 'passed', summary: `${REQUIRED_HARNESS_FILES.length} required files validated`, details: [] }
    : { id: 'harness_structure', status: 'failed', summary: `${failures.length} structure violations`, details: failures };
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const result = verifyStructure(process.cwd());
  const stream = result.status === 'passed' ? process.stdout : process.stderr;
  stream.write(`${JSON.stringify(result)}\n`);
  if (result.status !== 'passed') process.exitCode = 1;
}
