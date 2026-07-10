---
name: backend-delivery
description: Implement and verify bounded Armada TypeScript, Fastify, API, service, telemetry, and backend test changes.
---

# Backend Delivery

## Trigger

Use for changes under `src/`, backend tests, or API documentation.

## Workflow

1. Read root and `src/AGENTS.md` plus affected contracts and tests.
2. Route the task with its intended paths and stop if Class C lacks explicit scope.
3. Trace request input through validation, authorization, storage, and response.
4. Write a failing focused test for behavior changes.
5. Implement the smallest contract-preserving change.
6. Update OpenAPI or operational docs when public behavior changes.
7. Run the focused test, lint, typecheck, build, and full local verification.

## Stop Conditions

Stop for missing product behavior, unavailable protected infrastructure, secrets,
production mutation, or scope growth beyond the approved route.
