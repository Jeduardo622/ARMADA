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

Release-gating note: the EmojiOne row carries an obligation — either
delete the sample sprites in the release-prep pass or ship its
attribution. Tracked so it cannot be forgotten.
