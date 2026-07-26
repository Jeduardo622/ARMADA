import js from '@eslint/js';
import prettier from 'eslint-config-prettier';
import globals from 'globals';
import tseslint from 'typescript-eslint';

// Flat-config translation of the former .eslintrc.cjs. Every element is
// carried over deliberately: the `files` scope replaces `--ext .ts`, the
// `project` parser option keeps type-aware rules working, and `prettier`
// stays last so it can turn off formatting rules from the sets above it.
export default tseslint.config(
  // `dist` and `node_modules` carry over from ignorePatterns. `.worktrees`
  // is new but not a policy change: eslint 8 skipped dot-directories by
  // default, and flat config does not, so linting it would newly pull in
  // gitignored local worktree checkouts that were never linted before.
  { ignores: ['dist', 'node_modules', '.worktrees'] },
  {
    files: ['**/*.ts'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended, prettier],
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'module',
      globals: { ...globals.node, ...globals.es2020 },
      parserOptions: {
        project: ['./tsconfig.json']
      }
    },
    rules: {
      '@typescript-eslint/no-misused-promises': ['error', { checksVoidReturn: false }],
      // eslint 9 dropped this from `recommended`; pinned explicitly so the
      // migration loses no rule coverage that eslint 8 enforced here.
      'no-inner-declarations': 'error'
    }
  }
);
