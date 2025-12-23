# Armada Unity Client Scaffold

This folder contains Unity-side scaffolding to connect the Armada backend (`http://localhost:4500`) for iOS/Android targets. It is written for Unity **2022.3 LTS** and avoids extra packages beyond built‑in Addressables.

## Quick setup
1) Install Unity 2022.3 LTS with Android Build Support (SDK/NDK + OpenJDK). On mac, also install iOS build support if you need device builds.
2) Create a new 3D (URP if desired) project, then copy the `Assets` and `ProjectSettings` content from this folder into the project root. If you already have a project, copy `Assets/Armada` and `Assets/Editor` into your `Assets` folder.
3) In **Project Settings → Time**, set **Fixed Timestep** to `0.0166667` (60 Hz) for deterministic sim. Also set **Maximum Allowed Timestep** to `0.05`.
4) Open **Window → Asset Management → Addressables** and enable Addressables. Let Unity create the default `AddressableAssetSettings.asset` if prompted.
5) Open **Armada/Config/ArmadaClientConfig.asset** in the Inspector (or create one via `Assets → Create → Armada → Client Config`) and set:
   - `Base Url`: `http://localhost:4500`
   - `Config Signing Key`: value shared with backend (`CONFIG_SIGNING_KEY`)
   - Feature toggles as desired.
6) Play mode: add `Armada/Bootstrap/ArmadaBootstrap.cs` to an empty GameObject in your first scene. Press Play to auto-auth, fetch config, and start telemetry flushing.

## Addressables profiles
- Default profile keeps local content (`LocalBuildPath`, `LocalLoadPath`) unchanged.
- Remote profile created by `Assets/Editor/AddressablesProfileSetup.cs` (run once via `Assets → Armada → Configure Addressables`). Remote paths:
  - Build: `ServerData/[BuildTarget]`
  - Load: `http://localhost:4500/content/{Platform}` (adjust for CDN)
- Labels are defined for `core`, `ui`, `audio`, `sim-data`. Keep catalog hash updates enabled.

## Backend notes
- Auth: `/auth/guest` returns a bearer token; the client caches and attaches it to every non-health request.
- Config: `/config/{namespace}` returns `{ config, signature, algorithm }` plus `ETag` header. We verify HMAC-SHA256 using `CONFIG_SIGNING_KEY` and cache by ETag.
- Telemetry: `/telemetry/ingest` accepts `TelemetryIngestRequest`. Client batches events, guards payload size (~10 KB serialized), and posts on a timer or when the queue is large.
- Feature flags: any `403` is treated as “feature off” for that surface; friendly messaging for `401/403/429` and offline.
- Sim: `/sim/preview` request/response hashes are logged for parity checks.

## Validation against backend
1) In repo root run `docker-compose up -d && npm install && npm run dev` to start backend on `:4500`.
2) Enter Play Mode; verify console logs:
   - `Auth token acquired` with player id
   - Config fetch success with verified signature
   - Missions/inventory panels populate (feature off if `403`)
   - Telemetry flush logs `Accepted`
   - Sim preview logs request/response hashes
3) For mobile, set target platform to Android/iOS and build; IL2CPP works with these scripts.

## Determinism hooks
- `DeterministicSimHooks` sets `Time.fixedDeltaTime`, seeds `UnityEngine.Random` with a stable seed, and exposes a method to seed your sim before previews.
- `SimService` hashes request/response bodies (SHA256 hex) for replay parity.


