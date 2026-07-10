# Backend Instructions

These rules apply to `src/` and supplement the root guide.

- Use `.codex/skills/backend-delivery/SKILL.md` for backend changes.
- Preserve Fastify request/response contracts and Zod validation at boundaries.
- Authentication, authorization, ownership, runtime configuration, external
  services, and public API behavior are Class C protected areas.
- Add focused route or service tests before changing behavior.
- Keep player ownership and server-authoritative economy rules fail-closed.
- Do not log tokens, credentials, raw authorization headers, or sensitive payloads.
- Contract changes require OpenAPI updates, compatibility evidence, and rollback.
- Run the focused Vitest file, `npm run lint`, `npm run typecheck`, and finally
  `npm run verify:local` for non-trivial changes.
