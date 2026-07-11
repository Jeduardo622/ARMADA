import { configDefaults, defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    setupFiles: ['./tests/setup-env.ts'],
    exclude: [...configDefaults.exclude, '**/dist/**', '**/.worktrees/**']
  }
});
