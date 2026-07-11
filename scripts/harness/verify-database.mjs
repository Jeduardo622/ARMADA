import { spawnSync } from 'node:child_process';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const POSTGRES_IMAGE = 'postgres:16-alpine';
const VERIFY_USER = 'armada_verify';
const VERIFY_PASSWORD = 'armada_verify';
const VERIFY_DATABASE = 'armada_verify';

export function buildPostgresRunArgs(containerName) {
  return [
    'run', '--detach', '--rm', '--name', containerName,
    '--env', `POSTGRES_USER=${VERIFY_USER}`,
    '--env', `POSTGRES_PASSWORD=${VERIFY_PASSWORD}`,
    '--env', `POSTGRES_DB=${VERIFY_DATABASE}`,
    '--health-cmd', `pg_isready -U ${VERIFY_USER} -d ${VERIFY_DATABASE}`,
    '--health-interval', '1s',
    '--health-timeout', '5s',
    '--health-retries', '30',
    '--publish', '127.0.0.1::5432',
    POSTGRES_IMAGE
  ];
}

function defaultRun(command, args, options = {}) {
  return spawnSync(command, args, {
    cwd: options.cwd,
    encoding: 'utf8',
    env: options.env ?? process.env,
    windowsHide: true
  });
}

function commandFailure(label, result) {
  const output = `${result.stdout ?? ''}\n${result.stderr ?? ''}\n${result.error?.message ?? ''}`.trim();
  return `${label} failed${output ? `: ${output.slice(-2000)}` : ''}`;
}

function waitForHealthy(containerName, dependencies, timeoutMs = 60_000) {
  const deadline = dependencies.now() + timeoutMs;
  while (dependencies.now() < deadline) {
    const inspect = dependencies.runCommand('docker', ['inspect', '--format', '{{.State.Health.Status}}', containerName]);
    if (inspect.status === 0 && inspect.stdout.trim() === 'healthy') return null;
    if (inspect.status !== 0 || inspect.stdout.trim() === 'unhealthy') {
      return commandFailure('PostgreSQL container health check', inspect);
    }
    dependencies.sleep(1000);
  }
  return `PostgreSQL container did not become healthy within ${timeoutMs}ms`;
}

export function installSignalCleanup(containerName, removeContainer = () => defaultRun('docker', ['rm', '--force', containerName])) {
  let cleaned = false;
  const cleanup = () => {
    if (cleaned) return;
    cleaned = true;
    removeContainer();
  };
  const onSigint = () => {
    cleanup();
    process.exit(130);
  };
  const onSigterm = () => {
    cleanup();
    process.exit(143);
  };
  process.once('SIGINT', onSigint);
  process.once('SIGTERM', onSigterm);
  return {
    cleanup,
    dispose() {
      process.removeListener('SIGINT', onSigint);
      process.removeListener('SIGTERM', onSigterm);
    }
  };
}

export function createDatabaseVerifier(overrides = {}) {
  const dependencies = {
    runCommand: defaultRun,
    now: () => Date.now(),
    sleep: (milliseconds) => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, milliseconds),
    ...overrides
  };

  return function verifyDatabaseWithDependencies(root = process.cwd()) {
    const containerName = `armada-db-verify-${process.pid}-${dependencies.now()}`;
    const details = [];
    let started = false;
    let signalCleanup;
    try {
      const docker = dependencies.runCommand('docker', ['version', '--format', '{{.Server.Version}}']);
    if (docker.status !== 0) details.push(commandFailure('Docker availability check', docker));
    if (details.length === 0) {
      const start = dependencies.runCommand('docker', buildPostgresRunArgs(containerName));
      started = start.status === 0;
      if (started) {
        signalCleanup = installSignalCleanup(containerName, () =>
          dependencies.runCommand('docker', ['rm', '--force', containerName]));
      }
      if (!started) details.push(commandFailure('Ephemeral PostgreSQL startup', start));
    }
    if (details.length === 0) {
      const healthFailure = waitForHealthy(containerName, dependencies);
      if (healthFailure) details.push(healthFailure);
    }

    let databaseUrl;
    if (details.length === 0) {
      const port = dependencies.runCommand('docker', ['port', containerName, '5432/tcp']);
      const match = String(port.stdout ?? '').trim().match(/:(\d+)$/);
      if (port.status !== 0 || !match) details.push(commandFailure('Ephemeral PostgreSQL port discovery', port));
      else databaseUrl = `postgresql://${VERIFY_USER}:${VERIFY_PASSWORD}@127.0.0.1:${match[1]}/${VERIFY_DATABASE}`;
    }

    if (details.length === 0) {
      const env = { ...process.env, DATABASE_URL: databaseUrl };
      for (const [label, args] of [
        ['Prisma schema validation', ['validate']],
        ['Prisma migration deployment', ['migrate', 'deploy']],
        ['Prisma migration status', ['migrate', 'status']]
      ]) {
        const result = dependencies.runCommand(
          process.execPath,
          [resolve(root, 'node_modules/prisma/build/index.js'), ...args],
          { cwd: root, env }
        );
        if (result.status !== 0) {
          details.push(commandFailure(label, result));
          break;
        }
      }
    }
  } finally {
    if (started) {
      signalCleanup?.dispose();
      const cleanup = dependencies.runCommand('docker', ['rm', '--force', containerName]);
      if (cleanup.status !== 0) details.push(commandFailure('Ephemeral PostgreSQL cleanup', cleanup));
    }
  }

    return details.length === 0
      ? { id: 'database', status: 'passed', summary: 'Prisma migrations verified against ephemeral PostgreSQL', details: [] }
      : { id: 'database', status: 'failed', summary: `${details.length} database verification failure(s)`, details };
  };
}

export function verifyDatabase(root = process.cwd()) {
  return createDatabaseVerifier()(root);
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const result = verifyDatabase();
  const stream = result.status === 'passed' ? process.stdout : process.stderr;
  stream.write(`${JSON.stringify(result)}\n`);
  if (result.status !== 'passed') process.exitCode = 1;
}
