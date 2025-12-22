# Local Tooling Install Notes

## Core
- Node.js 20.x + npm (https://nodejs.org)
- TypeScript toolchain: `npm i -g typescript ts-node` (or `npm i -g tsx`)
- Postgres client: `psql` (bundled with Postgres) or `pgcli`
- Redis CLI: `redis-cli`
- MinIO CLI: `mc` (https://min.io/download)
- `jq` for JSON parsing

## Quick commands (mac/Linux; adapt for Windows)
- Node 20 via nvm: `nvm install 20 && nvm use 20`
- TypeScript: `npm i -g typescript ts-node`
- jq: `brew install jq` or `sudo apt install jq`

## MinIO CLI setup
```
mc alias set local http://localhost:9000 minio-access minio-secret
mc mb local/armada-dev
```

