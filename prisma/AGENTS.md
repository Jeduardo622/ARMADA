# Database Instructions

All changes under `prisma/` are Class C protected work.

- Inspect `prisma/schema.prisma`, migration history, API callers, and rollback
  implications before editing.
- Never rewrite an applied migration. Add a forward migration instead.
- Prefer additive schema changes and explicit compatibility windows.
- Include verification queries, data-safety assumptions, rollback steps, and a
  named human reviewer in the completion report.
- Do not connect to or mutate production data from the engineering harness.
- Run Prisma generation or validation as applicable, focused integration tests,
  and `npm run verify:local`.
