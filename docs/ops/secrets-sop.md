# Secrets & Keys SOP

- Storage: use vault/secret manager; no secrets in repo or client.
- Access: least privilege; audit access; rotate regularly.
- In CI: inject via secure env; never echo; mask logs.
- Client: no hard-coded secrets; platform keys only where required by SDKs; prefer backend-mediated flows.
- Rotation: schedule per key class; emergency rotation steps documented; owners assigned.
- Validation: secrets scan in CI; block merges on findings until cleared.

