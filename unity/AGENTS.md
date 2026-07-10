# Unity Client Instructions

These rules apply to `unity/` and supplement the root guide.

- Use `.codex/skills/unity-delivery/SKILL.md` for Unity and C# changes.
- Preserve deterministic simulation inputs, seeded randomness, stable ordering,
  schema versions, and backend contract parity.
- Changes to authentication, remote configuration, economy, telemetry, build
  settings, package manifests, or deployment are Class C.
- Keep runtime code out of editor-only assemblies and preserve `.meta` files.
- Validate project metadata and deterministic fixtures with `npm run verify:unity`.
- A static check is not Unity compilation. Report compilation as unavailable until
  a licensed runner executes it.
- Run `npm run verify:local` before PR handoff.
