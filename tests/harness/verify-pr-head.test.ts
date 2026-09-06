import { spawnSync } from 'node:child_process';
import { describe, expect, it, vi } from 'vitest';
import { verifyPrHead } from '../../scripts/harness/verify-pr-head.mjs';

const sha = 'a'.repeat(40);
const other = 'b'.repeat(40);
const args = ['--repo', 'owner/repo', '--pr', '123', '--sha', sha];
const response = (head = sha, state = 'OPEN') => ({
  status: 0, stdout: JSON.stringify({ number: 123, state, headRefOid: head })
});

describe('manual PR head verification', () => {
  it('queries the explicit GitHub repository and PR, never the local checkout SHA', () => {
    const run = vi.fn(() => response());
    expect(verifyPrHead(args, run)).toContain(`PASS owner/repo#123: remote head ${sha}`);
    expect(run).toHaveBeenCalledWith('gh', [
      'pr', 'view', '123', '--repo', 'github.com/owner/repo', '--json', 'number,state,headRefOid'
    ], expect.objectContaining({ shell: false, timeout: 30000 }));
  });

  it('fails with expected and observed SHAs when the PR advances', () => {
    expect(() => verifyPrHead(args, () => response(other)))
      .toThrow(`owner/repo#123: expected ${sha}, observed ${other}. Do not merge`);
  });

  it.each(['CLOSED', 'MERGED', undefined])('rejects a non-open PR: %s', (state) => {
    expect(() => verifyPrHead(args, () => ({ status: 0,
      stdout: JSON.stringify({ number: 123, headRefOid: sha, state }) }))).toThrow(/not OPEN/);
  });

  it.each(['', 'not json', '{}', 'null', JSON.stringify({ number: 124, state: 'OPEN', headRefOid: sha }),
    JSON.stringify({ number: 123, state: 'OPEN', headRefOid: 'abc' })])
  ('fails closed on invalid GitHub output: %s', (stdout) => {
    expect(() => verifyPrHead(args, () => ({ status: 0, stdout }))).toThrow(/invalid GitHub response/);
  });

  it.each([
    { status: 1, stdout: response().stdout },
    { status: null, error: new Error('ENOENT') },
    { status: null, error: new Error('ETIMEDOUT') }
  ])('fails closed on unavailable gh, authentication, network or timeout errors', (result) => {
    expect(() => verifyPrHead(args, () => result)).toThrow(/lookup failed.*Do not merge/);
  });

  it.each([
    [], [...args, '--unknown'], [...args, '--sha', other],
    ['--repo', 'owner/repo', '--pr', '0', '--sha', sha],
    ['--repo', 'owner/repo', '--pr', '123', '--sha', 'abc'],
    ['--repo', 'https://github.com/owner/repo', '--pr', '123', '--sha', sha],
    ['--repo', 'owner/repo;echo', '--pr', '123', '--sha', sha],
    ['--repo', 'owner/repo', '--pr', '123;echo', '--sha', sha],
    ['--repo', 'owner/repo', '--pr', '123', '--sha']
  ].map((input) => ({ input })))('rejects invalid arguments before any remote lookup: $input', ({ input }) => {
    const run = vi.fn(() => response());
    expect(() => verifyPrHead(input, run)).toThrow(/Usage:/);
    expect(run).not.toHaveBeenCalled();
  });

  it('normalizes full uppercase SHAs', () => {
    expect(verifyPrHead([...args.slice(0, -1), sha.toUpperCase()], () => response())).toContain('PASS');
  });

  it('returns a nonzero CLI exit and actionable stderr for invalid input', () => {
    const result = spawnSync(process.execPath, ['scripts/harness/verify-pr-head.mjs'], { encoding: 'utf8' });
    expect(result.status).toBe(1);
    expect(result.stdout).toBe('');
    expect(result.stderr).toContain('[pr-head] FAIL');
    expect(result.stderr).toContain('Usage:');
  });
});
