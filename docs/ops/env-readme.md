# Environment Setup Notes

- Copy `.env.example` to `.env`; never commit real secrets.
- Run local services via Docker: Postgres, Redis, MinIO, Unleash (or your flag service).
- Keep secrets in vault/secret manager; .env is for local only.
- Rotate keys regularly; follow `ops/secrets-sop.md`.
- Set telemetry/APM keys if you enable Sentry/Grafana/Amplitude; otherwise leave blank.
- Use `docker-compose up -d` to start the local stack (postgres, redis, minio, unleash). Console: MinIO http://localhost:9100; Unleash http://localhost:4242 (admin/admin).
- New env knobs:
  - `CORS_ORIGIN` for frontend origin (default localhost dev).
  - `RATE_LIMIT_WINDOW_MS` / `RATE_LIMIT_MAX` for API throttling.
  - `BODY_LIMIT_BYTES` for request size caps.
  - `STORAGE_REGION` optional for MinIO/S3 region; bucket auto-created if missing.
  - `CONFIG_SIGNING_KEY` used to HMAC config responses; keep secret.

