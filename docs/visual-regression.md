# Visual-Regression Capture Harness

Headless, deterministic frame capture of the real `SpectatorRenderer`
playing back a pinned resolved battle, diffed against committed baselines
(SHA-256 fast path + tight per-pixel tolerance). This is the art-iteration inner loop: change the view
layer, capture, look at the PNGs, diff against the last known-good frames.

## How it works

1. **Fixture** — `scripts/visual/generate-capture-fixture.ts` (run with
   `npx tsx`) replays the pinned seed-11 focus-fire-vs-split
   `pvp-skirmish-2v2` battle through the real engine
   (`resolveSimPreview`) and writes the resolved turn stream as
   server-wire-shaped JSON to
   `unity/Assets/Editor/VisualCapture/Fixtures/pvp-seed11-focus-fire.json`
   (committed). A drift guard fails the script if the sim no longer
   produces the pinned side-A-wins-at-turn-7 outcome.
2. **Capture** — `unity/Assets/Editor/VisualCapture/SpectatorVisualCapture.cs`
   builds the PvP-scene stage (same camera/light/board as
   `PvPNetplayDemoSceneBuilder`, ambient pinned, aspect pinned), feeds the
   fixture to the real `SpectatorRenderer.BeginTurns`, and drives
   `Tick(0.1f)` in a loop — many frames per Unity launch, since startup
   dominates the ~50 s cost. Frames render offscreen
   (`camera.targetTexture` → `Camera.Render()` → `ReadPixels` →
   `EncodeToPNG`; **never pass `-nographics`**).
3. **Diff** — `scripts/visual/capture.mjs` launches Unity batchmode in a
   project sandbox (`createUnityProjectSandbox`, so a developer's open
   Editor is never disturbed), then compares the captured frames against
   `tests/visual/baselines/<fixture>/`: SHA-256 as the fast path, with a
   per-pixel tolerance fallback (`scripts/visual/png.mjs`, zero-dependency
   decoder over node's zlib). Tolerance exists because GPU rasterization
   jitters curved specular edges by 1 LSB between otherwise identical runs
   (observed: 7 pixels, delta 1, on a capsule rim); a frame passes if every
   differing channel is within delta 2 and at most 0.01% of pixels changed.
   Anything beyond that is a real visual change.

## Commands

```bash
node scripts/visual/capture.mjs
```

Baseline capture + diff. Exit 1 on any mismatched/missing/orphaned frame,
with a report at `reports/unity/visual/<fixture>-diff.json`.

```bash
node scripts/visual/capture.mjs --update-baselines
```

Re-baseline deliberately after an intended visual change. The new PNGs
under `tests/visual/baselines/` are part of the same PR as the change that
caused them — a reviewer sees the before/after in the diff.

```bash
node scripts/visual/capture.mjs --mode sequence
```

Captures **every** 0.1 s tick (hundreds of frames) plus a browsable
`contact-sheet.html` into `reports/unity/visual/<fixture>-sequence/`.
Stills cannot show motion (`moveSeconds` 0.35, `flashSeconds` 0.45 are
most of the demo's feel); the dense sequence is the motion record.
Sequence output is never committed and never diffed.

```bash
node scripts/visual/capture.mjs --diff-only
```

Re-diffs existing captured frames without launching Unity.

Regenerate the fixture (only when the sim deliberately changed):

```bash
npx tsx scripts/visual/generate-capture-fixture.ts
```

## Capture beats (baseline mode)

- `frame-00000-opening` — the authored opening frame (tick 0).
- `frame-<tick>-turnNN` — the first tick of each turn banner.
- `frame-<tick>-complete` — after `RunComplete`.

Nine frames for the seed-11 fixture (opening + 7 turns + complete). The
tick index is embedded in the filename, so a timing change (step
durations, tick cadence) shows up as a rename — loudly — rather than a
silent re-capture at different moments.

## Determinism contract

- Fixed `Tick(0.1f)` drive; no wall clock, no `Update()`, no play mode.
- `RenderSettings.ambientMode = Flat` + pinned ambient color; no bake.
- `camera.aspect` pinned to 1920/1080 (batchmode has no game view, and the
  follow camera reads aspect every tick).
- `antiAliasing = 1`; PNG via `EncodeToPNG` (deterministic encoder).
- HUD label is deliberately unwired: captures must not depend on the TMP
  Essentials import. HUD narration is asserted by PlayMode tests instead.

## Environment

- `UNITY_EDITOR_PATH` must point at the licensed 2022.3.62f3 Editor; the
  runner preflights the version against `ProjectVersion.txt`.
- The runner always copies the project to a temp sandbox; generated
  `Library` state stays out of the repo and the developer's Editor lock is
  never contended.

## Limits

- The tolerance is tight (delta ≤ 2, ≤ 0.01% of pixels): any intended visual change re-baselines. That is
  the point — art-affecting diffs must be looked at, then committed.
- Stills only; motion review is the sequence contact sheet (a GIF step can
  be added later if a zero-dependency encoder is worth it).
- The stage mirrors the PvP scenes; mission-scene fixtures (fixed camera,
  obstacles, status effects) are a natural follow-up fixture file each.
