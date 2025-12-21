# CI/CD Overview

- Branch policy: feature branches → PR to main; required checks: lint, unit, sim/replay, contract tests, static analysis, dependency scan.
- Build: client builds per PR (dev config), nightly device-farm smoke; backend unit/integration tests.
- Artifacts: signed build artifacts stored; release candidates tagged.
- Flags/config: changes reviewed; audited; shipped separately from binaries when possible.
- Environments: dev/stage/prod; blue/green for backend; feature flags for staged rollout.
- Release: checklist-driven; automated tagging and changelog; store submission steps tracked.

