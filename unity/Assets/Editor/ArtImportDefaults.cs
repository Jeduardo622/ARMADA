#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Enforced import floors for everything under Assets/Art
/// (docs/design/asset-pipeline.md §3): a forgotten inspector can never
/// ship an uncompressed 4k texture or a read/write-enabled mesh. Presets
/// checked in beside the assets may refine these; this postprocessor is
/// the guaranteed floor. Assets/Art does not exist yet — the rules
/// predate the first import by design (W6).
/// </summary>
public sealed class ArtImportDefaults : AssetPostprocessor
{
    private const string ArtRoot = "Assets/Art/";
    private const string UiRoot = "Assets/Art/UI/";
    private const int MaxTextureSize = 2048;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ArtRoot))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        if (importer.maxTextureSize > MaxTextureSize)
        {
            importer.maxTextureSize = MaxTextureSize;
        }

        importer.textureCompression = TextureImporterCompression.Compressed;

        // Platform overrides take precedence over the default block, so an
        // Android/iPhone override left at 4096/uncompressed would bypass the
        // floor (Codex P2 on the W6 PR): clamp the mobile overrides too.
        foreach (var platform in new[] { "Android", "iPhone" })
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            if (!settings.overridden)
            {
                continue;
            }

            if (settings.maxTextureSize > MaxTextureSize)
            {
                settings.maxTextureSize = MaxTextureSize;
            }

            settings.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(settings);
        }

        if (assetPath.StartsWith(UiRoot))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
        }
        else
        {
            importer.mipmapEnabled = true;
        }
    }

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(ArtRoot))
        {
            return;
        }

        var importer = (ModelImporter)assetImporter;
        importer.isReadable = false;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
    }
}
#endif
