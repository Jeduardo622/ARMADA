# Third-Party Asset License Ledger

Every third-party asset in the repository has a row here, its license
file committed beside it, and a compatibility check recorded in the PR
that introduced it (docs/design/asset-pipeline.md §6).

| Asset | Source | License | Scope of use | License file in repo |
| --- | --- | --- | --- | --- |
| Liberation Sans (font + SDF atlas) | TMP Essential Resources (com.unity.textmeshpro 3.0.9) | SIL Open Font License 1.1 | Default HUD font in all scenes | `unity/Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt` |
| EmojiOne sample sprites | TMP Essential Resources (com.unity.textmeshpro 3.0.9) | CC-BY 4.0 (attribution required) | Unused sample content shipped with TMP essentials; remove before release or attribute | `unity/Assets/TextMesh Pro/Sprites/EmojiOne Attribution.txt` |
| TMP shaders/resources | Unity Technologies package | Unity Companion License | Text rendering | Package license (com.unity.textmeshpro) |
| URP, Addressables, other com.unity.* packages | Unity Technologies | Unity Companion License | Engine features | Package licenses (Packages/manifest.json) |
| MCP for Unity (com.coplaydev.unity-mcp) | CoplayDev git dependency (Packages/manifest.json) | MIT (per upstream repository) | Local editor tooling only (agent harness); editor assembly, never in player builds | Upstream LICENSE (github.com/CoplayDev/unity-mcp) |
| Pirate Kit 2.1 — 6 ship models + colormap (curated from 70-file pack) | Kenney, kenney.nl/assets/pirate-kit | CC0 1.0 | Sourced ship views: sloop/frigate/capital × both liveries (`shp-*-src--*.fbx`, `tex-ship-colormap.png`) | `unity/Assets/Art/Ships/license-kenney-pirate-kit.txt` |
| Ships Pack — Sail ship model | Quaternius, quaternius.com/packs/ships.html (via OpenGameArt mirror) | CC0 1.0 | Clipper source candidate (`shp-clipper-src.fbx`, unwired pending mast/sail split work) | `unity/Assets/Art/Ships/license-quaternius-ships.txt` |
| Particle Pack — 7 curated sprites (smoke/flare/circle/spark) | Kenney, kenney.nl/assets/particle-pack | CC0 1.0 | P3 effect sprites (`Art/Effects/fx-*.png`); consumers land with the effects slice | `unity/Assets/Art/Effects/license-kenney-particle-pack.txt` |
| Fantasy UI Borders — 3 curated panel frames | Kenney, kenney.nl/assets/fantasy-ui-borders | CC0 1.0 | P4 HUD skin slots (`Art/UI/ui-panel-border-*.png`); consumers land with the UI-skin slice | `unity/Assets/Art/UI/license-kenney-fantasy-ui-borders.txt` |
| Old Parchment Paper (2 textures) | cron, opengameart.org/content/old-parchment-paper | CC0 1.0 | P4 narration banner background (`Art/UI/ui-parchment*.png`) | `unity/Assets/Art/UI/license-oga-parchment.txt` |
| Interface Sounds — 3 curated clicks | Kenney, kenney.nl/assets/interface-sounds | CC0 1.0 | P5 UI ticks (`Art/Audio/sfx-ui-click--*.ogg`); consumers land with the audio slice | `unity/Assets/Art/Audio/license-kenney-interface-sounds.txt` |
| Cannon fire / Cannon hit | Thimras, opengameart.org | CC0 1.0 | P5 broadside fire/impact (`sfx-cannon-*.ogg`) | `unity/Assets/Art/Audio/license-oga-audio.txt` |
| Enemy Ship Approaching (battle theme) | yd, opengameart.org | CC0 1.0 | P5 battle music (`mus-battle-theme.ogg`) | `unity/Assets/Art/Audio/license-oga-audio.txt` |
| Beach Ocean Waves (4 loops) | jasinski via qubodup, opengameart.org | CC0 1.0 | P5 sea ambience (`sfx-sea-wave--*.flac`) | `unity/Assets/Art/Audio/license-oga-audio.txt` |
| Solo Seagull Sound Effects (3 curated) | Rango Mango, opengameart.org | CC0 1.0 (author notes derivation from a YouTube recording — provenance caveat recorded, replaceable) | P5 gull ambience layer (`sfx-gull--*.wav`) | `unity/Assets/Art/Audio/license-oga-audio.txt` |

Release-gating note: the EmojiOne row carries an obligation — either
delete the sample sprites in the release-prep pass or ship its
attribution. Tracked so it cannot be forgotten.
