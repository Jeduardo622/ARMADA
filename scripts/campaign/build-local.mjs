import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, statSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createUnityProjectSandbox } from '../harness/unity-project-sandbox.mjs';
import { preflightUnityEditor } from '../harness/verify-unity-compile.mjs';

export function buildArguments(platform, projectPath, logPath) {
  if (!['windows', 'android'].includes(platform)) throw new Error(`Unsupported platform: ${platform}`);
  return ['-batchmode', '-quit', '-projectPath', projectPath, '-buildTarget',
    platform === 'android' ? 'Android' : 'Win64', '-executeMethod',
    `CampaignLocalBuild.${platform === 'android' ? 'Android' : 'Windows'}`, '-logFile', logPath];
}

export function buildLocal(platform) {
  const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
  // Validate before creating any sandbox or output directory.
  buildArguments(platform, '', '');
  const editor = process.env.UNITY_EDITOR_PATH;
  const preflight = preflightUnityEditor(root, editor);
  if (preflight.status !== 'passed') throw new Error(preflight.summary);
  const outputDir = resolve(root, 'reports/campaign', platform);
  mkdirSync(outputDir, { recursive: true });
  const output = resolve(outputDir, platform === 'android' ? 'armada.apk' : 'Armada.exe');
  const log = resolve(outputDir, 'build.log');
  const sandbox = createUnityProjectSandbox(root);
  const started = Date.now();
  let result;
  console.log(`Building ${platform} campaign from ${sandbox.projectPath}`);
  try {
    result = spawnSync(editor, buildArguments(platform, sandbox.projectPath, log), {
      cwd: root, encoding: 'utf8', timeout: 3_600_000, windowsHide: true,
      env: { ...process.env, ARMADA_CAMPAIGN_BUILD_OUT: output }
    });
  } finally { sandbox.cleanup(); }
  if (result.error || result.status !== 0 || !existsSync(output) ||
      statSync(output).size === 0 || statSync(output).mtimeMs < started - 1000)
    throw new Error(`Campaign build failed; see ${log}. ${result.error?.message ?? ''}`);
  console.log(`Built local campaign: ${output} (${statSync(output).size} bytes)`);
  return output;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { buildLocal(process.argv[2] ?? 'windows'); }
  catch (error) { console.error(error.message); process.exitCode = 1; }
}
