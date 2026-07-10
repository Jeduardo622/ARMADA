import { describe, expect, it } from 'vitest';
import { runEvaluations } from '../../scripts/harness/run-evals.mjs';
import { verifyContracts } from '../../scripts/harness/verify-contracts.mjs';
import { verifyUnity } from '../../scripts/harness/verify-unity.mjs';
import { CHECK_DEFINITIONS } from '../../scripts/harness/verify-local.mjs';

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
        summary: 'licensed Unity runner unavailable'
      }
    });
  });

  it('evaluates all routing fixtures deterministically', () => {
    expect(runEvaluations(process.cwd())).toMatchObject({
      id: 'harness_evals',
      status: 'passed',
      fixtureCount: 13
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
});
