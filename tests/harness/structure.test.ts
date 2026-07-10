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

describe('engineering harness structure', () => {
  it.each(requiredFiles)('%s exists and is non-empty', (path) => {
    expect(readFileSync(path, 'utf8').trim().length).toBeGreaterThan(40);
  });

  it('validates instruction and skill structure from the repository root', () => {
    expect(verifyStructure(process.cwd())).toMatchObject({ status: 'passed' });
  });
});
