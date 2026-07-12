# Shadow Policy Context Hardening Design

## Goal

Improve hosted shadow-evaluation consistency by making the existing classifier
algorithm explicit to the model without exposing fixture answers or changing the
locked benchmark, scorer, weights, threshold, or critical rules.

## Change

The public policy contract becomes version 2 and describes the classifier as a
deterministic procedure:

1. Inspect prohibited intents in policy order and return Class D immediately on
   the first match with `harness_policy` as the only required check.
2. Otherwise initialize checks from every matching path scope, falling back to
   `harness_policy` only when no scope matches.
3. Evaluate every protected area against both paths and intent, then union every
   matched area ID, reviewer, and check.
4. If no protected area matches, apply the docs-only-or-no-paths plus advisory
   pattern rule before the Class B fallback.
5. Deduplicate and lexicographically sort only `allowedActions`,
   `protectedAreas`, `requiredChecks`, and `requiredReviewers`; preserve result
   and check-claim order.

The base prompt repeats only these general execution rules. It contains no
fixture IDs, prompts, expected outputs, private expectations, or replay data.

## Verification

- Contract tests require the structured algorithm and Class D invariants.
- Prompt tests require the deterministic instructions and continue proving that
  private expectations and replay data are absent.
- Benchmark-lock tests prove the corpus and safety core remain unchanged.
- Focused tests, replay, `npm run verify:local`, protected CI, and three
  sequential hosted evaluations provide completion evidence.

## Rollback

Revert the hardening commit and rerun `npm run verify:local`. The V2.1 locked
benchmark and scorer remain unchanged throughout.
