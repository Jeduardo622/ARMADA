---
name: security-review
description: Review and verify Armada authentication, authorization, secrets, dependencies, trust boundaries, and protected changes.
---

# Security Review

## Trigger

Use for auth, permissions, external inputs, dependencies, secrets, CI, database,
deployment, economy, or production-data paths.

## Workflow

1. Route the complete path set and treat the task as Class C or D as returned.
2. Map trust boundaries, caller identity, validation, authorization, and sinks.
3. Preserve fail-closed behavior and least privilege.
4. Add focused negative tests for unauthorized and malformed inputs.
5. Run secret and dependency checks plus the complete verification contract.
6. Document reviewers, rollback, blocked infrastructure, and residual exposure.

## Stop Conditions

Stop for secret extraction, production mutation, control bypass, destructive
actions, missing authorization decisions, or scope too broad to contain safely.
