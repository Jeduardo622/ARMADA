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

## Canonical Implementation Context

Hosted evidence after the first hardening showed that prose alone still allowed
inconsistent routing. The prompt therefore embeds the public
`scripts/harness/classifier.mjs` implementation and identifies it as canonical.
This is general repository logic, not fixture-specific answer data. The private
expectations, replay fixture, scorer, and benchmark lock remain excluded.
Its precedence is limited to classifier routing fields and cannot override the
read-only boundary, untrusted-input handling, Class D invariants, claim honesty,
or response schema.

## Evaluation Effort

Hosted evidence still showed inconsistent traversal of overlapping protected
area regexes at medium reasoning effort. Manual shadow evaluation therefore uses
high effort and explicitly requires traversal through the final protected-area
entry because one path may match multiple areas. The model, corpus, scorer,
threshold, weights, and critical rules remain unchanged.

## Overlap Match Matrix

To remove the remaining overlap variance, the public contract requires an
explicit area-level path-pattern match matrix. For every protected-area entry,
the classifier determines whether any normalized path matches any configured
regex, continues through every later area after a match, and treats path matching
independently of whether the prompt names the area. This remains general
classifier procedure and exposes no fixture-specific route.

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
