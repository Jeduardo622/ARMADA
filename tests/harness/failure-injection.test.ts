import { describe, expect, it } from 'vitest';
import { createDatabaseVerifier } from '../../scripts/harness/verify-database.mjs';
import { createDeploymentVerifier } from '../../scripts/harness/verify-deployment.mjs';

type CommandResult = {
  status: number;
  stdout: string;
  stderr: string;
  error?: Error;
};

function result(status = 0, stdout = '', stderr = '', error?: Error): CommandResult {
  return { status, stdout, stderr, error };
}

function databaseRunner(failAt?: string) {
  const calls: string[] = [];
  const runCommand = (command: string, args: string[]) => {
    let stage = 'unknown';
    if (command === 'docker' && args[0] === 'version') stage = 'docker';
    else if (command === 'docker' && args[0] === 'run') stage = 'start';
    else if (command === 'docker' && args[0] === 'inspect') stage = 'health';
    else if (command === 'docker' && args[0] === 'port') stage = 'port';
    else if (command === 'docker' && args[0] === 'rm') stage = 'cleanup';
    else if (args.at(-1) === 'validate') stage = 'validate';
    else if (args.slice(-2).join(' ') === 'migrate deploy') stage = 'migrate-deploy';
    else if (args.slice(-2).join(' ') === 'migrate status') stage = 'migrate-status';
    calls.push(stage);
    if (stage === failAt) return result(1, '', `${stage} injected failure`);
    if (stage === 'health') return result(0, 'healthy\n');
    if (stage === 'port') return result(0, '127.0.0.1:25432\n');
    return result();
  };
  return { calls, runCommand };
}

describe('database verifier failure injection', () => {
  it('fails without attempting cleanup when Docker is unavailable', () => {
    const calls: string[] = [];
    const verify = createDatabaseVerifier({
      runCommand: () => {
        calls.push('docker');
        return result(1, '', '', new Error('docker unavailable'));
      }
    });
    const verification = verify('C:/repo');
    expect(verification.status).toBe('failed');
    expect(verification.details.join('\n')).toContain('docker unavailable');
    expect(calls).toEqual(['docker']);
  });

  it('cleans up exactly once when the container becomes unhealthy', () => {
    const runner = databaseRunner();
    const runCommand = (command: string, args: string[]) => {
      if (command === 'docker' && args[0] === 'inspect') {
        runner.calls.push('health');
        return result(0, 'unhealthy\n');
      }
      return runner.runCommand(command, args);
    };
    const verification = createDatabaseVerifier({ runCommand })('C:/repo');
    expect(verification.status).toBe('failed');
    expect(verification.details.join('\n')).toContain('health check');
    expect(runner.calls.filter((stage) => stage === 'cleanup')).toHaveLength(1);
  });

  it('times out health polling without real waiting and still cleans up', () => {
    const runner = databaseRunner();
    let nowCalls = 0;
    const times = [0, 0, 60_001];
    const runCommand = (command: string, args: string[]) => {
      if (command === 'docker' && args[0] === 'inspect') {
        runner.calls.push('health');
        return result(0, 'starting\n');
      }
      return runner.runCommand(command, args);
    };
    const verification = createDatabaseVerifier({
      runCommand,
      now: () => times[Math.min(nowCalls++, times.length - 1)]!,
      sleep: () => {}
    })('C:/repo');
    expect(verification.status).toBe('failed');
    expect(verification.details.join('\n')).toContain('did not become healthy');
    expect(runner.calls.filter((stage) => stage === 'cleanup')).toHaveLength(1);
  });

  it.each(['validate', 'migrate-deploy', 'migrate-status'])('cleans up after %s failure', (stage) => {
    const runner = databaseRunner(stage);
    const verification = createDatabaseVerifier({ runCommand: runner.runCommand })('C:/repo');
    expect(verification.status).toBe('failed');
    expect(verification.details.join('\n')).toContain('injected failure');
    expect(runner.calls.filter((value) => value === 'cleanup')).toHaveLength(1);
  });

  it('reports cleanup failure instead of passing', () => {
    const runner = databaseRunner('cleanup');
    const verification = createDatabaseVerifier({ runCommand: runner.runCommand })('C:/repo');
    expect(verification.status).toBe('failed');
    expect(verification.details.join('\n')).toContain('Ephemeral PostgreSQL cleanup failed');
    expect(runner.calls.filter((stage) => stage === 'cleanup')).toHaveLength(1);
  });
});

const validCompose = {
  services: {
    postgres: {
      image: 'postgres:16-alpine',
      environment: { POSTGRES_USER: 'postgres', POSTGRES_PASSWORD: 'postgres', POSTGRES_DB: 'armada' },
      ports: [{ target: 5432, published: '15432' }],
      healthcheck: { test: ['CMD-SHELL', 'pg_isready -U postgres'] }
    }
  }
};
const validEnv = 'DATABASE_URL=postgres://postgres:postgres@localhost:15432/armada\n';

describe('deployment verifier failure injection', () => {
  it('includes process spawn errors when Compose cannot execute', () => {
    const verify = createDeploymentVerifier({
      runCommand: () => result(1, '', '', new Error('compose executable unavailable')),
      readTextFile: () => validEnv
    });
    const verification = verify('C:/repo');
    expect(verification.status).toBe('failed');
    expect(verification.details.join('\n')).toContain('compose executable unavailable');
  });

  it('fails on malformed Compose JSON', () => {
    const verify = createDeploymentVerifier({
      runCommand: () => result(0, '{not-json'),
      readTextFile: () => validEnv
    });
    const verification = verify('C:/repo');
    expect(verification.status).toBe('failed');
    expect(verification.details.join('\n')).toContain('unable to validate Compose model');
  });

  it('fails when the environment contract cannot be read', () => {
    const verify = createDeploymentVerifier({
      runCommand: () => result(0, JSON.stringify(validCompose)),
      readTextFile: () => { throw new Error('.env.example missing'); }
    });
    const verification = verify('C:/repo');
    expect(verification.status).toBe('failed');
    expect(verification.details.join('\n')).toContain('.env.example missing');
  });

  it('fails when the rendered Compose model omits PostgreSQL', () => {
    const verify = createDeploymentVerifier({
      runCommand: () => result(0, JSON.stringify({ services: {} })),
      readTextFile: () => validEnv
    });
    const verification = verify('C:/repo');
    expect(verification.status).toBe('failed');
    expect(verification.details).toContain('Compose must define a postgres service');
  });

  it('passes a fully matching injected deployment contract', () => {
    const verify = createDeploymentVerifier({
      runCommand: () => result(0, JSON.stringify(validCompose)),
      readTextFile: () => validEnv
    });
    expect(verify('C:/repo')).toMatchObject({ id: 'deployment', status: 'passed' });
  });
});
