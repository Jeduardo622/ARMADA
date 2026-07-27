import { readFileSync } from 'node:fs';
import { ESLint } from 'eslint';
import { describe, expect, it } from 'vitest';
import vitestConfig from '../../vitest.config';

// Agent worktrees are full checkouts parked inside the repository: Codex uses
// `.worktrees/`, Claude Code uses `.claude/worktrees/`. Every tool that walks
// the repository must skip both. Leaving one out is not a cosmetic gap:
//
//   - eslint: each checkout carries its own tsconfig.json, and a second
//     candidate root makes typescript-eslint refuse to parse *any* file, so
//     the whole lint run fails rather than just the worktree files.
//   - vitest: a focused run silently executes the other checkout's suite too.
//   - .gitignore: without a committed entry, every clone but this one sees
//     agent worktrees as untracked.
//
// CI is a clean checkout with no worktrees present, so a green lint or test
// job there can never observe the bug. These guards therefore assert against
// the configuration objects themselves rather than against a tool run.

// Representative paths only. They need not exist: the assertions below are
// against the configuration, not against whatever worktrees happen to be
// checked out on the machine running the suite.
const WORKTREE_PATHS = [
  '.claude/worktrees/agent-worktree/src/foo.ts',
  '.worktrees/agent-worktree/src/foo.ts'
];

// Paths that must stay visible, so an over-broad ignore rule fails here
// instead of quietly disabling the checks it was meant to scope.
const REPOSITORY_PATHS = ['src/index.ts', 'tests/harness/worktree-isolation.test.ts'];

describe('agent worktree isolation', () => {
  it('excludes both worktree roots from eslint', async () => {
    // `overrideConfigFile` pins the root config and disables config discovery.
    // Without it this assertion depends on local state: a real worktree carries
    // its own eslint.config.js, and discovery resolves paths under it against
    // that nested config instead, so the check would pass on a clean CI clone
    // and fail on the very machine that has the worktree it is guarding.
    const eslint = new ESLint({ cwd: process.cwd(), overrideConfigFile: 'eslint.config.js' });

    for (const path of WORKTREE_PATHS) {
      expect(await eslint.isPathIgnored(path), path).toBe(true);
    }
    for (const path of REPOSITORY_PATHS) {
      expect(await eslint.isPathIgnored(path), path).toBe(false);
    }
  });

  it('excludes both worktree roots from Vitest discovery', () => {
    const patterns = vitestConfig.test?.exclude;
    expect(patterns).toBeDefined();

    const isExcluded = (path: string) =>
      patterns!.some((pattern) => globToRegExp(pattern).test(path));

    for (const path of WORKTREE_PATHS) {
      expect(isExcluded(path), path).toBe(true);
    }
    for (const path of REPOSITORY_PATHS) {
      expect(isExcluded(path), path).toBe(false);
    }
  });

  it('ignores both worktree roots from a committed .gitignore entry', () => {
    // `.git/info/exclude` covers these paths in this checkout but is local and
    // never committed, so it cannot be the source of the rule for other clones.
    const entries = readFileSync('.gitignore', 'utf8')
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter((line) => line.length > 0 && !line.startsWith('#'));

    expect(entries).toContain('.worktrees/');
    expect(entries).toContain('.claude/worktrees/');
    expect(entries.filter((entry) => entry.startsWith('!'))).toEqual([]);
  });
});

// Vitest matches `exclude` with picomatch, which is not a declared dependency
// here. This covers the subset of glob syntax the config actually uses --
// `**/`, trailing `**`, and `*` -- which is enough to evaluate the patterns
// above behaviourally rather than by substring-matching the config source.
function globToRegExp(pattern: string): RegExp {
  let source = '';
  for (let index = 0; index < pattern.length; index += 1) {
    const char = pattern[index];
    if (char !== '*') {
      source += char.replace(/[.+?^${}()|[\]\\]/g, '\\$&');
      continue;
    }
    if (pattern[index + 1] === '*') {
      if (pattern[index + 2] === '/') {
        source += '(?:.*/)?';
        index += 2;
      } else {
        source += '.*';
        index += 1;
      }
    } else {
      source += '[^/]*';
    }
  }
  return new RegExp(`^${source}$`);
}
