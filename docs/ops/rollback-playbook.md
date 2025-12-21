# Rollback Playbook

## Flags/Config First
- Revert to last-good config; verify via monitoring (crash/perf/funnel).
- Announce internally; log change with timestamp/owner.

## Backend Deploy
- Use blue/green: switch traffic back to previous version; confirm health checks.
- If DB migration involved: execute down migration only if safe; otherwise feature-flag off and hotfix.

## Client Hotfix (if required)
- Gate feature with flag; ship hotfix only if flag insufficient.
- Coordinate store submission and comms; monitor post-fix metrics.

## Aftermath
- Create incident report; track follow-ups; add tests/alerts to prevent recurrence.

