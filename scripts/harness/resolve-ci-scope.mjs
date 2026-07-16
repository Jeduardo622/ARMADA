import { appendFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import {
  readChangedPaths,
  requiresUnityCompilation,
  requiresUnityTests,
  resolveVerificationMetadata
} from './verify-local.mjs';

function isEnabled(value) {
  return value === '1' || String(value).toLowerCase() === 'true';
}

export function determineCiScope(changedPaths, env = process.env) {
  const paths = [...new Set(changedPaths.map((path) => String(path).replaceAll('\\', '/')))];
  const { routing } = resolveVerificationMetadata(paths, env);
  const unityRequired = isEnabled(env.FORCE_UNITY) ||
    routing.requiredChecks.includes('unity_compilation') ||
    routing.requiredChecks.includes('unity_tests') ||
    requiresUnityCompilation(paths, env) ||
    requiresUnityTests(paths, env);
  return { changedPaths: paths, unityRequired };
}

export function resolveCiScope(root = process.cwd(), env = process.env) {
  return determineCiScope(readChangedPaths(root, env), env);
}

export function formatGitHubOutput(scope) {
  return `unity_required=${scope.unityRequired ? 'true' : 'false'}\n`;
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const scope = resolveCiScope();
  if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, formatGitHubOutput(scope), 'utf8');
  process.stdout.write(`${JSON.stringify(scope)}\n`);
}
