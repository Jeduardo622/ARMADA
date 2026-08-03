#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Local development APK for the device perf checklist
/// (docs/device-baseline.md; approved proposal I — Class C, bounded:
/// local build only, never distributed). Every player setting is applied
/// here in code at build time, and the runner builds from a project
/// sandbox, so no ProjectSettings churn ever lands in the repo — the
/// committed project stays exactly as CI verifies it.
/// -batchmode -quit -buildTarget Android
/// -executeMethod AndroidLocalBuild.Build
/// </summary>
public static class AndroidLocalBuild
{
    private static readonly string[] Scenes =
    {
        // SpectatorDemo boots first: it renders the full visual stack
        // (water, HUD) without input, the cleanest cold-start stopwatch.
        "Assets/Scenes/SpectatorDemo.unity",
        "Assets/Scenes/Mission10Play.unity",
        "Assets/Scenes/PvPHotseatDemo.unity",
        "Assets/Scenes/PvPNetplayDemo.unity"
    };

    public static void Build()
    {
        // Mobile reference target (docs/perf-budgets.md): IL2CPP + ARM64.
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.armada.devbuild");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.productName = "Armada Dev";
        // The checklist's mid-fight measurements point the client at a LAN
        // backend over plain HTTP (Codex P1 on this PR: the project default
        // NotAllowed blocks cleartext). AlwaysAllowed — rather than a
        // Development build — keeps the release-config perf numbers
        // representative; acceptable only because this APK never ships.
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

        var output = Path.GetFullPath(
            Environment.GetEnvironmentVariable("ARMADA_APK_OUT")
            ?? Path.Combine("..", "reports", "android", "armada-dev.apk"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        var report = BuildPipeline.BuildPlayer(Scenes, output, BuildTarget.Android, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[AndroidLocalBuild] build {report.summary.result}: {report.summary.totalErrors} errors");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }

            return;
        }

        Debug.Log($"[AndroidLocalBuild] {report.summary.totalSize} bytes -> {output}");
    }
}
#endif
