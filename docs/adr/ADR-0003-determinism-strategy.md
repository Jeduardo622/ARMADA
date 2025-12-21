# ADR-0003: Determinism Strategy
- Date: 2025-12-21
- Status: Accepted
- Context: WEGO combat requires authoritative, reproducible outcomes for fairness, replay validation, and testing.
- Options: Best-effort determinism with floats; full fixed-step deterministic sim with integer/fixed math and seeded RNG; server-side simulation.
- Decision: Client-run fixed-step deterministic sim with fixed-point or integer math where needed, single-source RNG seeded per battle, lockstep-style state progression, and replay validation in CI; server-side validates outcomes for economy progression.
- Consequences: More upfront rigor; simplifies QA (record/replay), supports anti-tamper checks, and consistent tuning; requires discipline on math libs and serialization.

