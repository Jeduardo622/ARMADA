import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { basename, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const SECRET_RULES = [
  { id: 'private-key', pattern: /-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/g },
  { id: 'openai-token', pattern: /\bsk-(?:proj-)?[A-Za-z0-9_-]{20,}\b/g },
  { id: 'github-token', pattern: /\bgh[pousr]_[A-Za-z0-9]{20,}\b/g },
  { id: 'aws-access-key', pattern: /\bAKIA[A-Z0-9]{16}\b/g },
  {
    id: 'credential-assignment',
    pattern: /\b(?:SECRET|TOKEN|PASSWORD|API_KEY)\s*=\s*(?!change-me\b|test[-_]\w+\b|dev[-_]\w+\b|minio[-_]\w+\b|<[^>]+>|\s*(?:#|$))[^\s#]{8,}/g
  }
];

export function scanTextForSecrets(file, content) {
  const findings = [];
  for (const rule of SECRET_RULES) {
    rule.pattern.lastIndex = 0;
    if (rule.pattern.test(content)) findings.push({ file, ruleId: rule.id });
  }
  return findings;
}

export function scanTrackedEnvironmentFiles(files) {
  return files
    .filter((file) => basename(file).startsWith('.env') && basename(file) !== '.env.example')
    .map((file) => ({ file, ruleId: 'tracked-env-file' }));
}

function trackedFiles() {
  return execFileSync('git', ['ls-files', '-z'], { encoding: 'utf8' })
    .split('\0')
    .filter(Boolean);
}

function stagedAdditions() {
  const diff = execFileSync('git', ['diff', '--cached', '--unified=0', '--no-color'], { encoding: 'utf8' });
  const additions = new Map();
  let currentFile = null;
  for (const line of diff.split(/\r?\n/)) {
    if (line.startsWith('+++ b/')) {
      currentFile = line.slice(6);
    } else if (currentFile && line.startsWith('+') && !line.startsWith('+++')) {
      additions.set(currentFile, `${additions.get(currentFile) ?? ''}${line.slice(1)}\n`);
    }
  }
  return additions;
}

export function verifySecrets(root = process.cwd()) {
  const files = trackedFiles();
  const findings = scanTrackedEnvironmentFiles(files);
  for (const file of files) {
    try {
      findings.push(...scanTextForSecrets(file, readFileSync(resolve(root, file), 'utf8')));
    } catch {
      // Binary and concurrently removed files are ignored; staged additions remain checked below.
    }
  }
  for (const [file, content] of stagedAdditions()) findings.push(...scanTextForSecrets(file, content));
  const unique = [...new Map(findings.map((item) => [`${item.file}:${item.ruleId}`, item])).values()];
  return unique.length === 0
    ? { id: 'secrets', status: 'passed', summary: 'No tracked or staged secrets detected', details: [] }
    : { id: 'secrets', status: 'failed', summary: `${unique.length} potential secret findings`, details: unique };
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const result = verifySecrets();
  const stream = result.status === 'passed' ? process.stdout : process.stderr;
  stream.write(`${JSON.stringify(result)}\n`);
  if (result.status !== 'passed') process.exitCode = 1;
}
