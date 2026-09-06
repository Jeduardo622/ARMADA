#!/usr/bin/env node
import { spawnSync } from 'node:child_process';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const USAGE = 'Usage: node scripts/harness/verify-pr-head.mjs --repo <owner/repo> --pr <number> --sha <full-40-character-SHA>';
const SHA = /^[a-f0-9]{40}$/i;

export function verifyPrHead(args, run = spawnSync) {
  const values = new Map();
  for (let index = 0; index < args.length; index += 2) {
    const flag = args[index];
    if (!['--repo', '--pr', '--sha'].includes(flag) || values.has(flag) || !args[index + 1]) {
      throw new Error(USAGE);
    }
    values.set(flag, args[index + 1]);
  }
  const repo = values.get('--repo') ?? '';
  const pr = values.get('--pr') ?? '';
  const expected = values.get('--sha') ?? '';
  if (!/^[A-Za-z0-9][A-Za-z0-9-]*\/[A-Za-z0-9_][A-Za-z0-9_.-]*$/.test(repo) ||
      !/^[1-9][0-9]*$/.test(pr) || !Number.isSafeInteger(Number(pr)) || !SHA.test(expected)) {
    throw new Error(USAGE);
  }
  const target = `${repo}#${pr}`;
  const result = run('gh', ['pr', 'view', pr, '--repo', `github.com/${repo}`,
    '--json', 'number,state,headRefOid'], {
    encoding: 'utf8', shell: false, windowsHide: true, timeout: 30000
  });
  if (result.error || result.status !== 0) {
    throw new Error(`${target}: GitHub lookup failed. Do not merge; check gh installation, GitHub authentication, repository access and network, then rerun.`);
  }
  let remote;
  try {
    remote = JSON.parse(result.stdout);
  } catch {
    throw new Error(`${target}: invalid GitHub response. Do not merge; rerun the check.`);
  }
  if (!remote || remote.number !== Number(pr) || typeof remote.headRefOid !== 'string' || !SHA.test(remote.headRefOid)) {
    throw new Error(`${target}: invalid GitHub response. Do not merge; rerun the check.`);
  }
  if (remote.state !== 'OPEN') {
    throw new Error(`${target}: PR is not OPEN. Do not merge.`);
  }
  if (remote.headRefOid.toLowerCase() !== expected.toLowerCase()) {
    throw new Error(`${target}: expected ${expected.toLowerCase()}, observed ${remote.headRefOid.toLowerCase()}. Do not merge; review and verify the new head before rerunning.`);
  }
  return `PASS ${target}: remote head ${remote.headRefOid.toLowerCase()} matches intended SHA. Snapshot only; recheck immediately before human merge.`;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  try {
    process.stdout.write(`[pr-head] ${verifyPrHead(process.argv.slice(2))}\n`);
  } catch (error) {
    process.stderr.write(`[pr-head] FAIL ${error.message}\n`);
    process.exitCode = 1;
  }
}
