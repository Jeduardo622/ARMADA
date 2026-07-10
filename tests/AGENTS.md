# Test Instructions

These rules apply to `tests/` and supplement the root guide.

- Use `.codex/skills/qa-verification/SKILL.md` for test and evidence work.
- Write the failing regression before changing behavior and confirm the failure is
  caused by the missing behavior rather than test setup.
- Keep tests deterministic, isolated, secret-free, and runnable from a clean
  checkout without local services unless explicitly marked integration tests.
- Test public behavior and policy outcomes rather than implementation details.
- Harness fixtures require stable IDs and exact expected routing fields.
- Run focused tests first and the full verification contract before handoff.
