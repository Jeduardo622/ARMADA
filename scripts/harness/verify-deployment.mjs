import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

function readDatabaseUrl(envText) {
  const line = envText.split(/\r?\n/).find((entry) => entry.startsWith('DATABASE_URL='));
  return line?.slice('DATABASE_URL='.length).trim();
}

export function validateDeploymentConfig(compose, envText) {
  const violations = [];
  const postgres = compose?.services?.postgres;
  if (!postgres) return ['Compose must define a postgres service'];
  if (postgres.image !== 'postgres:16-alpine') {
    violations.push('Compose PostgreSQL image must be pinned to postgres:16-alpine');
  }
  if (!postgres.healthcheck?.test) violations.push('Compose PostgreSQL service must define a healthcheck');

  const port = postgres.ports?.find((entry) => Number(entry.target) === 5432);
  if (!port?.published) violations.push('Compose PostgreSQL service must publish container port 5432');

  const databaseUrlText = readDatabaseUrl(envText);
  if (!databaseUrlText) {
    violations.push('.env.example must define DATABASE_URL');
  } else {
    try {
      const databaseUrl = new URL(databaseUrlText);
      if (!['postgres:', 'postgresql:'].includes(databaseUrl.protocol)) {
        violations.push('.env.example DATABASE_URL must use PostgreSQL');
      }
      if (!['localhost', '127.0.0.1'].includes(databaseUrl.hostname)) {
        violations.push('.env.example DATABASE_URL must target the local Docker database');
      }
      if (port?.published && databaseUrl.port !== String(port.published)) {
        violations.push(`DATABASE_URL port ${databaseUrl.port || 'default'} does not match Compose PostgreSQL host port ${port.published}`);
      }
      const environment = postgres.environment ?? {};
      const expectedUser = environment.POSTGRES_USER;
      const expectedPassword = environment.POSTGRES_PASSWORD;
      const expectedDatabase = environment.POSTGRES_DB;
      if (expectedUser && decodeURIComponent(databaseUrl.username) !== String(expectedUser)) {
        violations.push('DATABASE_URL username does not match Compose POSTGRES_USER');
      }
      if (expectedPassword && decodeURIComponent(databaseUrl.password) !== String(expectedPassword)) {
        violations.push('DATABASE_URL password does not match Compose POSTGRES_PASSWORD');
      }
      if (expectedDatabase && decodeURIComponent(databaseUrl.pathname.slice(1)) !== String(expectedDatabase)) {
        violations.push('DATABASE_URL database does not match Compose POSTGRES_DB');
      }
    } catch {
      violations.push('.env.example DATABASE_URL must be a valid URL');
    }
  }
  return violations;
}

function runCommand(command, args, options = {}) {
  return spawnSync(command, args, {
    cwd: options.cwd,
    encoding: 'utf8',
    windowsHide: true
  });
}

function readTextFile(path) {
  return readFileSync(path, 'utf8');
}

export function createDeploymentVerifier(overrides = {}) {
  const dependencies = { runCommand, readTextFile, ...overrides };

  return function verifyDeploymentWithDependencies(root = process.cwd()) {
    const compose = dependencies.runCommand('docker', ['compose', 'config', '--format', 'json'], { cwd: root });
    const violations = [];
    if (compose.status !== 0) {
      const output = [compose.stderr, compose.error?.message]
        .filter(Boolean)
        .map((value) => String(value).trim())
        .filter(Boolean)
        .join('\n')
        .slice(-2000);
      violations.push(`docker compose config failed${output ? `: ${output}` : ''}`);
    } else {
      try {
        const model = JSON.parse(compose.stdout);
        const envText = dependencies.readTextFile(resolve(root, '.env.example'));
        violations.push(...validateDeploymentConfig(model, envText));
      } catch (error) {
        violations.push(`unable to validate Compose model: ${error.message}`);
      }
    }
    return violations.length === 0
      ? { id: 'deployment', status: 'passed', summary: 'Docker Compose deployment contract validated', details: [] }
      : { id: 'deployment', status: 'failed', summary: `${violations.length} deployment verification failure(s)`, details: violations };
  };
}

export function verifyDeployment(root = process.cwd()) {
  return createDeploymentVerifier()(root);
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const result = verifyDeployment();
  const stream = result.status === 'passed' ? process.stdout : process.stderr;
  stream.write(`${JSON.stringify(result)}\n`);
  if (result.status !== 'passed') process.exitCode = 1;
}
