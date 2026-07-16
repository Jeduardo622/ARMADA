import { describe, expect, it } from 'vitest';
import {
  scanTextForSecrets,
  scanTrackedEnvironmentFiles
} from '../../scripts/harness/verify-secrets.mjs';
import {
  collectBlockingAdvisories,
  evaluateDependencyAudit
} from '../../scripts/harness/verify-dependencies.mjs';

describe('secret policy', () => {
  it('reports a private key without returning its value', () => {
    const value = ['-----BEGIN', ' PRIVATE KEY-----', 'very-sensitive-value'].join('');
    const findings = scanTextForSecrets('config.txt', value);
    expect(findings).toEqual([{ file: 'config.txt', ruleId: 'private-key' }]);
    expect(JSON.stringify(findings)).not.toContain('very-sensitive-value');
  });

  it('allows documented test and development placeholders', () => {
    expect(scanTextForSecrets('.env.example', 'JWT_SECRET=change-me\nAPI_TOKEN=dev-token')).toEqual([]);
  });

  it('rejects tracked environment files except the example', () => {
    expect(scanTrackedEnvironmentFiles(['.env.example', '.env.production'])).toEqual([
      { file: '.env.production', ruleId: 'tracked-env-file' }
    ]);
  });
});

describe('dependency policy', () => {
  const audit = {
    vulnerabilities: {
      risky: {
        via: [
          { source: 123, name: 'risky', severity: 'high', title: 'Risky advisory' }
        ]
      }
    },
    metadata: { vulnerabilities: { high: 1, critical: 0, total: 1 } }
  };

  it('collects high and critical advisory records', () => {
    expect(collectBlockingAdvisories(audit)).toEqual([
      { advisoryId: 123, package: 'risky', severity: 'high', title: 'Risky advisory' }
    ]);
  });

  it('fails an advisory without an active exception', () => {
    expect(evaluateDependencyAudit(audit, [], new Date('2026-07-09T00:00:00Z'))).toMatchObject({
      status: 'failed',
      unexcepted: [{ advisoryId: 123 }]
    });
  });

  it('accepts an exact active exception', () => {
    const exceptions = [{
      advisoryId: 123,
      package: 'risky',
      rationale: 'pre-existing finding',
      owner: 'Armada engineering',
      introduced: '2026-07-09',
      expires: '2026-10-07'
    }];
    expect(evaluateDependencyAudit(audit, exceptions, new Date('2026-07-09T00:00:00Z'))).toMatchObject({
      status: 'passed',
      unexcepted: []
    });
  });
});
