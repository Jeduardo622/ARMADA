#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Batch-mode standalone build of the PvP netplay demo, so one machine can
/// run two clients (editor Play Mode + this build) against the local
/// backend. The scene list is passed explicitly and nothing is written to
/// EditorBuildSettings, so project build settings stay untouched. Output
/// goes to the repo-root build/ folder, which is gitignored.
/// Usage: -batchmode -executeMethod PvpNetplayBuildScript.Build
/// </summary>
public static class PvpNetplayBuildScript
{
    private const string ScenePath = "Assets/Scenes/PvPNetplayDemo.unity";
    private const string OutputPath = "../build/PvPNetplayDemo/PvPNetplayDemo.exe";

    [MenuItem("Assets/Armada/Build PvP Netplay Standalone (Win64)")]
    public static void Build()
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"[PvpNetplayBuildScript] Build {summary.result}: {summary.outputPath} ({summary.totalSize} bytes, {summary.totalErrors} errors)");
        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            // Non-zero exit so batch callers see the failure.
            EditorApplication.Exit(1);
        }
    }
}
#endif
