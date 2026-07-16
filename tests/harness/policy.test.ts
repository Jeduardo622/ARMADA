import { describe, expect, it } from 'vitest';
import {
  validateCompletionReport,
  validateDependencyExceptions
} from '../../scripts/harness/verify-policy.mjs';
import policyFixtures from './fixtures/policy.json';
import reportFixtures from './fixtures/reports.json';

describe('completion report policy', () => {
  it('rejects a check reported passed without execution', () => {
    expect(() => validateCompletionReport(policyFixtures.invalidFalsePassReport)).toThrow(/executed/);
  });

  it('requires rollback evidence for protected work', () => {
    expect(() => validateCompletionReport(policyFixtures.invalidProtectedReport)).toThrow(/rollback/);
  });

  it('accepts protected evidence with an explicit unavailable Unity result', () => {
    expect(() => validateCompletionReport(policyFixtures.validProtectedReport)).not.toThrow();
  });

  it.each(reportFixtures)('$id', (fixture) => {
    const validate = () => validateCompletionReport(fixture.report);
    if (fixture.valid) expect(validate).not.toThrow();
    else expect(validate).toThrow();
  });
});

describe('dependency exception policy', () => {
  it('rejects exceptions longer than 90 days', () => {
    expect(() =>
      validateDependencyExceptions([
        {
          advisoryId: 1,
          package: 'example',
          rationale: 'pre-existing finding',
          owner: 'Armada engineering',
          introduced: '2026-07-09',
          expires: '2026-10-08'
        }
      ], new Date('2026-07-09T00:00:00Z'))
    ).toThrow(/90 days/);
  });

  it('rejects expired exceptions', () => {
    expect(() =>
      validateDependencyExceptions([
        {
          advisoryId: 1,
          package: 'example',
          rationale: 'pre-existing finding',
          owner: 'Armada engineering',
          introduced: '2026-04-01',
          expires: '2026-06-30'
        }
      ], new Date('2026-07-09T00:00:00Z'))
    ).toThrow(/expired/);
  });
});
