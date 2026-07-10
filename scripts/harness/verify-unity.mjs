import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const REQUIRED_UNITY_FILES = [
  'unity/ProjectSettings/ProjectVersion.txt',
  'unity/Packages/manifest.json',
  'unity/Assets/Armada/Core/ApiClient.cs',
  'unity/Assets/Armada/Core/DeterministicSimHooks.cs',
  'unity/Assets/Armada/Services/SimService.cs'
];

export function verifyUnity(root) {
  const details = REQUIRED_UNITY_FILES
    .filter((path) => !existsSync(resolve(root, path)))
    .map((path) => `missing ${path}`);
  const manifestPath = resolve(root, 'unity/Packages/manifest.json');
  if (existsSync(manifestPath)) {
    try {
      const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
      if (!manifest.dependencies?.['com.unity.addressables']) details.push('Addressables package is missing');
      if (!manifest.dependencies?.['com.unity.test-framework']) details.push('Unity test framework is missing');
    } catch {
      details.push('Unity package manifest is invalid JSON');
    }
  }
  const hooksPath = resolve(root, 'unity/Assets/Armada/Core/DeterministicSimHooks.cs');
  if (existsSync(hooksPath)) {
    const hooks = readFileSync(hooksPath, 'utf8');
    if (!hooks.includes('Random.InitState') || !hooks.includes('fixedDeltaTime')) {
      details.push('Deterministic hooks must seed randomness and set fixedDeltaTime');
    }
  }
  return {
    id: 'unity_static',
    status: details.length === 0 ? 'passed' : 'failed',
    summary: details.length === 0 ? `${REQUIRED_UNITY_FILES.length} Unity metadata boundaries validated` : `${details.length} Unity violations`,
    details,
    compilation: {
      id: 'unity_compilation',
      executed: false,
      status: 'not_applicable',
      summary: 'licensed Unity runner unavailable'
    }
  };
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const result = verifyUnity(process.cwd());
  const stream = result.status === 'passed' ? process.stdout : process.stderr;
  stream.write(`${JSON.stringify(result)}\n`);
  if (result.status !== 'passed') process.exitCode = 1;
}
