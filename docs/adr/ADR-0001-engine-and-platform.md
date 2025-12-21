# ADR-0001: Engine & Platform
- Date: 2025-12-21
- Status: Accepted
- Context: Mobile 3v3 WEGO naval tactics game needs deterministic sim, broad device reach, robust tooling, and fast iteration.
- Options: Unity; Unreal; custom engine.
- Decision: Unity for iOS/Android with C#, using fixed-step deterministic sim patterns and addressables.
- Consequences: Mature tooling, wide device support, strong asset pipelines; must enforce determinism discipline and profiling on mid-tier devices; licensing aligned with mobile scale.

