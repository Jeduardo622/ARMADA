#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the TMP Essential Resources into Assets/TextMesh Pro from the
/// com.unity.textmeshpro package, headlessly. The essentials (default font
/// asset, SDF shaders) were previously a manual "import when prompted" step
/// (docs/demo.md), which meant fresh clones and sandboxed batch captures
/// rendered no HUD text at all. Run once and commit the imported assets;
/// keeping this entry point lets a package upgrade re-import deliberately.
/// Usage: -batchmode -projectPath unity -executeMethod
///        ImportTmpEssentials.Import   (no -quit: the importer exits itself
///        when the asynchronous package import completes)
/// </summary>
public static class ImportTmpEssentials
{
    [MenuItem("Assets/Armada/Import TMP Essential Resources")]
    public static void Import()
    {
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
            typeof(TMPro.TMP_Settings).Assembly);
        if (package == null)
        {
            Fail("com.unity.textmeshpro package not found");
            return;
        }

        var packagePath = Path.Combine(
            package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
        if (!File.Exists(packagePath))
        {
            Fail($"essentials package missing at {packagePath}");
            return;
        }

        if (Application.isBatchMode)
        {
            AssetDatabase.importPackageCompleted += _ =>
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[ImportTmpEssentials] Import complete.");
                EditorApplication.Exit(0);
            };
            AssetDatabase.importPackageFailed += (_, message) => Fail(message);
        }

        AssetDatabase.ImportPackage(packagePath, interactive: false);
    }

    private static void Fail(string reason)
    {
        Debug.LogError($"[ImportTmpEssentials] {reason}");
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }
}
#endif
