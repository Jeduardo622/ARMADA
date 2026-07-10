# CI And Repository Automation Instructions

All changes under `.github/` are Class C protected work.

- Use least-privilege workflow permissions and pinned major action versions.
- Required jobs must execute real commands; placeholders and silent skips fail.
- Do not add production credentials, deployment authority, or write permissions
  to the v1 engineering harness.
- Keep `npm run verify:local` as the shared local/CI contract.
- Upload diagnostic artifacts with `if: always()` without exposing secret values.
- Validate workflow structure, run full local verification, document rollback,
  and require human merge.
