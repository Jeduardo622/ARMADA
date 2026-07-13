# Shadow Evaluation Case Isolation Design

## Goal

Remove cross-case model contamination from the ten-fixture shadow evaluation
without changing the public corpus, private expectations, scorer, weights,
threshold, critical rules, or report format.

## Architecture

The dispatch workflow invokes a reusable workflow ten times, once per committed
public fixture. Each call runs on a separate GitHub-hosted runner, builds exactly
one prompt/schema pair, and uses the same pinned, read-only, high-effort,
ephemeral safety boundary as the prior workflow.

After all calls complete, a secret-free trusted job decodes their bounded job
outputs. The transport layer reads exactly one response file for
each trusted fixture ID, validates the bounded envelope and suite version, checks
that the embedded result ID matches its trusted filename, restores canonical
suite order, and emits one combined response for the unchanged grader. Raw
per-case response files are deleted whether aggregation succeeds or fails.

## Trust Boundary

- Fixture IDs and ordering come only from the committed public suite.
- Per-case prompts contain no other fixture prompts and no private expectations.
- The full public suite used for aggregation is stored outside the model working
  directory; only the selected case appears inside each prompt.
- Per-case schemas contain no expected answers and allow only one result.
- A mismatched, missing, malformed, duplicate, or oversized response fails the
  evaluation infrastructure rather than being partially graded.
- The combined response remains bounded by the existing 64 KiB transport limit.
- No new workflow permission, write authority, secret, or artifact is introduced.

## Verification

Builder tests prove one-case prompt/schema isolation. Transport tests prove
canonical aggregation and fail-closed cleanup. Workflow structure tests prove ten
runner-isolated reusable calls and deterministic aggregation before grading.
Focused tests, replay, full local verification, protected CI, and sequential
post-merge hosted evaluations provide completion evidence.

## Rollback

Revert the case-isolation PR and rerun `npm run verify:local`. The prior single
batch call resumes with the benchmark and grader unchanged.
