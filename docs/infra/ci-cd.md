# CI/CD Overview

- Branch policy: feature branches → PR to main; required checks: lint, unit, sim/replay, contract tests, static analysis, dependency scan.
- Build: client builds per PR (dev config), nightly device-farm smoke; backend unit/integration tests.
- Artifacts: signed build artifacts stored; release candidates tagged.
- Flags/config: changes reviewed; audited; shipped separately from binaries when possible.
- Environments: dev/stage/prod; blue/green for backend; feature flags for staged rollout.
- Release: checklist-driven; automated tagging and changelog; store submission steps tracked.
- PR hygiene: use template; link issues/ADRs; include test evidence; note flags/config changes and telemetry impacts.

## Unity Test CI

- GitHub-hosted GameCI runs Armada's `Armada.Client.EditModeTests` and
  `Armada.Client.PlayModeTests` assemblies with Unity `2022.3.62f3`.
- The Unity container image is pinned by tag and digest in
  `.github/workflows/ci.yml`; update it only after verifying the replacement
  image exists and compiling locally with the same Editor version.
- Repository secrets `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` are
  required. Add them in GitHub repository settings; never paste them into a
  workflow, issue, pull request, artifact, or tracked file.
- Personal licenses require the GameCI manual activation flow to produce the
  license file stored as `UNITY_LICENSE`. Do not upload a Unity license file as
  a workflow artifact or commit it to the repository.
- Fork pull requests do not receive Unity secrets and fail the named credential
  preflight. Do not use `pull_request_target` or attach a self-hosted runner to
  this public repository to work around that boundary.
- Successful EditMode and PlayMode XML files are hashed into
  `reports/harness/unity-ci-evidence.json`. `npm run verify:local` accepts that
  evidence only when its commit SHA and Unity version match the current run.

