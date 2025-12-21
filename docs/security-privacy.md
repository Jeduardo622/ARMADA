# Security & Privacy (MVP)

## Data Handling
- Minimal PII; no sensitive data stored client-side beyond tokens.
- TLS everywhere; short-lived tokens; signed requests.
- Store compliance: platform purchase flows; clear privacy policy.

## Integrity
- Authoritative economy on server; reject client-trusted values.
- Signed configs/flags; cache busting; version pins.
- Replay sanity checks; detect impossible states.
- Rate limiting + WAF; basic device integrity signals where available.

## Access & Ops
- Least privilege for services; rotated keys; audit logs for flags/config.
- Backups for DB/object storage; tested restores.
- Dependency scanning; security linters in CI.

## Launch Blockers
- PII review complete; data map documented.
- Payments verified in sandbox; no hard-coded secrets in client.

