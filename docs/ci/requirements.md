# CI Requirements

- Required checks on PR: lint, unit, sim/replay determinism tests, contract/schema tests, static analysis, dependency/vuln scan, secrets scan.
- Device smoke: nightly on target low/mid devices; per-PR optional smoke for risky changes.
- Failing checks block merge; exceptions require PM + QA + Eng Lead approval noted in PR.
- Artifacts: store build artifacts; attach test reports; keep crash/perf logs for smokes.
- Branch protection: main protected; feature branches via PR only.

