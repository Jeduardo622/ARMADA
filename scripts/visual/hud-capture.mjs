/**
 * HUD aspect-matrix capture runner (D2-B slice 3): launches Unity batchmode
 * in a project sandbox, renders every generated scene's world+HUD at the
 * phone/tablet aspect matrix via HudLayoutCapture, and diffs the frames
 * against committed baselines with the same SHA + per-pixel tolerance as
 * the playback harness.
 *
 * Usage:
 *   node scripts/visual/hud-capture.mjs
 *   node scripts/visual/hud-capture.mjs --update-baselines
 *   node scripts/visual/hud-capture.mjs --diff-only
 *
 * Requires UNITY_EDITOR_PATH. Never passes -nographics.
 * Outputs: reports/unity/visual/hud/ (frames + contact sheet, gitignored),
 * reports/unity/visual/hud-diff.json, baselines in tests/visual/baselines/hud/.
 */
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync
} from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createUnityProjectSandbox } from '../harness/unity-project-sandbox.mjs';
import { preflightUnityEditor } from '../harness/verify-unity-compile.mjs';
import { comparePixels, decodePng } from './png.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const outDir = resolve(root, 'reports/unity/visual/hud');
const baselineDir = resolve(root, 'tests/visual/baselines/hud');

const args = { update: false, diffOnly: false };
for (const arg of process.argv.slice(2)) {
  if (arg === '--update-baselines') args.update = true;
  else if (arg === '--diff-only') args.diffOnly = true;
  else {
    console.error(`[hud-capture] unknown argument: ${arg}`);
    process.exit(2);
  }
}

const sha256 = (path) => createHash('sha256').update(readFileSync(path)).digest('hex');

function runUnity() {
  const editorPath = process.env.UNITY_EDITOR_PATH;
  const preflight = preflightUnityEditor(root, editorPath);
  if (preflight.status !== 'passed') {
    console.error(`[hud-capture] ${preflight.summary}`);
    process.exit(1);
  }

  rmSync(outDir, { recursive: true, force: true });
  mkdirSync(outDir, { recursive: true });
  const logPath = join(outDir, 'capture.log');

  const sandbox = createUnityProjectSandbox(root);
  let result;
  try {
    result = spawnSync(
      editorPath,
      ['-batchmode', '-quit', '-projectPath', sandbox.projectPath,
       '-executeMethod', 'HudLayoutCapture.Capture', '-logFile', logPath],
      {
        cwd: root,
        encoding: 'utf8',
        timeout: 900_000,
        windowsHide: true,
        env: { ...process.env, ARMADA_HUD_CAPTURE_OUT: outDir }
      }
    );
  } finally {
    sandbox.cleanup();
  }

  const manifestPath = join(outDir, 'manifest.json');
  if ((result.status ?? 1) !== 0 || !existsSync(manifestPath)) {
    console.error(`[hud-capture] Unity capture failed (exit ${result.status ?? 'unknown'}); see ${logPath}`);
    process.exit(1);
  }
  return JSON.parse(readFileSync(manifestPath, 'utf8'));
}

function writeContactSheet(frames) {
  const cells = frames
    .map((f) => `<figure><img src="${f}" loading="lazy" alt="${f}"/><figcaption>${f}</figcaption></figure>`)
    .join('\n');
  const html = `<!doctype html><meta charset="utf-8"><title>HUD aspect matrix</title>
<style>body{background:#111;color:#ddd;font:12px monospace;margin:16px}
main{display:grid;grid-template-columns:repeat(auto-fill,minmax(360px,1fr));gap:8px}
figure{margin:0}img{width:100%;height:auto;display:block}figcaption{padding:2px 0 8px}</style>
<main>\n${cells}\n</main>\n`;
  writeFileSync(join(outDir, 'contact-sheet.html'), html);
}

let manifest;
if (args.diffOnly) {
  const manifestPath = join(outDir, 'manifest.json');
  if (!existsSync(manifestPath)) {
    console.error(`[hud-capture] --diff-only but no manifest at ${manifestPath}`);
    process.exit(1);
  }
  manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
} else {
  manifest = runUnity();
}

const frames = manifest.Frames ?? manifest.frames ?? [];
writeContactSheet(frames);

if (args.update) {
  rmSync(baselineDir, { recursive: true, force: true });
  mkdirSync(baselineDir, { recursive: true });
  for (const frame of frames) copyFileSync(join(outDir, frame), join(baselineDir, frame));
  console.log(`[hud-capture] baselines updated: ${frames.length} frames -> ${baselineDir}`);
  process.exit(0);
}

const report = { matched: [], mismatched: [], missingBaseline: [], orphanedBaseline: [], toleranceMatches: [] };
for (const frame of frames) {
  const capturedPath = join(outDir, frame);
  const baselinePath = join(baselineDir, frame);
  if (!existsSync(baselinePath)) {
    report.missingBaseline.push(frame);
    continue;
  }
  if (sha256(capturedPath) === sha256(baselinePath)) {
    report.matched.push(frame);
    continue;
  }
  const result = comparePixels(decodePng(readFileSync(baselinePath)), decodePng(readFileSync(capturedPath)));
  if (result.equal) {
    report.matched.push(frame);
    report.toleranceMatches.push({ frame, changedPixels: result.changedPixels, maxDelta: result.maxDelta });
  } else {
    report.mismatched.push({ frame, changedPixels: result.changedPixels, maxDelta: result.maxDelta });
  }
}
const baselineFrames = existsSync(baselineDir) ? readdirSync(baselineDir).filter((n) => n.endsWith('.png')) : [];
for (const frame of baselineFrames) {
  if (!frames.includes(frame)) report.orphanedBaseline.push(frame);
}

writeFileSync(resolve(root, 'reports/unity/visual/hud-diff.json'), `${JSON.stringify(report, null, 2)}\n`);
const clean = report.mismatched.length === 0 && report.missingBaseline.length === 0 && report.orphanedBaseline.length === 0;
console.log(
  `[hud-capture] diff: ${report.matched.length} matched, ${report.mismatched.length} mismatched, ` +
    `${report.missingBaseline.length} missing baseline, ${report.orphanedBaseline.length} orphaned`
);
if (!clean) {
  console.error('[hud-capture] HUD layout changed (or baselines missing); review frames, then re-baseline with --update-baselines');
  process.exit(1);
}
console.log('[hud-capture] all frames match committed baselines');
