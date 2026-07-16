---
name: unity-delivery
description: Implement and verify Armada Unity client, CSharp, deterministic simulation, API parity, and Addressables changes.
---

# Unity Delivery

## Trigger

Use for changes under `unity/` or backend contracts consumed by Unity.

## Workflow

1. Read root and `unity/AGENTS.md`, Unity README, package manifest, and API models.
2. Route all intended C#, metadata, package, and contract paths.
3. Identify determinism, serialization, frame-time, and device risks.
4. Add or update deterministic fixtures and contract checks before behavior edits.
5. Preserve stable ordering, explicit seeds, schema versions, and `.meta` files.
6. Run Unity metadata checks, backend contract checks, and full verification.
7. Set `UNITY_EDITOR_PATH` and run licensed batch compilation when an Editor is
   available. Protected Unity tooling paths make compilation required
   automatically; otherwise report compilation as not applicable.

## Stop Conditions

Stop for unapproved package/build changes, missing contract decisions, secrets,
deployment requests, or claims that static validation equals Unity compilation.
