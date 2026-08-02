/**
 * Visual-regression capture runner: launches licensed Unity batchmode in a
 * project sandbox (never against the developer's possibly-open Editor),
 * executes SpectatorVisualCapture.Capture against the committed fixture, and
 * diffs the captured frames against the committed baselines by SHA-256
 * (frame output is byte-deterministic — proven by the capture spike).
 *
 * Usage:
 *   node scripts/visual/capture.mjs                  # baseline capture + diff
 *   node scripts/visual/capture.mjs --mode sequence  # every-tick frames + contact sheet (no diff)
 *   node scripts/visual/capture.mjs --update-baselines
 *   node scripts/visual/capture.mjs --diff-only      # diff existing frames, no Unity launch
 *   node scripts/visual/capture.mjs --fixture pvp-seed11-focus-fire
 *
 * Requires UNITY_EDITOR_PATH (licensed Editor matching the project version).
 * IMPORTANT: never passes -nographics — offscreen rendering needs a
 * graphics device.
 *
 * Outputs: reports/unity/visual/<fixture>/ (frames + manifest, gitignored),
 * reports/unity/visual/<fixture>-diff.json (diff report), and committed
 * baselines under tests/visual/baselines/<fixture>/.
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

function parseArgs(argv) {
  const args = { mode: 'baseline', fixture: 'pvp-seed11-focus-fire', update: false, diffOnly: false };
  for (let i = 2; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--mode') args.mode = argv[++i];
    else if (arg === '--fixture') args.fixture = argv[++i];
    else if (arg === '--update-baselines') args.update = true;
    else if (arg === '--diff-only') args.diffOnly = true;
    else {
      console.error(`[visual-capture] unknown argument: ${arg}`);
      process.exit(2);
    }
  }
  if (args.mode !== 'baseline' && args.mode !== 'sequence') {
    console.error(`[visual-capture] unknown mode: ${args.mode}`);
    process.exit(2);
  }
  return args;
}

function sha256(path) {
  return createHash('sha256').update(readFileSync(path)).digest('hex');
}

function runUnityCapture(args, outDir) {
  const editorPath = process.env.UNITY_EDITOR_PATH;
  const preflight = preflightUnityEditor(root, editorPath);
  if (preflight.status !== 'passed') {
    console.error(`[visual-capture] ${preflight.summary}`);
    process.exit(1);
  }

  const fixtureRelative = `Assets/Editor/VisualCapture/Fixtures/${args.fixture}.json`;
  if (!existsSync(resolve(root, 'unity', fixtureRelative))) {
    console.error(`[visual-capture] fixture not found: unity/${fixtureRelative}`);
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
      [
        '-batchmode',
        '-quit',
        '-projectPath',
        sandbox.projectPath,
        '-executeMethod',
        'SpectatorVisualCapture.Capture',
        '-logFile',
        logPath
        // Deliberately NO -nographics: offscreen rendering needs a device.
      ],
      {
        cwd: root,
        encoding: 'utf8',
        timeout: 900_000,
        windowsHide: true,
        env: {
          ...process.env,
          ARMADA_CAPTURE_FIXTURE: resolve(sandbox.projectPath, fixtureRelative),
          ARMADA_CAPTURE_OUT: outDir,
          ARMADA_CAPTURE_MODE: args.mode
        }
      }
    );
  } finally {
    sandbox.cleanup();
  }

  const manifestPath = join(outDir, 'manifest.json');
  if ((result.status ?? 1) !== 0 || !existsSync(manifestPath)) {
    console.error(
      `[visual-capture] Unity capture failed (exit ${result.status ?? 'unknown'}); see ${logPath}`
    );
    process.exit(1);
  }
  return JSON.parse(readFileSync(manifestPath, 'utf8'));
}

function diffAgainstBaselines(args, outDir, manifest) {
  const baselineDir = resolve(root, 'tests/visual/baselines', args.fixture);
  const captured = manifest.Frames ?? manifest.frames ?? [];
  const report = {
    fixture: args.fixture,
    mode: args.mode,
    totalTicks: manifest.TotalTicks ?? manifest.totalTicks ?? null,
    matched: [],
    mismatched: [],
    missingBaseline: [],
    orphanedBaseline: []
  };

  const baselineFrames = existsSync(baselineDir)
    ? readdirSync(baselineDir).filter((name) => name.endsWith('.png'))
    : [];

  for (const frame of captured) {
    const capturedPath = join(outDir, frame);
    const baselinePath = join(baselineDir, frame);
    if (!existsSync(baselinePath)) {
      report.missingBaseline.push(frame);
      continue;
    }
    // SHA fast path; on mismatch fall back to a per-pixel tolerance, because
    // GPU rasterization can jitter curved specular edges by 1 LSB between
    // otherwise identical runs (observed on the capsule rim: 7 px, delta 1).
    if (sha256(capturedPath) === sha256(baselinePath)) {
      report.matched.push(frame);
      continue;
    }
    const result = comparePixels(
      decodePng(readFileSync(baselinePath)),
      decodePng(readFileSync(capturedPath))
    );
    if (result.equal) {
      report.matched.push(frame);
      report.toleranceMatches = report.toleranceMatches ?? [];
      report.toleranceMatches.push({
        frame,
        changedPixels: result.changedPixels,
        maxDelta: result.maxDelta
      });
    } else {
      report.mismatched.push({
        frame,
        changedPixels: result.changedPixels,
        maxDelta: result.maxDelta,
        reason: result.reason ?? 'exceeds tolerance'
      });
    }
  }
  for (const frame of baselineFrames) {
    if (!captured.includes(frame)) {
      report.orphanedBaseline.push(frame);
    }
  }

  const reportPath = resolve(root, 'reports/unity/visual', `${args.fixture}-diff.json`);
  mkdirSync(dirname(reportPath), { recursive: true });
  writeFileSync(reportPath, `${JSON.stringify(report, null, 2)}\n`);

  const clean =
    report.mismatched.length === 0 &&
    report.missingBaseline.length === 0 &&
    report.orphanedBaseline.length === 0;
  console.log(
    `[visual-capture] diff: ${report.matched.length} matched, ` +
      `${report.mismatched.length} mismatched, ${report.missingBaseline.length} missing baseline, ` +
      `${report.orphanedBaseline.length} orphaned -> ${reportPath}`
  );
  return { clean, report, baselineDir };
}

function updateBaselines(outDir, manifest, baselineDir) {
  rmSync(baselineDir, { recursive: true, force: true });
  mkdirSync(baselineDir, { recursive: true });
  const captured = manifest.Frames ?? manifest.frames ?? [];
  for (const frame of captured) {
    copyFileSync(join(outDir, frame), join(baselineDir, frame));
  }
  console.log(`[visual-capture] baselines updated: ${captured.length} frames -> ${baselineDir}`);
}

function writeContactSheet(outDir, manifest) {
  const captured = manifest.Frames ?? manifest.frames ?? [];
  const cells = captured
    .map(
      (frame) =>
        `<figure><img src="${frame}" loading="lazy" alt="${frame}"/><figcaption>${frame}</figcaption></figure>`
    )
    .join('\n');
  const html = `<!doctype html><meta charset="utf-8"><title>${manifest.Fixture ?? ''} contact sheet</title>
<style>body{background:#111;color:#ddd;font:12px monospace;margin:16px}
main{display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:8px}
figure{margin:0}img{width:100%;height:auto;display:block}figcaption{padding:2px 0 8px}</style>
<main>\n${cells}\n</main>\n`;
  const path = join(outDir, 'contact-sheet.html');
  writeFileSync(path, html);
  console.log(`[visual-capture] contact sheet -> ${path}`);
}

const args = parseArgs(process.argv);
const outDir = resolve(root, 'reports/unity/visual', args.fixture + (args.mode === 'sequence' ? '-sequence' : ''));

let manifest;
if (args.diffOnly) {
  const manifestPath = join(outDir, 'manifest.json');
  if (!existsSync(manifestPath)) {
    console.error(`[visual-capture] --diff-only but no manifest at ${manifestPath}`);
    process.exit(1);
  }
  manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
} else {
  manifest = runUnityCapture(args, outDir);
}

writeContactSheet(outDir, manifest);

if (args.mode === 'sequence') {
  // Sequence output is for human review only; no baselines, no diff.
  process.exit(process.exitCode ?? 0);
}

// Draw-call budget gate (docs/perf-budgets.md: < 1.5k mid-fight); stats
// come from the capture manifest's per-frame rendering statistics.
const stats = manifest.FrameStats ?? manifest.frameStats ?? [];
for (const line of stats) {
  const match = /batches=(\d+)/.exec(line);
  if (match && Number(match[1]) > 1500) {
    console.error(`[visual-capture] draw-call budget breach: ${line}`);
    process.exitCode = 1;
  }
}

const { clean, baselineDir } = diffAgainstBaselines(args, outDir, manifest);
if (args.update) {
  updateBaselines(outDir, manifest, baselineDir);
  process.exit(process.exitCode ?? 0);
}
if (!clean) {
  console.error('[visual-capture] visual regression detected (or baselines missing); ');
  console.error('  review frames, then re-baseline deliberately with --update-baselines');
  process.exit(1);
}
console.log('[visual-capture] all frames match committed baselines');
