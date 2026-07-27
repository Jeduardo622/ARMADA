import { configDefaults, defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    setupFiles: ['./tests/setup-env.ts'],
    // Local worktree checkouts carry a full copy of this suite. Codex parks
    // them in `.worktrees`, Claude Code in `.claude/worktrees`; without both,
    // a focused run silently executes the other checkout's tests too.
    exclude: [
      ...configDefaults.exclude,
      '**/dist/**',
      '**/.worktrees/**',
      '**/.claude/worktrees/**'
    ]
  }
});
