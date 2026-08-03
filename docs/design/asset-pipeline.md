# Asset Pipeline Conventions (W6)

> **Status: Drafted with the art-readiness arc** (no assets exist yet —
> that is the point: these rules exist before the first import, per the
> arc's W6 mandate). The rules bind every art asset that lands; changing
> them updates this document in the same PR.

## 1. Folder structure

All imported art lives under `unity/Assets/Art/`, mirrored by
Addressables groups (docs/design/render-pipeline.md §4):

```
Assets/Art/
  Ships/<class>/          ship models, textures, per-class materials
  Board/                  sea-surface props: rocks, debris (lane B)
  Effects/                muzzle, splash, rake, sink, fire, wake
  UI/                     skin atlases (parchment, rope, icons)
  Audio/                  banks and clips
  Shared/                 palettes, common materials, LUTs, shaders
```

- Code-created placeholder visuals (primitives, flat tints) stay
  code-created and never move into `Art/`.
- `Assets/Settings/` is pipeline configuration only; `Assets/Scenes/`
  is generated output only — no hand-placed art in either.

## 2. Naming

- Folders `PascalCase`; asset files `kebab-case` with a type prefix:
  `shp-` ship model, `env-` board/environment prop, `tex-` texture,
  `mat-` material, `fx-` effect prefab, `ui-` sprite/atlas,
  `sfx-`/`mus-` audio
  (e.g. `shp-frigate-aurorian.fbx`, `tex-frigate-aurorian-albedo.png`).
- One asset per file; variants suffix with `--variant`
  (`tex-sail--crimson.png`). No spaces, no version numbers in names —
  git is the version history.

## 3. Import defaults (enforced, not advisory)

`unity/Assets/Editor/ArtImportDefaults.cs` is an `AssetPostprocessor`
that applies these to everything under `Assets/Art/` at import time,
so a forgotten inspector never ships an uncompressed 4k texture:

- Textures: max size **2048**, mipmaps on, compressed (platform default
  → ASTC on the Android reference target per `docs/perf-budgets.md`);
  UI sprites under `Art/UI/` import as Sprite (2D), mipmaps off.
- Models: read/write off, mesh compression on; import materials off
  (materials are authored, not generated per-import).
- Refinement beyond these floors uses Unity Presets checked in beside
  the assets; the postprocessor stays the guaranteed floor.

## 4. Material and shader rules (URP, decision D3)

- All art materials target **URP/Lit** (or URP/Unlit for pure-flat UI
  props); Shader Graph shaders live in `Art/Shared/` with their
  subgraphs. No Standard-shader materials — the pipeline no longer
  renders them.
- Tinting contract: anything the renderer tints (ship hulls via
  `ShipView.TintRenderer`) must expose `[MainColor] _BaseColor`.

## 5. `.meta` / GUID discipline

- Every asset commits **with its `.meta`** in the same commit — a
  missing meta regenerates a new GUID on the next machine and silently
  breaks references.
- Generated scenes reference assets **by path** through the builders
  (`docs/pvp.md`): renaming or moving an asset means updating the
  builder constant and regenerating scenes in the same PR — never
  hand-editing a `.unity` file.
- Tool-created assets (this repo's editor entry points) may hand-author
  metas with random GUIDs; imported art always takes the Unity-generated
  meta.
- Prefabs that art replaces (the `ShipViewProvider` seam) are referenced
  by serialized field, so their GUIDs are load-bearing: moving a prefab
  is safe, deleting and re-creating one is not.

## 6. Licensing / attribution ledger

Every third-party asset lands with (a) a row in
`docs/asset-licenses.md`, (b) its license/attribution file committed
beside the asset, and (c) a license-compatibility check in the PR
description (no copyleft that captures the build; attribution
obligations recorded). The ledger is already live with the TMP
essentials entries — the convention starts enforced, not aspirational.

## 7. Addressables

Group per top-level `Art/` folder (`ships`, `board`, `effects`, `ui`,
`audio`), local packing until the remote-catalog follow-up
(render-pipeline.md §4) lands. `ships` was created with the first
prefab-backed `ShipViewProvider`; `board` with the lane-B board props.
