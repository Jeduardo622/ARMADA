# Android Device Baseline (proposal I — measurement checklist)

> **Status: checklist authored, measurements pending a human with a
> device.** The perf budgets in `docs/perf-budgets.md` become device
> numbers here. The APK is a local dev build only (`com.armada.devbuild`)
> — never distributed, never store-facing.

## 1. Build

```bash
node scripts/build/android-build.mjs
```

Produces `reports/android/armada-dev.apk` (gitignored) from a project
sandbox — the committed project, including `ProjectSettings/`, is never
mutated; `AndroidLocalBuild.Build` configures IL2CPP/ARM64/package id in
code at build time. Requires `UNITY_EDITOR_PATH` and the Android module
(present on this workstation at `D:\Unity\Editors\2022.3.62f3`).

## 2. Install (USB debugging enabled on the device)

```bash
adb install -r reports/android/armada-dev.apk
```

`adb` ships with the Editor's Android SDK:
`D:\Unity\Editors\2022.3.62f3\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`.

## 3. Backend note (for mid-fight measurements)

The scenes point at `http://localhost:4500` via the serialized
`ArmadaClientConfig` assets — unreachable from a device. To measure a
real fight: run the backend on this workstation, put the device on the
same network, and temporarily edit the config asset's base URL to the
workstation's LAN address in the Editor before building. Do not commit
that edit. Cold-start and idle-render measurements need no backend
(SpectatorDemo boots first and renders the full water + HUD stack while
waiting).

## 4. Measurements (record every run below)

| Budget (perf-budgets.md) | Target | How |
| --- | --- | --- |
| Cold start | < 12 s | Stopwatch: tap icon → first rendered frame. Kill the app first (`adb shell am force-stop com.armada.devbuild`). 3 runs, record all. |
| Mission load | < 6 s | Stopwatch: scene transition into Mission10Play after backend connect. |
| Frame rate mid-fight | 30 fps avg, p5 > 24 | `adb shell dumpsys gfxinfo com.armada.devbuild` during turn playback, or an on-screen fps counter slice if gfxinfo proves too coarse. |
| Runtime memory | < 800 MB | `adb shell dumpsys meminfo com.armada.devbuild` (TOTAL PSS) during playback. |

## 5. Results

| Date | Device (model / Android / RAM) | Cold start ×3 | Mission load | fps avg / p5 | PSS MB | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| _pending_ | | | | | | |

A row here graduates the perf budgets from aspirational to measured;
regressions against a recorded row are findings for the next slice.
