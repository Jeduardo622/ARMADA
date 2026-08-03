/**
 * Generated-scene rebuild runner: launches licensed Unity batchmode in a
 * project sandbox (never against the developer's possibly-open Editor),
 * executes RebuildAllGeneratedScenes.BuildAll, and copies the regenerated
 * Assets/Scenes outputs (scenes, config assets, .metas) back into the repo.
 *
 * Scenes are generated and never hand-edited (docs/pvp.md); this is the CLI
 * form of "Assets → Armada → Rebuild All Generated Scenes" for use after any
 * builder or serialized-default change.
 *
 * Usage:
 *   node scripts/visual/rebuild-scenes.mjs
 *
 * Requires UNITY_EDITOR_PATH (licensed Editor matching the project version).
 */
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createUnityProjectSandbox } from '../harness/unity-project-sandbox.mjs';
import { preflightUnityEditor } from '../harness/verify-unity-compile.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

function sha256(path) {
  return createHash('sha256').update(readFileSync(path)).digest('hex');
}

const editorPath = process.env.UNITY_EDITOR_PATH;
const preflight = preflightUnityEditor(root, editorPath);
if (preflight.status !== 'passed') {
  console.error(`[rebuild-scenes] ${preflight.summary}`);
  process.exit(1);
}

const logDir = resolve(root, 'reports/unity');
mkdirSync(logDir, { recursive: true });
const logPath = join(logDir, 'rebuild-scenes.log');

const sandbox = createUnityProjectSandbox(root);
let result;
try {
  result = spawnSync(
    editorPath,
    [
      '-batchmode',
      '-quit',
      '-projectPath',
      sandbox.projectPath,
      '-executeMethod',
      'RebuildAllGeneratedScenes.BuildAll',
      '-logFile',
      logPath
    ],
    { cwd: root, encoding: 'utf8', timeout: 900_000, windowsHide: true }
  );

  if ((result.status ?? 1) !== 0) {
    // exitCode, not process.exit(): an immediate exit would skip the
    // finally-block sandbox cleanup and strand the copied project in tmp.
    console.error(`[rebuild-scenes] Unity rebuild failed (exit ${result.status ?? 'unknown'}); see ${logPath}`);
    process.exitCode = 1;
  } else {
    // Copy the regenerated Assets/Scenes surface (scenes, config assets and
    // every .meta) back into the repo, reporting what actually changed.
    const sandboxScenes = resolve(sandbox.projectPath, 'Assets/Scenes');
    const repoScenes = resolve(root, 'unity/Assets/Scenes');
    const changed = [];
    const created = [];
    for (const name of readdirSync(sandboxScenes)) {
      const from = join(sandboxScenes, name);
      const to = join(repoScenes, name);
      if (!existsSync(to)) {
        copyFileSync(from, to);
        created.push(name);
      } else if (sha256(from) !== sha256(to)) {
        copyFileSync(from, to);
        changed.push(name);
      }
    }
    console.log(
      `[rebuild-scenes] done: ${changed.length} changed (${changed.join(', ') || 'none'}), ` +
        `${created.length} created (${created.join(', ') || 'none'})`
    );
  }
} finally {
  sandbox.cleanup();
}
