import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { validateDependencyExceptions } from './verify-policy.mjs';

export function collectBlockingAdvisories(audit) {
  const advisories = new Map();
  for (const vulnerability of Object.values(audit?.vulnerabilities ?? {})) {
    for (const via of Array.isArray(vulnerability.via) ? vulnerability.via : []) {
      if (!via || typeof via !== 'object') continue;
      if (!['high', 'critical'].includes(via.severity) || !Number.isInteger(via.source)) continue;
      advisories.set(via.source, {
        advisoryId: via.source,
        package: via.name,
        severity: via.severity,
        title: via.title
      });
    }
  }
  return [...advisories.values()].sort((left, right) => left.advisoryId - right.advisoryId);
}

export function evaluateDependencyAudit(audit, exceptions, now = new Date()) {
  validateDependencyExceptions(exceptions, now);
  const advisories = collectBlockingAdvisories(audit);
  const exceptionById = new Map(exceptions.map((item) => [item.advisoryId, item]));
  const unexcepted = advisories.filter((advisory) => {
    const exception = exceptionById.get(advisory.advisoryId);
    return !exception || exception.package !== advisory.package;
  });
  return {
    id: 'dependencies',
    status: unexcepted.length === 0 ? 'passed' : 'failed',
    summary: unexcepted.length === 0
      ? `${advisories.length} blocking advisories covered by active exceptions`
      : `${unexcepted.length} high or critical advisories lack active exceptions`,
    details: advisories,
    unexcepted,
    activeExceptions: advisories.length - unexcepted.length,
    severityCounts: audit?.metadata?.vulnerabilities ?? {}
  };
}

function runAudit() {
  const executable = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  const result = spawnSync(executable, ['audit', '--json'], {
    encoding: 'utf8',
    shell: process.platform === 'win32'
  });
  if (!result.stdout?.trim()) throw new Error(result.error?.message ?? result.stderr ?? 'npm audit returned no JSON');
  return JSON.parse(result.stdout);
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  try {
    const exceptions = JSON.parse(readFileSync(fileURLToPath(new URL('./dependency-exceptions.json', import.meta.url)), 'utf8'));
    const result = evaluateDependencyAudit(runAudit(), exceptions);
    const stream = result.status === 'passed' ? process.stdout : process.stderr;
    stream.write(`${JSON.stringify(result)}\n`);
    if (result.status !== 'passed') process.exitCode = 1;
  } catch (error) {
    process.stderr.write(`${JSON.stringify({ id: 'dependencies', status: 'failed', summary: error.message })}\n`);
    process.exitCode = 1;
  }
}
