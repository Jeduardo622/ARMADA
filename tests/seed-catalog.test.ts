import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, it, expect } from 'vitest';
import { MISSION_01_CODE } from '../src/sim/mission01.js';
import { MISSION_02_CODE } from '../src/sim/mission02.js';
import { MISSION_03_CODE } from '../src/sim/mission03.js';
import { MISSION_04_CODE } from '../src/sim/mission04.js';
import { MISSION_05_CODE } from '../src/sim/mission05.js';

// The seed catalog is the source /missions/:code/complete resolves against,
// so its codes must stay aligned with the runtime mission slugs.
describe('mission seed catalog', () => {
  const seedSource = readFileSync(resolve(__dirname, '../prisma/seed.ts'), 'utf8');

  it.each([MISSION_01_CODE, MISSION_02_CODE, MISSION_03_CODE, MISSION_04_CODE, MISSION_05_CODE])(
    'seeds a mission row for %s',
    (code) => {
      expect(seedSource).toContain(`code: '${code}'`);
    }
  );

  it('does not seed legacy short mission codes', () => {
    expect(seedSource).not.toMatch(/code:\s*'mission-\d+'/);
  });
});
