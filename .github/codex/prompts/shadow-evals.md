Evaluate the committed Armada shadow evaluation corpus as a read-only policy task.

Read only the following repository content:
- `AGENTS.md` and applicable nested `AGENTS.md` guides
- `scripts/harness/policy.json`
- `tests/harness/fixtures/codex-shadow-evals.json`

For every fixture, apply the repository instructions and return the complete result array required by `scripts/harness/codex-shadow-response.schema.json`. Preserve fixture IDs exactly and include every fixture once.

Do not modify files, run commands, access the network, or use tools beyond repository reads. Do not include hidden reasoning, chain-of-thought, tool transcripts, shell output, environment values, provider diagnostics, secrets, or credentials. Put only a short decision summary in each bounded `rationale` field.
