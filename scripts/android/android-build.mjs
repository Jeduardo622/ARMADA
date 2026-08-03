/**
 * Local Android dev-APK build (approved proposal I; Class C, bounded).
 * Builds from a project sandbox (createUnityProjectSandbox) so the
 * committed project — including ProjectSettings — is never mutated; all
 * player configuration happens in AndroidLocalBuild.Build at build time.
 * Output: reports/android/armada-dev.apk (gitignored; never distributed).
 *
 * Usage: node scripts/android/android-build.mjs
 * Requires UNITY_EDITOR_PATH (licensed 2022.3.62f3 with the Android
 * module + SDK/NDK/OpenJDK). IL2CPP link time dominates; expect tens of
 * minutes on first build.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createUnityProjectSandbox } from '../harness/unity-project-sandbox.mjs';
import { preflightUnityEditor } from '../harness/verify-unity-compile.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

const editorPath = process.env.UNITY_EDITOR_PATH;
const preflight = preflightUnityEditor(root, editorPath);
if (preflight.status !== 'passed') {
  console.error(`[android-build] ${preflight.summary}`);
  process.exit(1);
}

const outDir = resolve(root, 'reports', 'android');
mkdirSync(outDir, { recursive: true });
const apkPath = join(outDir, 'armada-dev.apk');
const logPath = join(outDir, 'build.log');

const sandbox = createUnityProjectSandbox(root);
console.log(`[android-build] sandbox: ${sandbox.projectPath}`);
let result;
try {
  result = spawnSync(
    editorPath,
    [
      '-batchmode',
      '-quit',
      '-projectPath',
      sandbox.projectPath,
      '-buildTarget',
      'Android',
      '-executeMethod',
      'AndroidLocalBuild.Build',
      '-logFile',
      logPath
    ],
    {
      cwd: root,
      encoding: 'utf8',
      timeout: 3_600_000,
      windowsHide: true,
      env: { ...process.env, ARMADA_APK_OUT: apkPath }
    }
  );
} finally {
  sandbox.cleanup();
}

if ((result.status ?? 1) !== 0 || !existsSync(apkPath)) {
  console.error(`[android-build] failed (exit ${result.status ?? 'unknown'}); see ${logPath}`);
  process.exit(1);
}

const sizeMb = (statSync(apkPath).size / (1024 * 1024)).toFixed(1);
console.log(`[android-build] ${sizeMb} MB -> ${apkPath}`);
console.log('[android-build] next: docs/device-baseline.md (install + measure checklist)');
